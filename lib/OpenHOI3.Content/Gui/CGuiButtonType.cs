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

	/// <summary>
	/// The sound a click makes, and a checkbox's when ticked; empty for its sprite's or the default's
	/// (<c>docs/mechanics/sound.md</c>).
	/// </summary>
	public string ClickSound { get; init; } = "";

	/// <summary><c>upsound</c>: a checkbox's sound when unticked; a button's when shown up, which no vanilla button sets.</summary>
	public string UpSound { get; init; } = "";

	/// <summary><c>downsound</c> and <c>oversound</c>: read as the original reads them; no vanilla button sets either.</summary>
	public string DownSound { get; init; } = "";

	public string OverSound { get; init; } = "";

	/// <summary>In radians.</summary>
	public float Rotation { get; init; }

	/// <summary>Declared with <c>checkboxType</c>.</summary>
	public bool IsCheckbox { get; init; }

	/// <summary>
	/// Its size where it has no sprite to take one from - the message log's rows are such buttons.
	/// That the original sizes a button by it was not read; the log's rows would be nothing without.
	/// </summary>
	public CGuiPoint Size { get; init; }

	/// <summary>Where its caption stands: in the middle unless the file says otherwise.</summary>
	public EGuiTextFormat Format { get; init; } = EGuiTextFormat.Centre;
}
