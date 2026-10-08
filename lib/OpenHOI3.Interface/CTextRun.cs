namespace OpenHOI3.Interface;

/// <summary>
/// A stretch of text in one colour. The colour is the letter its escape named, or
/// <see cref="CColouredText.NoColour"/> for text before any escape, which is drawn in
/// the font's own.
/// </summary>
public readonly record struct CTextRun(string Text, char Colour);
