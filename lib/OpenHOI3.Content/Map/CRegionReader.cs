using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Reads <c>map/region.txt</c>: one named block each, listing province ids, and the word
/// <c>peace</c> in a few. The regions keep the file's order, which is the order a
/// province is given them in. See <c>docs/mechanics/aircombat.md</c>, section 2.
/// </summary>
public static class CRegionReader
{
	private const string Peace = "peace";

	public static List<CRegion> Read(string path, int provinceCount, Action<string> warn)
	{
		if (!File.Exists(path))
		{
			warn($"no regions: '{path}' is missing");
			return new List<CRegion>();
		}
		return Read(ClausewitzParser.ParseFile(path), provinceCount, text => warn($"'{path}': {text}"));
	}

	public static List<CRegion> Read(ClausewitzNode root, int provinceCount, Action<string> warn)
	{
		var regions = new List<CRegion>();
		int unknown = 0;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			var provinces = new List<int>();
			bool useForPeace = false;
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null && string.Equals(entry.Value, Peace, StringComparison.OrdinalIgnoreCase))
				{
					useForPeace = true;
				}
				else if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
					&& id > 0 && id < provinceCount)
				{
					provinces.Add(id);
				}
				else
				{
					// The original passes over an id beyond the map without a word.
					unknown++;
				}
			}
			regions.Add(new CRegion(block.Key, provinces, useForPeace));
		}
		if (unknown > 0)
		{
			warn($"{unknown} entries are neither the id of a province nor '{Peace}'");
		}
		return regions;
	}
}
