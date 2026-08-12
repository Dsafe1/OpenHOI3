using Godot;

namespace OpenHOI3;

/// <summary>
/// Entry point.
///
/// The game's own interface has not been built yet. What exists so far is the reading
/// side of the asset converter, under <c>src/Core</c> and <c>src/Gfx</c>, which has no
/// screen of its own.
/// </summary>
public partial class Main : Control
{
	public override void _Ready()
	{
		GD.Print("OpenHOI3 starting.");
		MouseFilter = MouseFilterEnum.Ignore;

		var placeholder = new Label
		{
			Text = "OpenHOI3",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(placeholder);
	}
}
