using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenHOI3.Content.Shaders;

/// <summary>
/// Writes a pass of an effect's technique as a Godot spatial shader: the functions its two entry points reach, typed as
/// HLSL types them and written in Godot's stricter language - every conversion HLSL makes without a word made explicit -
/// its parameters as uniforms, its samplers with their filters, a vertex function that reads a vertex's channels by
/// semantic and writes the clip-space position, and a fragment function that reads the varyings by semantic and writes
/// the colour. Matrices are kept transposed - an HLSL row is a Godot column - which makes <c>mul(a, b)</c> <c>b * a</c>
/// and leaves indexing and constructors as written. What it cannot translate is reported, and the code still written.
/// See <c>docs/mechanics/shaders.md</c>, section 3.
/// </summary>
public sealed class CGodotShaderWriter
{
	private static readonly Regex Swizzle = new("^([xyzw]{1,4}|[rgba]{1,4})$", RegexOptions.Compiled);
	private static readonly Regex IntegerLiteral = new(@"^-?(\d+|0[xX][0-9a-fA-F]+)$", RegexOptions.Compiled);

	/// <summary>Words of Godot's shading language and its built-ins a name of an effect's may not be.</summary>
	private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
	{
		"void", "bool", "bvec2", "bvec3", "bvec4", "int", "ivec2", "ivec3", "ivec4", "uint", "uvec2", "uvec3", "uvec4", "float",
		"vec2", "vec3", "vec4", "mat2", "mat3", "mat4", "sampler2D", "isampler2D", "usampler2D", "sampler2DArray", "sampler3D",
		"samplerCube", "samplerExternalOES", "struct", "const", "uniform", "varying", "in", "out", "inout", "flat", "smooth",
		"lowp", "mediump", "highp", "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue",
		"return", "discard", "true", "false", "render_mode", "shader_type", "global", "instance", "group_uniforms",
		"stencil_mode", "texture", "textureLod", "textureProj", "textureGrad", "textureSize", "texelFetch", "mix", "fract",
		"clamp", "step", "smoothstep", "length", "distance", "dot", "cross", "normalize", "reflect", "refract", "inversesqrt",
		"sqrt", "pow", "exp", "exp2", "log", "log2", "abs", "sign", "floor", "ceil", "round", "trunc", "mod", "min", "max",
		"sin", "cos", "tan", "asin", "acos", "atan", "sinh", "cosh", "tanh", "radians", "degrees", "lessThan", "greaterThan",
		"equal", "notEqual", "any", "all", "not", "transpose", "inverse", "determinant", "matrixCompMult", "outerProduct",
		"dFdx", "dFdy", "fwidth", "vertex", "fragment", "light", "start", "process", "sky", "fog", "PI", "TAU", "E", "TIME",
		"VERTEX", "NORMAL", "TANGENT", "BINORMAL", "UV", "UV2", "COLOR", "POSITION", "POINT_SIZE", "ALBEDO", "ALPHA",
		"EMISSION", "ROUGHNESS", "METALLIC", "SPECULAR", "DEPTH", "FRAGCOORD", "FRONT_FACING", "SCREEN_UV", "VIEWPORT_SIZE",
		"CUSTOM0", "CUSTOM1", "CUSTOM2", "CUSTOM3", "MODEL_MATRIX", "VIEW_MATRIX", "PROJECTION_MATRIX", "INSTANCE_ID",
		"VERTEX_ID", "BONE_INDICES", "BONE_WEIGHTS", "LIGHT", "ATTENUATION", "AO", "RIM", "CLEARCOAT", "SSS_STRENGTH",
		"precision", "invariant", "sample", "centroid", "isnan", "isinf", "packHalf2x16", "unpackHalf2x16",
	};

	private readonly CFxEffect _effect;
	private readonly List<string> _problems = new();
	private readonly Dictionary<string, Symbol> _globals = new(StringComparer.Ordinal);
	private Stack<Dictionary<string, Symbol>> _scopes = new();
	private CFxFunction? _function;

	/// <summary>A name's type, and whether it is an array.</summary>
	private readonly record struct Symbol(CFxType Type, int? ArraySize, string Code);

	/// <summary>Translated code and the HLSL type of what it gives.</summary>
	private readonly record struct Typed(string Code, CFxType Type, int? ArraySize = null);

	private readonly EShaderTarget _target;
	private readonly string? _drawnTexture;

	/// <summary>The functions that sample the drawn texture, themselves or through what they call, which are handed it.</summary>
	private readonly HashSet<string> _drawing = new(StringComparer.Ordinal);

	/// <summary>The hidden parameter the drawn texture is handed down in: Godot's <c>TEXTURE</c> is seen in the stages alone.</summary>
	private const string Drawn = "fx_drawn";

	private readonly IReadOnlyDictionary<string, string>? _device;

	/// <summary>The effect's parameters made Godot's global uniforms, each under the name given.</summary>
	private readonly IReadOnlyDictionary<string, string>? _shared;

	private CGodotShaderWriter(CFxEffect effect, EShaderTarget target, string? drawnTexture, IReadOnlyDictionary<string, string>? device,
		IReadOnlyDictionary<string, string>? shared)
	{
		_effect = effect;
		_target = target;
		_drawnTexture = target == EShaderTarget.Canvas ? drawnTexture : null;
		_device = device;
		_shared = shared;
	}

	private bool Canvas => _target == EShaderTarget.Canvas;

	/// <summary>
	/// A pass of an effect's technique as a Godot shader, for the 3D renderer or the 2D canvas. On the canvas a texture of
	/// the effect may be named the drawn one: every sampler of it reads the texture Godot draws the element with - its
	/// <c>TEXTURE</c> - instead of a uniform, so the element is drawn as any other is. The device's render states as the
	/// engine leaves them at the draw - <c>SRCBLEND</c>, <c>ALPHATESTENABLE</c>, <c>ZENABLE</c> and the rest, by name - are
	/// what the pass's own are laid over; with none, a blend by source alpha. Parameters named in
	/// <paramref name="shared"/> - one matrix for a thousand meshes - become Godot's global uniforms under the names given,
	/// which the engine has to have registered before the shader is compiled.
	/// </summary>
	public static CGodotShader Write(CFxEffect effect, CFxTechnique technique, CFxPass pass, EShaderTarget target = EShaderTarget.Spatial,
		string? drawnTexture = null, IReadOnlyDictionary<string, string>? device = null, IReadOnlyDictionary<string, string>? shared = null)
	{
		return new CGodotShaderWriter(effect, target, drawnTexture, device, shared).WritePass(technique, pass);
	}

	/// <summary>A name as the shader has it: the effect's own, but for one Godot's language reserves.</summary>
	public static string Rename(string name) => Reserved.Contains(name) || name.StartsWith("gl_", StringComparison.Ordinal) ? "fx_" + name : name;

	private void Problem(int line, string message) => _problems.Add($"line {line}: {message}");

	private CGodotShader WritePass(CFxTechnique technique, CFxPass pass)
	{
		var code = new StringBuilder();
		var uniforms = new List<CShaderUniform>();
		var textures = new List<CShaderTexture>();
		var inputs = new List<CShaderVertexInput>();
		CFxFunction? vertex = pass.Vertex is { } v ? _effect.Function(v.Function) : null;
		CFxFunction? pixel = pass.Pixel is { } p ? _effect.Function(p.Function) : null;
		if (pass.Vertex is not null && vertex is null)
		{
			Problem(technique.Line, $"the vertex shader '{pass.Vertex.Function}' is not a function of the file");
		}
		if (pass.Pixel is not null && pixel is null)
		{
			Problem(technique.Line, $"the pixel shader '{pass.Pixel.Function}' is not a function of the file");
		}

		SetStates(pass);
		code.Append(Canvas ? "shader_type canvas_item;\n" : "shader_type spatial;\n");
		code.Append("render_mode ").Append(string.Join(", ", RenderModes())).Append(";\n\n");

		// What the two entry points reach, in the file's order, which is the order Godot needs them declared in.
		var reached = new HashSet<string>(StringComparer.Ordinal);
		var names = new HashSet<string>(StringComparer.Ordinal);
		foreach (CFxFunction entry in new[] { vertex, pixel }.OfType<CFxFunction>())
		{
			Reach(entry, reached, names);
		}
		foreach (CFxExpression argument in new[] { pass.Vertex, pass.Pixel }.OfType<CFxShaderBinding>().SelectMany(b => b.Arguments))
		{
			Collect(argument, names);
		}
		FindDrawing(reached);

		foreach (CFxStruct type in StructsUsed(reached, vertex, pixel))
		{
			code.Append("struct ").Append(Rename(type.Name)).Append(" {\n");
			foreach (CFxField field in type.Fields)
			{
				code.Append('\t').Append(GodotType(field.Type)).Append(' ').Append(Rename(field.Name))
					.Append(field.ArraySize is { } size ? $"[{size}]" : "").Append(";\n");
			}
			code.Append("};\n\n");
		}

		WriteGlobals(code, names, uniforms);
		WriteSamplers(code, names, textures);
		code.Append(Helpers);

		var varyings = new Dictionary<string, CFxType>(StringComparer.Ordinal);
		if (vertex is null)
		{
			// The fixed pipeline's vertex stage gives the vertex's colour and its first texture coordinate.
			varyings["COLOR0"] = CFxType.Vector(EFxBase.Float, 4);
			varyings["TEXCOORD0"] = CFxType.Vector(EFxBase.Float, 2);
		}
		else
		{
			foreach ((string semantic, CFxType type) in Outputs(vertex))
			{
				if (semantic != "POSITION" && IsVarying(semantic))
				{
					varyings[semantic] = type;
				}
			}
		}
		foreach ((string semantic, CFxType type) in varyings)
		{
			code.Append(type.Base == EFxBase.Float ? "varying " : "varying flat ").Append(GodotType(type)).Append(' ')
				.Append(VaryingName(semantic)).Append(";\n");
		}
		if (Canvas)
		{
			// The element's colour Godot gives it - its modulate - multiplied into what the pixel shader gives (section 3).
			code.Append("varying vec4 fx_modulate;\n");
		}
		if (varyings.Count > 0 || Canvas)
		{
			code.Append('\n');
		}

		foreach (CFxFunction function in _effect.Functions)
		{
			if (reached.Contains(function.Name) && function == _effect.Function(function.Name))
			{
				WriteFunction(code, function);
			}
		}

		if (vertex is not null && pass.Vertex is not null)
		{
			WriteVertex(code, vertex, pass.Vertex, varyings, inputs);
		}
		else
		{
			WriteFixedVertex(code, inputs);
		}
		if (pixel is not null && pass.Pixel is not null)
		{
			WriteFragment(code, pixel, pass.Pixel, varyings, pass);
		}
		else
		{
			WriteFixedFragment(code);
		}
		return new CGodotShader(technique.Name, pass.Name, code.ToString(), uniforms, textures, inputs, pass.States, _problems);
	}

	// ----------------------------------------------------------------------------------------------- the pass's frame

	/// <summary>The states in force for the pass: the engine's at the draw, the pass's own laid over them, upper case.</summary>
	private Dictionary<string, string> _states = new(StringComparer.Ordinal);

