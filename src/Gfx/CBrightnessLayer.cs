using Godot;

namespace OpenHOI3.Gfx;

/// <summary>
/// The player's gamma setting over everything drawn: the original sends the screen a ramp that scales every channel by
/// the setting's fiftieth, held to 0.1 and 1.9, and cut at full - a brightness scale and no power curve. Here a layer over
/// every other multiplies what is under it the same way, and is put away where the scale is one. See
/// <c>docs/mechanics/gameoptions.md</c>, section 3.
/// </summary>
public sealed partial class CBrightnessLayer : CanvasLayer
{
	private const string Code = """
		shader_type canvas_item;

		uniform sampler2D screen : hint_screen_texture, filter_nearest;
		uniform float scale = 1.0;

		void fragment() {
			vec3 colour = texture(screen, SCREEN_UV).rgb;
			COLOR = vec4(min(colour * scale, vec3(1.0)), 1.0);
		}
		""";

	private static readonly StringName ScaleName = "scale";

	private ColorRect _cover = null!;
	private ShaderMaterial _material = null!;

	public override void _Ready()
	{
		Name = "Brightness";
		Layer = 128;
		_material = new ShaderMaterial { Shader = new Shader { Code = Code } };
		_cover = new ColorRect { Name = "Cover", Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_cover.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_cover);
	}

	/// <summary>The screen's brightness, one leaving it as drawn.</summary>
	public void SetBrightness(float scale)
	{
		if (_cover is null)
		{
			return;
		}
		_material.SetShaderParameter(ScaleName, scale);
		_cover.Visible = !Mathf.IsEqualApprox(scale, 1f);
	}
}
