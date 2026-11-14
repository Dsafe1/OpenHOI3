using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Reads <c>map/coastlines.txt</c>: its one block, <c>strategic_resource_coastlines</c>, names the regions of
/// <c>map/region.txt</c> along whose coasts two countries' naval bases let them share a strategic resource. The answer
/// is the regions named, as indices into the map's regions and in <c>region.txt</c>'s order, each once. See
/// <c>docs/mechanics/modifiers.md</c>, section 6.
/// </summary>
public static class CCoastlinesReader
{
	/// <summary>The file's one key.</summary>
	public const string Key = "strategic_resource_coastlines";

	public static List<int> Read(string path, IReadOnlyList<CRegion> regions, Action<string> warn)
	{
		if (!File.Exists(path))
		{
			warn($"no coastlines: '{path}' is missing, so no strategic resource is shared by sea");
			return new List<int>();
		}
		return Read(ClausewitzParser.ParseFile(path), regions, text => warn($"'{path}': {text}"));
	}

	public static List<int> Read(ClausewitzNode root, IReadOnlyList<CRegion> regions, Action<string> warn)
	{
		// The original makes the list afresh at each block of the key, so the last one stands; everything else in
		// the file it passes over without a word.
		List<string>? names = null;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is not null && block.IsBlock && block.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))
			{
				names = new List<string>();
				foreach (ClausewitzNode entry in block.Children)
				{
					if (entry.Key is null && entry.Value is { Length: > 0 } name)
					{
						names.Add(name);
					}
					else
					{
						warn($"'{Key}' lists region names, and '{entry.Key} = ...' is not one");
					}
				}
			}
			else
			{
				warn($"'{block.Key ?? block.Value}' is not '{Key}', the file's one key");
			}
		}
		if (names is null)
		{
			return new List<int>();
		}

		// Each region of the map, in region.txt's order, that one of the names names - matched as written, case and all -
		// is taken once, however often it is named.
		var coastlines = new List<int>();
		var found = new HashSet<string>(StringComparer.Ordinal);
		for (int index = 0; index < regions.Count; index++)
		{
			if (names.Contains(regions[index].Name, StringComparer.Ordinal))
			{
				coastlines.Add(index);
				found.Add(regions[index].Name);
			}
		}
		// The original warns where it took fewer regions than the block has names, which a name given twice also does.
		if (coastlines.Count < names.Count)
		{
			var missing = names.FindAll(name => !found.Contains(name));
			warn(missing.Count > 0
				? $"no region is called {string.Join(", ", missing)}"
				: $"'{Key}' names a region more than once");
		}
		return coastlines;
	}
}
