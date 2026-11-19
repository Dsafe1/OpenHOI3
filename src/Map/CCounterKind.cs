using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gfx;
using OpenHOI3.Gui;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Map;

/// <summary>
/// One of the two kinds of counter - the far one or the near one - with the pictures its type names and the install's
/// own <c>gfx/FX/counter.fx</c>, translated for the canvas, which puts a counter together from them where it is drawn:
/// the plate in its owner's colour, the unit type's picture, the size mark, the corner's mark and the selection's ring.
/// A material for each look a counter has had on screen is kept. See <c>docs/mechanics/mapunits.md</c>, section 2.
/// </summary>
public sealed class CCounterKind
{
	/// <summary>The picture a counter carries when its unit type has none of its own.</summary>
	private const string Unknown = "unknown";

	public const string EffectFile = "counter.fx";
	private const string Technique = "Standard";

	/// <summary>The effect's texture that is the plate: the one each counter's rectangle is drawn with.</summary>
	private const string PlateTexture = "BackgroundTex";

	/// <summary>The global uniform the selection's pulse is, set once a frame for every counter.</summary>
	public const string SharedPulse = "fx_counter_selection";

	private static readonly Dictionary<string, string> Shared = new() { ["SelectionIntensity"] = SharedPulse };

	/// <summary>The device's states around the counters (<c>reversing/findings/FINDINGS-counters.md</c>, section 10.5).</summary>
	private static readonly Dictionary<string, string> States = new()
	{
		["ALPHABLENDENABLE"] = "1", ["SRCBLEND"] = "SRCALPHA", ["DESTBLEND"] = "INVSRCALPHA",
		["ALPHATESTENABLE"] = "1", ["ALPHAREF"] = "8", ["ALPHAFUNC"] = "GREATEREQUAL",
	};

	private static bool _registered;

	private readonly Texture2D _plate;
	private readonly Texture2D _mask;
	private readonly Texture2D _sizes;
	private readonly Shader _shader;
	private readonly CGodotShader _translation;
	private readonly float _sizeOffset;
	private readonly string _family;
	private readonly Dictionary<(uint Colour, string Key, int Size, ECounterMark Mark, ECounterRing Ring), ShaderMaterial> _materials = new();
	private readonly Dictionary<string, Texture2D?> _pictures = new(StringComparer.OrdinalIgnoreCase);

	private CCounterKind(CCounterType type, Texture2D plate, Texture2D mask, Texture2D sizes, Shader shader,
		CGodotShader translation, string family)
	{
		Type = type;
		_plate = plate;
		_mask = mask;
		_sizes = sizes;
		_shader = shader;
		_translation = translation;
		_family = family;
		Aspect = (float)plate.GetWidth() / plate.GetHeight();
		// A texture here is its picture alone, where the original's is padded to a power of two.
		_sizeOffset = CCounterShading.SizeOffset(sizes.GetWidth(), sizes.GetWidth(), type.Frames);
	}

	public CCounterType Type { get; }

	/// <summary>The plate's width over its height.</summary>
	public float Aspect { get; }

	/// <summary>The texture each counter's rectangle is drawn with: the effect samples it as its plate.</summary>
	public Texture2D Plate => _plate;

	/// <summary>What the figures are written in, or null when no file declares the font the type names.</summary>
	public CGuiFont? Font { get; private init; }

	/// <summary>Whether a counter of this kind has its unit's name written on it: the near one does.</summary>
	public bool CarriesName { get; private init; }

	/// <summary>
	/// Makes the kind from its declaration, or answers null - with a line in the log - when there is no declaration, one
	/// of its three pictures cannot be read or <c>counter.fx</c> has no technique to draw it with.
	/// </summary>
	public static CCounterKind? Create(CGuiContext gui, CCounterType? type, string family, bool carriesName)
	{
		if (type is null)
		{
			CDiagnostics.Problem("map: a counter type is not declared, so that kind of counter is not drawn");
			return null;
		}
		Texture2D? plate = Load(gui, type.BackgroundFile);
		Texture2D? mask = Load(gui, type.MaskFile);
		Texture2D? sizes = Load(gui, type.SizeFile);
		if (plate is null || mask is null || sizes is null)
		{
			CDiagnostics.Problem($"map: counter type '{type.Name}' lacks one of its three pictures and is not drawn");
			return null;
		}
		RegisterShared();
		(Shader? shader, CGodotShader? translation) = gui.Effects.Get(EffectFile, Technique, EShaderTarget.Canvas, PlateTexture,
			device: States, shared: Shared);
		if (shader is null || translation is null)
		{
			CDiagnostics.Problem($"map: gfx/FX/{EffectFile} gives no '{Technique}' technique, so counter type '{type.Name}' is not drawn");
			return null;
		}
		CGuiFont? font = gui.Fonts.Find(type.Font);
		if (font is null && type.Font.Length != 0)
		{
			CDiagnostics.Problem($"map: counter type '{type.Name}' names the font '{type.Font}', which no file declares");
		}
		return new CCounterKind(type, plate, mask, sizes, shader, translation, family)
		{
			Font = font,
			CarriesName = carriesName,
		};
	}

