namespace OpenHOI3.Content.Gui;

/// <summary>
/// A <c>textBoxType</c> or an <c>instantTextBoxType</c>: a box of text in one font.
/// </summary>
public sealed class CGuiTextBoxType : CGuiType
{
	public string Font { get; init; } = "";

	/// <summary>What it shows until something sets it. A localisation key, or plain text.</summary>
	public string Text { get; init; } = "";

	public int MaxWidth { get; init; }

	public int MaxHeight { get; init; }

	/// <summary>The gap kept between the box's edge and its text.</summary>
	public CGuiPoint BorderSize { get; init; }

	public EGuiTextFormat Format { get; init; }

	public bool FixedSize { get; init; }

	/// <summary>Declared with <c>instantTextBoxType</c>.</summary>
	public bool IsInstant { get; init; }
}
