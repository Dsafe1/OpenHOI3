using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gui;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Gfx;

/// <summary>
/// A country's flag as a <c>maskedShieldType</c> sprite shows it: the flag through the effect the sprite names - the
/// install's own <c>gfx/FX/maskedflag.fx</c> in the base game, translated for the canvas - cut to its mask's alpha in the
/// middle of its overlay, by the technique of the element's look, and then the overlay over it, plainly. Each shield is a
/// <see cref="CShieldView"/> of its own; what a sprite's shields share - its pictures, its quad, its shaders - is made
/// once. See <c>docs/mechanics/interface.md</c>, section 5, and <c>reversing/findings/FINDINGS-maskedflag.md</c>.
/// </summary>
public sealed class CShields
{
	/// <summary>The effect a sprite that names none draws through.</summary>
	private const string DefaultEffect = "maskedflag.fx";

	/// <summary>The effect's texture that is the flag: the one the quad is drawn with.</summary>
	private const string FlagTexture = "tex0";

	/// <summary>The effect's techniques, in the order of <see cref="EButtonLook"/>: there is no <c>up</c>, <c>tec0</c> is it.</summary>
	private static readonly string[] Techniques = { "tec0", "over", "down", "disable" };

	private readonly CTextureManager _textures;
	private readonly CEffectShaders _effects;
	private readonly Dictionary<string, CShieldParts?> _parts = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Texture2D?> _flags = new(StringComparer.OrdinalIgnoreCase);

	public CShields(CTextureManager textures, CEffectShaders effects)
	{
		_textures = textures;
		_effects = effects;
	}

	/// <summary>
	/// The size a sprite's shield is drawn at, its overlay's - or its mask's where it has no overlay - or null where it
	/// cannot be drawn.
	/// </summary>
	public Vector2? Size(CSpriteType sprite) => Parts(sprite)?.Box;

	/// <summary>A country's shield as a sprite draws it, or null where its flag or the sprite's pictures are missing.</summary>
	public CShieldView? View(CCountry country, CSpriteType sprite)
	{
		if (Parts(sprite) is not { } parts || Flag(country) is not { } flag)
		{
			return null;
		}
		return new CShieldView(parts, flag);
	}

	private Texture2D? Flag(CCountry country)
	{
		if (!_flags.TryGetValue(country.FlagFile, out Texture2D? flag))
		{
			flag = country.FlagFile.Length == 0 || !_textures.Exists(country.FlagFile) ? null : _textures.LoadFromGamePath(country.FlagFile);
			_flags[country.FlagFile] = flag;
		}
		return flag;
	}

	private CShieldParts? Parts(CSpriteType sprite)
	{
		if (!_parts.TryGetValue(sprite.Name, out CShieldParts? parts))
		{
			parts = MakeParts(sprite);
			_parts[sprite.Name] = parts;
		}
		return parts;
	}

	private CShieldParts? MakeParts(CSpriteType sprite)
	{
		Texture2D? mask = sprite.MaskFile.Length == 0 ? null : _textures.LoadFromGamePath(sprite.MaskFile);
		if (mask is null)
		{
			CDiagnostics.Problem($"interface: the shield '{sprite.Name}' has no mask to cut a flag with, and is not drawn");
			return null;
		}
		Texture2D? overlay = sprite.OverlayFile.Length == 0 ? null : _textures.LoadFromGamePath(sprite.OverlayFile);
		if (overlay is null)
		{
			CDiagnostics.Problem($"interface: the shield '{sprite.Name}' has no texture to draw over a flag");
		}
		string file = EffectName(sprite.EffectFile);
		var shaders = new Shader?[Techniques.Length];
		CGodotShader? translation = null;
		for (int look = 0; look < Techniques.Length; look++)
		{
			(Shader? shader, CGodotShader? made) = _effects.Get(file, Techniques[look], EShaderTarget.Canvas, FlagTexture);
			shaders[look] = shader;
			translation ??= made;
		}
		if (translation is null || shaders[0] is null)
		{
			CDiagnostics.Problem($"interface: gfx/FX/{file} gives the shield '{sprite.Name}' no '{Techniques[0]}' technique, and it is not drawn");
			return null;
		}
		Vector2 maskSize = mask.GetSize();
		Vector2 box = overlay?.GetSize() ?? maskSize;
		return new CShieldParts(Quad(translation, maskSize, box), box, overlay, mask, shaders, translation);
	}

