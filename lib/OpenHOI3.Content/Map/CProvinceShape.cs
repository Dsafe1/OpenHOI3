namespace OpenHOI3.Content.Map;

/// <summary>A province's place on the raster: its pixel bounding box and how much of it there is.</summary>
public readonly record struct CProvinceShape(int MinX, int MinY, int MaxX, int MaxY, int PixelCount)
{
	public bool IsEmpty => PixelCount == 0;

	/// <summary>The centre of the bounding box, which is the original's default position.</summary>
	public int CentreX => MinX + (MaxX - MinX + 1) / 2;

	public int CentreY => MinY + (MaxY - MinY + 1) / 2;
}
