using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// Makes the original's whole <c>map/cache</c> out of the map files, in the original's formats so that the same readers take either: the
/// drawing half - <c>quad_index.bin</c>, <c>all_quads.bin</c>, the tiles' pictures, <c>border_adj.bin</c>, <c>border_bitmap.bin</c>,
/// <c>map.bin</c> - the files of <see cref="CMapDataFiles"/> and last <c>time</c>. For Their Finest Hour's map and the beach noise's
/// default each file is the shipped one byte for byte. See <c>docs/mechanics/mapcache.md</c>.
/// </summary>
public static class CMapCacheBuilder
{
	/// <summary>Builds the files into a folder, which is made; the stamp goes in its file when all the rest is written.</summary>
	public static void Build(CMapCacheSource source, string folder, uint stamp, int noiseCallsBefore = CBeachNoise.DefaultCallsBefore)
	{
		Directory.CreateDirectory(Path.Combine(folder, "quad_textures"));
		WriteFile(folder, CMapDataFiles.AdjacenciesFile, stream => CMapDataFiles.WriteAdjacencies(stream, source.Edges));
		File.WriteAllBytes(Path.Combine(folder, CMapDataFiles.NavalFile), source.NavalTable);
		WriteFile(folder, CMapDataFiles.ClimateFile, stream => CMapDataFiles.WriteClimate(stream, source.Climate));
		WriteFile(folder, CMapDataFiles.ProvincesFile, stream => CMapDataFiles.WriteProvinceRaster(stream, source.Width, source.Height, source.Provinces));
		WriteFile(folder, CMapDataFiles.FormatFile, CMapDataFiles.WriteQuadFormat);
		byte[] raster = CMapBinBuilder.Raster(source);
		using (FileStream file = File.Create(Path.Combine(folder, CMapBin.File)))
		{
			CMapBinBuilder.Write(file, source, raster);
		}
		byte[] bitmap = CBorderBitmapBuilder.Build(source, raster);
		File.WriteAllBytes(Path.Combine(folder, CBorderCache.BitmapFile), bitmap);

		var pixels = new CProvincePixels(source);
		var tiles = new CCacheTiles(source, pixels);
		var noise = new CBeachNoise(noiseCallsBefore);
		bool[] near = NearWater(source);
		var junctions = new List<(int X, int Y)>?[source.ProvinceCount];
		var written = new List<(int Tile, int Bytes)>();
		using (FileStream quads = File.Create(Path.Combine(folder, CQuadCache.QuadsFile)))
		using (var writer = new BinaryWriter(quads))
		{
			for (int tile = 0; tile < tiles.Count; tile++)
			{
				IReadOnlyList<int>? provinces = tiles.Provinces(tile);
				if (provinces is null || !HasLand(source, provinces))
				{
					continue;
				}
				int before = checked((int)quads.Position);
				WriteTile(writer, source, pixels, raster, bitmap, tile, provinces, tiles.Middle(tile), noise, near, junctions);
				writer.Flush();
				written.Add((tile, checked((int)quads.Position) - before));
				using FileStream picture = File.Create(Path.Combine(folder, CQuadCache.TextureFile(tile)));
				CTerrainIndexTexture.Build(source, pixels, provinces).Write(picture);
			}
		}
		WriteIndex(Path.Combine(folder, CQuadCache.IndexFile), written);
		WriteJunctions(Path.Combine(folder, CBorderCache.PairsFile), junctions);
		File.WriteAllBytes(Path.Combine(folder, CMapCache.StampFile), BitConverter.GetBytes(stamp));
	}

	private static void WriteFile(string folder, string name, Action<Stream> write)
	{
		using FileStream file = File.Create(Path.Combine(folder, name));
		write(file);
	}

