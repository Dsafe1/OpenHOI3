using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Reads the terrain categories and works out which one each province is.
///
/// A province's terrain is the one painted most often on its own pixels in a small
/// square around where its name is written. That is the original's rule, read out of
/// it: see <c>docs/mechanics/map.md</c>, section 9.
/// </summary>
public static class CTerrainReader
{
	/// <summary>The side of the square a land province's terrain is counted in, in pixels.</summary>
	public const int Window = 20;

	/// <summary>
	/// The one province the original names by number: its terrain is whatever is painted
	/// at its text position, with nothing counted.
	/// </summary>
	public const int ProvinceTakenAtItsText = 1737;

	/// <summary>What the terrain definition file declares.</summary>
	/// <param name="Categories">In file order.</param>
	/// <param name="CategoryOfIndex">
	/// For each of the bitmap's 256 palette indices, which category it paints, or -1.
	/// </param>
	public readonly record struct Definition(CTerrain[] Categories, int[] CategoryOfIndex)
	{
		/// <summary>For each palette index, the trees.txt entry its palette entry's <c>tree</c> names, or null.</summary>
		public string?[]? TreeOfIndex { get; init; }

		/// <summary>
		/// For each palette index, d: the entry's <c>tree_density</c>, or where it gives none 4 less the index's place in its
		/// <c>color</c> list, never below nought. Not a density: the row of the tree sheet, 4 − d, and no trees at nought
		/// (<c>reversing/findings/FINDINGS-mapdraw.md</c>, C.8).
		/// </summary>
		public int[]? TreeRowOfIndex { get; init; }
	}

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
				CFixedPoint.TryParse(category.GetString("movement_cost"), out CFixedPoint movementCost);
				CFixedPoint.TryParse(category.GetString("attack"), out CFixedPoint attack);
				CFixedPoint.TryParse(category.GetString("defence"), out CFixedPoint defence);
				CFixedPoint.TryParse(category.GetString("humidity"), out CFixedPoint humidity);
				CFixedPoint.TryParse(category.GetString("attrition"), out CFixedPoint attrition);
				CFixedPoint.TryParse(category.GetString("precipitation"), out CFixedPoint precipitation);
				CFixedPoint.TryParse(category.GetString("temperature"), out CFixedPoint temperature);
				categories.Add(new CTerrain(category.Key, category.GetBool("is_water"),
					category.GetRgb("color", CColor.Gray))
				{
					MovementCost = movementCost,
					Attack = attack,
					Defence = defence,
					Humidity = humidity,
					Attrition = attrition,
					Precipitation = precipitation,
					Temperature = temperature,
				});
			}
		}

		var categoryOfIndex = new int[256];
		Array.Fill(categoryOfIndex, -1);
		var treeOfIndex = new string?[256];
		var treeRowOfIndex = new int[256];
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
			string tree = entry.GetString("tree");
			// An int read as the original's %i reads it; −1 where the key is not there.
			int density = int.TryParse(entry.GetString("tree_density"), NumberStyles.Integer, CultureInfo.InvariantCulture,
				out int given) ? given : -1;
			int place = 0;
			foreach (ClausewitzNode value in indices.Children)
			{
				if (int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
					&& index >= 0 && index < categoryOfIndex.Length)
				{
					categoryOfIndex[index] = category;
					treeOfIndex[index] = tree.Length == 0 ? null : tree;
					treeRowOfIndex[index] = density > -1 ? density : Math.Max(0, 4 - place);
				}
				place++;
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
		return new Definition(categories.ToArray(), categoryOfIndex) { TreeOfIndex = treeOfIndex, TreeRowOfIndex = treeRowOfIndex };
	}

	/// <summary>How many pixels of the bitmap are painted with an index the definition gives no terrain.</summary>
	public static int CountUnknown(CBitmapFile terrain, Definition definition)
	{
		int unknown = 0;
		for (int stored = 0; stored < terrain.Height; stored++)
		{
			foreach (byte index in terrain.StoredRow(stored))
			{
				if (definition.CategoryOfIndex[index] < 0)
				{
					unknown++;
				}
			}
		}
		return unknown;
	}

	/// <summary>
	/// The category painted at a point of the map, or -1. A point off the east or the
	/// north edge reads as palette index 0, as the original has it.
	/// </summary>
	public static int CategoryAt(CBitmapFile terrain, Definition definition, int x, int y)
	{
		int index = x >= 0 && y >= 0 && x < terrain.Width && y < terrain.Height
			? terrain.StoredRow(terrain.Height - 1 - y)[x]
			: 0;
		return definition.CategoryOfIndex[index];
	}

	/// <summary>
	/// The terrain of each province, or null where nothing is painted that the
	/// definition knows:
	///
	///     it starts as what is painted at the province's unit point;
	///     on land, the 20 x 20 pixels from ten west and ten south of its text position
	///     are gone through, and of those that are the province's own the category
	///     painted most often replaces it - the one declared first, of equals;
	///     land that came out as water takes the first category that is not water.
	/// </summary>
	/// <param name="provinceRaster"><c>[y * width + x]</c> with y counting north.</param>
	public static CTerrain?[] FindTerrain(CBitmapFile terrain, ReadOnlySpan<ushort> provinceRaster,
		Definition definition, CMapPositions positions, Func<int, bool> isSea)
	{
		int width = terrain.Width;
		int height = terrain.Height;
		int provinceCount = positions.Unit.Length;
		int categoryCount = definition.Categories.Length;
		var counts = new int[Math.Max(categoryCount, 1)];
		var result = new CTerrain?[provinceCount];

		int firstLand = -1;
		for (int category = 0; category < categoryCount; category++)
		{
			if (!definition.Categories[category].IsWater)
			{
				firstLand = category;
				break;
			}
		}

		for (int id = 1; id < provinceCount; id++)
		{
			int chosen = CategoryAt(terrain, definition, positions.Unit[id].X, positions.Unit[id].Y);
			CMapPosition text = positions.Text[id];

			if (id == ProvinceTakenAtItsText)
			{
				chosen = CategoryAt(terrain, definition, text.X, text.Y);
			}
			else if (!isSea(id))
			{
				Array.Clear(counts);
				for (int x = text.X - Window / 2; x < text.X + Window / 2; x++)
				{
					for (int y = text.Y - Window / 2; y < text.Y + Window / 2; y++)
					{
						if (x < 0 || y < 0 || x >= width || y >= height || provinceRaster[y * width + x] != id)
						{
							continue;
						}
						int category = definition.CategoryOfIndex[terrain.StoredRow(height - 1 - y)[x]];
						if (category >= 0)
						{
							counts[category]++;
						}
					}
				}
				int most = 0;
				for (int category = 0; category < categoryCount; category++)
				{
					if (counts[category] > most)             // strictly more, so the first declared wins a tie
					{
						most = counts[category];
						chosen = category;
					}
				}
			}

			if (!isSea(id) && chosen >= 0 && definition.Categories[chosen].IsWater && firstLand >= 0)
			{
				chosen = firstLand;
			}
			result[id] = chosen < 0 ? null : definition.Categories[chosen];
		}
		return result;
	}
}
