using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Shaders;

namespace OpenHOI3.Tests.Content;

/// <summary>The effect files read and translated to Godot shaders, as <c>docs/mechanics/shaders.md</c> has it.</summary>
public class ShaderTranslatorTests
{
	private static string Preprocess(string source, Dictionary<string, string>? files = null)
	{
		var problems = new List<string>();
		string text = CFxPreprocessor.Process(source, name => files is not null && files.TryGetValue(name, out string? found) ? found : null,
			problems.Add);
		Assert.Empty(problems);
		return text;
	}

	private static CGodotShader Translate(string source, string technique = "t", int pass = 0)
	{
		CFxEffect effect = CFxParser.Parse(Preprocess(source));
		Assert.Empty(effect.Problems);
		CFxTechnique found = effect.Technique(technique)!;
		return CGodotShaderWriter.Write(effect, found, found.Passes[pass]);
	}

	private const string Frame = @"
float4x4 WorldViewProjection;
struct VS_INPUT { float4 vPosition : POSITION; float2 vTexCoord : TEXCOORD0; };
struct VS_OUTPUT { float4 vPosition : POSITION; float2 vTexCoord : TEXCOORD0; float4 vColour : COLOR0; };
VS_OUTPUT VS(const VS_INPUT v)
{
	VS_OUTPUT Out = (VS_OUTPUT)0;
	Out.vPosition = mul(v.vPosition, WorldViewProjection);
	Out.vTexCoord = v.vTexCoord;
	Out.vColour = 2;
	return Out;
}
";