	private static bool HasLand(CMapCacheSource source, IReadOnlyList<int> provinces)
	{
		foreach (int province in provinces)
		{
			if (source.IsLand(province))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The pixels within two of water, and the map's first three rows and columns: no other pixel has beach or surf.</summary>
	private static bool[] NearWater(CMapCacheSource source)
	{
		int width = source.Width;
		int height = source.Height;
		var near = new bool[width * height];
		for (int at = 0; at < near.Length; at++)
		{
			near[at] = source.WaterIndex[source.Terrain[at]];
		}
		for (int round = 0; round < 2; round++)
		{
			var grown = (bool[])near.Clone();
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					if (!near[y * width + x])
					{
						continue;
					}
					for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
					{
						for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
						{
							grown[ny * width + nx] = true;
						}
					}
				}
			}
			near = grown;
		}
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (x < 3 || y < 3)
				{
					near[y * width + x] = true;
				}
			}
		}
		return near;
	}

	private static void WriteTile(BinaryWriter writer, CMapCacheSource source, CProvincePixels pixels, byte[] raster, byte[] bitmap, int tile,
		IReadOnlyList<int> provinces, (int X, int Y) middle, CBeachNoise noise, bool[] near, List<(int X, int Y)>?[] junctions)
	{
		CLandMesh land = CLandMesh.Build(source, pixels, raster, provinces, middle);
		CBorderMesh borders = CBorderMesh.Build(source, pixels, raster, bitmap, provinces, middle, junctions);
		var beach = new CBeachMesh();
		var surf = new List<CWavePixel>();
		var kinds = new List<int>();
		foreach (int province in provinces)
		{
			if (source.IsLand(province))
			{
				AddShore(source, pixels, raster, province, near, beach, surf, kinds);
			}
		}
		beach.Finish(source, middle, noise);

		writer.Write(tile);
		writer.Write(land.Vertices.Count * 2);
		writer.Write(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(land.Vertices)));
		writer.Write(land.Indices.Count * 2);
		writer.Write(provinces.Count);
		ReadOnlySpan<ushort> indices = CollectionsMarshal.AsSpan(land.Indices);
		for (int group = 0; group < provinces.Count; group++)
		{
			ReadOnlySpan<ushort> part = indices[land.GroupStarts[group]..land.GroupStarts[group + 1]];
			writer.Write(part.Length * 2);
			writer.Write(MemoryMarshal.AsBytes(part));
		}
		writer.Write(borders.Indices.Count * 2);
		writer.Write(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(borders.Indices)));
		writer.Write(borders.Vertices.Count);
		writer.Write(CollectionsMarshal.AsSpan(borders.Vertices));
		byte[] beachVertices = beach.VertexBytes();
		writer.Write(beachVertices.Length);
		writer.Write(beachVertices);
		if (beachVertices.Length != 0)
		{
			writer.Write(beach.Indices.Count * 2);
			writer.Write(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(beach.Indices)));
		}
		writer.Write(surf.Count);
		foreach (CWavePixel record in surf)
		{
			writer.Write(record.X);
			writer.Write(record.Y);
			writer.Write(record.Type);
			(float a, float b, float c, float d) = CSurfRecords.OffsetsOf(record.Type);
			writer.Write(a);
			writer.Write(b);
			writer.Write(c);
			writer.Write(d);
		}
	}

	/// <summary>
	/// A province's beach and surf, its pixels in order. A beach's colour reads the list of each pixel's one or - where the raster cuts
	/// it - two entries by the pixel's own place in the pixel list, so an earlier cut shifts what a later pixel finds: as read.
	/// </summary>
	private static void AddShore(CMapCacheSource source, CProvincePixels pixels, byte[] raster, int province, bool[] near, CBeachMesh beach,
		List<CWavePixel> surf, List<int> kinds)
	{
		ReadOnlySpan<int> list = pixels.Of(province);
		var offsets = new List<int>(list.Length);
		int stride = source.Width + 1;
		foreach (int pixel in list)
		{
			int cut = raster[pixels.Y(pixel) * stride + pixels.X(pixel)];
			offsets.Add(0);
			if ((cut & 0xF) != 0)
			{
				offsets.Add((cut & (2 | 4)) != 0 ? -1 : 1);
			}
		}
		for (int i = 0; i < list.Length; i++)
		{
			if (!near[list[i]])
			{
				continue;
			}
			int x = pixels.X(list[i]);
			int y = pixels.Y(list[i]);
			CSurfRecords.Add(source, x, y, surf);
			CBeachMesh.PixelKinds(source, x, y, kinds);
			foreach (int kind in kinds)
			{
				beach.Add(x, y, kind, offsets[i]);
			}
		}
	}

	private static void WriteIndex(string path, List<(int Tile, int Bytes)> written)
	{
		int total = 0;
		int largest = 0;
		foreach ((_, int bytes) in written)
		{
			total += bytes;
			largest = Math.Max(largest, bytes);
		}
		using var writer = new BinaryWriter(File.Create(path));
		writer.Write(written.Count);
		writer.Write(total);
		writer.Write(largest);
		foreach ((int tile, int bytes) in written)
		{
			writer.Write(tile);
			writer.Write(bytes);
		}
	}

	private static void WriteJunctions(string path, List<(int X, int Y)>?[] junctions)
	{
		using var writer = new BinaryWriter(File.Create(path));
		writer.Write(junctions.Length);
		foreach (List<(int X, int Y)>? list in junctions)
		{
			writer.Write(list?.Count ?? 0);
			if (list is not null)
			{
				foreach ((int x, int y) in list)
				{
					writer.Write(x);
					writer.Write(y);
				}
			}
		}
	}
}
