namespace OpenHOI3.Content.Map;

/// <summary>
/// A pixel of the land borders' corner cache (<c>docs/mechanics/mapdraw.md</c>, section 4): its place, x a column and y
/// counted from the south; its four bytes from <c>border_bitmap.bin</c> - the province pattern's column, a shade, the
/// country pattern's column, the diagonal's code - of which the third is worked out again from the controllers; whether it
/// is a junction pixel the passes visit; and the byte <c>map.bin</c> holds there.
/// </summary>
public sealed class CBorderCorner
{
	public required int X { get; init; }

	public required int Y { get; init; }

	public byte Province { get; set; }

	public byte Shade { get; set; }

	public byte Country { get; set; }

	public byte Diagonal { get; set; }

	public bool Active { get; set; }

	/// <summary>The map cache's byte at the pixel, whose low four bits flag a diagonal cut.</summary>
	public byte Cut { get; set; }

	/// <summary>The entries of the pixels west, east, north and south, where the cache has them.</summary>
	public CBorderCorner? Left { get; set; }

	public CBorderCorner? Right { get; set; }

	public CBorderCorner? Up { get; set; }

	public CBorderCorner? Down { get; set; }
}
