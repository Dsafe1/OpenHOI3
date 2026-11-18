namespace OpenHOI3.Content.Gui;

/// <summary>
/// A glyph of one of the original's font descriptors, in the file's pixels: its box in the texture - its width and
/// height - where that box stands from the pen and from the top of the line, how far the pen then moves, and where the
/// box's corner is in the texture. The map's country names are laid out by these boxes (<c>docs/mechanics/mapnames.md</c>);
/// a large font's glyphs are cut out of its texture by them (<c>docs/mechanics/interface.md</c>, section 6).
/// </summary>
public readonly record struct CFontGlyph(int Width, int Height, int XOffset, int YOffset, int Advance, int X = 0, int Y = 0);