	/// <summary>
	/// The states a pass draws under: the device's as the engine left them at the draw - or, where none are given, the
	/// blend by source alpha the interface draws under - with every state the pass sets laid over them, as Direct3D lays a
	/// pass's states over the device's (section 3.7).
	/// </summary>
	private void SetStates(CFxPass pass)
	{
		_states = new Dictionary<string, string>(StringComparer.Ordinal);
		if (_device is null)
		{
			_states["SRCBLEND"] = "SRCALPHA";
			_states["DESTBLEND"] = "INVSRCALPHA";
		}
		else
		{
			foreach ((string name, string value) in _device)
			{
				_states[name.ToUpperInvariant()] = Normalised(value);
			}
		}
		foreach (CFxPassState state in pass.States)
		{
			_states[state.Name] = Normalised(state.Value);
		}
	}

	private static string Normalised(string value)
	{
		string upper = value.Trim().ToUpperInvariant();
		foreach (string prefix in new[] { "D3DBLEND_", "D3DCMP_", "D3DCULL_" })
		{
			if (upper.StartsWith(prefix, StringComparison.Ordinal))
			{
				return upper[prefix.Length..];
			}
		}
		return upper;
	}

	/// <summary>A state in force, or Direct3D's default where neither the engine nor the pass set it.</summary>
	private string State(string name, string fallback) => _states.TryGetValue(name, out string? value) ? value : fallback;

	private bool On(string name, bool fallback)
	{
		string value = State(name, fallback ? "TRUE" : "FALSE");
		return value is "TRUE" or "1";
	}

	/// <summary>
	/// How the pass's colour meets what is drawn: Direct3D's two blend factors as one of Godot's blend modes - null where
	/// the colour is written as it is - whether alpha is written for it, what alpha is written, and whether the colour is
	/// multiplied by its own alpha first, which is how SRCALPHA / ZERO - the far beaches' - is drawn. A pair with no mode
	/// of Godot's is mixed by alpha and reported.
	/// </summary>
	private (string? Mode, string Alpha, bool Premultiply) Blend()
	{
		if (!On("ALPHABLENDENABLE", false))
		{
			return (null, "1.0", false);
		}
		string source = State("SRCBLEND", "ONE");
		string destination = State("DESTBLEND", "ZERO");
		switch (source, destination)
		{
			case ("SRCALPHA", "INVSRCALPHA"):
				return ("blend_mix", "fx_colour.a", false);
			case ("ONE", "ZERO"):
				return (null, "1.0", false);
			case ("SRCALPHA", "ZERO"):
				return (null, "1.0", true);
			case ("SRCALPHA", "ONE"):
				return ("blend_add", "fx_colour.a", false);
			case ("ONE", "ONE"):
				return ("blend_add", "1.0", false);
			case ("ONE", "INVSRCALPHA"):
				return ("blend_premul_alpha", "fx_colour.a", false);
			case ("DESTCOLOR", "ZERO") or ("ZERO", "SRCCOLOR"):
				return ("blend_mul", "1.0", false);
			default:
				Problem(0, $"the blend {source} / {destination} has no mode of Godot's: mixed by alpha");
				return ("blend_mix", "fx_colour.a", false);
		}
	}

