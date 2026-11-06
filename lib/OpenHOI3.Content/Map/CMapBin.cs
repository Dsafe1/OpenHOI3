using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;

namespace OpenHOI3.Content.Map;

/// <summary>
/// The original's <c>map/cache/map.bin</c>: a head, the rivers' sources - every pixel of <c>rivers.bmp</c> whose index is 0,
/// by rows from the south and then from the west - and the raster the land borders' junctions read
/// (<c>reversing/findings/FINDINGS-mapdraw.md</c>, B.7 and C.6). It is not tested against the <c>time</c> stamp as the rest
/// of the cache is, but by date: it is the map's while no file it is built from is missing or newer than it [exe]. Those are
/// nine of default.map's files - and here a tenth, <c>rivers.bmp</c>, which the original leaves out, so that an edited
/// river bitmap is traced from its own sources and not from the last ones (the maintainer's word, 2026-11-06;
/// <c>docs/compatibility.md</c>, entry 20). See <c>docs/mechanics/mapdraw.md</c>, section 4.
/// </summary>
public static class CMapBin
{
	public const string File = "map.bin";

	/// <summary>The bytes before the count of sources: "MAP_CACHE", padding and two numbers.</summary>
	public const int Head = 20;

	/// <summary>The default.map keys of the files map.bin is tested against, in the original's order, with their defaults.</summary>
	private static readonly (string Key, string Default)[] Sources =
	{
		("positions", "positions.txt"), ("provinces", "provinces.bmp"), ("terrain", "terrain.bmp"),
		("definitions", "definition.csv"), ("adjacencies", "adjacencies.csv"), ("terrain_definition", "terrain.txt"),
		("continent", "continent.txt"), ("region", "region.txt"), ("coastlines", "coastlines.txt"),
		// Ours: the river bitmap, whose sources the file holds.
		("rivers", "rivers.bmp"),
	};

	/// <summary>
	/// The first of the files map.bin is built from that is missing or written later than it, as a path for a message; null
	/// where none is, and map.bin is the map's.
	/// </summary>
	public static string? NewerSource(CDirectorySettings directories, string mapBin)
	{
		DateTime built = System.IO.File.GetLastWriteTimeUtc(mapBin);
		ClausewitzNode definition = ClausewitzParser.ParseFile(directories.Resolve("map/default.map"));
		foreach ((string key, string fallback) in Sources)
		{
			string path = directories.Resolve("map/" + definition.GetString(key, fallback));
			if (!System.IO.File.Exists(path) || System.IO.File.GetLastWriteTimeUtc(path) > built)
			{
				return path;
			}
		}
		return null;
	}

	/// <summary>The rivers' sources as the file holds them, x a column and y counted from the south; null where it does not read.</summary>
	public static (int X, int Y)[]? RiverSources(byte[] map)
	{
		if (map.Length < Head + 4)
		{
			return null;
		}
		int count = BitConverter.ToInt32(map, Head);
		if (count < 0 || Head + 4 + 8L * count > map.Length)
		{
			return null;
		}
		var sources = new (int X, int Y)[count];
		for (int i = 0; i < count; i++)
		{
			int at = Head + 4 + 8 * i;
			sources[i] = (BitConverter.ToInt32(map, at), BitConverter.ToInt32(map, at + 4));
		}
		return sources;
	}

	/// <summary>
	/// The rivers' sources a current map.bin holds; or, where it is missing, does not read or is older than a file it is
	/// built from, the sources its builder would write - every index-0 pixel of the river bitmap by rows from the south and
	/// then from the west [exe] - and why told.
	/// </summary>
	public static IReadOnlyList<(int X, int Y)> RiverSources(CMapCache cache, byte[] rivers, int width, int height,
		Action<string> problem)
	{
		if (cache.Find(File) is not { } path)
		{
			problem($"rivers: no {CMapCache.Folder}/{File}; the sources are taken from the river bitmap as its builder takes them");
		}
		else if (NewerSource(cache.Directories, path) is { } newer)
		{
			problem($"rivers: '{newer}' is missing or newer than {path}; the sources are taken from the river bitmap as its "
				+ "builder takes them");
		}
		else if (RiverSources(System.IO.File.ReadAllBytes(path)) is { } held)
		{
			return held;
		}
		else
		{
			problem($"rivers: {path} does not read; the sources are taken from the river bitmap as its builder takes them");
		}
		return BitmapSources(rivers, width, height);
	}

	/// <summary>The sources map.bin's builder writes: every pixel of index 0, by rows from the south and then from the west [exe].</summary>
	public static List<(int X, int Y)> BitmapSources(byte[] rivers, int width, int height)
	{
		var sources = new List<(int X, int Y)>();
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if (rivers[y * width + x] == 0)
				{
					sources.Add((x, y));
				}
			}
		}
		return sources;
	}
}
