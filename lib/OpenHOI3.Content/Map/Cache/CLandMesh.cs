using System.Collections.Generic;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// A tile's land: one quad for each horizontal run of a province's pixels, its ends moved a pixel where the map raster cuts them
/// so that a border steps diagonally, and the corners welded within the province. See <c>docs/mechanics/mapcache.md</c>, section 6.
/// </summary>
public sealed class CLandMesh
{
	/// <summary>Four shorts a vertex: x and y from the tile's middle, the province id's low and high byte.</summary>
	public List<short> Vertices { get; } = new();

	public List<ushort> Indices { get; } = new();

	/// <summary>Where each province of the tile's list starts in <see cref="Indices"/>, and at the end their total.</summary>
	public List<int> GroupStarts { get; } = new();

	public int VertexCount => Vertices.Count / 4;

	public static CLandMesh Build(CMapCacheSource source, CProvincePixels pixels, byte[] mapRaster, IReadOnlyList<int> provinces,
		(int X, int Y) middle)
	{
		var mesh = new CLandMesh();
		var welded = new Dictionary<int, int>();
		var positions = new List<(short X, short Y)>();
		var triangles = new List<int>();
		foreach (int province in provinces)
		{
			mesh.GroupStarts.Add(mesh.Indices.Count);
			if (source.IsLand(province))
			{
				mesh.AddProvince(source, pixels, mapRaster, province, middle, welded, positions, triangles);
			}
		}
		mesh.GroupStarts.Add(mesh.Indices.Count);
		return mesh;
	}

	private void AddProvince(CMapCacheSource source, CProvincePixels pixels, byte[] mapRaster, int province, (int X, int Y) middle,
		Dictionary<int, int> welded, List<(short X, short Y)> positions, List<int> triangles)
	{
		positions.Clear();
		triangles.Clear();
		welded.Clear();
		int stride = source.Width + 1;
		System.ReadOnlySpan<int> list = pixels.Of(province);
		int at = 0;
		while (at < list.Length)
		{
			int y = pixels.Y(list[at]);
			int xs = pixels.X(list[at]);
			int xe = xs;
			at++;
			while (at < list.Length && list[at] == list[at - 1] + 1 && pixels.Y(list[at]) == y)
			{
				xe++;
				at++;
			}
			int row = y * stride;
			int left0 = xs - 1;
			int left1 = xs - 1;
			int right0 = xe;
			int right1 = xe;
			MoveLeft(mapRaster, row, xs, ref left0, ref left1);
			MoveRight(mapRaster, row, xe, ref right0, ref right1);
			int first = positions.Count;
			positions.Add(((short)(left0 - middle.X), (short)(y - 1 - middle.Y)));
			positions.Add(((short)(right0 - middle.X), (short)(y - 1 - middle.Y)));
			positions.Add(((short)(left1 - middle.X), (short)(y - middle.Y)));
			positions.Add(((short)(right1 - middle.X), (short)(y - middle.Y)));
			triangles.Add(first + 1);
			triangles.Add(first);
			triangles.Add(first + 2);
			triangles.Add(first + 1);
			triangles.Add(first + 2);
			triangles.Add(first + 3);
		}
		// The weld: a corner's first occurrence in the province keeps its place, and a triangle that loses a side goes.
		int baseIndex = VertexCount;
		var remap = new int[positions.Count];
		int unique = 0;
		for (int i = 0; i < positions.Count; i++)
		{
			int key = positions[i].X << 16 | (ushort)positions[i].Y;
			if (!welded.TryGetValue(key, out int index))
			{
				index = unique++;
				welded[key] = index;
				Vertices.Add(positions[i].X);
				Vertices.Add(positions[i].Y);
				Vertices.Add((short)(province & 0xFF));
				Vertices.Add((short)(province >> 8));
			}
			remap[i] = index;
		}
		for (int i = 0; i < triangles.Count; i += 3)
		{
			int a = remap[triangles[i]];
			int b = remap[triangles[i + 1]];
			int c = remap[triangles[i + 2]];
			if (a != b && a != c && b != c)
			{
				Indices.Add((ushort)(a + baseIndex));
				Indices.Add((ushort)(b + baseIndex));
				Indices.Add((ushort)(c + baseIndex));
			}
		}
	}

	/// <summary>A raster byte; a run at the map's first column reads the byte before its row, and the very first nothing.</summary>
	private static int At(byte[] raster, int index) => index >= 0 ? raster[index] : 0;

	/// <summary>The first bit of 2, 8, 4, 1, 0x20 found at the pixel west of the run decides which of the two corners moves.</summary>
	private static void MoveLeft(byte[] raster, int row, int xs, ref int south, ref int north)
	{
		int cut = At(raster, row + xs - 1);
		if ((cut & 2) != 0)
		{
			return;
		}
		if ((cut & 8) != 0)
		{
			south = xs - 2;
		}
		else if ((cut & 4) != 0)
		{
			north = xs;
		}
		else if ((cut & 1) != 0)
		{
			north = xs - 2;
		}
		else if ((cut & 0x20) != 0)
		{
			int own = At(raster, row + xs);
			if ((own & 2) != 0)
			{
				south = xs;
			}
			else if ((own & 8) != 0)
			{
			}
			else if ((own & 4) != 0)
			{
				north = xs;
			}
			else if ((own & 1) != 0)
			{
				north = xs - 2;
			}
		}
	}

	private static void MoveRight(byte[] raster, int row, int xe, ref int south, ref int north)
	{
		int cut = At(raster, row + xe);
		if ((cut & 2) != 0)
		{
			return;
		}
		if ((cut & 8) != 0)
		{
			south = xe - 1;
		}
		else if ((cut & 4) != 0)
		{
			north = xe + 1;
		}
		else if ((cut & 1) != 0)
		{
			north = xe - 1;
		}
		else if ((cut & 0x20) != 0)
		{
			int next = At(raster, row + xe + 1);
			if ((next & 2) != 0)
			{
				south = xe + 1;
			}
			else if ((next & 8) != 0)
			{
			}
			else if ((next & 4) != 0)
			{
				north = xe + 1;
			}
			else if ((next & 1) != 0)
			{
				north = xe - 1;
			}
		}
	}
}
