namespace OpenHOI3.Content.Gui;

/// <summary>
/// A <c>listboxType</c>: a box that stacks windows one under the other and scrolls
/// when they do not fit. What goes in it is for the screen that owns it to decide; the
/// file gives only the box.
/// </summary>
public sealed class CGuiListBoxType : CGuiType
{
	public CGuiPoint Size { get; init; }

	/// <summary>The gap left between one entry and the next.</summary>
	public int Spacing { get; init; }

	/// <summary>The name of the top-level <c>scrollbarType</c> it scrolls with.</summary>
	public string Scrollbar { get; init; } = "";

	public CGuiPoint BorderSize { get; init; }

	/// <summary>The sprite drawn behind the entries, or an empty string.</summary>
	public string Background { get; init; } = "";

	/// <summary>
	/// Where the scrollbar stands from the box's top right corner: the original puts it at the box's width plus x, and at
	/// y (<c>docs/mechanics/interface.md</c>, section 2). Nought where the file gives none - the box's right edge at its top.
	/// </summary>
	public CGuiPoint Offset { get; init; }
}