	/// <summary>
	/// The alpha test in force, as a condition that discards: <c>ALPHATESTENABLE</c> with its <c>ALPHAFUNC</c> against
	/// <c>ALPHAREF</c> over 255 - the engine's 8 and GREATEREQUAL (<c>reversing/findings/FINDINGS-mapdraw.md</c>, C.2) - or
	/// null where nothing is tested.
	/// </summary>
	private string? AlphaTest()
	{
		if (!On("ALPHATESTENABLE", false))
		{
			return null;
		}
		string reference = State("ALPHAREF", "0");
		int value = reference.StartsWith("0X", StringComparison.Ordinal)
			? int.Parse(reference[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
			: int.TryParse(reference, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
		string limit = (value / 255.0).ToString("0.0########", CultureInfo.InvariantCulture);
		string? passes = State("ALPHAFUNC", "ALWAYS") switch
		{
			"ALWAYS" => null,
			"NEVER" => "false",
			"LESS" => $"fx_colour.a < {limit}",
			"LESSEQUAL" => $"fx_colour.a <= {limit}",
			"EQUAL" => $"fx_colour.a == {limit}",
			"GREATER" => $"fx_colour.a > {limit}",
			"NOTEQUAL" => $"fx_colour.a != {limit}",
			"GREATEREQUAL" => $"fx_colour.a >= {limit}",
			string other => Unknown(other),
		};
		return passes is null ? null : $"!({passes})";

		string? Unknown(string function)
		{
			Problem(0, $"the alpha test {function} is not known: not tested");
			return null;
		}
	}

	/// <summary>
	/// The states in force as Godot's render modes: drawn unshaded, without Godot's fog and never culled - the map is flat
	/// and the winding the original's matrices give was not set against Godot's - blended as <see cref="Blend"/> says, and
	/// the depth test and depth writes off where <c>ZENABLE</c> and <c>ZWRITEENABLE</c> are (spatial alone).
	/// </summary>
	private IEnumerable<string> RenderModes()
	{
		yield return "unshaded";
		if (!Canvas)
		{
			yield return "cull_disabled";
			yield return "fog_disabled";
		}
		(string? mode, string _, bool _) = Blend();
		if (mode is not null)
		{
			yield return mode;
		}
		if (!Canvas && !On("ZENABLE", true))
		{
			yield return "depth_test_disabled";
		}
		if (!Canvas && !On("ZWRITEENABLE", true))
		{
			yield return "depth_draw_never";
		}
		else if (!Canvas && mode is not null)
		{
			// Direct3D writes depth whether or not a pass blends; Godot's blended materials do not unless told to, and what is
			// drawn after them - the sea after the beaches - would then cover them.
			yield return "depth_draw_always";
		}
	}

	/// <summary>The colour a pass gives, written as the states say: tested, multiplied by its alpha, blended or solid.</summary>
	private void WriteColour(StringBuilder code)
	{
		if (AlphaTest() is { } discard)
		{
			code.Append("\tif (").Append(discard).Append(") { discard; }\n");
		}
		(string? mode, string alpha, bool premultiply) = Blend();
		string rgb = premultiply ? "(fx_colour.rgb * fx_colour.a)" : "fx_colour.rgb";
		if (Canvas)
		{
			// Godot's 2D draws in gamma space, as Direct3D 9 does: no conversion.
			code.Append("\tCOLOR = vec4(clamp(").Append(rgb).Append(", 0.0, 1.0), clamp(").Append(alpha).Append(", 0.0, 1.0)) * fx_modulate;\n");
			return;
		}
		code.Append("\tALBEDO = fx_to_linear(").Append(rgb).Append(");\n");
		if (mode is not null)
		{
			code.Append("\tALPHA = clamp(").Append(alpha).Append(", 0.0, 1.0);\n");
		}
	}

	/// <summary>
	/// Helpers every translation carries: HLSL's <c>fmod</c>, which keeps the sign of the dividend where Godot's
	/// <c>mod</c> keeps the divisor's, and the conversion of a colour from the gamma space Direct3D 9 draws in to the
	/// linear space Godot's 3D draws in.
	/// </summary>
	private const string Helpers =
		"float fx_fmod(float a, float b) { return a - b * trunc(a / b); }\n" +
		"vec2 fx_fmod2(vec2 a, vec2 b) { return a - b * trunc(a / b); }\n" +
		"vec3 fx_fmod3(vec3 a, vec3 b) { return a - b * trunc(a / b); }\n" +
		"vec4 fx_fmod4(vec4 a, vec4 b) { return a - b * trunc(a / b); }\n" +
		"vec3 fx_to_linear(vec3 c) {\n" +
		"\tc = clamp(c, vec3(0.0), vec3(1.0));\n" +
		"\treturn mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));\n" +
		"}\n\n";

	private void WriteGlobals(StringBuilder code, HashSet<string> names, List<CShaderUniform> uniforms)
	{
		foreach (CFxVariable variable in _effect.Variables)
		{
			if (variable.Type.Base is EFxBase.Texture or EFxBase.String)
			{
				continue;
			}
			string name = Rename(variable.Name);
			_globals[variable.Name] = new Symbol(variable.Type, variable.ArraySize, name);
			if (!names.Contains(variable.Name))
			{
				continue;
			}
			string array = variable.ArraySize is { } size ? $"[{size}]" : "";
			string type = GodotType(variable.Type);
			string? value = variable.Initialiser is { } initialiser ? Initialiser(initialiser, variable.Type, variable.ArraySize) : null;
			if (variable.IsParameter && variable.ArraySize is null && _shared?.TryGetValue(variable.Name, out string? shared) == true)
			{
				// Godot's global uniforms take no value of their own: the engine registers each with one.
				_globals[variable.Name] = new Symbol(variable.Type, null, shared);
				code.Append("global uniform ").Append(type).Append(' ').Append(shared).Append(";\n");
				uniforms.Add(new CShaderUniform(variable.Name, shared, variable.Type, type, null, variable.Semantic,
					variable.Annotations, Shared: true));
			}
			else if (variable.IsParameter)
			{
				code.Append("uniform ").Append(type).Append(' ').Append(name).Append(array);
				if (value is not null && variable.ArraySize is null)
				{
					code.Append(" = ").Append(value);
				}
				code.Append(";\n");
				uniforms.Add(new CShaderUniform(variable.Name, name, variable.Type, type, variable.ArraySize, variable.Semantic,
					variable.Annotations));
			}
			else
			{
				if (value is null)
				{
					Problem(variable.Line, $"the constant '{variable.Name}' has no value");
					value = Zero(variable.Type);
				}
				code.Append("const ").Append(type).Append(' ').Append(name).Append(array).Append(" = ")
					.Append(variable.ArraySize is { } count ? $"{type}[{count}]({StripArray(value)})" : value).Append(";\n");
			}
		}
		code.Append('\n');
	}

	private static string StripArray(string value) => value.StartsWith('{') ? value[1..^1] : value;

	private void WriteSamplers(StringBuilder code, HashSet<string> names, List<CShaderTexture> textures)
	{
		HashSet<string> drawn = DrawnSamplers();
		foreach (CFxSampler sampler in _effect.Samplers)
		{
			bool isDrawn = drawn.Contains(sampler.Name);
			string name = isDrawn ? Drawn : Rename(sampler.Name);
			_globals[sampler.Name] = new Symbol(sampler.Type, null, name);
			if (!names.Contains(sampler.Name))
			{
				continue;
			}
			string State(string key, string fallback) => sampler.States.TryGetValue(key, out string? value) ? value.ToUpperInvariant() : fallback;
			// Direct3D's defaults: point filtering, no mipmaps, wrapping.
			bool linear = State("MINFILTER", "POINT") is "LINEAR" or "ANISOTROPIC" || State("MAGFILTER", "POINT") is "LINEAR" or "ANISOTROPIC";
			bool mipmap = State("MIPFILTER", "NONE") is not ("NONE" or "0");
			bool repeat = State("ADDRESSU", "WRAP") == "WRAP";
			string filter = (linear ? "filter_linear" : "filter_nearest") + (mipmap ? "_mipmap" : "");
			// The drawn texture is Godot's TEXTURE, handed down; its filter and repeat are the element's to set from these.
			if (!isDrawn)
			{
				code.Append("uniform ").Append(sampler.Type.Name == "samplerCUBE" ? "samplerCube" : "sampler2D").Append(' ').Append(name)
					.Append(" : ").Append(filter).Append(repeat ? ", repeat_enable" : ", repeat_disable").Append(";\n");
			}
			CFxVariable? texture = _effect.Variables.FirstOrDefault(v => v.Name == sampler.Texture);
			string? file = texture?.Annotations.TryGetValue("name", out string? named) == true ? named
				: texture?.Annotations.TryGetValue("ResourceName", out string? resource) == true ? resource : null;
			textures.Add(new CShaderTexture(sampler.Name, name, sampler.Texture, file, filter, repeat));
		}
		code.Append('\n');
	}

	// ------------------------------------------------------------------------------------------ what the entries reach

	/// <summary>The samplers that read the drawn texture.</summary>
	private HashSet<string> DrawnSamplers()
	{
		return _drawnTexture is null ? new HashSet<string>()
			: _effect.Samplers.Where(s => string.Equals(s.Texture, _drawnTexture, StringComparison.Ordinal)).Select(s => s.Name).ToHashSet();
	}

	/// <summary>Which reached functions sample the drawn texture, themselves or through a function they call.</summary>
	private void FindDrawing(HashSet<string> reached)
	{
		HashSet<string> samplers = DrawnSamplers();
		if (samplers.Count == 0)
		{
			return;
		}
		var direct = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		foreach (CFxFunction function in _effect.Functions.Where(f => reached.Contains(f.Name)))
		{
			var found = new HashSet<string>(StringComparer.Ordinal);
			Collect(function.Body, found);
			direct[function.Name] = found;
		}
		bool changed = true;
		while (changed)
		{
			changed = false;
			foreach ((string name, HashSet<string> found) in direct)
			{
				if (!_drawing.Contains(name) && found.Any(n => samplers.Contains(n) || _drawing.Contains(n)) && _drawing.Add(name))
				{
					changed = true;
				}
			}
		}
	}

	private void Reach(CFxFunction function, HashSet<string> reached, HashSet<string> names)
	{
		if (!reached.Add(function.Name))
		{
			return;
		}
		var found = new HashSet<string>(StringComparer.Ordinal);
		Collect(function.Body, found);
		foreach (string name in found)
		{
			names.Add(name);
			if (_effect.Function(name) is { } called && called != function)
			{
				Reach(called, reached, names);
			}
		}
	}

	private static void Collect(CFxStatement statement, HashSet<string> names)
	{
		switch (statement)
		{
			case CFxBlock block:
				foreach (CFxStatement inner in block.Statements)
				{
					Collect(inner, names);
				}
				break;
			case CFxDeclaration declaration:
				foreach (CFxDeclarator name in declaration.Names)
				{
					if (name.Initialiser is not null)
					{
						Collect(name.Initialiser, names);
					}
				}
				break;
			case CFxExpressionStatement expression:
				Collect(expression.Expression, names);
				break;
			case CFxReturn { Value: { } value }:
				Collect(value, names);
				break;
			case CFxIf branch:
				Collect(branch.Condition, names);
				Collect(branch.Then, names);
				if (branch.Else is not null)
				{
					Collect(branch.Else, names);
				}
				break;
			case CFxFor loop:
				if (loop.Initialiser is not null)
				{
					Collect(loop.Initialiser, names);
				}
				if (loop.Condition is not null)
				{
					Collect(loop.Condition, names);
				}
				if (loop.Step is not null)
				{
					Collect(loop.Step, names);
				}
				Collect(loop.Body, names);
				break;
		}
	}

	private static void Collect(CFxExpression expression, HashSet<string> names)
	{
		switch (expression)
		{
			case CFxName name:
				names.Add(name.Name);
				break;
			case CFxCall call:
				names.Add(call.Name);
				foreach (CFxExpression argument in call.Arguments)
				{
					Collect(argument, names);
				}
				break;
			case CFxUnary unary:
				Collect(unary.Operand, names);
				break;
			case CFxBinary binary:
				Collect(binary.Left, names);
				Collect(binary.Right, names);
				break;
			case CFxConditional conditional:
				Collect(conditional.Condition, names);
				Collect(conditional.WhenTrue, names);
				Collect(conditional.WhenFalse, names);
				break;
			case CFxMember member:
				Collect(member.Target, names);
				break;
			case CFxIndex index:
				Collect(index.Target, names);
				Collect(index.Index, names);
				break;
			case CFxCast cast:
				Collect(cast.Operand, names);
				break;
			case CFxAssign assign:
				Collect(assign.Target, names);
				Collect(assign.Value, names);
				break;
			case CFxInitialiserList list:
				foreach (CFxExpression item in list.Items)
				{
					Collect(item, names);
				}
				break;
		}
	}

	/// <summary>Every struct the reached functions and the two entries name, with the structs those name, in the file's order.</summary>
	private IEnumerable<CFxStruct> StructsUsed(HashSet<string> reached, CFxFunction? vertex, CFxFunction? pixel)
	{
		var used = new HashSet<string>(StringComparer.Ordinal);
		void Use(CFxType type)
		{
			if (type.Base == EFxBase.Struct && type.Name is { } name && used.Add(name) && _effect.Struct(name) is { } found)
			{
				foreach (CFxField field in found.Fields)
				{
					Use(field.Type);
				}
			}
		}
		foreach (CFxFunction function in _effect.Functions.Where(f => reached.Contains(f.Name)))
		{
			Use(function.ReturnType);
			foreach (CFxParameter parameter in function.Parameters)
			{
				Use(parameter.Type);
			}
			UseIn(function.Body, Use);
		}
		return _effect.Structs.Where(s => used.Contains(s.Name));
	}

	private static void UseIn(CFxStatement statement, Action<CFxType> use)
	{
		switch (statement)
		{
			case CFxBlock block:
				foreach (CFxStatement inner in block.Statements)
				{
					UseIn(inner, use);
				}
				break;
			case CFxDeclaration declaration:
				use(declaration.Type);
				break;
			case CFxIf branch:
				UseIn(branch.Then, use);
				if (branch.Else is not null)
				{
					UseIn(branch.Else, use);
				}
				break;
			case CFxFor loop:
				if (loop.Initialiser is not null)
				{
					UseIn(loop.Initialiser, use);
				}
				UseIn(loop.Body, use);
				break;
		}
	}

	// ------------------------------------------------------------------------------------------------------ functions

	private int _locals;

	/// <summary>
	/// A local's or a parameter's name: its own, but where it would hide a global, a function, a struct or a local of an
	/// enclosing block - which HLSL takes and Godot calls a redefinition - a fresh one.
	/// </summary>
	private string LocalName(string name)
	{
		string renamed = Rename(name);
		bool hides = Lookup(name) is not null || _effect.Function(name) is not null || _effect.Struct(name) is not null;
		return hides ? $"fx_{++_locals}_{name}" : renamed;
	}

	private void WriteFunction(StringBuilder code, CFxFunction function)
	{
		_function = function;
		_scopes = new Stack<Dictionary<string, Symbol>>();
		var scope = new Dictionary<string, Symbol>(StringComparer.Ordinal);
		_scopes.Push(scope);
		code.Append(GodotType(function.ReturnType)).Append(' ').Append(Rename(function.Name)).Append('(');
		var parameters = new List<string>();
		foreach (CFxParameter parameter in function.Parameters)
		{
			string modifier = parameter.Modifier is "out" or "inout" ? parameter.Modifier + " " : "";
			string name = LocalName(parameter.Name);
			scope[parameter.Name] = new Symbol(parameter.Type, parameter.ArraySize, name);
			parameters.Add($"{modifier}{GodotType(parameter.Type)} {name}{(parameter.ArraySize is { } size ? $"[{size}]" : "")}");
		}
		if (_drawing.Contains(function.Name))
		{
			parameters.Add($"sampler2D {Drawn}");
		}
		code.Append(string.Join(", ", parameters)).Append(") ");
		WriteBlock(code, function.Body, 0);
		code.Append("\n\n");
		_function = null;
	}

	private void WriteBlock(StringBuilder code, CFxBlock block, int depth)
	{
		_scopes.Push(new Dictionary<string, Symbol>(StringComparer.Ordinal));
		code.Append("{\n");
		foreach (CFxStatement statement in block.Statements)
		{
			WriteStatement(code, statement, depth + 1);
		}
		code.Append(new string('\t', depth)).Append('}');
		_scopes.Pop();
	}

	private void WriteStatement(StringBuilder code, CFxStatement statement, int depth)
	{
		string indent = new('\t', depth);
		switch (statement)
		{
			case CFxBlock block:
				code.Append(indent);
				WriteBlock(code, block, depth);
				code.Append('\n');
				break;
			case CFxDeclaration declaration:
				foreach (CFxDeclarator name in declaration.Names)
				{
					string renamed = LocalName(name.Name);
					string type = GodotType(declaration.Type);
					code.Append(indent).Append(declaration.IsConst ? "const " : "").Append(type).Append(' ').Append(renamed)
						.Append(name.ArraySize is { } size ? $"[{size}]" : "");
					if (name.Initialiser is { } given)
					{
						string value = Initialiser(given, declaration.Type, name.ArraySize);
						code.Append(" = ").Append(name.ArraySize is { } count ? $"{type}[{count}]({StripArray(value)})" : value);
					}
					else if (declaration.IsConst)
					{
						Problem(declaration.Line, $"the constant '{name.Name}' has no value");
					}
					code.Append(";\n");
					_scopes.Peek()[name.Name] = new Symbol(declaration.Type, name.ArraySize, renamed);
				}
				break;
			case CFxExpressionStatement expression:
				code.Append(indent).Append(StatementExpression(expression.Expression)).Append(";\n");
				break;
			case CFxReturn ret:
				if (ret.Value is null)
				{
					code.Append(indent).Append("return;\n");
				}
				else
				{
					CFxType returnType = _function?.ReturnType ?? CFxType.Void;
					code.Append(indent).Append("return ").Append(Convert(Expression(ret.Value), returnType, ret.Line)).Append(";\n");
				}
				break;
			case CFxIf branch:
				code.Append(indent).Append("if (").Append(Condition(branch.Condition)).Append(") ");
				WriteBody(code, branch.Then, depth);
				if (branch.Else is not null)
				{
					code.Append(" else ");
					WriteBody(code, branch.Else, depth);
				}
				code.Append('\n');
				break;
			case CFxFor loop:
				_scopes.Push(new Dictionary<string, Symbol>(StringComparer.Ordinal));
				string initialiser = "";
				if (loop.Initialiser is not null)
				{
					var inner = new StringBuilder();
					WriteStatement(inner, loop.Initialiser, 0);
					initialiser = inner.ToString().TrimEnd('\n', ';');
				}
				code.Append(indent).Append("for (").Append(initialiser).Append("; ")
					.Append(loop.Condition is null ? "" : Condition(loop.Condition)).Append("; ")
					.Append(loop.Step is null ? "" : StatementExpression(loop.Step)).Append(") ");
				WriteBody(code, loop.Body, depth);
				code.Append('\n');
				_scopes.Pop();
				break;
			case CFxJump jump:
				code.Append(indent).Append(jump.Keyword).Append(";\n");
				break;
		}
	}

	private void WriteBody(StringBuilder code, CFxStatement body, int depth)
	{
		CFxBlock block = body as CFxBlock ?? new CFxBlock(body.Line, new[] { body });
		WriteBlock(code, block, depth);
	}

	/// <summary>An expression standing as a statement: an assignment, a call, an increment - and <c>clip</c>, which discards.</summary>
	private string StatementExpression(CFxExpression expression)
	{
		if (expression is CFxCall { Name: "clip" } clip && clip.Arguments.Count == 1)
		{
			Typed value = Expression(clip.Arguments[0]);
			return value.Type.IsScalar
				? $"if ({Convert(value, CFxType.Float, clip.Line)} < 0.0) {{ discard; }}"
				: $"if (any(lessThan({value.Code}, {GodotType(value.Type.WithBase(EFxBase.Float))}(0.0)))) {{ discard; }}";
		}
		if (expression is CFxCall { Name: "sincos" } sincos && sincos.Arguments.Count == 3)
		{
			Typed angle = Expression(sincos.Arguments[0]);
			Typed sine = Expression(sincos.Arguments[1]);
			Typed cosine = Expression(sincos.Arguments[2]);
			return $"{sine.Code} = {Convert(new Typed($"sin({angle.Code})", angle.Type), sine.Type, sincos.Line)}; " +
				$"{cosine.Code} = {Convert(new Typed($"cos({angle.Code})", angle.Type), cosine.Type, sincos.Line)}";
		}
		return Expression(expression).Code;
	}

	private string Condition(CFxExpression expression)
	{
		Typed value = Expression(expression);
		if (value.Type.Base == EFxBase.Bool && value.Type.IsScalar)
		{
			return Strip(value.Code);
		}
		if (!value.Type.IsScalar)
		{
			Problem(expression.Line, $"a condition of type {value.Type}: its first component is tested");
		}
		Typed first = new(Convert(value, value.Type.IsScalar ? value.Type : CFxType.Vector(value.Type.Base, 1), expression.Line),
			CFxType.Vector(value.Type.Base, 1));
		return first.Type.Base == EFxBase.Int ? $"{first.Code} != 0" : $"{first.Code} != 0.0";
	}

	private static string Strip(string code)
	{
		if (code.Length < 2 || code[0] != '(' || code[^1] != ')')
		{
			return code;
		}
		int depth = 0;
		for (int i = 0; i < code.Length - 1; i++)
		{
			depth += code[i] == '(' ? 1 : code[i] == ')' ? -1 : 0;
			if (depth == 0)
			{
				return code;
			}
		}
		return code[1..^1];
	}

	// ---------------------------------------------------------------------------------------------------- expressions

	private Symbol? Lookup(string name)
	{
		foreach (Dictionary<string, Symbol> scope in _scopes)
		{
			if (scope.TryGetValue(name, out Symbol symbol))
			{
				return symbol;
			}
		}
		return _globals.TryGetValue(name, out Symbol global) ? global : null;
	}

	private Typed Expression(CFxExpression expression)
	{
		switch (expression)
		{
			case CFxLiteral literal:
				return Literal(literal);
			case CFxName name:
				if (Lookup(name.Name) is { } symbol)
				{
					return new Typed(symbol.Code, symbol.Type, symbol.ArraySize);
				}
				Problem(name.Line, $"'{name.Name}' is not declared");
				return new Typed(Rename(name.Name), CFxType.Float);
			case CFxUnary unary:
				return Unary(unary);
			case CFxBinary binary:
				return Binary(binary);
			case CFxConditional conditional:
				return Conditional(conditional);
			case CFxCall call:
				return Call(call);
			case CFxMember member:
				return Member(member);
			case CFxIndex index:
				return Index(index);
			case CFxCast cast:
				return Cast(cast);
			case CFxAssign assign:
				return Assign(assign);
			case CFxInitialiserList list:
				Problem(list.Line, "a list of values where no type says what it makes");
				return new Typed("0.0", CFxType.Float);
			default:
				Problem(expression.Line, $"an expression not translated: {expression.GetType().Name}");
				return new Typed("0.0", CFxType.Float);
		}
	}

	private static Typed Literal(CFxLiteral literal)
	{
		string text = literal.Text;
		if (text is "true" or "false")
		{
			return new Typed(text, CFxType.Bool);
		}
		string trimmed = text.TrimEnd('f', 'F', 'h', 'H', 'u', 'U', 'l', 'L');
		bool isFloat = text.Length != trimmed.Length && text[^1] is 'f' or 'F' or 'h' or 'H'
			|| trimmed.Contains('.') || (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('e', StringComparison.OrdinalIgnoreCase));
		if (!isFloat)
		{
			if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				trimmed = long.Parse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
			}
			return new Typed(trimmed, CFxType.Int);
		}
		return new Typed(FloatText(trimmed), CFxType.Float);
	}

	/// <summary>A float as Godot writes one: a digit on each side of the point.</summary>
	private static string FloatText(string text)
	{
		if (text.StartsWith('.'))
		{
			text = "0" + text;
		}
		int exponent = text.IndexOfAny(new[] { 'e', 'E' });
		string mantissa = exponent < 0 ? text : text[..exponent];
		string rest = exponent < 0 ? "" : text[exponent..];
		if (!mantissa.Contains('.'))
		{
			mantissa += ".0";
		}
		else if (mantissa.EndsWith('.'))
		{
			mantissa += "0";
		}
		return mantissa + rest;
	}

	private Typed Unary(CFxUnary unary)
	{
		Typed operand = Expression(unary.Operand);
		switch (unary.Operator)
		{
			case "-":
				return new Typed($"(-{operand.Code})", operand.Type.Base == EFxBase.Bool ? operand.Type.WithBase(EFxBase.Int) : operand.Type);
			case "+":
				return operand;
			case "!":
				if (operand.Type.IsScalar)
				{
					return new Typed($"(!{BoolOf(operand, unary.Line)})", CFxType.Bool);
				}
				CFxType bools = operand.Type.WithBase(EFxBase.Bool);
				return new Typed($"not({Convert(operand, bools, unary.Line)})", bools);
			case "~":
				return new Typed($"(~{operand.Code})", operand.Type);
			default:
				return new Typed(unary.Postfix ? $"{operand.Code}{unary.Operator}" : $"{unary.Operator}{operand.Code}", operand.Type);
		}
	}

	private string BoolOf(Typed value, int line)
	{
		return value.Type.Base == EFxBase.Bool ? value.Code : Convert(value, CFxType.Bool, line);
	}

	private Typed Binary(CFxBinary binary)
	{
		Typed left = Expression(binary.Left);
		Typed right = Expression(binary.Right);
		string op = binary.Operator;
		if (op is "&&" or "||")
		{
			if (!left.Type.IsScalar || !right.Type.IsScalar)
			{
				Problem(binary.Line, $"'{op}' between vectors");
			}
			return new Typed($"({BoolOf(left, binary.Line)} {op} {BoolOf(right, binary.Line)})", CFxType.Bool);
		}
		if (left.Type.IsMatrix || right.Type.IsMatrix)
		{
			return MatrixArithmetic(binary, left, right);
		}
		CFxType common = Common(left.Type, right.Type, op is "+" or "-" or "*" or "/" or "%");
		// A scalar beside a vector stays a scalar, which Godot takes, but takes the vector's kind of number.
		string a = Convert(left, left.Type.IsScalar && !common.IsScalar ? CFxType.Vector(common.Base, 1) : common, binary.Line);
		string b = Convert(right, right.Type.IsScalar && !common.IsScalar ? CFxType.Vector(common.Base, 1) : common, binary.Line);
		switch (op)
		{
			case "<" or ">" or "<=" or ">=" or "==" or "!=":
				if (common.IsScalar)
				{
					return new Typed($"({a} {op} {b})", CFxType.Bool);
				}
				a = Convert(left, common, binary.Line);
				b = Convert(right, common, binary.Line);
				string function = op switch
				{
					"<" => "lessThan",
					">" => "greaterThan",
					"<=" => "lessThanEqual",
					">=" => "greaterThanEqual",
					"==" => "equal",
					_ => "notEqual",
				};
				return new Typed($"{function}({a}, {b})", common.WithBase(EFxBase.Bool));
			case "%":
				if (common.Base == EFxBase.Float)
				{
					a = Convert(left, common, binary.Line);
					b = Convert(right, common, binary.Line);
					return new Typed(common.IsScalar ? $"fx_fmod({a}, {b})" : $"fx_fmod{common.Columns}({a}, {b})", common);
				}
				return new Typed($"({a} % {b})", common);
			default:
				return new Typed($"({a} {op} {b})", common);
		}
	}

	private Typed MatrixArithmetic(CFxBinary binary, Typed left, Typed right)
	{
		if (left.Type.IsMatrix && right.Type.IsMatrix)
		{
			if (left.Type.Rows != right.Type.Rows || left.Type.Columns != right.Type.Columns)
			{
				Problem(binary.Line, $"'{binary.Operator}' between a {left.Type} and a {right.Type}");
			}
			return binary.Operator == "*"
				? new Typed($"matrixCompMult({left.Code}, {right.Code})", left.Type)
				: new Typed($"({left.Code} {binary.Operator} {right.Code})", left.Type);
		}
		Typed matrix = left.Type.IsMatrix ? left : right;
		Typed other = left.Type.IsMatrix ? right : left;
		if (!other.Type.IsScalar)
		{
			Problem(binary.Line, $"'{binary.Operator}' between a matrix and a {other.Type}, which HLSL does component by component");
		}
		string scalar = Convert(other, CFxType.Float, binary.Line);
		return left.Type.IsMatrix
			? new Typed($"({matrix.Code} {binary.Operator} {scalar})", matrix.Type)
			: new Typed($"({scalar} {binary.Operator} {matrix.Code})", matrix.Type);
	}

	/// <summary>
	/// The type two operands are brought to: the larger kind of number - float over int over bool, and arithmetic never
	/// in bool - and, of two vectors, the shorter, the longer cut down as HLSL cuts it.
	/// </summary>
	private static CFxType Common(CFxType a, CFxType b, bool arithmetic)
	{
		EFxBase scalar = a.Base == EFxBase.Float || b.Base == EFxBase.Float ? EFxBase.Float
			: a.Base == EFxBase.Int || b.Base == EFxBase.Int ? EFxBase.Int
			: arithmetic ? EFxBase.Int : EFxBase.Bool;
		int size = a.IsScalar ? b.Columns : b.IsScalar ? a.Columns : Math.Min(a.Columns, b.Columns);
		return CFxType.Vector(scalar, size);
	}

	private Typed Conditional(CFxConditional conditional)
	{
		Typed condition = Expression(conditional.Condition);
		Typed whenTrue = Expression(conditional.WhenTrue);
		Typed whenFalse = Expression(conditional.WhenFalse);
		CFxType common = whenTrue.Type.IsNumeric && whenFalse.Type.IsNumeric
			? Common(whenTrue.Type, whenFalse.Type, false)
			: whenTrue.Type;
		string a = Convert(whenTrue, common, conditional.Line);
		string b = Convert(whenFalse, common, conditional.Line);
		if (!condition.Type.IsScalar)
		{
			CFxType bools = CFxType.Vector(EFxBase.Bool, common.Columns);
			return new Typed($"mix({b}, {a}, {Convert(condition, bools, conditional.Line)})", common);
		}
		return new Typed($"({BoolOf(condition, conditional.Line)} ? {a} : {b})", common);
	}

	private Typed Member(CFxMember member)
	{
		Typed target = Expression(member.Target);
		if (target.Type.Base == EFxBase.Struct)
		{
			CFxField? field = _effect.Struct(target.Type.Name!)?.Fields.FirstOrDefault(f => f.Name == member.Member);
			if (field is null)
			{
				Problem(member.Line, $"'{target.Type.Name}' has no field '{member.Member}'");
				return new Typed($"{target.Code}.{Rename(member.Member)}", CFxType.Float);
			}
			return new Typed($"{target.Code}.{Rename(member.Member)}", field.Type, field.ArraySize);
		}
		if (target.Type.IsNumeric && !target.Type.IsMatrix && Swizzle.IsMatch(member.Member))
		{
			int length = member.Member.Length;
			CFxType type = CFxType.Vector(target.Type.Base, length);
			if (target.Type.IsScalar)
			{
				if (member.Member.Any(c => c is not ('x' or 'r')))
				{
					Problem(member.Line, $"a scalar has no component '{member.Member}'");
				}
				return length == 1 ? target : new Typed($"{GodotType(type)}({target.Code})", type);
			}
			return new Typed($"{Parenthesised(target.Code)}.{member.Member}", type);
		}
		Problem(member.Line, $"'.{member.Member}' on a {target.Type}");
		return new Typed($"{target.Code}.{member.Member}", CFxType.Float);
	}

	private Typed Index(CFxIndex index)
	{
		Typed target = Expression(index.Target);
		Typed position = Expression(index.Index);
		string at = Convert(position, CFxType.Int, index.Line);
		if (target.ArraySize is not null)
		{
			return new Typed($"{target.Code}[{at}]", target.Type);
		}
		if (target.Type.IsMatrix)
		{
			// A row of the HLSL matrix is a column of the Godot one, as long as the padded square.
			int padded = Math.Max(target.Type.Rows, target.Type.Columns);
			CFxType row = CFxType.Vector(target.Type.Base, target.Type.Columns);
			string code = $"{target.Code}[{at}]";
			return new Typed(padded == target.Type.Columns ? code : $"{code}.{"xyzw"[..target.Type.Columns]}", row);
		}
		if (target.Type.IsVector)
		{
			return new Typed($"{target.Code}[{at}]", CFxType.Vector(target.Type.Base, 1));
		}
		Problem(index.Line, $"an index into a {target.Type}");
		return target;
	}

	private Typed Cast(CFxCast cast)
	{
		if (cast.Type.Base == EFxBase.Struct)
		{
			if (cast.Operand is CFxLiteral { Text: "0" or "0.0" or "0.0f" or "0.f" })
			{
				return new Typed(Zero(cast.Type), cast.Type);
			}
			Problem(cast.Line, $"a cast to the struct '{cast.Type.Name}' of anything but nought");
			return new Typed(Zero(cast.Type), cast.Type);
		}
		Typed operand = Expression(cast.Operand);
		return new Typed(Convert(operand, cast.Type, cast.Line), cast.Type);
	}

	private Typed Assign(CFxAssign assign)
	{
		Typed target = Expression(assign.Target);
		Typed value = Expression(assign.Value);
		if (assign.Operator == "=")
		{
			return new Typed($"{target.Code} = {Convert(value, target.Type, assign.Line)}", target.Type);
		}
		string op = assign.Operator[..1];
		if (target.Type.IsMatrix || value.Type.IsMatrix)
		{
			if (op == "*" && target.Type.IsMatrix && value.Type.IsMatrix)
			{
				return new Typed($"{target.Code} = matrixCompMult({target.Code}, {value.Code})", target.Type);
			}
			return new Typed($"{target.Code} {assign.Operator} {Convert(value, value.Type.IsScalar ? CFxType.Float : target.Type, assign.Line)}", target.Type);
		}
		// A scalar on the right of a vector stays a scalar of the vector's kind; anything else becomes the target's type.
		CFxType right = value.Type.IsScalar ? CFxType.Vector(target.Type.Base, 1) : target.Type;
		string converted = Convert(value, right, assign.Line);
		if (op == "%" && target.Type.Base == EFxBase.Float)
		{
			string helper = target.Type.IsScalar ? "fx_fmod" : $"fx_fmod{target.Type.Columns}";
			return new Typed($"{target.Code} = {helper}({target.Code}, {Convert(value, target.Type, assign.Line)})", target.Type);
		}
		return new Typed($"{target.Code} {assign.Operator} {converted}", target.Type);
	}

	private string Initialiser(CFxExpression initialiser, CFxType type, int? arraySize)
	{
		if (initialiser is not CFxInitialiserList list)
		{
			return Convert(Expression(initialiser), type, initialiser.Line);
		}
		if (arraySize is not null)
		{
			return "{" + string.Join(", ", list.Items.Select(item => Initialiser(item, type, null))) + "}";
		}
		if (type.Base == EFxBase.Struct)
		{
			CFxStruct? found = _effect.Struct(type.Name!);
			if (found is null || found.Fields.Count != list.Items.Count)
			{
				Problem(list.Line, $"a list of {list.Items.Count} values for the struct '{type.Name}'");
				return Zero(type);
			}
			return $"{Rename(type.Name!)}({string.Join(", ", list.Items.Select((item, i) => Initialiser(item, found.Fields[i].Type, found.Fields[i].ArraySize)))})";
		}
		// A vector or a matrix from its components, in order: a matrix's row by row, which the transposed storage keeps.
		var scalars = new List<string>();
		foreach (CFxExpression item in list.Items)
		{
			Typed value = Expression(item);
			scalars.Add(value.Type.IsScalar ? Convert(value, CFxType.Vector(type.Base, 1), item.Line)
				: Convert(value, value.Type.WithBase(type.Base), item.Line));
		}
		return $"{GodotType(type)}({string.Join(", ", scalars)})";
	}

	// ---------------------------------------------------------------------------------------------------------- calls

	private Typed Call(CFxCall call)
	{
		if (CFxType.FromName(call.Name) is { IsNumeric: true } constructed)
		{
			return Construct(call, constructed);
		}
		if (_effect.Function(call.Name) is { } function)
		{
			return UserCall(call, function);
		}
		var arguments = call.Arguments.Select(Expression).ToList();
		return Intrinsic(call, arguments);
	}

	private Typed Construct(CFxCall call, CFxType type)
	{
		var arguments = call.Arguments.Select(Expression).ToList();
		if (type.IsScalar)
		{
			return arguments.Count == 1 ? new Typed(Convert(arguments[0], type, call.Line), type)
				: new Typed($"{GodotType(type)}({string.Join(", ", arguments.Select(a => a.Code))})", type);
		}
		if (type.IsMatrix && type.Rows != type.Columns)
		{
			Problem(call.Line, $"a {type} built from its components");
		}
		var parts = arguments.Select(argument => argument.Type.IsScalar
			? Convert(argument, CFxType.Vector(type.Base, 1), call.Line)
			: Convert(argument, argument.Type.WithBase(type.Base), call.Line));
		if (arguments.Count == 1 && arguments[0].Type.IsScalar && type.IsMatrix)
		{
			Problem(call.Line, $"a {type} made of one value, which Godot puts on the diagonal alone");
		}
		return new Typed($"{GodotType(type)}({string.Join(", ", parts)})", type);
	}

	private Typed UserCall(CFxCall call, CFxFunction function)
	{
		if (call.Arguments.Count != function.Parameters.Count)
		{
			Problem(call.Line, $"'{call.Name}' given {call.Arguments.Count} arguments for {function.Parameters.Count}");
		}
		var arguments = new List<string>();
		for (int i = 0; i < call.Arguments.Count; i++)
		{
			Typed argument = Expression(call.Arguments[i]);
			if (i >= function.Parameters.Count)
			{
				arguments.Add(argument.Code);
				continue;
			}
			CFxParameter parameter = function.Parameters[i];
			if (parameter.Modifier is "out" or "inout")
			{
				if (!SameType(argument.Type, parameter.Type))
				{
					Problem(call.Line, $"'{call.Name}' writes a {parameter.Type} into a {argument.Type}");
				}
				arguments.Add(argument.Code);
			}
			else
			{
				arguments.Add(Convert(argument, parameter.Type, call.Line));
			}
		}
		if (_drawing.Contains(function.Name))
		{
			arguments.Add(Drawn);
		}
		return new Typed($"{Rename(function.Name)}({string.Join(", ", arguments)})", function.ReturnType);
	}

	private Typed Intrinsic(CFxCall call, List<Typed> args)
	{
		string name = call.Name;
		int line = call.Line;
		Typed Fail(string message)
		{
			Problem(line, message);
			return new Typed("0.0", CFxType.Float);
		}
		bool Count(int expected)
		{
			if (args.Count == expected)
			{
				return true;
			}
			Problem(line, $"'{name}' given {args.Count} arguments for {expected}");
			return false;
		}
		CFxType FloatOf(CFxType type) => type.IsMatrix ? type.WithBase(EFxBase.Float) : CFxType.Vector(EFxBase.Float, type.Columns);
		// Every argument brought to one float type, the shortest vector of them, scalars spread out.
		CFxType Same(IEnumerable<Typed> values)
		{
			var list = values.ToList();
			int size = list.Where(v => !v.Type.IsScalar).Select(v => v.Type.Columns).DefaultIfEmpty(1).Min();
			return CFxType.Vector(EFxBase.Float, size);
		}
		string As(Typed value, CFxType type) => Convert(value, type, line);
		switch (name)
		{
			case "mul":
				return Count(2) ? Multiply(args[0], args[1], line) : Fail("mul");
			case "tex2D" or "tex1D":
				if (args.Count == 4)
				{
					return new Typed($"textureGrad({args[0].Code}, {As(args[1], CFxType.Vector(EFxBase.Float, 2))}, " +
						$"{As(args[2], CFxType.Vector(EFxBase.Float, 2))}, {As(args[3], CFxType.Vector(EFxBase.Float, 2))})",
						CFxType.Vector(EFxBase.Float, 4));
				}
				return Count(2) ? new Typed($"texture({args[0].Code}, {TextureCoordinate(args[1], name == "tex1D", line)})",
					CFxType.Vector(EFxBase.Float, 4)) : Fail(name);
			case "tex2Dlod":
				return Count(2) ? new Typed($"textureLod({args[0].Code}, {Parenthesised(As(args[1], CFxType.Vector(EFxBase.Float, 4)))}.xy, " +
					$"{Parenthesised(As(args[1], CFxType.Vector(EFxBase.Float, 4)))}.w)", CFxType.Vector(EFxBase.Float, 4)) : Fail(name);
			case "tex2Dbias":
				return Count(2) ? new Typed($"texture({args[0].Code}, {Parenthesised(As(args[1], CFxType.Vector(EFxBase.Float, 4)))}.xy, " +
					$"{Parenthesised(As(args[1], CFxType.Vector(EFxBase.Float, 4)))}.w)", CFxType.Vector(EFxBase.Float, 4)) : Fail(name);
			case "tex2Dproj":
				return Count(2) ? new Typed($"textureProj({args[0].Code}, {As(args[1], CFxType.Vector(EFxBase.Float, 4))})",
					CFxType.Vector(EFxBase.Float, 4)) : Fail(name);
			case "texCUBE":
				return Count(2) ? new Typed($"texture({args[0].Code}, {As(args[1], CFxType.Vector(EFxBase.Float, 3))})",
					CFxType.Vector(EFxBase.Float, 4)) : Fail(name);
			case "lerp":
			{
				if (!Count(3))
				{
					return Fail(name);
				}
				CFxType type = Same(args.Take(2));
				CFxType weight = args[2].Type.IsScalar ? CFxType.Float : type;
				return new Typed($"mix({As(args[0], type)}, {As(args[1], type)}, {As(args[2], weight)})", type);
			}
			case "saturate":
			{
				if (!Count(1))
				{
					return Fail(name);
				}
				CFxType type = FloatOf(args[0].Type);
				return new Typed($"clamp({As(args[0], type)}, 0.0, 1.0)", type);
			}
			case "clamp":
			{
				if (!Count(3))
				{
					return Fail(name);
				}
				CFxType type = Same(args);
				CFxType bound(Typed b) => b.Type.IsScalar ? CFxType.Float : type;
				return new Typed($"clamp({As(args[0], type)}, {As(args[1], bound(args[1]))}, {As(args[2], bound(args[2]))})", type);
			}
			case "min" or "max" or "pow" or "atan2" or "fmod" or "step" or "reflect" or "distance":
			{
				if (!Count(2))
				{
					return Fail(name);
				}
				CFxType type = Same(args);
				string a = As(args[0], type);
				string b = As(args[1], type);
				return name switch
				{
					"atan2" => new Typed($"atan({a}, {b})", type),
					"fmod" => new Typed(type.IsScalar ? $"fx_fmod({a}, {b})" : $"fx_fmod{type.Columns}({a}, {b})", type),
					"distance" => new Typed($"distance({a}, {b})", CFxType.Float),
					_ => new Typed($"{name}({a}, {b})", type),
				};
			}
			case "smoothstep":
			{
				if (!Count(3))
				{
					return Fail(name);
				}
				CFxType type = Same(args);
				return new Typed($"smoothstep({As(args[0], type)}, {As(args[1], type)}, {As(args[2], type)})", type);
			}
			case "dot":
			{
				if (!Count(2))
				{
					return Fail(name);
				}
				CFxType type = Same(args);
				return new Typed(type.IsScalar ? $"({As(args[0], type)} * {As(args[1], type)})" : $"dot({As(args[0], type)}, {As(args[1], type)})",
					CFxType.Float);
			}
			case "cross":
				return Count(2) ? new Typed($"cross({As(args[0], CFxType.Vector(EFxBase.Float, 3))}, {As(args[1], CFxType.Vector(EFxBase.Float, 3))})",
					CFxType.Vector(EFxBase.Float, 3)) : Fail(name);
			case "length":
				return Count(1) ? new Typed($"length({As(args[0], FloatOf(args[0].Type))})", CFxType.Float) : Fail(name);
			case "refract":
			{
				if (!Count(3))
				{
					return Fail(name);
				}
				CFxType type = Same(args.Take(2));
				return new Typed($"refract({As(args[0], type)}, {As(args[1], type)}, {As(args[2], CFxType.Float)})", type);
			}
			case "any" or "all":
			{
				if (!Count(1))
				{
					return Fail(name);
				}
				if (args[0].Type.IsScalar)
				{
					return new Typed(BoolOf(args[0], line), CFxType.Bool);
				}
				CFxType bools = args[0].Type.WithBase(EFxBase.Bool);
				return new Typed($"{name}({As(args[0], bools)})", CFxType.Bool);
			}
			case "transpose":
				return Count(1) ? new Typed($"transpose({args[0].Code})",
					CFxType.Matrix(args[0].Type.Base, args[0].Type.Columns, args[0].Type.Rows)) : Fail(name);
			case "determinant":
				return Count(1) ? new Typed($"determinant({args[0].Code})", CFxType.Float) : Fail(name);
			case "log10":
			{
				if (!Count(1))
				{
					return Fail(name);
				}
				CFxType type = FloatOf(args[0].Type);
				return new Typed($"(log({As(args[0], type)}) / log(10.0))", type);
			}
			case "rcp":
			{
				if (!Count(1))
				{
					return Fail(name);
				}
				CFxType type = FloatOf(args[0].Type);
				return new Typed($"(1.0 / {Parenthesised(As(args[0], type))})", type);
			}
		}
		string? unary = name switch
		{
			"frac" => "fract",
			"rsqrt" => "inversesqrt",
			"ddx" => "dFdx",
			"ddy" => "dFdy",
			"fwidth" => "fwidth",
			"normalize" or "abs" or "sign" or "floor" or "ceil" or "round" or "trunc" or "sqrt" or "exp" or "exp2" or "log"
				or "log2" or "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or "sinh" or "cosh" or "tanh"
				or "radians" or "degrees" => name,
			_ => null,
		};
		if (unary is not null)
		{
			if (!Count(1))
			{
				return Fail(name);
			}
			CFxType type = args[0].Type.Base == EFxBase.Int && name is "abs" or "sign" ? args[0].Type : FloatOf(args[0].Type);
			return new Typed($"{unary}({As(args[0], type)})", type);
		}
		return Fail($"the function '{name}' is not translated");
	}

	private string TextureCoordinate(Typed coordinate, bool oneDimensional, int line)
	{
		if (oneDimensional)
		{
			return $"vec2({Convert(coordinate, CFxType.Float, line)}, 0.5)";
		}
		return Convert(coordinate, CFxType.Vector(EFxBase.Float, 2), line);
	}

	/// <summary>
	/// <c>mul(a, b)</c>: a vector times a matrix, a matrix times a vector, two matrices, or anything by a scalar. The
	/// Godot matrix holds the HLSL rows as its columns, padded square, so <c>mul(v, M)</c> is <c>M * v</c> and
	/// <c>mul(M, v)</c> is <c>v * M</c>, the vector padded with noughts and the result cut to the HLSL size.
	/// </summary>
	private Typed Multiply(Typed a, Typed b, int line)
	{
		if (a.Type.IsScalar || b.Type.IsScalar)
		{
			CFxType type = a.Type.IsScalar ? b.Type : a.Type;
			if (type.IsMatrix)
			{
				return new Typed($"({Convert(a.Type.IsScalar ? a : b, CFxType.Float, line)} * {(a.Type.IsScalar ? b : a).Code})", type);
			}
			CFxType common = Common(a.Type, b.Type, true);
			return new Typed($"({Convert(a, a.Type.IsScalar ? CFxType.Vector(common.Base, 1) : common, line)} * " +
				$"{Convert(b, b.Type.IsScalar ? CFxType.Vector(common.Base, 1) : common, line)})", common);
		}
		if (a.Type.IsVector && b.Type.IsVector)
		{
			CFxType common = Common(a.Type, b.Type, true).WithBase(EFxBase.Float);
			return new Typed($"dot({Convert(a, common, line)}, {Convert(b, common, line)})", CFxType.Float);
		}
		if (a.Type.IsVector && b.Type.IsMatrix)
		{
			int padded = Math.Max(b.Type.Rows, b.Type.Columns);
			string vector = Pad(Convert(a, CFxType.Vector(EFxBase.Float, Math.Min(a.Type.Columns, b.Type.Rows)), line),
				Math.Min(a.Type.Columns, b.Type.Rows), padded);
			// Sizes that do not match are HLSL's implicit truncation - mul(float3, float4x4) uses the first three rows -
			// which the noughts the vector is padded with give.
			return new Typed(Cut($"({b.Code} * {vector})", padded, b.Type.Columns), CFxType.Vector(EFxBase.Float, b.Type.Columns));
		}
		if (a.Type.IsMatrix && b.Type.IsVector)
		{
			int padded = Math.Max(a.Type.Rows, a.Type.Columns);
			string vector = Pad(Convert(b, CFxType.Vector(EFxBase.Float, Math.Min(b.Type.Columns, a.Type.Columns)), line),
				Math.Min(b.Type.Columns, a.Type.Columns), padded);
			return new Typed(Cut($"({vector} * {a.Code})", padded, a.Type.Rows), CFxType.Vector(EFxBase.Float, a.Type.Rows));
		}
		if (a.Type.IsMatrix && b.Type.IsMatrix)
		{
			if (a.Type.Columns != b.Type.Rows || Math.Max(a.Type.Rows, a.Type.Columns) != Math.Max(b.Type.Rows, b.Type.Columns))
			{
				Problem(line, $"mul of a {a.Type} by a {b.Type}");
			}
			return new Typed($"({b.Code} * {a.Code})", CFxType.Matrix(EFxBase.Float, a.Type.Rows, b.Type.Columns));
		}
		Problem(line, $"mul of a {a.Type} by a {b.Type}");
		return new Typed("0.0", CFxType.Float);
	}

	private static string Pad(string code, int size, int padded)
	{
		if (size >= padded)
		{
			return code;
		}
		return $"vec{padded}({code}{string.Concat(Enumerable.Repeat(", 0.0", padded - size))})";
	}

	private static string Cut(string code, int padded, int size)
	{
		return size >= padded ? code : size == 1 ? $"{code}.x" : $"{code}.{"xyzw"[..size]}";
	}

	// ---------------------------------------------------------------------------------------------------- conversions

	private static bool SameType(CFxType a, CFxType b)
	{
		return a.Base == b.Base && a.Rows == b.Rows && a.Columns == b.Columns && a.IsMatrixType == b.IsMatrixType && a.Name == b.Name;
	}

	/// <summary>
	/// A value brought to a type as HLSL brings it without a word: a vector cut down to the first components, a scalar
	/// spread over a vector, a vector's first component for a scalar, one kind of number to another, a matrix's upper
	/// left corner for a smaller one. A shorter vector to a longer, or a vector to a matrix, is reported.
	/// </summary>
	private string Convert(Typed value, CFxType to, int line)
	{
		CFxType from = value.Type;
		if (SameType(from, to) || to.Base == EFxBase.Void)
		{
			return value.Code;
		}
		if (!from.IsNumeric || !to.IsNumeric)
		{
			if (!(from.Base == to.Base && from.Name == to.Name))
			{
				Problem(line, $"a {from} where a {to} is wanted");
			}
			return value.Code;
		}
		if (to.IsMatrix)
		{
			if (!from.IsMatrix)
			{
				Problem(line, $"a {from} where a {to} is wanted");
				return value.Code;
			}
			int fromSize = Math.Max(from.Rows, from.Columns);
			int toSize = Math.Max(to.Rows, to.Columns);
			return fromSize == toSize ? value.Code : $"{GodotType(to)}({value.Code})";
		}
		if (from.IsMatrix)
		{
			Problem(line, $"a {from} where a {to} is wanted");
			return value.Code;
		}
		string code = value.Code;
		int size = from.Columns;
		if (size > to.Columns)
		{
			code = $"{Parenthesised(code)}.{"xyzw"[..to.Columns]}";
			size = to.Columns;
		}
		else if (size < to.Columns && size > 1)
		{
			Problem(line, $"a {from} where a {to} is wanted");
			code = $"{GodotType(CFxType.Vector(from.Base, to.Columns))}({code}{string.Concat(Enumerable.Repeat(from.Base == EFxBase.Float ? ", 0.0" : from.Base == EFxBase.Int ? ", 0" : ", false", to.Columns - size))})";
			size = to.Columns;
		}
		if (from.Base != to.Base)
		{
			code = size == 1 ? ScalarConvert(code, from.Base, to.Base) : $"{GodotType(CFxType.Vector(to.Base, size))}({code})";
		}
		if (size == 1 && to.Columns > 1)
		{
			code = $"{GodotType(to)}({code})";
		}
		return code;
	}

	private static string ScalarConvert(string code, EFxBase from, EFxBase to)
	{
		if (to == EFxBase.Float && IntegerLiteral.IsMatch(code))
		{
			return code + ".0";
		}
		if (to == EFxBase.Float && code is "true" or "false")
		{
			return code == "true" ? "1.0" : "0.0";
		}
		if (to == EFxBase.Bool)
		{
			return from == EFxBase.Float ? $"({code} != 0.0)" : $"({code} != 0)";
		}
		return $"{(to == EFxBase.Float ? "float" : "int")}({code})";
	}

	private static string Parenthesised(string code)
	{
		return Regex.IsMatch(code, @"^[A-Za-z_][\w.\[\]]*$") ? code : $"({code})";
	}

	/// <summary>The Godot type of an HLSL one: a matrix of rows by columns as the padded square.</summary>
	public static string GodotType(CFxType type)
	{
		if (type.Base == EFxBase.Struct)
		{
			return Rename(type.Name!);
		}
		if (type.Base == EFxBase.Void)
		{
			return "void";
		}
		if (type.Base == EFxBase.Sampler)
		{
			return type.Name == "samplerCUBE" ? "samplerCube" : "sampler2D";
		}
		if (type.IsMatrix)
		{
			int size = Math.Max(type.Rows, type.Columns);
			return size == 1 ? "float" : $"mat{size}";
		}
		string prefix = type.Base switch
		{
			EFxBase.Int => "i",
			EFxBase.Bool => "b",
			_ => "",
		};
		if (type.Columns == 1)
		{
			return type.Base switch
			{
				EFxBase.Int => "int",
				EFxBase.Bool => "bool",
				_ => "float",
			};
		}
		return $"{prefix}vec{type.Columns}";
	}

	/// <summary>A type's nought: a struct's made of every field's.</summary>
	private string Zero(CFxType type)
	{
		if (type.Base == EFxBase.Struct)
		{
			CFxStruct? found = _effect.Struct(type.Name!);
			if (found is null)
			{
				return Rename(type.Name!) + "()";
			}
			return $"{Rename(type.Name!)}({string.Join(", ", found.Fields.Select(f => f.ArraySize is { } size ? $"{GodotType(f.Type)}[{size}]({string.Join(", ", Enumerable.Repeat(Zero(f.Type), size))})" : Zero(f.Type)))})";
		}
		if (type.IsMatrix)
		{
			return $"{GodotType(type)}(0.0)";
		}
		string scalar = type.Base switch
		{
			EFxBase.Int => "0",
			EFxBase.Bool => "false",
			_ => "0.0",
		};
		return type.Columns == 1 ? scalar : $"{GodotType(type)}({scalar})";
	}

	// ------------------------------------------------------------------------------------------------- the two stages

	/// <summary>Whether an output of the vertex shader reaches the pixel shader: every one but the point size and the fog.</summary>
	private static bool IsVarying(string semantic) => semantic is not ("PSIZE" or "FOG");

	private static string VaryingName(string semantic) => "fx_v_" + semantic.ToLowerInvariant();

	/// <summary>A semantic as written without its optional index: <c>COLOR</c> is <c>COLOR0</c>, <c>TEXCOORD</c> <c>TEXCOORD0</c>.</summary>
	private static string Normal(string semantic)
	{
		string upper = semantic.ToUpperInvariant();
		return upper switch
		{
			"COLOR" or "DIFFUSE" => "COLOR0",
			"SPECULAR" => "COLOR1",
			"TEXCOORD" => "TEXCOORD0",
			"POSITION0" => "POSITION",
			"NORMAL0" => "NORMAL",
			"DEPTH0" => "DEPTH",
			_ => upper,
		};
	}

	/// <summary>What a function gives by semantic: its return, a returned struct's fields, its out parameters.</summary>
	private IEnumerable<(string Semantic, CFxType Type)> Outputs(CFxFunction function)
	{
		if (function.ReturnSemantic is { } returned)
		{
			yield return (Normal(returned), function.ReturnType);
		}
		else if (function.ReturnType.Base == EFxBase.Struct && _effect.Struct(function.ReturnType.Name!) is { } type)
		{
			foreach (CFxField field in type.Fields.Where(f => f.Semantic is not null))
			{
				yield return (Normal(field.Semantic!), field.Type);
			}
		}
		foreach (CFxParameter parameter in function.Parameters.Where(p => p.Modifier is "out" or "inout"))
		{
			if (parameter.Semantic is { } semantic)
			{
				yield return (Normal(semantic), parameter.Type);
			}
			else if (parameter.Type.Base == EFxBase.Struct && _effect.Struct(parameter.Type.Name!) is { } structure)
			{
				foreach (CFxField field in structure.Fields.Where(f => f.Semantic is not null))
				{
					yield return (Normal(field.Semantic!), field.Type);
				}
			}
		}
	}

	/// <summary>
	/// <c>vertex()</c>: each input built from the mesh's channels by semantic, the entry point called with the
	/// <c>compile</c> line's values for its <c>uniform</c> parameters, the position written to <c>POSITION</c> and every
	/// other output to its varying, a colour clamped to nought and one as Direct3D 9 clamps it.
	/// </summary>
	private void WriteVertex(StringBuilder code, CFxFunction entry, CFxShaderBinding binding, Dictionary<string, CFxType> varyings,
		List<CShaderVertexInput> inputs)
	{
		_scopes = new Stack<Dictionary<string, Symbol>>();
		_scopes.Push(new Dictionary<string, Symbol>(StringComparer.Ordinal));
		code.Append("void vertex() {\n");
		if (Canvas)
		{
			code.Append("\tfx_modulate = COLOR;\n");
		}
		var channels = new ChannelAllocator();
		bool readsColour = false;
		string Read(string semantic, CFxType type)
		{
			string normal = Normal(semantic);
			readsColour |= normal == "COLOR0";
			(string channel, string expression, CFxType given) = Canvas ? CanvasChannel(normal) : channels.For(normal, type);
			if (!inputs.Any(i => i.Semantic == normal))
			{
				inputs.Add(new CShaderVertexInput(normal, channel, given.Columns));
			}
			return Convert(new Typed(expression, given), type, entry.Line);
		}
		var arguments = new List<string>();
		var outputs = new List<(string Local, CFxParameter Parameter)>();
		int uniformIndex = 0;
		foreach (CFxParameter parameter in entry.Parameters)
		{
			if (parameter.Modifier == "uniform")
			{
				arguments.Add(uniformIndex < binding.Arguments.Count
					? Convert(Expression(binding.Arguments[uniformIndex++]), parameter.Type, entry.Line)
					: Zero(parameter.Type));
				continue;
			}
			if (parameter.Modifier is "out" or "inout")
			{
				string local = "fx_out_" + parameter.Name;
				code.Append('\t').Append(GodotType(parameter.Type)).Append(' ').Append(local).Append(" = ").Append(Zero(parameter.Type)).Append(";\n");
				outputs.Add((local, parameter));
				arguments.Add(local);
				continue;
			}
			arguments.Add(InputValue(parameter.Type, parameter.Semantic, Read));
		}
		bool returns = entry.ReturnType.Base != EFxBase.Void;
		if (_drawing.Contains(entry.Name))
		{
			arguments.Add("TEXTURE");
		}
		string call = $"{Rename(entry.Name)}({string.Join(", ", arguments)})";
		if (returns)
		{
			code.Append('\t').Append(GodotType(entry.ReturnType)).Append(" fx_result = ").Append(call).Append(";\n");
		}
		else
		{
			code.Append('\t').Append(call).Append(";\n");
		}
		if (Canvas && readsColour)
		{
			// The vertex colour the shader read already carries the element's modulate: not again.
			code.Append("\tfx_modulate = vec4(1.0);\n");
		}
		bool position = false;
		void Write(string semantic, string expression, CFxType type)
		{
			string normal = Normal(semantic);
			if (normal == "POSITION")
			{
				// On the canvas Godot places the element; the position the shader works out is not used.
				if (!Canvas)
				{
					code.Append("\tPOSITION = ").Append(Convert(new Typed(expression, type), CFxType.Vector(EFxBase.Float, 4), entry.Line)).Append(";\n");
					// Direct3D's clip depth runs from nought near to w far, Godot's reversed from w near to nought far; and
					// Direct3D's clip y runs up the screen where the rendering device's runs down, which Godot's own
					// projection corrects and a written POSITION has to itself.
					code.Append("\tPOSITION.z = POSITION.w - POSITION.z;\n");
					code.Append("\tPOSITION.y = -POSITION.y;\n");
				}
				position = true;
			}
			else if (varyings.TryGetValue(normal, out CFxType? varying))
			{
				string value = Convert(new Typed(expression, type), varying, entry.Line);
				if (normal.StartsWith("COLOR", StringComparison.Ordinal) && varying.Base == EFxBase.Float)
				{
					value = $"clamp({value}, 0.0, 1.0)";
				}
				code.Append('\t').Append(VaryingName(normal)).Append(" = ").Append(value).Append(";\n");
			}
		}
		if (returns)
		{
			WriteOutputs(entry.ReturnType, entry.ReturnSemantic, "fx_result", Write);
		}
		foreach ((string local, CFxParameter parameter) in outputs)
		{
			WriteOutputs(parameter.Type, parameter.Semantic, local, Write);
		}
		if (!position)
		{
			Problem(entry.Line, $"the vertex shader '{entry.Name}' gives no POSITION");
		}
		code.Append("}\n\n");
	}

	private void WriteOutputs(CFxType type, string? semantic, string value, Action<string, string, CFxType> write)
	{
		if (semantic is not null)
		{
			write(semantic, value, type);
			return;
		}
		if (type.Base == EFxBase.Struct && _effect.Struct(type.Name!) is { } structure)
		{
			foreach (CFxField field in structure.Fields.Where(f => f.Semantic is not null))
			{
				write(field.Semantic!, $"{value}.{Rename(field.Name)}", field.Type);
			}
		}
	}

	/// <summary>An entry point's input: a value read by its semantic, or a struct of them built field by field.</summary>
	private string InputValue(CFxType type, string? semantic, Func<string, CFxType, string> read)
	{
		if (semantic is not null)
		{
			return read(semantic, type);
		}
		if (type.Base == EFxBase.Struct && _effect.Struct(type.Name!) is { } structure)
		{
			var fields = structure.Fields.Select(field => field.Semantic is null ? Zero(field.Type) : read(field.Semantic, field.Type));
			return $"{Rename(type.Name!)}({string.Join(", ", fields)})";
		}
		return Zero(type);
	}

	/// <summary>
	/// <c>fragment()</c>: each input read from its varying by semantic - one the vertex shader did not give, nought, as
	/// is the position, which a pixel shader of these models cannot read - the entry point called, and its colour written,
	/// its alpha only where the pass blends; a colour from gamma space into Godot's linear one.
	/// </summary>
	private void WriteFragment(StringBuilder code, CFxFunction entry, CFxShaderBinding binding, Dictionary<string, CFxType> varyings, CFxPass pass)
	{
		_scopes = new Stack<Dictionary<string, Symbol>>();
		_scopes.Push(new Dictionary<string, Symbol>(StringComparer.Ordinal));
		code.Append("void fragment() {\n");
		string Read(string semantic, CFxType type)
		{
			string normal = Normal(semantic);
			if (normal == "POSITION")
			{
				return Zero(type);
			}
			if (normal == "VPOS")
			{
				return Convert(new Typed("FRAGCOORD", CFxType.Vector(EFxBase.Float, 4)), type, entry.Line);
			}
			if (normal == "VFACE")
			{
				return Convert(new Typed("(FRONT_FACING ? 1.0 : -1.0)", CFxType.Float), type, entry.Line);
			}
			if (!varyings.TryGetValue(normal, out CFxType? varying))
			{
				Problem(entry.Line, $"the pixel shader '{entry.Name}' reads {normal}, which the vertex shader does not give");
				return Zero(type);
			}
			return type.Columns > varying.Columns && !type.IsMatrix
				? $"{GodotType(type)}({VaryingName(normal)}{string.Concat(Enumerable.Repeat(", 0.0", type.Columns - varying.Columns))})"
				: Convert(new Typed(VaryingName(normal), varying), type, entry.Line);
		}
		var arguments = new List<string>();
		var outputs = new List<(string Local, CFxParameter Parameter)>();
		int uniformIndex = 0;
		foreach (CFxParameter parameter in entry.Parameters)
		{
			if (parameter.Modifier == "uniform")
			{
				arguments.Add(uniformIndex < binding.Arguments.Count
					? Convert(Expression(binding.Arguments[uniformIndex++]), parameter.Type, entry.Line)
					: Zero(parameter.Type));
				continue;
			}
			if (parameter.Modifier is "out" or "inout")
			{
				string local = "fx_out_" + parameter.Name;
				code.Append('\t').Append(GodotType(parameter.Type)).Append(' ').Append(local).Append(" = ").Append(Zero(parameter.Type)).Append(";\n");
				outputs.Add((local, parameter));
				arguments.Add(local);
				continue;
			}
			arguments.Add(InputValue(parameter.Type, parameter.Semantic, Read));
		}
		if (_drawing.Contains(entry.Name))
		{
			arguments.Add("TEXTURE");
		}
		string call = $"{Rename(entry.Name)}({string.Join(", ", arguments)})";
		bool returns = entry.ReturnType.Base != EFxBase.Void;
		if (returns)
		{
			code.Append('\t').Append(GodotType(entry.ReturnType)).Append(" fx_result = ").Append(call).Append(";\n");
		}
		else
		{
			code.Append('\t').Append(call).Append(";\n");
		}
		bool colour = false;
		void Write(string semantic, string expression, CFxType type)
		{
			string normal = Normal(semantic);
			if (normal == "COLOR0")
			{
				string value = Convert(new Typed(expression, type), CFxType.Vector(EFxBase.Float, 4), entry.Line);
				code.Append("\tvec4 fx_colour = ").Append(value).Append(";\n");
				WriteColour(code);
				colour = true;
			}
			else if (normal == "DEPTH")
			{
				code.Append("\tDEPTH = ").Append(Convert(new Typed(expression, type), CFxType.Float, entry.Line)).Append(";\n");
			}
		}
		if (returns)
		{
			WriteOutputs(entry.ReturnType, entry.ReturnSemantic, "fx_result", Write);
		}
		foreach ((string local, CFxParameter parameter) in outputs)
		{
			WriteOutputs(parameter.Type, parameter.Semantic, local, Write);
		}
		if (!colour)
		{
			Problem(entry.Line, $"the pixel shader '{entry.Name}' gives no COLOR");
		}
		code.Append("}\n");
	}

	/// <summary>
	/// The fixed pipeline's vertex stage, for a pass with no vertex shader: the position by the engine's transform, its
	/// colour and first texture coordinate passed on (section 3.6).
	/// </summary>
	private void WriteFixedVertex(StringBuilder code, List<CShaderVertexInput> inputs)
	{
		inputs.Add(new CShaderVertexInput("POSITION", "VERTEX", Canvas ? 2 : 3));
		inputs.Add(new CShaderVertexInput("COLOR0", "COLOR", 4));
		inputs.Add(new CShaderVertexInput("TEXCOORD0", "UV", 2));
		if (Canvas)
		{
			code.Append("void vertex() {\n\tfx_modulate = vec4(1.0);\n");
			code.Append('\t').Append(VaryingName("COLOR0")).Append(" = COLOR;\n");
			code.Append('\t').Append(VaryingName("TEXCOORD0")).Append(" = UV;\n");
			code.Append("}\n\n");
			return;
		}
		code.Append("uniform mat4 fx_fixed_transform;\n");
		code.Append("void vertex() {\n");
		code.Append("\tPOSITION = fx_fixed_transform * vec4(VERTEX, 1.0);\n");
		code.Append('\t').Append(VaryingName("COLOR0")).Append(" = COLOR;\n");
		code.Append('\t').Append(VaryingName("TEXCOORD0")).Append(" = UV;\n");
		code.Append("}\n\n");
	}

	/// <summary>The fixed pipeline's colour, for a pass with no pixel shader: the vertex's colour, as its vertex stage gave it.</summary>
	private void WriteFixedFragment(StringBuilder code)
	{
		code.Append("void fragment() {\n");
		string colour = VaryingName("COLOR0");
		// On the canvas the fixed pipeline's first stage by default: the texture times the vertex's colour.
		// The vertex colour already carries the element's modulate; the fixed vertex stage leaves fx_modulate at one.
		code.Append("\tvec4 fx_colour = ").Append(Canvas ? $"texture(TEXTURE, {VaryingName("TEXCOORD0")}) * {colour}" : colour).Append(";\n");
		WriteColour(code);
		code.Append("}\n");
	}

	/// <summary>
	/// What a vertex input is on the canvas: the position Godot's own (with nought depth and w one), the first texture
	/// coordinate its UV, the colour its vertex colour; anything else the canvas does not have, nought and reported.
	/// </summary>
	private (string Channel, string Expression, CFxType Type) CanvasChannel(string semantic)
	{
		switch (semantic)
		{
			case "POSITION":
				return ("VERTEX", "vec4(VERTEX, 0.0, 1.0)", CFxType.Vector(EFxBase.Float, 4));
			case "TEXCOORD0":
				return ("UV", "UV", CFxType.Vector(EFxBase.Float, 2));
			case "COLOR0":
				return ("COLOR", "COLOR", CFxType.Vector(EFxBase.Float, 4));
			default:
				Problem(0, $"the canvas gives no {semantic}; it is nought");
				return ("none", "vec4(0.0)", CFxType.Vector(EFxBase.Float, 4));
		}
	}

	/// <summary>
	/// Which channel of a Godot mesh each semantic of a vertex is read from: the position the vertex, the normal, the
	/// tangent and the colour their own, and the texture coordinates <c>UV</c> and <c>UV2</c> while they fit in two
	/// components, otherwise and after them <c>CUSTOM0</c> to <c>CUSTOM3</c>, four each.
	/// </summary>
	private sealed class ChannelAllocator
	{
		private readonly Dictionary<string, (string Channel, string Expression, CFxType Type)> _given = new(StringComparer.Ordinal);
		private readonly Queue<string> _uv = new(new[] { "UV", "UV2" });
		private readonly Queue<string> _custom = new(new[] { "CUSTOM0", "CUSTOM1", "CUSTOM2", "CUSTOM3" });

		public (string Channel, string Expression, CFxType Type) For(string semantic, CFxType wanted)
		{
			if (_given.TryGetValue(semantic, out var known))
			{
				return known;
			}
			(string, string, CFxType) result = semantic switch
			{
				// A position of four components - the sea's (x, z, u, v) - is a whole custom channel; any other the vertex with w one.
				"POSITION" when wanted.Columns == 4 && !wanted.IsMatrix && _custom.Count > 0 => Custom(),
				"POSITION" => ("VERTEX", "vec4(VERTEX, 1.0)", CFxType.Vector(EFxBase.Float, 4)),
				"NORMAL" => ("NORMAL", "NORMAL", CFxType.Vector(EFxBase.Float, 3)),
				"TANGENT" => ("TANGENT", "vec4(TANGENT, 1.0)", CFxType.Vector(EFxBase.Float, 4)),
				"BINORMAL" => ("BINORMAL", "BINORMAL", CFxType.Vector(EFxBase.Float, 3)),
				"COLOR0" => ("COLOR", "COLOR", CFxType.Vector(EFxBase.Float, 4)),
				"BLENDINDICES" => ("BONE_INDICES", "vec4(BONE_INDICES)", CFxType.Vector(EFxBase.Float, 4)),
				"BLENDWEIGHT" => ("BONE_WEIGHTS", "BONE_WEIGHTS", CFxType.Vector(EFxBase.Float, 4)),
				_ => Other(wanted),
			};
			_given[semantic] = result;
			return result;
		}

		private (string, string, CFxType) Custom()
		{
			string channel = _custom.Dequeue();
			return (channel, channel, CFxType.Vector(EFxBase.Float, 4));
		}

		private (string, string, CFxType) Other(CFxType wanted)
		{
			if (wanted.Columns <= 2 && !wanted.IsMatrix && _uv.Count > 0)
			{
				string channel = _uv.Dequeue();
				return (channel, channel, CFxType.Vector(EFxBase.Float, 2));
			}
			if (_custom.Count > 0)
			{
				string channel = _custom.Dequeue();
				return (channel, channel, CFxType.Vector(EFxBase.Float, 4));
			}
			return ("none", "vec4(0.0)", CFxType.Vector(EFxBase.Float, 4));
		}
	}
}
