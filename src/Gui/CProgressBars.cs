using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gfx;
using OpenHOI3.Sim;

namespace OpenHOI3.Gui;

/// <summary>
/// How a <c>progressbartype</c> is drawn: through the effect file it names - the install's own <c>gfx/FX/progress.fx</c> -
/// translated for the canvas, by <c>ColorProgress</c> where both its pictures load and by <c>tec0</c>, its two colours,
/// where either does not; the split at <c>CurrentState</c>, the share filled. See <c>docs/mechanics/interface.md</c>,
/// section 5, and <c>reversing/findings/FINDINGS-progressfx.md</c>.
/// </summary>
public sealed class CProgressBars
{
	private const string DefaultEffect = "progress.fx";
	private const string ColourTechnique = "tec0";
	private const string PictureTechnique = "ColorProgress";

	/// <summary>The effect's texture over the filled part: the one the bar is drawn with.</summary>
	private const string FilledTexture = "tex0";
	private const string RestTexture = "tex1";

	/// <summary>The device's states around the interface: the pass turns the alpha test on, at the engine's reference of 8.</summary>
	private static readonly System.Collections.Generic.Dictionary<string, string> States = new()
	{
		["ALPHABLENDENABLE"] = "1", ["SRCBLEND"] = "SRCALPHA", ["DESTBLEND"] = "INVSRCALPHA",
		["ALPHAREF"] = "8", ["ALPHAFUNC"] = "GREATEREQUAL",
	};

	private readonly CEffectShaders _effects;

	public CProgressBars(CEffectShaders effects)
	{
		_effects = effects;
	}

	/// <summary>
	/// One bar's material, its colours the sprite's and nothing filled, or null - with a line in the log - where its effect
	/// has no technique to draw it with. Each bar has its own, since each has its own share.
	/// </summary>
	public CProgressBarMaterial? Material(CSpriteType sprite, Texture2D? filled, Texture2D? rest)
	{
		bool pictures = filled is not null && rest is not null;
		string file = sprite.EffectFile.Split('\\', '/').LastOrDefault(part => part.Length > 0) ?? DefaultEffect;
		string technique = pictures ? PictureTechnique : ColourTechnique;
		(Shader? shader, CGodotShader? translation) = _effects.Get(file, technique, EShaderTarget.Canvas, FilledTexture, device: States);
		if (shader is null || translation is null)
		{
			CDiagnostics.Problem($"interface: gfx/FX/{file} has no '{technique}' to draw the progress bar '{sprite.Name}' with, and it is not drawn");
			return null;
		}
		var material = new CProgressBarMaterial(new ShaderMaterial { Shader = shader }, translation);
		if (pictures)
		{
			material.SetTexture(RestTexture, rest!);
		}
		material.Set("vFirstColor", Colour(sprite.Colour));
		material.Set("vSecondColor", Colour(sprite.ColourTwo));
		material.Set("CurrentState", 0f);
		return material;
	}

	/// <summary>
	/// A <c>scrollingSprite</c>'s material: its effect's <c>ScrollTech</c>, which samples its picture shifted along u by
	/// <c>CurrentState</c> - the day and night overlay over the minimap (<c>reversing/findings/FINDINGS-minimap.md</c>,
	/// section 4). Null, with a line in the log, where the effect has none.
	/// </summary>
	public CProgressBarMaterial? ScrollMaterial(CSpriteType sprite)
	{
		string file = sprite.EffectFile.Split('\\', '/').LastOrDefault(part => part.Length > 0) ?? DefaultEffect;
		(Shader? shader, CGodotShader? translation) = _effects.Get(file, ScrollTechnique, EShaderTarget.Canvas, FilledTexture, device: States);
		if (shader is null || translation is null)
		{
			CDiagnostics.Problem($"interface: gfx/FX/{file} has no '{ScrollTechnique}' to draw the scrolling sprite '{sprite.Name}' with, and it is not drawn");
			return null;
		}
		var material = new CProgressBarMaterial(new ShaderMaterial { Shader = shader }, translation);
		material.Set("CurrentState", 0f);
		return material;
	}

	private const string ScrollTechnique = "ScrollTech";

	public static Color Colour(CColor colour) => new(colour.R, colour.G, colour.B, colour.A);
}
