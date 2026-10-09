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
		warn ??= CDiagnostics.Warn;
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
			warn($"Could not read '{path}': {e.Message}");
			return tactics;
		}
		return Read(root, warn);
	}

	/// <summary>The tactics of a file already parsed.</summary>
	public static List<CCombatTactic> Read(ClausewitzNode root, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var tactics = new List<CCombatTactic>();
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknownKeys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknownConditions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ClausewitzNode block in root.Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			if (!names.Add(block.Key))
			{
				warn($"'{TacticFile}' declares the tactic '{block.Key}' more than once; the first declaration stands");
				continue;
			}

			CTacticCondition? condition = null;
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
						condition = ReadGroup(entry, ETacticCondition.All, unknownConditions);
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
							unknownKeys.Add($"{entry.Key} = {entry.Value}");
						}
						break;
					default:
						unknownKeys.Add(entry.Key);
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

		CCombatTactics.Link(tactics, warn);
		if (unknownKeys.Count > 0)
		{
			warn($"'{TacticFile}' gives tactics {unknownKeys.Count} keys that are not understood: {string.Join(", ", unknownKeys)}");
		}
		if (unknownConditions.Count > 0)
		{
			warn($"tactics' trigger blocks have {unknownConditions.Count} conditions that are not understood " +
				$"and never hold: {string.Join(", ", unknownConditions)}");
		}
		return tactics;
	}

	private static CTacticCondition ReadGroup(ClausewitzNode block, ETacticCondition kind, SortedSet<string> unknown)
	{
		var group = new CTacticCondition(kind);
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null)
			{
				continue;
			}
			string key = entry.Key.ToLowerInvariant();
			if (entry.IsBlock)
			{
				switch (key)
				{
					case "or":
						group.Children.Add(ReadGroup(entry, ETacticCondition.Any, unknown));
						break;
					case "and":
						group.Children.Add(ReadGroup(entry, ETacticCondition.All, unknown));
						break;
					case "not":
						group.Children.Add(ReadGroup(entry, ETacticCondition.None, unknown));
						break;
					default:
						unknown.Add(entry.Key);
						group.Children.Add(new CTacticCondition(ETacticCondition.Never));
						break;
				}
				continue;
			}

			string value = entry.Value ?? "";
			bool wanted = value.Equals("yes", StringComparison.OrdinalIgnoreCase);
			bool isYesNo = wanted || value.Equals("no", StringComparison.OrdinalIgnoreCase);
			bool isNumber = CFixedPoint.TryParse(value, out CFixedPoint amount);
			switch (key)
			{
				case "is_attacker" when isYesNo:
					group.Children.Add(new CTacticCondition(ETacticCondition.IsAttacker, wanted));
					break;
				case "frontage_full" when isYesNo:
					group.Children.Add(new CTacticCondition(ETacticCondition.FrontageFull, wanted));
					break;
				case "reserves" when isYesNo:
					group.Children.Add(new CTacticCondition(ETacticCondition.Reserves, wanted));
					break;
				case "has_armour_unit" when isYesNo:
					group.Children.Add(new CTacticCondition(ETacticCondition.HasArmourUnit, wanted));
					break;
				case "skill_advantage" when isNumber:
					group.Children.Add(new CTacticCondition(ETacticCondition.SkillAdvantage, amount: (int)amount.ToInt()));
					break;
				case "skill" when isNumber:
					group.Children.Add(new CTacticCondition(ETacticCondition.Skill, amount: (int)amount.ToInt()));
					break;
				case "trait" when value.Length > 0:
					group.Children.Add(new CTacticCondition(ETacticCondition.Trait, name: value));
					break;
				default:
					unknown.Add(value.Length > 0 ? $"{entry.Key} = {value}" : entry.Key);
					group.Children.Add(new CTacticCondition(ETacticCondition.Never));
					break;
			}
		}
		return group;
	}
}