	/// <summary>The file of <c>gfx/FX</c> a sprite's <c>effectFile</c> names, written with either slash.</summary>
	private static string EffectName(string written)
	{
		string name = written.Split('\\', '/').LastOrDefault(part => part.Length > 0) ?? "";
		return name.Length == 0 ? DefaultEffect : name;
	}

	/// <summary>
	/// The flag's quad, as the original builds it (<c>FINDINGS-maskedflag.md</c>, section 4): the mask, W by H, in the
	/// middle of the overlay's box, with the original's mix of whole-number and half halves - an odd W or H puts its
	/// fourth corner half a pixel off - its first texture coordinates the flag's, 0 to 1, and its second the mask's.
	/// Drawn unturned: the element turns the whole shield. The box is y up in the original; here y runs down.
	/// </summary>
	private static ArrayMesh Quad(CGodotShader translation, Vector2 mask, Vector2 box)
	{
		float ox = (box.X - mask.X) * 0.5f;
		float oy = (box.Y - mask.Y) * 0.5f;
		float w2 = (int)mask.X / 2;
		float h2 = (int)mask.Y / 2;
		float halfW = mask.X / 2f;
		float halfH = mask.Y / 2f;
		// The corners at nought rotation, y up: R(...) + the turning point, with R the identity.
		Vector2[] up =
		{
			new(ox - w2 + w2, oy - h2 + h2),
			new(ox - halfW + halfW, oy + halfH + halfH),
			new(ox + halfW + halfW, oy + halfH + halfH),
			new(ox + w2 + halfW, oy - h2 + halfH),
		};
		Vector2[] flag = { new(0, 1), new(0, 0), new(1, 0), new(1, 1) };
		// The original's mask fills W/P(W) by H/P(H) of a texture padded to powers of two; a texture here is the picture alone.
		Vector2[] masked = { new(0, 1), new(0, 0), new(1, 0), new(1, 1) };

		using var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = up.Select(corner => new Vector2(corner.X, box.Y - corner.Y)).ToArray();
		// The fan v0 v1 v2 v3 as two triangles.
		arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
		Mesh.ArrayFormat format = 0;
		foreach (CShaderVertexInput input in translation.Inputs)
		{
			Vector2[]? values = input.Semantic switch
			{
				"TEXCOORD0" => flag,
				"TEXCOORD1" => masked,
				_ => null,
			};
			if (values is null)
			{
				continue;
			}
			if (input.Channel == "UV")
			{
				arrays[(int)Mesh.ArrayType.TexUV] = values;
			}
			else if (input.Channel is "CUSTOM0" or "CUSTOM1")
			{
				int slot = input.Channel[^1] - '0';
				var custom = new float[values.Length * 4];
				for (int v = 0; v < values.Length; v++)
				{
					custom[4 * v] = values[v].X;
					custom[4 * v + 1] = values[v].Y;
					custom[4 * v + 3] = 1f;
				}
				arrays[(int)Mesh.ArrayType.Custom0 + slot] = custom;
				format |= (Mesh.ArrayFormat)((long)Mesh.ArrayCustomFormat.RgbaFloat
					<< ((int)Mesh.ArrayFormat.FormatCustom0Shift + slot * (int)Mesh.ArrayFormat.FormatCustomBits));
			}
		}
		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, null, null, format);
		return mesh;
	}
}
