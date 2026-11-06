using System.Collections.Generic;

namespace OpenHOI3.Content.Map;

/// <summary>
/// One tile's record of the original's quad cache, as it hands it to the map's draws: the land's vertices - each a
/// tile-local pixel corner and a province id, which terrain_2_0.fx reads as SHORT2 and SHORT2 - and its triangles, one
/// group of them for each province of the tile; the land borders' vertices (twelve bytes each: SHORT4 and a colour) and
/// triangles; the beaches' vertices (two floats and a colour carrying the province id) and triangles; and the surf's
/// records, kept as their bytes. See <c>docs/mechanics/mapdraw.md</c>, section 2.
/// </summary>
public sealed class CQuadTile
{
	/// <summary>A tile is 50 map pixels a side [exe].</summary>
	public const int Size = 50;

	/// <summary>The tile's number: column + columns × row, the columns the map's width / 50 + 1.</summary>
	public required int Index { get; init; }

	/// <summary>The land's vertices, x and z for each: tile-local pixel corners from the tile's middle.</summary>
	public required short[] LandPositions { get; init; }

	/// <summary>The province of each land vertex.</summary>
	public required ushort[] LandProvinces { get; init; }

	/// <summary>The land's triangles, three indices each, the groups one after another.</summary>
	public required ushort[] LandIndices { get; init; }

	/// <summary>Where each province's group of triangles starts in <see cref="LandIndices"/>, counted in indices.</summary>
	public required IReadOnlyList<int> GroupStarts { get; init; }

	/// <summary>The land borders' triangles.</summary>
	public required ushort[] BorderIndices { get; init; }

	/// <summary>The land borders' vertices as stored, four shorts and a colour each (<c>FINDINGS-mapdraw.md</c>, B.3).</summary>
	public required short[] BorderShorts { get; init; }

	/// <summary>The land borders' vertices' colours, one packed 0xAARRGGBB each.</summary>
	public required uint[] BorderColours { get; init; }

	/// <summary>The beaches' vertices, x and z for each, floats on the half pixel.</summary>
	public required float[] BeachPositions { get; init; }

	/// <summary>The beaches' vertices' colours, one packed 0xAARRGGBB each, red and green the province's id.</summary>
	public required uint[] BeachColours { get; init; }

	/// <summary>The beaches' triangles.</summary>
	public required ushort[] BeachIndices { get; init; }

	/// <summary>The surf's records for the tile, 0x1C bytes each, not read yet.</summary>
	public required byte[] ShoreRecords { get; init; }

	public int Column(int columns) => Index % columns;

	public int Row(int columns) => Index / columns;

	/// <summary>The tile's middle in the map's pixels: column × 50 + 25, row × 50 + 25 [exe].</summary>
	public (int X, int Y) Middle(int columns) => (Column(columns) * Size + Size / 2, Row(columns) * Size + Size / 2);

	/// <summary>
	/// The tile's land rectangle - the bounding box of the pixels of its provinces - as its land vertices give it: a
	/// province's mesh reaches from one less than its first pixel to its last, in the middle's coordinates
	/// (<c>reversing/findings/FINDINGS-mapdraw.md</c>, A.1), so the rectangle is the vertices' extent with one off its low
	/// end. The original keeps it as it assigns the provinces; its terrain-index texture is this and 4 a side larger.
	/// </summary>
	public (int X, int Y, int Width, int Height) LandRectangle(int columns)
	{
		if (LandPositions.Length == 0)
		{
			return (0, 0, 0, 0);
		}
		(int midX, int midY) = Middle(columns);
		int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
		for (int v = 0; v < LandPositions.Length; v += 2)
		{
			minX = System.Math.Min(minX, LandPositions[v]);
			maxX = System.Math.Max(maxX, LandPositions[v]);
			minY = System.Math.Min(minY, LandPositions[v + 1]);
			maxY = System.Math.Max(maxY, LandPositions[v + 1]);
		}
		return (midX + minX + 1, midY + minY + 1, maxX - minX, maxY - minY);
	}
}
