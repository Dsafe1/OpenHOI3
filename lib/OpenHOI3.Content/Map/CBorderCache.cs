using System;
using System.Collections.Generic;
using System.IO;

namespace OpenHOI3.Content.Map;

/// <summary>
/// The land borders' corner cache, read from the install's map cache as the original builds it once as the map is made
/// (<c>docs/mechanics/mapdraw.md</c>, section 4; <c>reversing/findings/FINDINGS-mapdraw.md</c>, B.7): each province's
/// junction pixels from <c>border_adj.bin</c>, and for each of them and its four neighbours the four bytes
/// <c>border_bitmap.bin</c> holds there and the byte of <c>map.bin</c>'s raster. Pixels are x a column and y counted from the
/// south; both rasters are a pixel wider and higher than the map, row y at y × (width + 1).
/// </summary>
public sealed class CBorderCache
{
	public const string PairsFile = "map/cache/border_adj.bin";
	public const string BitmapFile = "map/cache/border_bitmap.bin";
	public const string MapFile = "map/cache/map.bin";

	/// <summary>The map cache's head: its tag, a word and two numbers [exe].</summary>
	private const int MapHead = 20;

	private readonly Dictionary<long, CBorderCorner> _corners = new();

	private CBorderCache(IReadOnlyList<(int X, int Y)>[] pairs, List<CBorderCorner> sorted)
	{
		Pairs = pairs;
		Corners = sorted;
		foreach (CBorderCorner corner in sorted)
		{
			_corners[Key(corner.X, corner.Y)] = corner;
		}
		foreach (CBorderCorner corner in sorted)
		{
			corner.Left = At(corner.X - 1, corner.Y);
			corner.Right = At(corner.X + 1, corner.Y);
			corner.Up = At(corner.X, corner.Y + 1);
			corner.Down = At(corner.X, corner.Y - 1);
		}
	}

	/// <summary>Each province's junction pixels in the file's order, by its id.</summary>
	public IReadOnlyList<(int X, int Y)>[] Pairs { get; }

	/// <summary>Every entry, by rows from the south and then from the west, which is the order the passes go in.</summary>
	public IReadOnlyList<CBorderCorner> Corners { get; }

	/// <summary>The entry at a pixel, or null.</summary>
	public CBorderCorner? At(int x, int y) => _corners.GetValueOrDefault(Key(x, y));

	private static long Key(int x, int y) => (long)y << 32 | (uint)x;

	/// <summary>The cache, or null where the install has not the three files or they do not fit the map.</summary>
	public static CBorderCache? Load(CDirectorySettings directories, int width, int height, Action<string> problem)
	{
		string pairsPath = directories.Resolve(PairsFile);
		string bitmapPath = directories.Resolve(BitmapFile);
		string mapPath = directories.Resolve(MapFile);
		if (!File.Exists(pairsPath) || !File.Exists(bitmapPath) || !File.Exists(mapPath))
		{
			problem($"borders: no {PairsFile}, {BitmapFile} and {MapFile}; the borders' junctions are not drawn");
			return null;
		}
		return Read(File.ReadAllBytes(pairsPath), File.ReadAllBytes(bitmapPath), File.ReadAllBytes(mapPath), width, height,
			problem);
	}

	public static CBorderCache? Read(byte[] pairsFile, byte[] bitmap, byte[] map, int width, int height, Action<string> problem)
	{
		int stride = width + 1;
		long pixels = (long)stride * (height + 1);
		if (bitmap.Length != pixels * 4)
		{
			problem($"borders: {BitmapFile} holds {bitmap.Length} bytes where the map wants {pixels * 4}");
			return null;
		}
		int mapRaster = map.Length >= MapHead + 4 ? MapHead + 4 + 8 * BitConverter.ToInt32(map, MapHead) : -1;
		if (mapRaster < 0 || map.Length - mapRaster != pixels)
		{
			problem($"borders: {MapFile}'s raster does not fit the map");
			return null;
		}

		// border_adj.bin: a count, then for each province a count and its pixels (x, y) [exe].
		var reader = new BinaryReader(new MemoryStream(pairsFile));
		int provinces = reader.ReadInt32();
		var pairs = new IReadOnlyList<(int X, int Y)>[provinces];
		for (int id = 0; id < provinces; id++)
		{
			int count = reader.ReadInt32();
			var list = new (int X, int Y)[count];
			for (int k = 0; k < count; k++)
			{
				list[k] = (reader.ReadInt32(), reader.ReadInt32());
			}
			pairs[id] = list;
		}
		if (reader.BaseStream.Position != pairsFile.Length)
		{
			problem($"borders: {PairsFile} does not read to its end");
		}

		// Each junction pixel an active entry, and its four neighbours where absent and inside the bitmap [exe].
		var corners = new Dictionary<long, CBorderCorner>();
		CBorderCorner Make(int x, int y)
		{
			long at = (long)y * stride + x;
			return new CBorderCorner
			{
				X = x,
				Y = y,
				Province = bitmap[at * 4],
				Shade = bitmap[at * 4 + 1],
				Country = bitmap[at * 4 + 2],
				Diagonal = bitmap[at * 4 + 3],
				Cut = map[mapRaster + at],
			};
		}
		foreach (IReadOnlyList<(int X, int Y)> list in pairs)
		{
			foreach ((int x, int y) in list)
			{
				if (x < 0 || y < 0 || x > width || y > height)
				{
					continue;
				}
				CBorderCorner corner = Make(x, y);
				corner.Active = y >= 1 && y <= height - 1;
				corners[Key(x, y)] = corner;
				foreach ((int nx, int ny) in new[] { (x + 1, y), (x, y + 1), (x - 1, y), (x, y - 1) })
				{
					if (nx >= 0 && ny >= 0 && nx <= width && ny <= height && !corners.ContainsKey(Key(nx, ny)))
					{
						corners[Key(nx, ny)] = Make(nx, ny);
					}
				}
			}
		}
		var sorted = new List<CBorderCorner>(corners.Values);
		sorted.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
		return new CBorderCache(pairs, sorted);
	}
}
