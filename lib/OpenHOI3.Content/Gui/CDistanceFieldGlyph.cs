namespace OpenHOI3.Content.Gui;

/// <summary>
/// A glyph's place in a <see cref="CDistanceFieldAtlas"/>, in the font descriptor's pixels: the character's number in
/// the descriptor, the corner of its field in the atlas and the field's size, where that stands from the pen and from
/// the top of the line - the descriptor's offsets less the margin - and how far the pen moves. A glyph with no ink has
/// no field, its size nought.
/// </summary>
public readonly record struct CDistanceFieldGlyph(int Code, int AtlasX, int AtlasY, int Width, int Height, int XOffset,
	int YOffset, int Advance);
