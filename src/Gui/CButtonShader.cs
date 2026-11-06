using Godot;
using OpenHOI3.Content.Shaders;
using OpenHOI3.Gfx;

namespace OpenHOI3.Gui;

/// <summary>
/// How a button looks under the mouse, held down and switched off: the four techniques of the install's own
/// <c>gfx/FX/buttonstate.fx</c> - <c>up</c>, <c>over</c>, <c>down</c> and <c>disable</c> - translated for the canvas
/// (<c>docs/mechanics/shaders.md</c>), its texture <c>tex0</c> the one the element is drawn with, so a mod's file is the one
/// drawn. In the base game under the mouse a tenth is added to red, green and blue, held down a tenth taken off, and
/// switched off the picture drawn in grey by the weights 0.212671, 0.715160 and 0.072169. A frame of the sprite is never
/// swapped for any of them. See <c>docs/mechanics/interface.md</c>, section 5.
/// </summary>
public sealed class CButtonShader
{
	public const string File = "buttonstate.fx";

	/// <summary>The texture of the file the element's own is: what its techniques sample.</summary>
	public const string DrawnTexture = "tex0";

	/// <summary>The techniques, in the order of <see cref="EButtonLook"/>.</summary>
	private static readonly string[] Techniques = { "up", "over", "down", "disable" };

	private readonly Shader?[] _shaders = new Shader?[Techniques.Length];

	public CButtonShader(CEffectShaders effects)
	{
		for (int look = 0; look < Techniques.Length; look++)
		{
			_shaders[look] = effects.Get(File, Techniques[look], EShaderTarget.Canvas, DrawnTexture).Shader;
		}
	}

	/// <summary>A material for one element, drawn up. Each has its own, since each has its own look.</summary>
	public ShaderMaterial Material()
	{
		return new ShaderMaterial { Shader = _shaders[(int)EButtonLook.Up] };
	}

	/// <summary>An element drawn the way a look is: that technique's shader on its material.</summary>
	public void Set(ShaderMaterial material, EButtonLook look)
	{
		Shader? shader = _shaders[(int)look];
		if (material.Shader != shader)
		{
			material.Shader = shader;
		}
	}
}
