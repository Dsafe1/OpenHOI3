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
		// Converted only where the renderer converts back: the Compatibility renderer, OpenGL, writes it as it is.
		Assert.Contains("#if CURRENT_RENDERER == RENDERER_COMPATIBILITY\n\treturn c;\n#else\n", shader.Code);
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
		Assert.Contains("render_mode unshaded, cull_disabled, fog_disabled, blend_mix, depth_draw_always;", shader.Code);
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
	public void On_the_canvas_the_drawn_texture_is_Godots_own_handed_down_and_the_element_keeps_its_modulate()
	{
		CFxEffect effect = CFxParser.Parse(Preprocess(@"
texture tex0 < string name = ""Texture.tga""; >;
float4x4 WorldViewProjectionMatrix;
float vXOffset = 0;
sampler MapTexture = sampler_state { Texture = <tex0>; MinFilter = Point; MagFilter = Point; };
struct VS_INPUT { float4 vPosition : POSITION; float2 vTexCoord : TEXCOORD0; };
struct VS_OUTPUT { float4 vPosition : POSITION; float2 vTexCoord : TEXCOORD0; };
VS_OUTPUT VertexShaderStd(const VS_INPUT v) { VS_OUTPUT Out = (VS_OUTPUT)0; Out.vPosition = mul(v.vPosition, WorldViewProjectionMatrix);
	Out.vTexCoord = v.vTexCoord; Out.vTexCoord.x += vXOffset; return Out; }
float4 Sample(float2 uv) { return tex2D(MapTexture, uv); }
float4 PixelShaderOver(VS_OUTPUT v) : COLOR { return Sample(v.vTexCoord) + float4(0.1, 0.1, 0.1, 0); }
technique over { pass p0 { ALPHABLENDENABLE = True; VertexShader = compile vs_2_0 VertexShaderStd(); PixelShader = compile ps_2_0 PixelShaderOver(); } }"));
		CFxTechnique technique = effect.Technique("over")!;

		CGodotShader shader = CGodotShaderWriter.Write(effect, technique, technique.Passes[0], EShaderTarget.Canvas, "tex0");

		Assert.Empty(shader.Problems);
		Assert.StartsWith("shader_type canvas_item;", shader.Code);
		// No uniform for the drawn texture: every function reaching it is handed Godot's TEXTURE.
		Assert.DoesNotContain("uniform sampler2D MapTexture", shader.Code);
		Assert.Contains("vec4 Sample(vec2 uv, sampler2D fx_drawn)", shader.Code);
		Assert.Contains("return texture(fx_drawn, uv);", shader.Code);
		Assert.Contains("PixelShaderOver(VS_OUTPUT(vec4(0.0), fx_v_texcoord0), TEXTURE)", shader.Code);
		// Godot places the element: the vertex shader runs for its texture coordinate alone, read from UV.
		Assert.DoesNotContain("POSITION =", shader.Code);
		Assert.Contains("VertexShaderStd(VS_INPUT(vec4(VERTEX, 0.0, 1.0), UV))", shader.Code);
		Assert.Contains("COLOR = vec4(clamp(fx_colour.rgb, 0.0, 1.0), clamp(fx_colour.a, 0.0, 1.0)) * fx_modulate;", shader.Code);
	}

	[Fact]
	public void A_vertex_input_with_fewer_components_than_declared_is_filled_as_Direct3D_fills_it()
	{
		// progress.fx's shape: a float3 coordinate from a stream of two.
		CFxEffect effect = CFxParser.Parse(Preprocess(@"
float CurrentState;
struct VS_INPUT { float4 vPosition : POSITION; float3 vTexCoord : TEXCOORD0; };
struct VS_OUTPUT { float4 vPosition : POSITION; float3 vTexCoord0 : TEXCOORD0; };
VS_OUTPUT VS(const VS_INPUT v) { VS_OUTPUT Out = (VS_OUTPUT)0; Out.vPosition = v.vPosition; Out.vTexCoord0 = v.vTexCoord; return Out; }
float4 PS(VS_OUTPUT v) : COLOR { if (v.vTexCoord0.x <= CurrentState) return float4(1, 0, 0, 1); else return float4(0, 1, 0, 1); }
technique tec0 { pass p0 { VertexShader = compile vs_1_1 VS(); PixelShader = compile ps_2_0 PS(); } }"));
		CFxTechnique technique = effect.Technique("tec0")!;

		CGodotShader shader = CGodotShaderWriter.Write(effect, technique, technique.Passes[0], EShaderTarget.Canvas);

		Assert.Empty(shader.Problems);
		Assert.Contains("vec3(UV, 0.0)", shader.Code);
	}

	[Fact]
	public void On_the_canvas_a_second_set_of_texture_coordinates_is_read_from_a_custom_channel()
	{
		// maskedflag.fx's shape: the flag's coordinates and the mask's, each its own set.
		CFxEffect effect = CFxParser.Parse(Preprocess(@"
texture tex0; texture tex1;
sampler BaseTexture = sampler_state { Texture = <tex0>; };
sampler MaskTexture = sampler_state { Texture = <tex1>; };
struct VS_INPUT { float4 vPosition : POSITION; float2 vTexCoord : TEXCOORD0; float2 vMaskCoord : TEXCOORD1; float2 vMore : TEXCOORD2; float2 vLast : TEXCOORD3; };
struct VS_OUTPUT { float4 vPosition : POSITION; float2 vTexCoord0 : TEXCOORD0; float2 vTexCoord1 : TEXCOORD1; };
VS_OUTPUT VS(const VS_INPUT v) { VS_OUTPUT Out = (VS_OUTPUT)0; Out.vPosition = v.vPosition; Out.vTexCoord0 = v.vTexCoord;
	Out.vTexCoord1 = v.vMaskCoord + v.vMore + v.vLast; return Out; }
float4 PS(VS_OUTPUT v) : COLOR { float4 c = tex2D(BaseTexture, v.vTexCoord0); c.a = tex2D(MaskTexture, v.vTexCoord1).a; return c; }
technique tec0 { pass p0 { ALPHABLENDENABLE = True; VertexShader = compile vs_1_1 VS(); PixelShader = compile ps_2_0 PS(); } }"));
		CFxTechnique technique = effect.Technique("tec0")!;

		CGodotShader shader = CGodotShaderWriter.Write(effect, technique, technique.Passes[0], EShaderTarget.Canvas, "tex0");

		Assert.Contains("CUSTOM0.xy", shader.Code);
		Assert.Contains("CUSTOM1.xy", shader.Code);
		Assert.Contains(shader.Inputs, input => input is { Semantic: "TEXCOORD1", Channel: "CUSTOM0" });
		Assert.Contains(shader.Inputs, input => input is { Semantic: "TEXCOORD2", Channel: "CUSTOM1" });
		// A canvas item's vertex has two custom channels: a fourth set is nought, and said so.
		Assert.Single(shader.Problems, problem => problem.Contains("the canvas gives no TEXCOORD3"));
	}

	[Fact]
	public void The_engines_device_states_are_what_a_passs_own_are_laid_over()
	{
		const string source = @"
float4 PS(float2 uv : TEXCOORD0) : COLOR { return float4(uv, 0, 0.5); }
technique beach { pass p { PixelShader = compile ps_2_0 PS(); } }
technique tested { pass p { ALPHATESTENABLE = True; PixelShader = compile ps_2_0 PS(); } }";
		CFxEffect effect = CFxParser.Parse(Preprocess(source));
		// The engine's baseline and the far beaches' draw: blending on, SRCALPHA / ZERO, alpha test at 8, depth off.
		var device = new Dictionary<string, string>
		{
			["ALPHABLENDENABLE"] = "1", ["SRCBLEND"] = "SRCALPHA", ["DESTBLEND"] = "ZERO", ["ALPHAREF"] = "8",
			["ALPHAFUNC"] = "GREATEREQUAL", ["ZENABLE"] = "0",
		};
		CFxTechnique beach = effect.Technique("beach")!;
		CFxTechnique tested = effect.Technique("tested")!;

		CGodotShader plain = CGodotShaderWriter.Write(effect, beach, beach.Passes[0], EShaderTarget.Spatial, null, device);
		CGodotShader alphaTested = CGodotShaderWriter.Write(effect, tested, tested.Passes[0], EShaderTarget.Spatial, null, device);

		// SRCALPHA / ZERO: the colour times its alpha written over what is there, which no blend mode of Godot's is.
		Assert.Contains("ALBEDO = fx_to_linear((fx_colour.rgb * fx_colour.a));", plain.Code);
		Assert.DoesNotContain("ALPHA =", plain.Code);
		Assert.Contains("depth_test_disabled", plain.Code);
		Assert.DoesNotContain("discard", plain.Code);
		// The pass turns the test on; the engine's reference and function hold.
		Assert.Contains("if (!(fx_colour.a >= 0.031372549)) { discard; }", alphaTested.Code);
	}

	[Fact]
	public void The_stencil_that_paints_a_pixel_once_is_a_test_against_a_value_of_its_layers_own()
	{
		const string source = @"
float4 PS(float2 uv : TEXCOORD0) : COLOR { return float4(uv, 0, 0.5); }
technique once { pass p { STENCILREF = 0; STENCILMASK = 255; STENCILWRITEMASK = 255; STENCILENABLE = True; STENCILFUNC = Equal;
	STENCILFAIL = Keep; STENCILZFAIL = Keep; STENCILPASS = Incr; PixelShader = compile ps_2_0 PS(); } }
technique other { pass p { STENCILENABLE = True; STENCILFUNC = Less; STENCILPASS = Replace; PixelShader = compile ps_2_0 PS(); } }
technique off { pass p { PixelShader = compile ps_2_0 PS(); } }";
		CFxEffect effect = CFxParser.Parse(Preprocess(source));
		CFxTechnique once = effect.Technique("once")!, other = effect.Technique("other")!, off = effect.Technique("off")!;

		// battlearrow.fx's: equal to nought and increment - the value one past the reference, not yet written, passes.
		CGodotShader first = CGodotShaderWriter.Write(effect, once, once.Passes[0]);
		Assert.Empty(first.Problems);
		Assert.Contains("stencil_mode read, write, compare_not_equal, 1;", first.Code);
		// After the engine's third clear: a value of its own, which passes over the earlier layers' as a cleared stencil.
		CGodotShader fourth = CGodotShaderWriter.Write(effect, once, once.Passes[0], stencilLayer: 3);
		Assert.Contains("stencil_mode read, write, compare_not_equal, 4;", fourth.Code);
		// Past what the stencil's byte holds, and a form Godot's has no match for: drawn without one, and said so.
		CGodotShader full = CGodotShaderWriter.Write(effect, once, once.Passes[0], stencilLayer: 255);
		Assert.DoesNotContain("stencil_mode", full.Code);
		Assert.Single(full.Problems, problem => problem.Contains("past the 255 values"));
		CGodotShader unmatched = CGodotShaderWriter.Write(effect, other, other.Passes[0]);
		Assert.DoesNotContain("stencil_mode", unmatched.Code);
		Assert.Single(unmatched.Problems, problem => problem.Contains("the stencil LESS / REPLACE"));
		// The canvas has no stencil, and a pass with it off writes none.
		Assert.DoesNotContain("stencil_mode", CGodotShaderWriter.Write(effect, once, once.Passes[0], EShaderTarget.Canvas).Code);
		Assert.DoesNotContain("stencil_mode", CGodotShaderWriter.Write(effect, off, off.Passes[0]).Code);
	}

	[Fact]
	public void A_shared_parameter_is_a_global_uniform_and_the_position_is_turned_to_Godots_clip_space()
	{
		CFxEffect effect = CFxParser.Parse(Preprocess(Frame + "float4 PS(VS_OUTPUT v) : COLOR { return v.vColour; }\n" +
			"technique t { pass p { VertexShader = compile vs_2_0 VS(); PixelShader = compile ps_2_0 PS(); } }"));
		CFxTechnique technique = effect.Technique("t")!;

		CGodotShader shader = CGodotShaderWriter.Write(effect, technique, technique.Passes[0],
			shared: new Dictionary<string, string> { ["WorldViewProjection"] = "fx_map_wvp" });

		Assert.Empty(shader.Problems);
		Assert.Contains("global uniform mat4 fx_map_wvp;", shader.Code);
		Assert.DoesNotContain("uniform mat4 WorldViewProjection", shader.Code);
		Assert.Contains("(fx_map_wvp * v.vPosition)", shader.Code);
		Assert.Contains(shader.Uniforms, u => u.FxName == "WorldViewProjection" && u.Name == "fx_map_wvp" && u.Shared);
		// Direct3D's clip depth turned to Godot's reversed one, and its y, which runs up the screen, turned down.
		Assert.Contains("POSITION.z = POSITION.w - POSITION.z;", shader.Code);
		Assert.Contains("POSITION.y = -POSITION.y;", shader.Code);
	}

	[Fact]
	public void A_pixel_shader_reading_what_the_vertex_shader_does_not_give_is_reported()
	{
		CGodotShader shader = Translate(Frame + "float4 PS(float2 a : TEXCOORD0, float2 b : TEXCOORD3) : COLOR { return float4(a, b); }\n" +
			"technique t { pass p { VertexShader = compile vs_2_0 VS(); PixelShader = compile ps_2_0 PS(); } }");

		Assert.Contains(shader.Problems, p => p.Contains("reads TEXCOORD3"));
	}
}
