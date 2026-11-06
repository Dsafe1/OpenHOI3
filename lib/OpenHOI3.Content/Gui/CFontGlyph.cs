namespace OpenHOI3.Content.Gui;

/// <summary>
/// A glyph of one of the original's font descriptors, in the file's pixels: its box in the texture - its width and
/// height - where that box stands from the pen and from the top of the line, and how far the pen then moves. The map's
/// country names are laid out by these boxes (<c>docs/mechanics/mapnames.md</c>).
/// </summary>
public readonly record struct CFontGlyph(int Width, int Height, int XOffset, int YOffset, int Advance);
