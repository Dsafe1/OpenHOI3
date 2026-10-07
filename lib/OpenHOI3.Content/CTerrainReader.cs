using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

/// <summary>
/// Reads the terrain categories and works out which one each province is.
///
/// A province's terrain is the category covering most of its pixels in the terrain
/// bitmap. That rule is the maintainer's, from experience of modding the original; it
/// has not been read out of the executable. See <c>docs/mechanics/map.md</c>, section 9.
/// </summary>
public static class CTerrainReader
{
	/// <summary>What the terrain definition file declares.</summary>
	/// <param name="Categories">In file order.</param>
	/// <param name="CategoryOfIndex">
	/// For each of the bitmap's 256 palette indices, which category it paints, or -1.
	/// </param>
	public readonly record struct Definition(CTerrain[] Categories, int[] CategoryOfIndex);

	/// <summary>
	/// The file is a <c>categories</c> block, then one entry per palette index: a
	/// <c>type</c> naming a category and a <c>color</c> list holding the index.
	/// </summary>
	public static Definition ReadDefinition(string path, Action<string> warn)
	{
		ClausewitzNode root = ClausewitzParser.ParseFile(path);

		var categories = new List<CTerrain>();
		var indexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		ClausewitzNode? block = root.Get("categories");
		if (block is not null)
		{
			foreach (ClausewitzNode category in block.Children)
			{
				if (category.Key is null || !category.IsBlock || indexByName.ContainsKey(category.Key))
				{
					continue;
				}
				indexByName[category.Key] = categories.Count;
				categories.Add(new CTerrain(category.Key, category.GetBool("is_water"),
					category.GetRgb("color", CColor.Gray)));
			}
		}

		var categoryOfIndex = new int[256];
		Array.Fill(categoryOfIndex, -1);
		var unknownTypes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key is null || !entry.IsBlock
				|| entry.Key.Equals("categories", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string type = entry.GetString("type");
			ClausewitzNode? indices = entry.Get("color");
			if (type.Length == 0 || indices is null)
			{
				continue;
			}
			if (!indexByName.TryGetValue(type, out int category))
			{
				unknownTypes.Add(type);
				continue;
			}
			foreach (ClausewitzNode value in indices.Children)
			{
				if (int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
					&& index >= 0 && index < categoryOfIndex.Length)
				{
					categoryOfIndex[index] = category;
				}
			}
		}

		if (categories.Count == 0)
		{
			warn($"'{path}' declares no terrain categories");
		}
		if (unknownTypes.Count > 0)
		{
			warn($"'{path}' paints with types that are not categories: {string.Join(", ", unknownTypes)}");
		}
		return new Definition(categories.ToArray(), categoryOfIndex);
	}

	/// <summary>
	/// The category covering the most pixels of each province, or null for a province
	/// with no counted pixel. A tie goes to the category declared first.
	/// </summary>
	/// <param name="provinceRaster"><c>[y * width + x]</c> with y counting north.</param>
	/// <param name="uncounted">Pixels whose palette index paints no category.</param>
	public static CTerrain?[] FindMajority(CBitmapFile terrain, ReadOnlySpan<ushort> provinceRaster,
		int provinceCount, Definition definition, out int uncounted)
	{
		int width = terrain.Width;
		int height = terrain.Height;
		int categoryCount = definition.Categories.Length;
		var counts = new int[provinceCount * Math.Max(categoryCount, 1)];
		uncounted = 0;

		for (int stored = 0; stored < height; stored++)
		{
			// The same way up as the province bitmap: the file's first row is the north.
			ReadOnlySpan<byte> row = terrain.StoredRow(stored);
			ReadOnlySpan<ushort> provinces = provinceRaster.Slice((height - 1 - stored) * width, width);
			for (int x = 0; x < width; x++)
			{
				int category = definition.CategoryOfIndex[row[x]];
				if (category < 0)
				{
					uncounted++;
					continue;
				}
				counts[provinces[x] * categoryCount + category]++;
			}
		}

		var majority = new CTerrain?[provinceCount];
		for (int id = 0; id < provinceCount; id++)
		{
			int best = -1;
			int bestCount = 0;
			for (int category = 0; category < categoryCount; category++)
			{
				int count = counts[id * categoryCount + category];
				if (count > bestCount)                       // strictly more, so the first declared wins a tie
				{
					best = category;
					bestCount = count;
				}
			}
			majority[id] = best < 0 ? null : definition.Categories[best];
		}
		return majority;
	}
}
