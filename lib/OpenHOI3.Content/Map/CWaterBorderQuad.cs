namespace OpenHOI3.Content.Map;

/// <summary>
/// A one-pixel quad of the sea zones' borders (<c>docs/mechanics/mapdraw.md</c>, section 4): the water pixel it covers, x a
/// column and y counted from the south, and the two codes <c>border_bitmap.bin</c> holds there - the province pattern's
/// column and the diagonal's - each choosing a column of its texture.
/// </summary>
public readonly record struct CWaterBorderQuad(short X, short Y, byte Province, byte Diagonal);
