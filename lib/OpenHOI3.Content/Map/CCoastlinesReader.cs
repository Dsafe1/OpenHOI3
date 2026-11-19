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
			warn(CDiagnostics.Located(path, 0, "no coastlines: the file is missing, so no strategic resource is shared by sea"));
			return new List<int>();
		}
		return Read(ClausewitzParser.ParseFile(path), regions, warn, path);
	}

	/// <param name="file">The file the text is, which each report is placed in with its line; none, the report alone.</param>
	public static List<int> Read(ClausewitzNode root, IReadOnlyList<CRegion> regions, Action<string> warn, string file = "")
	{
		// The original makes the list afresh at each block of the key, so the last one stands; everything else in
		// the file it passes over without a word.
		List<ClausewitzNode>? entries = null;
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is not null && block.IsBlock && block.Key.Equals(Key, StringComparison.OrdinalIgnoreCase))
			{
				entries = new List<ClausewitzNode>();
				foreach (ClausewitzNode entry in block.Children)
				{
					if (entry.Key is null && entry.Value is { Length: > 0 })
					{
						entries.Add(entry);
					}
					else
					{
						warn(CDiagnostics.Located(file, entry.Line, $"'{Key}' lists region names, and '{entry.Key} = ...' is not one"));
					}
				}
			}
			else
			{
				warn(CDiagnostics.Located(file, block.Line, $"'{block.Key ?? block.Value}' is not '{Key}', the file's one key"));
			}
		}
		if (entries is null)
		{
			return new List<int>();
		}
		List<string> names = entries.ConvertAll(entry => entry.Value!);

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
		// The original warns where it took fewer regions than the block has names, which a name given twice also does:
		// said here at each name that is no region and at each name given again.
		if (coastlines.Count < names.Count)
		{
			var named = new HashSet<string>(StringComparer.Ordinal);
			foreach (ClausewitzNode entry in entries)
			{
				string name = entry.Value!;
				if (!found.Contains(name))
				{
					warn(CDiagnostics.Located(file, entry.Line, $"no region is called {name}"));
				}
				else if (!named.Add(name))
				{
					warn(CDiagnostics.Located(file, entry.Line, $"'{Key}' names the region {name} more than once"));
				}
			}
		}
		return coastlines;
	}
}
