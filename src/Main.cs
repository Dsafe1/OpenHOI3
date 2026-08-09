using Godot;
using OpenHOI3.FrontEnd;

namespace OpenHOI3;

/// <summary>
/// Entry point. A Control rather than a plain Node so the interface below it has a
/// parent rect to anchor against: Godot only propagates the viewport size to Control
/// children, and anchoring inside a zero-sized parent collapses everything onto the
/// top-left corner.
/// </summary>
public partial class Main : Control
{
	public override void _Ready()
	{
		GD.Print("OpenHOI3 starting.");
		MouseFilter = MouseFilterEnum.Ignore;
		AddChild(new CFrontEnd());
	}
}
