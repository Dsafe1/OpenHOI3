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

	private const string AiPriority = "ai_prio";

	public static List<CRegion> Read(string path, int provinceCount, Action<string> warn)
	{
		if (!File.Exists(path))
		{
			warn(CDiagnostics.Located(path, 0, "no regions: the file is missing"));
			return new List<CRegion>();
		}
		return Read(ClausewitzParser.ParseFile(path), provinceCount, warn, path);
	}

	/// <param name="file">The file the text is, which each report is placed in with its line; none, the report alone.</param>
	public static List<CRegion> Read(ClausewitzNode root, int provinceCount, Action<string> warn, string file = "")
	{
		var regions = new List<CRegion>();
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			var provinces = new List<int>();
			bool useForPeace = false;
			bool aiPriority = false;
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null && string.Equals(entry.Value, Peace, StringComparison.OrdinalIgnoreCase))
				{
					useForPeace = true;
				}
				else if (entry.Key is null && string.Equals(entry.Value, AiPriority, StringComparison.OrdinalIgnoreCase))
				{
					aiPriority = true;
				}
				else if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
					&& id > 0 && id < provinceCount)
				{
					provinces.Add(id);
				}
				else
				{
					// The original passes over an id beyond the map without a word.
					warn(CDiagnostics.Located(file, entry.Line,
						$"'{entry.Value ?? entry.Key}' is neither the id of a province nor '{Peace}' or '{AiPriority}'"));
				}
			}
			regions.Add(new CRegion(block.Key, provinces, useForPeace, aiPriority));
		}
		return regions;
	}
}
