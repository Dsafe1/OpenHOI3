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
		warn ??= CDiagnostics.Problem;
		string path = directories.Resolve(IdeologiesFile);
		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (IOException e)
		{
			warn(CDiagnostics.Located(path, 0, $"could not be read: {e.Message}"));
			return new List<CIdeologyGroup>();
		}
		return Read(root, warn, path);
	}

	public static List<CIdeologyGroup> Read(ClausewitzNode root, Action<string> warn, string file = IdeologiesFile)
	{
		var groups = new List<CIdeologyGroup>();
		// The kinds of modifier known as each group is read: a faction's own kind is made once its block is read, so a
		// group's blocks know only the factions before it (modifiers.md, section 2).
		var factions = new List<string>();
		const string what = "ideologies and faction modifiers";
		void Passed(ClausewitzNode node, string message) => warn(CDiagnostics.Located(file, node.Line, message));
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				// Said, where it was passed over (clausewitz.md, section 3).
				Passed(block, $"'{block.Key ?? block.Value}' is no group: a name and a block; passed over");
				continue;
			}
			var kinds = new Sim.Modifiers.CModifierKeys(factions);
			ClausewitzNode? position = block.Get("position");
			ClausewitzNode? faction = block.Get("faction");
			if (position is null)
			{
				Passed(block, $"the group '{block.Key}' has no position");
			}
			foreach (ClausewitzNode part in position?.Children ?? new List<ClausewitzNode>())
			{
				if (part.Key is null || !(part.Key.Equals("x", StringComparison.OrdinalIgnoreCase)
					|| part.Key.Equals("y", StringComparison.OrdinalIgnoreCase)))
				{
					Passed(part, $"'{block.Key}/position/{part.Key ?? part.Value}' is a key nothing reads; passed over");
				}
			}
			foreach (ClausewitzNode part in faction?.Children ?? new List<ClausewitzNode>())
			{
				if (part.Key is null || !FactionKeys.Contains(part.Key))
				{
					Passed(part, $"'{block.Key}/faction/{part.Key ?? part.Value}' is a key nothing reads; passed over");
				}
			}
			foreach (ClausewitzNode part in block.Children)
			{
				if (part.Key is null || !part.IsBlock)
				{
					Passed(part, $"'{block.Key}/{part.Key ?? part.Value}' is no ideology: a name and a block; passed over");
				}
			}
			ClausewitzNode? modifier = faction?.Get("modifier");
			var group = new CIdeologyGroup(block.Key, Thousandths(position, "x"), Thousandths(position, "y"))
			{
				Faction = faction?.GetString("tag") ?? "",
				FactionNeutrality = Number(faction?.GetString("neutrality")),
				Influence = faction?.GetString("influence") ?? "",
				FactionIcon = int.TryParse(faction?.GetString("icon"), out int icon) ? icon : 0,
				FactionModifier = modifier is { IsBlock: true } ? ReadModifier(modifier, block.Key, kinds, warn, file) : new(),
			};
			ReadRules(faction?.Get("rule"), group, warn, file);
			foreach (ClausewitzNode ideology in block.Children)
			{
				if (ideology.Key is not null && ideology.IsBlock && ideology != position && ideology != faction)
				{
					group.Ideologies.Add(ideology.Key);
					group.IdeologyOrder.Add(ideology.Key);
					group.IdeologyColours[ideology.Key] = ideology.GetRgb("color", CColor.Gray);
					// An ideology is a modifier with a colour of its own; what of it is a kind is read and not kept.
					CModifierReader.ReadBlock(ideology, IdeologyKeys, kinds,
						entry => warn(CModifierReader.Unknown(file, what, ideology, entry)));
				}
			}
			groups.Add(group);
			factions.Add(group.Faction);
		}
		return groups;
	}

	/// <summary>
	/// A faction's rules: each key one of <see cref="CFactionRules"/>, <c>yes</c> or <c>no</c>. Any other
	/// value is reported and leaves the rule unset; a key that is no rule is reported, as the original
	/// logs "unknown rule".
	/// </summary>
	private static void ReadRules(ClausewitzNode? block, CIdeologyGroup group, Action<string> warn, string file)
	{
		if (block is not { IsBlock: true })
		{
			return;
		}
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null)
			{
				continue;
			}
			if (!CFactionRules.IsRule(entry.Key))
			{
				warn(CDiagnostics.Located(file, entry.Line, $"unknown rule '{entry.Key}' in the group '{group.Name}'"));
			}
			else if (string.Equals(entry.Value, "yes", StringComparison.OrdinalIgnoreCase))
			{
				group.FactionRules.Add(entry.Key);
			}
			else if (!string.Equals(entry.Value, "no", StringComparison.OrdinalIgnoreCase))
			{
				warn(CDiagnostics.Located(file, entry.Line, $"the rule '{entry.Key}' of the group '{group.Name}' is neither yes nor no"));
			}
		}
	}

	/// <summary>An ideology's own key besides the kinds of modifier.</summary>
	private static readonly HashSet<string> IdeologyKeys = new(StringComparer.OrdinalIgnoreCase) { "color" };

	/// <summary>What a group's <c>faction</c> block holds.</summary>
	private static readonly HashSet<string> FactionKeys = new(StringComparer.OrdinalIgnoreCase)
	{
		"tag", "neutrality", "influence", "icon", "modifier", "rule",
	};

	private static Sim.Modifiers.CModifier ReadModifier(ClausewitzNode block, string group,
		Sim.Modifiers.CModifierKeys kinds, Action<string> warn, string file)
	{
		return CModifierReader.ReadBlock(block, new HashSet<string>(), kinds,
			entry => warn(CModifierReader.Unknown(file, "ideologies and faction modifiers", block, entry)),
			entry => warn(CDiagnostics.Located(file, entry.Line,
				$"{entry.Key} = {entry.Value} in the group '{group}''s faction modifier is no number")));
	}

	private static long Thousandths(ClausewitzNode? point, string key) => Number(point?.GetString(key)).Raw;

	private static CFixedPoint Number(string? text) =>
		CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
}
