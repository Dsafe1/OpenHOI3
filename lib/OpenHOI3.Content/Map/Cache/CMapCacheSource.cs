using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// What the map cache is made from, all of it out of the map files: the province raster, which provinces are sea, each one's
/// first region and the order of its neighbours, the terrain bitmap's palette indices and what terrain.txt says of each, and
/// the rivers' sources. Every raster is <c>[y * Width + x]</c> with y counted from the south. See
/// <c>docs/mechanics/mapcache.md</c>.
/// </summary>
public sealed class CMapCacheSource
{
	public required int Width { get; init; }

	public required int Height { get; init; }

	public required ushort[] Provinces { get; init; }

	/// <summary>Per province id. Id 0 is not sea.</summary>
	public required bool[] IsSea { get; init; }

	/// <summary>Per province id, the first region of <c>region.txt</c> that lists it, counted from 1; 0 for none.</summary>
	public required int[] FirstRegion { get; init; }

	/// <summary>Per province id, its neighbours in the order of its ways out, which is what a border's slot counts.</summary>
	public required int[][] Neighbours { get; init; }

	public required byte[] Terrain { get; init; }

	/// <summary>Per palette index, its entry's <c>priority</c>, or -1 where it gives none or there is no entry.</summary>
	public required int[] Priority { get; init; }

	/// <summary>Per palette index, whether terrain.txt has an entry for it.</summary>
	public required bool[] HasEntry { get; init; }

	/// <summary>Per palette index, whether its entry's terrain is water.</summary>
	public required bool[] WaterIndex { get; init; }

	/// <summary>terrain.txt's <c>terrain</c>, how many entries the texture sheet has: 256 in every map seen.</summary>
	public required int PaletteSize { get; init; }

	/// <summary>The pixels of the river bitmap of index 0, by rows from the south and then from the west.</summary>
	public required IReadOnlyList<(int X, int Y)> RiverSources { get; init; }

	/// <summary>Per province id, its ways out as the map has them, for <c>adjacencies.bin</c>.</summary>
	public IReadOnlyList<CProvinceEdge[]> Edges { get; init; } = Array.Empty<CProvinceEdge[]>();

	/// <summary>Per province id, its climate index, for <c>climate.bin</c>.</summary>
	public IReadOnlyList<byte> Climate { get; init; } = Array.Empty<byte>();

	/// <summary><c>navaldist.bin</c>'s bytes.</summary>
	public byte[] NavalTable { get; init; } = Array.Empty<byte>();

	public int ProvinceCount => IsSea.Length;

	/// <summary>Whether a province is land; id 0 counts, as in the original, and each caller that must asks for id 0 itself.</summary>
	public bool IsLand(int province) => !IsSea[province];

	/// <summary>
	/// Whether the terrain painted at a point is water [exe]: a point past the east or the north edge reads palette index 0,
	/// and a negative x reads the end of the row before.
	/// </summary>
	public bool IsWaterAt(int x, int y)
	{
		if (x >= Width || y >= Height)
		{
			return WaterIndex[0];
		}
		int at = y * Width + x;
		return at >= 0 && WaterIndex[Terrain[at]];
	}

	/// <summary>The sources of a loaded map; null, and told why, where it has no terrain to build from.</summary>
	public static CMapCacheSource? From(CMap map, Action<string> problem)
	{
		if (map.TerrainFile is not { } terrainFile)
		{
			problem("map cache: the map has no terrain bitmap, so no cache can be built");
			return null;
		}
		CTerrainReader.Definition definition = map.TerrainDefinition;
		var water = new bool[256];
		var hasEntry = new bool[256];
		for (int index = 0; index < 256; index++)
		{
			int category = definition.CategoryOfIndex[index];
			hasEntry[index] = category >= 0;
			water[index] = category >= 0 && definition.Categories[category].IsWater;
		}
		var firstRegion = new int[map.ProvinceCount];
		for (int region = 0; region < map.Regions.Count; region++)
		{
			foreach (int province in map.Regions[region].Provinces)
			{
				if (province < firstRegion.Length && firstRegion[province] == 0)
				{
					firstRegion[province] = region + 1;
				}
			}
		}
		var isSea = new bool[map.ProvinceCount];
		var neighbours = new int[map.ProvinceCount][];
		for (int province = 0; province < map.ProvinceCount; province++)
		{
			isSea[province] = map.IsSea(province);
			CProvinceEdge[] edges = map.Edges[province];
			neighbours[province] = edges.Select(edge => edge.To).ToArray();
		}
		IReadOnlyList<(int X, int Y)> sources = Array.Empty<(int, int)>();
		if (map.RiversFile is { } riversFile)
		{
			sources = CMapBin.BitmapSources(CBitmapFile.Read(riversFile).NorthRaster(), map.Width, map.Height);
		}
		return new CMapCacheSource
		{
			Width = map.Width,
			Height = map.Height,
			Provinces = map.Raster.ToArray(),
			IsSea = isSea,
			FirstRegion = firstRegion,
			Neighbours = neighbours,
			Terrain = CBitmapFile.Read(terrainFile).NorthRaster(),
			Priority = definition.PriorityOfIndex ?? Enumerable.Repeat(-1, 256).ToArray(),
			HasEntry = hasEntry,
			WaterIndex = water,
			PaletteSize = definition.PaletteSize,
			RiverSources = sources,
			Edges = map.Edges,
			Climate = map.ClimateIndex,
			NavalTable = CMapDataFiles.NavalTable(map),
		};
	}
}