	/// <summary>The pulse of every selected counter's ring, for the clock's seconds.</summary>
	public static void SetPulse(double seconds)
	{
		if (_registered)
		{
			RenderingServer.GlobalShaderParameterSet(SharedPulse, CCounterShading.SelectionIntensity(seconds));
		}
	}

	/// <summary>
	/// The material a counter is drawn with: made the first time its look is asked for and kept. Every parameter of the
	/// effect but the pulse is a counter's own and set here (<c>reversing/findings/FINDINGS-counters.md</c>, section 10.4).
	/// </summary>
	public ShaderMaterial Material(CGuiContext gui, CMapCounter counter, ECounterMark mark, ECounterRing ring)
	{
		var key = (Pack(counter.Colour), counter.TypeKey, counter.Size, mark, ring);
		if (_materials.TryGetValue(key, out ShaderMaterial? material))
		{
			return material;
		}
		material = new ShaderMaterial { Shader = _shader };
		SetTexture(material, "MaskTex", _mask);
		SetTexture(material, "SizeTex", _sizes);
		SetTexture(material, "CounterTex", Picture(gui, counter.TypeKey));
		Set(material, "SizeOffset", _sizeOffset);
		Set(material, "SizeFrame", (float)counter.Size);
		Set(material, "CountryColor", Colour(counter.Colour));
		Set(material, "vOtherColor", Colour(CCounterShading.ColourOf(mark)));
		// The selected unit's ring white, a unit's under it the type's second colour; with none the colour is left alone.
		if (ring != ECounterRing.None)
		{
			Set(material, "SelectionColor", ring == ECounterRing.Selected ? Colors.White : Colour(Type.Color2));
		}
		Set(material, "Selected", ring == ECounterRing.None ? 0f : 1f);
		Set(material, "CounterScale", 1f);
		_materials[key] = material;
		return material;
	}

	private void Set(ShaderMaterial material, string fxName, Variant value)
	{
		foreach (CShaderUniform uniform in _translation.Uniforms.Where(u => u.FxName == fxName && !u.Shared))
		{
			material.SetShaderParameter(uniform.Name, value);
		}
	}

	private void SetTexture(ShaderMaterial material, string texture, Texture2D? value)
	{
		if (value is null)
		{
			return;
		}
		foreach (CShaderTexture sampler in _translation.Textures.Where(t => string.Equals(t.Texture, texture, StringComparison.Ordinal)))
		{
			material.SetShaderParameter(sampler.Name, value);
		}
	}

	/// <summary>
	/// A unit type's picture in this kind's family of sprites, and where it has none the family's picture for a unit that
	/// is not known.
	/// </summary>
	private Texture2D? Picture(CGuiContext gui, string typeKey)
	{
		if (!_pictures.TryGetValue(typeKey, out Texture2D? picture))
		{
			CSpriteType? sprite = typeKey.Length == 0 ? null : gui.Sprites.Find(_family + typeKey);
			sprite ??= gui.Sprites.Find(_family + Unknown);
			picture = sprite is null ? null : Load(gui, sprite.TextureFile);
			_pictures[typeKey] = picture;
		}
		return picture;
	}

	/// <summary>A picture with its smaller copies, which the effect's samplers filter between.</summary>
	private static Texture2D? Load(CGuiContext gui, string file)
	{
		if (file.Length == 0 || gui.Textures.LoadImage(file) is not { } image)
		{
			return null;
		}
		if (image.IsCompressed())
		{
			image.Decompress();
		}
		image.Convert(Image.Format.Rgba8);
		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}

	/// <summary>The global uniform of the pulse, registered once for the process before the shader names it.</summary>
	private static void RegisterShared()
	{
		if (_registered)
		{
			return;
		}
		_registered = true;
		RenderingServer.GlobalShaderParameterAdd(SharedPulse, RenderingServer.GlobalShaderParameterType.Float, 0.5f);
	}

	private static Color Colour(CColor colour) => new(colour.R, colour.G, colour.B, colour.A);

	private static uint Pack(CColor colour)
	{
		static uint Part(float value) => (uint)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
		return (Part(colour.R) << 24) | (Part(colour.G) << 16) | (Part(colour.B) << 8) | Part(colour.A);
	}
}
