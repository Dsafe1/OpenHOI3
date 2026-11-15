namespace OpenHOI3.Content.Map;

/// <summary>A province's place on the raster: its pixel bounding box and how much of it there is.</summary>
public readonly record struct CProvinceShape(int MinX, int MinY, int MaxX, int MaxY, int PixelCount)
{
	public bool IsEmpty => PixelCount == 0;

	/// <summary>The centre of the bounding box, which is the original's default position.</summary>
	public int CentreX => MinX + (MaxX - MinX + 1) / 2;

	public int CentreY => MinY + (MaxY - MinY + 1) / 2;

	/// <summary>
	/// The centre the original's map keeps for the province of that id, which its distances between provinces are
	/// measured from: the pixels of id 0 - the colours no row of the definitions names - are never counted towards a box,
	/// so province 0 has none, and a centre with a part at nought is put at (1, 1), which is where province 0's stands.
	/// It is what a country with no capital, the rebels, measures from (<c>docs/mechanics/alignment.md</c>, section 5).
	/// </summary>
	public (int X, int Y) GameCentre(int id)
	{
		int x = IsEmpty || id == 0 ? 0 : CentreX;
		int y = IsEmpty || id == 0 ? 0 : CentreY;
		return x == 0 || y == 0 ? (1, 1) : (x, y);
	}
}
