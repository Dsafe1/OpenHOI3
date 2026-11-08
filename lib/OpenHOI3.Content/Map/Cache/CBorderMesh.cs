using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// A tile's land borders: for every pixel beside another land province a quad - or, on a diagonal cut, a triangle - that names the
/// texel of the border lookup its province and neighbour take, and the pixels where three provinces meet, which go on the
/// province's list of <c>border_adj.bin</c>. See <c>docs/mechanics/mapcache.md</c>, section 7.
/// </summary>
public sealed class CBorderMesh
{
	/// <summary>A province's texels in the lookup are this many to a row.</summary>
	private const int TexelsAProvince = 14;

	/// <summary>A junction past this place on the province's list reads this place's texel [exe].</summary>
	private const int LastJunctionSlot = 7;

	/// <summary>Added to or taken from a corner's coordinate, by the corner's own 0 or 1, to tell the shader which it is.</summary>
	private const int CornerOffset = 1000;

	private static readonly (int Dx, int Dy)[] NeighbourSteps =
	{
		(-1, 1), (0, 1), (1, 1), (-1, 0), (1, 0), (-1, -1), (0, -1), (1, -1),
	};

	/// <summary>Each type's four corners: a corner's x and y sign from the pixel's middle, and its 0 or 1 in each direction.</summary>
	private static readonly (int Sx, int Sy, int Fx, int Fy) A = (-1, -1, 0, 1), B = (-1, 1, 0, 0), C = (1, 1, 1, 0), D = (1, -1, 1, 1);

	private static readonly (int Sx, int Sy, int Fx, int Fy)[][] Shapes =
	{
		new[] { A, B, C, D }, new[] { A, B, D, D }, new[] { A, C, D, D }, new[] { A, B, C, C }, new[] { B, C, D, D },
	};

	private readonly record struct Vertex(short X, short Z, short U, short V, uint Colour);

	/// <summary>Sixteen-bit triangle indices.</summary>
	public List<ushort> Indices { get; } = new();

	/// <summary>Four shorts and a colour's four bytes a vertex.</summary>
	public List<byte> Vertices { get; } = new();

	/// <summary>
	/// The tile's borders. <paramref name="junctions"/> holds each province's list and is added to, in the walk's order.
	/// </summary>
	public static CBorderMesh Build(CMapCacheSource source, CProvincePixels pixels, byte[] mapRaster, byte[] borderBitmap,
		IReadOnlyList<int> provinces, (int X, int Y) middle, List<(int X, int Y)>?[] junctions)
	{
		var vertices = new List<Vertex>();
		var indices = new List<int>();
		foreach (int province in provinces)
		{
			if (source.IsLand(province))
			{
				foreach (int pixel in pixels.Of(province))
				{
					AddPixel(source, pixels.X(pixel), pixels.Y(pixel), province, mapRaster, borderBitmap, middle, junctions, vertices, indices);
				}
			}
		}
		var mesh = new CBorderMesh();
		mesh.Weld(vertices, indices);
		return mesh;
	}

	private static void AddPixel(CMapCacheSource source, int x, int y, int own, byte[] mapRaster, byte[] borderBitmap, (int X, int Y) middle,
		List<(int X, int Y)>?[] junctions, List<Vertex> vertices, List<int> indices)
	{
		int cell = y * (source.Width + 1) + x;
		int cut = mapRaster[cell];
		int first = (cut & 2) != 0 ? 4 : (cut & 8) != 0 ? 3 : (cut & 4) != 0 ? 2 : (cut & 1) != 0 ? 1 : 0;
		AddEntry(source, x, y, own, first, 0, cell, borderBitmap, middle, junctions, vertices, indices);
		if (first != 0)
		{
			// The other half of the diagonal: its colour is the province beside, its texel still this one's.
			(int type, int dx) = (cut & 2) != 0 ? (1, -1) : (cut & 4) != 0 ? (3, -1) : (cut & 8) != 0 ? (2, 1) : (4, 1);
			AddEntry(source, x, y, own, type, dx, cell, borderBitmap, middle, junctions, vertices, indices);
		}
	}

