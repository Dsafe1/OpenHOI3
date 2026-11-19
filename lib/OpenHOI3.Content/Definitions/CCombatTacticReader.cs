using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;

namespace OpenHOI3.Content.Definitions;

/// <summary>
/// Reads <c>common/combat_tactics.txt</c>: a block for each tactic, numbered from 1 in
/// the order of the file, with what it weighs, whom it suits, what counters it, what it
/// does to a battle and the <c>trigger</c> a side has to meet to pick it. See
/// <c>docs/mechanics/landcombat.md</c>, section 11.
///
/// The original logs a key it does not know and goes on. So does this; and a condition
/// of a <c>trigger</c> that is not understood is reported and never holds, so that a
/// tactic is not handed out on the strength of a test nothing made.
/// </summary>
public static class CCombatTacticReader
{
	private const string TacticFile = "common/combat_tactics.txt";

	public static List<CCombatTactic> Read(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var tactics = new List<CCombatTactic>();
		string path = directories.Resolve(TacticFile);
		if (!File.Exists(path))
		{
			return tactics;
		}

		ClausewitzNode root;
		try
		{
			root = ClausewitzParser.ParseFile(path);
		}
		catch (Exception e)
		{
			warn(CDiagnostics.Located(path, 0, $"could not be read: {e.Message}"));
			return tactics;
		}
		return Read(root, warn);
	}

	/// <summary>The tactics of a file already parsed.</summary>
	public static List<CCombatTactic> Read(ClausewitzNode root, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Problem;
		var tactics = new List<CCombatTactic>();
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Action<string> report = warn;
		void UnknownKey(ClausewitzNode entry, string text) =>
			report(CDiagnostics.Located(TacticFile, entry.Line, $"'{text}' is a key of a tactic that is not understood"));
		void UnknownCondition(ClausewitzNode entry, string text) => report(CDiagnostics.Located(TacticFile, entry.Line,
			$"'{text}' is a condition of a tactic's trigger block that is not understood and never holds"));
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			if (!names.Add(block.Key))
			{
				warn(CDiagnostics.Located(TacticFile, block.Line,
					$"the tactic '{block.Key}' is declared more than once; the first declaration stands"));
				continue;
			}

			CCombatCondition? condition = null;
			string picture = "";
			string counteredBy = "";
			var figures = new Dictionary<string, CFixedPoint>(StringComparer.OrdinalIgnoreCase);
			foreach (ClausewitzNode entry in block.Children)
			{
				if (entry.Key is null)
				{
					continue;
				}
				string key = entry.Key.ToLowerInvariant();
				switch (key)
				{
					case "trigger" when entry.IsBlock:
						condition = CCombatConditionReader.ReadGroup(entry, ECombatCondition.All, UnknownCondition);
						break;
					case "picture":
						picture = entry.Value ?? "";
						break;
					case "countered_by":
						counteredBy = entry.Value ?? "";
						break;
					case "base":
					case "aggressiveness":
					case "combat_width":
					case "attacker":
					case "defender":
					case "movement_speed":
						if (CFixedPoint.TryParse(entry.Value, out CFixedPoint figure))
						{
							figures[key] = figure;
						}
						else
						{
							UnknownKey(entry, $"{entry.Key} = {entry.Value}");
						}
						break;
					default:
						UnknownKey(entry, entry.Key);
						break;
				}
			}

			tactics.Add(new CCombatTactic(block.Key, tactics.Count + 1)
			{
				Picture = picture,
				CounteredBy = counteredBy,
				Base = figures.GetValueOrDefault("base"),
				Aggressiveness = figures.GetValueOrDefault("aggressiveness"),
				Width = figures.GetValueOrDefault("combat_width"),
				Attacker = figures.GetValueOrDefault("attacker"),
				Defender = figures.GetValueOrDefault("defender"),
				Speed = figures.GetValueOrDefault("movement_speed"),
				Condition = condition,
			});
		}

		// The game state's link knows no file or line, so what it says is placed in the file alone.
		CCombatTactics.Link(tactics, text => report(CDiagnostics.Located(TacticFile, 0, text)));
		return tactics;
	}
}
