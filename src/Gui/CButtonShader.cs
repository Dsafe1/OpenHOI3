using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// How a sprite looks under the mouse, held down and switched off: the four techniques - <c>up</c>, <c>over</c>,
/// <c>down</c> and <c>disable</c> - of the effect file the sprite names, translated for the canvas
/// (<c>docs/mechanics/shaders.md</c>), its texture <c>tex0</c> the one the element is drawn with, so a mod's file is the
/// one drawn. A sprite that names none is drawn through <c>gfx/FX/buttonstate.fx</c>, the original's default
/// (<c>reversing/findings/FINDINGS-buttonstatefx.md</c>); the base game's one other is <c>interpolated_buttonstate.fx</c>,
/// the same with a linear sampler, for the portraits' box. In the base game under the mouse a tenth is added to red, green
/// and blue, held down a tenth taken off, and switched off the picture drawn in grey by the weights 0.212671, 0.715160
/// and 0.072169. A frame of the sprite is never swapped for any of them. See <c>docs/mechanics/interface.md</c>, section 5.
/// </summary>
public sealed class CButtonShader
{
	public const string File = "buttonstate.fx";

	/// <summary>The texture of the file the element's own is: what its techniques sample.</summary>
	public const string DrawnTexture = "tex0";

	/// <summary>The meta a material keeps the effect file it was made for under.</summary>
	private const string FileMeta = "fx_file";

	/// <summary>The techniques, in the order of <see cref="EButtonLook"/>.</summary>
	private static readonly string[] Techniques = { "up", "over", "down", "disable" };

	private readonly CEffectShaders _effects;
	private readonly Dictionary<string, Shader?[]> _shaders = new(System.StringComparer.OrdinalIgnoreCase);

	public CButtonShader(CEffectShaders effects)
	{
		_effects = effects;
	}

	/// <summary>
	/// A material for one element, drawn up, through the effect its sprite names - <c>buttonstate.fx</c> where it names
	/// none or there is no sprite. Each has its own, since each has its own look.
	/// </summary>
	public ShaderMaterial Material(CSpriteType? sprite = null)
	{
		string file = FileOf(sprite);
		var material = new ShaderMaterial { Shader = Shaders(file)[(int)EButtonLook.Up] };
		material.SetMeta(FileMeta, file);
		return material;
	}

	/// <summary>An element drawn the way a look is: that technique's shader of its effect on its material.</summary>
	public void Set(ShaderMaterial material, EButtonLook look)
	{
		string file = material.HasMeta(FileMeta) ? material.GetMeta(FileMeta).AsString() : File;
		Shader? shader = Shaders(file)[(int)look];
		if (material.Shader != shader)
		{
			material.Shader = shader;
		}
	}

	/// <summary>The file of <c>gfx/FX</c> a sprite's <c>effectFile</c> names, written with either slash; the default where none.</summary>
	private static string FileOf(CSpriteType? sprite)
	{
		// A shield's and a bar's effect draws them by their own classes, with no 'up' among its techniques.
		string written = sprite is null || sprite.IsMaskedShield || sprite.IsProgressBar ? "" : sprite.EffectFile;
		return written.Split('\\', '/').LastOrDefault(part => part.Length > 0) ?? File;
	}

	private Shader?[] Shaders(string file)
	{
		if (!_shaders.TryGetValue(file, out Shader?[]? shaders))
		{
			shaders = new Shader?[Techniques.Length];
			for (int look = 0; look < Techniques.Length; look++)
			{
				shaders[look] = _effects.Get(file, Techniques[look], EShaderTarget.Canvas, DrawnTexture).Shader;
			}
			_shaders[file] = shaders;
		}
		return shaders;
	}
}
