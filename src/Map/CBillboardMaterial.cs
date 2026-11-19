using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gui;

namespace OpenHOI3.Map;

/// <summary>
/// The material a plain billboard is drawn with: the effect every plain <c>billboardType</c> shares - the install's own
/// <c>gfx/FX/billboard.fx</c>, kept under the key <c>Billboard</c> - translated for the canvas, technique <c>standard</c>,
/// its picture the texture the element is drawn with. Fed as the original feeds an unselected picture: <c>vMultiplier</c>
/// one (a half more for a selected one, which nothing selects), <c>vScale</c> one, <c>vFlip</c> one. *Ours:* a picture's
/// frame is chosen by the part of its texture drawn, so <c>vXOffset</c> stays nought and one material serves every
/// picture of a layer. See <c>reversing/findings/FINDINGS-billboardfx.md</c>.
/// </summary>
public static class CBillboardMaterial
{
	private const string DrawnTexture = "tex0";

	/// <summary>The device's states around the map's objects (<c>FINDINGS-billboardfx.md</c>, section 2).</summary>
	private static readonly Dictionary<string, string> States = new()
	{
		["ALPHABLENDENABLE"] = "1", ["SRCBLEND"] = "SRCALPHA", ["DESTBLEND"] = "INVSRCALPHA",
	};

	/// <summary>A new material for the plain billboards, or null - with a line in the log - where the effect has no <c>standard</c>.</summary>
	public static ShaderMaterial? Make(CGuiContext gui, CBillboardTypes billboards)
	{
		string written = billboards.EffectFileOf(CBillboardTypes.PlainEffect);
		string file = written.Split('\\', '/').LastOrDefault(part => part.Length > 0) ?? "";
		(Shader? shader, CGodotShader? translation) = file.Length == 0
			? (null, null)
			: gui.Effects.Get(file, CBillboardTypes.PlainTechnique, EShaderTarget.Canvas, DrawnTexture, device: States);
		if (shader is null || translation is null)
		{
			CDiagnostics.Problem($"map: the billboards' effect '{written}' gives no '{CBillboardTypes.PlainTechnique}', so they are drawn plainly");
			return null;
		}
		var material = new ShaderMaterial { Shader = shader };
		foreach ((string fxName, float value) in new[] { ("vMultiplier", 1f), ("vXOffset", 0f), ("vScale", 1f), ("vFlip", 1f) })
		{
			foreach (CShaderUniform uniform in translation.Uniforms.Where(u => u.FxName == fxName && !u.Shared))
			{
				material.SetShaderParameter(uniform.Name, value);
			}
		}
		return material;
	}
}