	[Fact]
	public void The_preprocessor_follows_nested_conditionals_and_includes_a_file_again_with_other_flags()
	{
		var files = new Dictionary<string, string>
		{
			["part.h"] = "#ifdef FAR\n#ifdef COLOUR\nfar_colour\n#else\nfar_plain\n#endif\n#else\nnear\n#endif\n#undef FAR\n#undef COLOUR\n",
		};
		string text = Preprocess("#include \"part.h\"\n#define FAR\n#include \"part.h\"\n#define FAR\n#define COLOUR\n#include \"part.h\"\n",
			files);

		Assert.Equal(new[] { "near", "far_plain", "far_colour" }, text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
	}

	[Fact]
	public void A_macro_is_replaced_word_by_word_but_not_inside_a_number_or_a_comment()
	{
		string text = Preprocess("#define f 2.0 // two\n#define TWICE f * f\nfloat x = TWICE + 0.5f; // f\n");

		Assert.Equal("float x = 2.0 * 2.0 + 0.5f;", text.Split('\n').Select(l => l.Trim()).First(l => l.Length > 0));
	}

	[Fact]
	public void A_sampler_its_states_and_a_passs_shaders_with_their_compile_arguments_are_read()
	{
		CFxEffect effect = CFxParser.Parse(Preprocess(@"
texture tex0 < string name = ""water.dds""; >;
sampler Water = sampler_state { Texture = <tex0>; MinFilter = Linear; MagFilter = Linear; MipFilter = None; AddressU = Clamp; };
float4 PS( float2 uv : TEXCOORD0, uniform bool bright ) : COLOR { return tex2D( Water, uv ); }
technique t { pass p0 { ALPHABLENDENABLE = True; PixelShader = compile ps_2_0 PS( true ); } }"));

		Assert.Empty(effect.Problems);
		CFxSampler sampler = Assert.Single(effect.Samplers);
		Assert.Equal(("tex0", "Linear", "Clamp"), (sampler.Texture, sampler.States["MINFILTER"], sampler.States["ADDRESSU"]));
		Assert.Equal("water.dds", effect.Variables.Single(v => v.Name == "tex0").Annotations["name"]);
		CFxPass pass = effect.Technique("t")!.Passes[0];
		Assert.Equal(("ps_2_0", "PS", 1), (pass.Pixel!.Target, pass.Pixel.Function, pass.Pixel.Arguments.Count));
		Assert.Equal("True", pass.States.Single(s => s.Name == "ALPHABLENDENABLE").Value);
	}

	[Fact]
	public void Mul_of_a_vector_by_a_matrix_is_the_transposed_matrix_times_the_vector()
	{
		CGodotShader shader = Translate(Frame + "float4 PS(VS_OUTPUT v) : COLOR { return v.vColour; }\n" +
			"technique t { pass p { VertexShader = compile vs_2_0 VS(); PixelShader = compile ps_2_0 PS(); } }");

		Assert.Empty(shader.Problems);
		Assert.Contains("(WorldViewProjection * v.vPosition)", shader.Code);
		Assert.Contains("POSITION = fx_result.vPosition;", shader.Code);
		// A colour output is clamped as Direct3D 9 clamps it, an int given to a float4 spread out as a float.
		Assert.Contains("fx_v_color0 = clamp(fx_result.vColour, 0.0, 1.0);", shader.Code);
		Assert.Contains("Out.vColour = vec4(2.0);", shader.Code);
		Assert.Contains("VS_OUTPUT Out = VS_OUTPUT(vec4(0.0), vec2(0.0), vec4(0.0));", shader.Code);
		Assert.Contains(shader.Inputs, i => i.Semantic == "TEXCOORD0" && i.Channel == "UV");
		Assert.Contains(shader.Uniforms, u => u.FxName == "WorldViewProjection" && u.Type == "mat4");
	}

	[Fact]
	public void A_float4x3_is_a_padded_mat4_whose_product_is_cut_to_three()
	{
		CGodotShader shader = Translate(@"
float4x3 Bones;
float4 PS(float2 uv : TEXCOORD0) : COLOR { float3 p = mul(float3(uv, 1), Bones); return float4(p, 1); }
technique t { pass p { PixelShader = compile ps_2_0 PS(); } }");

		Assert.Empty(shader.Problems);
		Assert.Contains("uniform mat4 Bones;", shader.Code);
		// HLSL's float3 by float4x3 uses the first three rows: the nought the vector is padded with does the same.
		Assert.Contains("vec3 p = (Bones * vec4(vec3(uv, 1.0), 0.0)).xyz;", shader.Code);
	}

	[Fact]
	public void What_HLSL_converts_without_a_word_is_written_out()
	{
		CGodotShader shader = Translate(@"
float4 PS(float2 uv : TEXCOORD0) : COLOR
{
	float4 c = float4(uv, 0, 1);
	float3 rgb = c;
	float grey = dot(rgb, float3(0.3, 0.6, 0.1));
	float3 mask = c < .5;
	float r = fmod(grey, 2);
	clip(grey - 0.5f);
	return float4(grey.rrr * mask + r, 1);
}
technique t { pass p { PixelShader = compile ps_2_0 PS(); } }");

		Assert.Empty(shader.Problems);
		Assert.Contains("vec3 rgb = c.xyz;", shader.Code);
		// The four compared, as HLSL compares them, and the first three kept.
		Assert.Contains("vec3 mask = vec3((lessThan(c, vec4(0.5))).xyz);", shader.Code);
		Assert.Contains("float r = fx_fmod(grey, 2.0);", shader.Code);
		Assert.Contains("if ((grey - 0.5) < 0.0) { discard; }", shader.Code);
		Assert.Contains("vec3(grey)", shader.Code);
		// The colour written into Godot's linear space, with no alpha where the pass does not blend.
		Assert.Contains("ALBEDO = fx_to_linear(fx_colour.rgb);", shader.Code);
		Assert.DoesNotContain("ALPHA =", shader.Code);
	}

	[Fact]
	public void A_local_hiding_a_global_is_renamed_and_a_blending_pass_writes_alpha()
	{
		CGodotShader shader = Translate(@"
float3 LightDirection;
float4 PS(float2 uv : TEXCOORD0) : COLOR { float3 LightDirection = -uv.xyx; return float4(LightDirection, 0.5); }
technique t { pass p { ALPHABLENDENABLE = True; PixelShader = compile ps_2_0 PS(); } }");

		Assert.Empty(shader.Problems);
		Assert.Contains("vec3 fx_1_LightDirection = (-uv.xyx);", shader.Code);
		Assert.Contains("ALPHA = clamp(fx_colour.a, 0.0, 1.0);", shader.Code);
		Assert.Contains("render_mode unshaded, cull_disabled, fog_disabled, blend_mix;", shader.Code);
	}

	[Fact]
	public void A_samplers_states_become_its_filter_and_repeat_and_its_texture_names_its_file()
	{
		CGodotShader shader = Translate(@"
texture tex0 < string ResourceName = ""stripes.dds""; >;
sampler Stripes = sampler_state { Texture = <tex0>; MinFilter = Linear; MagFilter = Linear; MipFilter = Linear; AddressU = Wrap; };
sampler Plain = sampler_state { Texture = <tex0>; AddressU = Clamp; };
float4 PS(float2 uv : TEXCOORD0) : COLOR { return tex2D(Stripes, uv) + tex2D(Plain, uv); }
technique t { pass p { PixelShader = compile ps_2_0 PS(); } }");

		Assert.Contains("uniform sampler2D Stripes : filter_linear_mipmap, repeat_enable;", shader.Code);
		// Direct3D's defaults: point filtering, no mipmaps.
		Assert.Contains("uniform sampler2D Plain : filter_nearest, repeat_disable;", shader.Code);
		Assert.All(shader.Textures, t => Assert.Equal(("tex0", "stripes.dds"), (t.Texture, t.File)));
	}

	[Fact]
	public void A_pixel_shader_reading_what_the_vertex_shader_does_not_give_is_reported()
	{
		CGodotShader shader = Translate(Frame + "float4 PS(float2 a : TEXCOORD0, float2 b : TEXCOORD3) : COLOR { return float4(a, b); }\n" +
			"technique t { pass p { VertexShader = compile vs_2_0 VS(); PixelShader = compile ps_2_0 PS(); } }");

		Assert.Contains(shader.Problems, p => p.Contains("reads TEXCOORD3"));
	}
}