	private static void AddEntry(CMapCacheSource source, int x, int y, int own, int type, int dx, int cell, byte[] borderBitmap,
		(int X, int Y) middle, List<(int X, int Y)>?[] junctions, List<Vertex> vertices, List<int> indices)
	{
		int width = source.Width;
		int height = source.Height;
		ushort[] raster = source.Provinces;
		bool[] sea = source.IsSea;
		int other = x + dx >= 0 && x + dx < width ? raster[y * width + x + dx] : 0;
		Span<int> around = stackalloc int[NeighbourSteps.Length];
		bool border = false;
		int chosen = -1;
		for (int i = 0; i < around.Length; i++)
		{
			int nx = x + NeighbourSteps[i].Dx;
			int ny = y + NeighbourSteps[i].Dy;
			int n = nx >= 0 && nx < width && ny >= 0 && ny < height ? raster[ny * width + nx] : 0;
			around[i] = n;
			if (n != own && n != 0 && !sea[n])
			{
				border = true;
				if (chosen == -1 || i is 1 or 3 or 4 or 6)
				{
					chosen = n;
				}
			}
		}
		if (!border)
		{
			return;
		}
		int north = around[1];
		int west = around[3];
		int east = around[4];
		int south = around[6];
		int slot;
		if (Meets(north, west, own, sea) || Meets(south, west, own, sea) || Meets(north, east, own, sea) || Meets(south, east, own, sea))
		{
			List<(int X, int Y)> list = junctions[own] ??= new List<(int X, int Y)>();
			int place = list.IndexOf((x, y));
			if (place < 0)
			{
				place = list.Count;
				list.Add((x, y));
			}
			slot = source.Neighbours[own].Length + Math.Min(place, LastJunctionSlot);
		}
		else
		{
			slot = Array.IndexOf(source.Neighbours[own], chosen);
		}
		int texel = own * TexelsAProvince + slot;
		short u = (short)(texel & 0x1FF);
		short v = (short)(texel >> 9);
		uint colour = (uint)((borderBitmap[cell * 4 + 3] * 8 & 0xFF) << 24 | (other & 0xFF) << 16 | (other >> 8 & 0xFF) << 8
			| (borderBitmap[cell * 4] * 8 & 0xFF));
		int origin = vertices.Count;
		indices.Add(origin);
		indices.Add(origin + 1);
		indices.Add(origin + 2);
		indices.Add(origin + 2);
		indices.Add(origin + 3);
		indices.Add(origin);
		foreach ((int sx, int sy, int fx, int fy) in Shapes[type])
		{
			// A pixel's corners are the half-pixels; the 0.5 taken off makes them whole, then the offset says which it is.
			int cornerX = x - middle.X + (sx > 0 ? 0 : -1);
			int cornerZ = y - middle.Y + (sy > 0 ? 0 : -1);
			vertices.Add(new Vertex(
				(short)(cornerX + (fx == 0 ? -CornerOffset : CornerOffset)),
				(short)(cornerZ + (fy == 0 ? -CornerOffset : CornerOffset)),
				u, v, colour));
		}
	}

	private static bool Meets(int a, int b, int own, bool[] sea)
	{
		return a != own && b != own && a != b && !sea[a] && !sea[b];
	}

	/// <summary>
	/// A vertex whose two low codes are both nought draws nothing and goes; the rest are welded, each kept at its first place;
	/// the triangles keep their order and one that loses a side goes.
	/// </summary>
	private void Weld(List<Vertex> vertices, List<int> indices)
	{
		const int Dropped = -2;
		var remap = new int[vertices.Count];
		var seen = new Dictionary<Vertex, int>();
		var kept = new List<Vertex>();
		for (int i = 0; i < vertices.Count; i++)
		{
			Vertex vertex = vertices[i];
			if ((vertex.Colour & 0xFF) == 0 && vertex.Colour >> 24 == 0)
			{
				remap[i] = Dropped;
				continue;
			}
			if (!seen.TryGetValue(vertex, out int index))
			{
				index = kept.Count;
				seen[vertex] = index;
				kept.Add(vertex);
			}
			remap[i] = index;
		}
		for (int i = 0; i < indices.Count; i += 3)
		{
			int a = remap[indices[i]];
			int b = remap[indices[i + 1]];
			int c = remap[indices[i + 2]];
			if (a != b && a != c && b != c)
			{
				Indices.Add((ushort)a);
				Indices.Add((ushort)b);
				Indices.Add((ushort)c);
			}
		}
		if (Indices.Count == 0)
		{
			return;
		}
		Span<byte> bytes = stackalloc byte[12];
		foreach (Vertex vertex in kept)
		{
			System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes, vertex.X);
			System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes[2..], vertex.Z);
			System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes[4..], vertex.U);
			System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes[6..], vertex.V);
			System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], vertex.Colour);
			foreach (byte value in bytes)
			{
				Vertices.Add(value);
			}
		}
	}
}
