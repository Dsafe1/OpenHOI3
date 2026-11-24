namespace OpenHOI3.Content.Gui;

/// <summary>
/// A <c>textBoxType</c> or an <c>instantTextBoxType</c>: a box of text in one font. An
/// <c>editBoxType</c> is read as one too - a box of text the player may type into - with
/// the <c>size</c> it declares as the most its text may take.
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

	/// <summary>The picture behind the text, as the file names it, or empty.</summary>
	public string TextureFile { get; init; } = "";

	public EGuiTextFormat Format { get; init; }

	public bool FixedSize { get; init; }

	/// <summary>Declared with <c>instantTextBoxType</c>.</summary>
	public bool IsInstant { get; init; }

	/// <summary>Declared with <c>editBoxType</c>: text the player may change. Typing is not built.</summary>
	public bool IsEdit { get; init; }

	/// <summary>
	/// An edit box of an enhanced game's that says <c>built_in_caret = yes</c>: its caret is a line the engine draws at
	/// the end of what is typed, as tall as the text's line, where the original's is the <c>cursor</c> picture - made
	/// for its own tall boxes, and off its place in a low one (<c>docs/mechanics/builtin.md</c>, section 7).
	/// </summary>
	public bool BuiltInCaret { get; init; }
}
