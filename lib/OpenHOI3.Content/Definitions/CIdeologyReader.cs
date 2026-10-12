using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/ideologies.txt</c>: the groups that are the alignment triangle's corners, in the
/// file's order - each one's position, its ideologies and its faction. See
/// <c>docs/mechanics/alignment.md</c>, section 1.
/// </summary>
public static class CIdeologyReader
{
	private const string IdeologiesFile = "common/ideologies.txt";

	public static List<CIdeologyGroup> Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		string path = directories.Resolve(IdeologiesFile);
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (IOException e)
		{
			warn($"Could not read '{path}': {e.Message}");
			return new List<CIdeologyGroup>();
		}
		return Read(root, warn);
	}

	public static List<CIdeologyGroup> Read(ClausewitzNode root, Action<string> warn)
	{
		var groups = new List<CIdeologyGroup>();
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			ClausewitzNode? position = block.Get("position");
			ClausewitzNode? faction = block.Get("faction");
			if (position is null)
			{
				warn($"'{IdeologiesFile}': the group '{block.Key}' has no position");
			}
			var group = new CIdeologyGroup(block.Key, Thousandths(position, "x"), Thousandths(position, "y"))
			{
				Faction = faction?.GetString("tag") ?? "",
				FactionNeutrality = Number(faction?.GetString("neutrality")),
				Influence = faction?.GetString("influence") ?? "",
			};
			foreach (ClausewitzNode ideology in block.Children)
			{
				if (ideology.Key is not null && ideology.IsBlock && ideology != position && ideology != faction)
				{
					group.Ideologies.Add(ideology.Key);
				}
			}
			groups.Add(group);
		}
		return groups;
	}

	private static long Thousandths(ClausewitzNode? point, string key) => Number(point?.GetString(key)).Raw;

	private static CFixedPoint Number(string? text) =>
		CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
}
