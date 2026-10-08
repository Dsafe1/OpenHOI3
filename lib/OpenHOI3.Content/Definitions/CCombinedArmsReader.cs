using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/combined_arms.txt</c>: one block, <c>combined_arms</c>, holding a
/// block for each group with its <c>value</c> and, for a group a division has to have
/// before any counts, <c>base = yes</c>. See <c>docs/mechanics/builders.md</c>, section 4.
/// </summary>
public static class CCombinedArmsReader
{
	private const string GroupFile = "common/combined_arms.txt";

	public static List<CCombinedArmsGroup> Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var groups = new List<CCombinedArmsGroup>();
		string path = directories.Resolve(GroupFile);
		if (!File.Exists(path))
		{
			return groups;
		}

		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			warn($"Could not read '{path}': {e.Message}");
			return groups;
		}

		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ClausewitzNode list in root.All("combined_arms"))
		{
			foreach (ClausewitzNode entry in list.Children)
			{
				if (entry.Key is null || !entry.IsBlock)
				{
					continue;
				}
				CFixedPoint value = CFixedPoint.Zero;
				bool isBase = false;
				foreach (ClausewitzNode setting in entry.Children)
				{
					if (setting.Key is null || setting.Value is null)
					{
						continue;
					}
					if (setting.Key.Equals("value", StringComparison.OrdinalIgnoreCase)
						&& CFixedPoint.TryParse(setting.Value, out CFixedPoint parsed))
					{
						value = parsed;
					}
					else if (setting.Key.Equals("base", StringComparison.OrdinalIgnoreCase))
					{
						isBase = setting.Value.Equals("yes", StringComparison.OrdinalIgnoreCase);
					}
					else
					{
						unknown.Add(setting.Key);
					}
				}
				groups.Add(new CCombinedArmsGroup(entry.Key, isBase, value));
			}
		}
		if (unknown.Count > 0)
		{
			warn($"'{GroupFile}' gives groups {unknown.Count} keys that are not understood: {string.Join(", ", unknown)}");
		}
		return groups;
	}
}
