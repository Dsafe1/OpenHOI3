using System.Collections.Generic;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// Which province goes in which tile of the cache: every province with pixels, sea too, in the tile its bounding box's centre
/// falls in, each tile's list by ascending id [exe, the order *measured*]. Tiles are <see cref="CQuadTile.Size"/> pixels
/// square, numbered from the south-west along each row.
/// </summary>
public sealed class CCacheTiles
{
	private readonly List<int>?[] _provinces;

	public CCacheTiles(CMapCacheSource source, CProvincePixels pixels)
	{
		Columns = source.Width / CQuadTile.Size + 1;
		Rows = source.Height / CQuadTile.Size + 1;
		_provinces = new List<int>?[Columns * Rows];
		for (int province = 1; province < source.ProvinceCount; province++)
		{
			if (pixels.Of(province).IsEmpty)
			{
				continue;
			}
			(int x0, int y0, int x1, int y1) = pixels.Box(province);
			int centreX = x0 + (x1 - x0 + 1) / 2;
			int centreY = y0 + (y1 - y0 + 1) / 2;
			int tile = centreX / CQuadTile.Size + Columns * (centreY / CQuadTile.Size);
			(_provinces[tile] ??= new List<int>()).Add(province);
		}
	}

	public int Columns { get; }

	public int Rows { get; }

	public int Count => Columns * Rows;

	/// <summary>A tile's provinces, or null for none.</summary>
	public IReadOnlyList<int>? Provinces(int tile) => _provinces[tile];

	/// <summary>A tile's middle on the map, which its meshes are measured from.</summary>
	public (int X, int Y) Middle(int tile)
	{
		return (CQuadTile.Size * (tile % Columns) + CQuadTile.Size / 2, CQuadTile.Size * (tile / Columns) + CQuadTile.Size / 2);
	}
}
