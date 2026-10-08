namespace OpenHOI3.Content.Gui;

/// <summary>
/// A <c>guiButtonType</c> or a <c>checkboxType</c>: a sprite that can be clicked, with a
/// caption over it when the file gives one. The two keywords are one kind of
/// declaration in the original and two kinds of thing on screen.
/// </summary>
public sealed class CGuiButtonType : CGuiType
{
	/// <summary>The sprite's name in the <c>.gfx</c> files.</summary>
	public string Sprite { get; init; } = "";

	/// <summary>A localisation key, or text to show as it is when no table has it.</summary>
	public string Text { get; init; } = "";

	public string Font { get; init; } = "";

	/// <summary>The key that presses it, as the file names it: <c>F1</c>, <c>ESCAPE</c>.</summary>
	public string Shortcut { get; init; } = "";

	public string ClickSound { get; init; } = "";

	/// <summary>In radians.</summary>
	public float Rotation { get; init; }

	/// <summary>Declared with <c>checkboxType</c>.</summary>
	public bool IsCheckbox { get; init; }
}
