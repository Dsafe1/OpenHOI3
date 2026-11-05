using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Content.History;

/// <summary>
/// Reads <c>history/wars</c>: one file a war, each a history of who joined and who left.
/// Folded to a date, it says which wars are being fought on it and by whom.
///
/// Its sides and whether it is limited are read with the history; its goals once the casus belli types
/// are, by <see cref="ReadGoals"/> (<c>docs/mechanics/war.md</c>, section 1).
/// </summary>
public static class CWarReader
{
	private const string WarsDirectory = "history/wars";

	/// <summary>Keys a war file has that are not about who is in it.</summary>
	private static readonly HashSet<string> ReadPast = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "limited_war", "war_goal",
	};

	/// <summary>
	/// When a war began: the earliest date its file has an entry for, as long as that is
	/// not after <paramref name="date"/>. A file with no dated entry gives the date asked about.
	/// </summary>
	private static CDate FirstDate(ClausewitzNode root, CDate date)
	{
		CDate first = date;
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key is not null && entry.IsBlock && CDate.TryParse(entry.Key, out CDate dated)
				&& dated.CompareTo(first) < 0)
			{
				first = dated;
			}
		}
		return first;
	}

	/// <summary>The wars with someone on both sides on <paramref name="date"/>.</summary>
	public static List<CWar> Read(CDirectorySettings directories, CCountries countries, CDate date,
		Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var wars = new List<CWar>();
		var unknownKeys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknownTags = new SortedSet<string>(StringComparer.Ordinal);

		foreach (string file in directories.ListFiles(WarsDirectory, "*.txt"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (IOException e)
			{
				warn($"Could not read '{file}': {e.Message}");
				continue;
			}

			// Its last action is the hour the history is folded to, not its first entry's: the start save's war of
			// 1935.10.3 has 1936.1.1.0 (savegame.md, section 10; measured).
			var war = new CWar(root.GetString("name"), FirstDate(root, date))
			{
				IsLimited = root.GetBool("limited_war"),
				Action = date.DayNumber * (long)CGameState.HoursPerDay,
			};
			foreach (ClausewitzNode entry in CHistory.Fold(root, date))
			{
				if (entry.Key is null || ReadPast.Contains(entry.Key))
				{
					continue;
				}

				List<CCountryTag>? side = entry.Key.ToLowerInvariant() switch
				{
					"add_attacker" or "rem_attacker" => war.Attackers,
					"add_defender" or "rem_defender" => war.Defenders,
					_ => null,
				};
				if (side is null)
				{
					unknownKeys.Add(entry.Key);
					continue;
				}
				if (!CCountryTag.TryParse(entry.Value, out CCountryTag tag) || countries.Find(tag) is null)
				{
					unknownTags.Add(entry.Value ?? "");
					continue;
				}

				if (entry.Key.StartsWith("add", StringComparison.OrdinalIgnoreCase))
				{
					if (!side.Contains(tag))
					{
						side.Add(tag);
					}
				}
				else
				{
					side.Remove(tag);
				}
			}

			if (war.Attackers.Count > 0 && war.Defenders.Count > 0)
			{
				wars.Add(war);
			}
		}

		if (unknownKeys.Count > 0)
		{
			warn($"war history has {unknownKeys.Count} keys that are not understood: {string.Join(", ", unknownKeys)}");
		}
		if (unknownTags.Count > 0)
		{
			warn($"war history names {unknownTags.Count} countries that are not defined: {string.Join(", ", unknownTags)}");
		}
		return wars;
	}

	/// <summary>
	/// Each fought war's goals, out of its file's <c>war_goal</c> blocks dated on or before the game's day:
	/// filed under the actor with no test of the goal, as the original's loader files them. A war is found
	/// by its name.
	/// </summary>
	public static void ReadGoals(CDirectorySettings directories, CGameState state)
	{
		foreach (string file in directories.ListFiles(WarsDirectory, "*.txt"))
		{
			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file);
			}
			catch (IOException)
			{
				continue;
			}
			string name = root.GetString("name");
			CWar? war = state.Wars.Find(w => w.MadeName is null && w.Name == name);
			if (war is null)
			{
				continue;
			}
			foreach (ClausewitzNode entry in CHistory.Fold(root, state.Date))
			{
				if (entry.Key is not { } key || !key.Equals("war_goal", StringComparison.OrdinalIgnoreCase) || !entry.IsBlock
					|| !CCountryTag.TryParse(entry.GetString("actor"), out CCountryTag actor)
					|| !CCountryTag.TryParse(entry.GetString("receiver"), out CCountryTag receiver))
				{
					continue;
				}
				var goal = new CWarGoal(CCasusBellis.Find(state, entry.GetString("casus_belli")), actor, receiver)
				{
					Country = CCountryTag.TryParse(entry.GetString("country"), out CCountryTag third) ? third : null,
					Region = state.Regions.FirstOrDefault(r => r.Name == entry.GetString("region")),
				};
				if (!war.Goals.TryGetValue(actor, out List<CWarGoal>? goals))
				{
					war.Goals[actor] = goals = new List<CWarGoal>();
				}
				goals.Add(goal);
			}
		}
	}
}
