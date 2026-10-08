using Godot;

namespace OpenHOI3.Gui;

/// <summary>
/// How a button looks under the mouse, held down and switched off: the original's
/// <c>gfx/FX/buttonstate.fx</c>, which is plain text in the install, written again for
/// this engine. Under the mouse a tenth is added to red, green and blue; held down a
/// tenth is taken off; switched off the picture is drawn in grey, by the weights
/// 0.212671, 0.715160 and 0.072169. A frame of the sprite is never swapped for any of
/// them. See <c>docs/mechanics/interface.md</c>, section 5.
/// </summary>
public sealed class CButtonShader
{
	private const string Code = """
		shader_type canvas_item;

		uniform int look = 0;

		// The tint a node is drawn with. By the time fragment() runs COLOR has had the
		// picture multiplied into it, so it is kept from here.
		varying vec4 tint;

		void vertex() {
			tint = COLOR;
		}

		void fragment() {
			vec4 colour = texture(TEXTURE, UV);
			if (look == 1) {
				colour.rgb += vec3(0.1);
			} else if (look == 2) {
				colour.rgb -= vec3(0.1);
			} else if (look == 3) {
				colour.rgb = vec3(dot(colour.rgb, vec3(0.212671, 0.715160, 0.072169)));
			}
			COLOR = colour * tint;
		}
		""";

	private static readonly StringName Look = "look";

	private readonly Shader _shader = new() { Code = Code };

	/// <summary>A material for one button. Each has its own, since each has its own look.</summary>
	public ShaderMaterial Material()
	{
		return new ShaderMaterial { Shader = _shader };
	}

	public static void Set(ShaderMaterial material, EButtonLook look)
	{
		material.SetShaderParameter(Look, (int)look);
	}
}
