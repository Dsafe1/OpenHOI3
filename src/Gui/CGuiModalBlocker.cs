using Godot;

namespace OpenHOI3.Gui;

/// <summary>
/// The whole screen under a window that holds the game while it is up - the in-game menu: it takes every click, the
/// wheel and the pointer, so that nothing behind it is pressed, scrolled or dragged. What reads the mouse before the
/// interface does - a list's wheel - asks whether one is under the pointer. See <c>docs/mechanics/ingamemenu.md</c>,
/// section 1.
/// </summary>
public sealed partial class CGuiModalBlocker : Control
{
	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	/// <summary>Whether a blocker is what the pointer is over: everything behind one is out of reach.</summary>
	public static bool IsUnderPointer(Viewport viewport)
	{
		return viewport.GuiGetHoveredControl() is CGuiModalBlocker;
	}
}
