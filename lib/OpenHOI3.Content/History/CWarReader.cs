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

	/// <summary>
	/// When a finished war ended: the latest date its file has an entry for that is not after <paramref name="date"/> - the
	/// original's last history entry, in year 2 or later; <see cref="CDate.Zero"/> where it has none.
	/// </summary>
	private static CDate LastDate(ClausewitzNode root, CDate date)
	{
		CDate last = CDate.Zero;
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key is not null && entry.IsBlock && CDate.TryParse(entry.Key, out CDate dated) && dated.Year >= 2
				&& dated.CompareTo(date) <= 0 && dated.CompareTo(last) > 0)
			{
				last = dated;
			}
		}
		return last;
	}

	/// <summary>
	/// Whether a war's history began on or before the date: its first dated entry is not after it, or it has none - the
	/// original's start of a war of no history is the null date, before every date.
	/// </summary>
	private static bool BeganBy(ClausewitzNode root, CDate date)
	{
		bool anyDated = false;
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key is not null && entry.IsBlock && CDate.TryParse(entry.Key, out CDate dated))
			{
				if (dated.CompareTo(date) <= 0)
				{
					return true;
				}
				anyDated = true;
			}
		}
		return !anyDated;
	}

	/// <summary>
	/// The wars with someone on both sides on <paramref name="date"/>. Every file is a war, whatever it holds: one that is
	/// not fought on the date and began on or before it - by its first dated entry, and any file with none - is a finished
	/// war, put on <paramref name="finished"/> where one is given; one that begins later is neither
	/// (<c>reversing/findings/FINDINGS-war.md</c>, C.13.1). So the base game's two files with no war in them are the two
	/// nameless finished wars every save of the original holds.
	/// </summary>
	/// <param name="joins">
	/// Given, each country an entry adds to a side, with each country on the other side at that moment, in the order
	/// replayed - what the original's adding brings up to date (<c>docs/mechanics/war.md</c>, section 4).
	/// </param>
	/// <param name="definesStart">
	/// The defines' start date, where a bookmark's later date is folded to: the original sorts its wars between its
	/// standing and its other list once at the end of each pass of the history, the pass to the defines' start date before
	/// the bookmark's, so a war standing after the first stays ahead of the bookmark's own and one that has ended by the
	/// bookmark goes to the back of the finished (<c>docs/mechanics/war.md</c>, section 1;
	/// <c>reversing/findings/FINDINGS-bookmarks.md</c>, section 2.2).
	/// </param>
	public static List<CWar> Read(CDirectorySettings directories, CCountries countries, CDate date,
		Action<string>? warn = null, List<CWar>? finished = null, List<(CCountryTag Joiner, CCountryTag Enemy)>? joins = null,
		CDate? definesStart = null)
	{
		warn ??= CDiagnostics.Problem;
		var wars = new List<CWar>();
		// The wars standing after the pass to the defines' start date, and those of the bookmark's pass; the finished wars
		// that stood after that pass and ended in the bookmark's, which the original moves to the back of its other list.
		var standingEarly = new List<CWar>();
		var endedLate = new List<CWar>();
		CDate early = definesStart is { } defines && defines.CompareTo(date) < 0 ? defines : date;
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
					foreach (CCountryTag enemy in ReferenceEquals(side, war.Attackers) ? war.Defenders : war.Attackers)
					{
						joins?.Add((tag, enemy));
					}
				}
				else
				{
					side.Remove(tag);
				}
			}

			bool stoodEarly = early.CompareTo(date) < 0 && StandsAt(root, countries, early);
			if (war.Attackers.Count > 0 && war.Defenders.Count > 0)
			{
				(stoodEarly ? standingEarly : wars).Add(war);
			}
			else if (BeganBy(root, date))
			{
				// A finished war keeps its name and what is left of its sides, and has no last action; it ended on its last
				// dated entry (warexhaustion.md, section 3).
				war.Action = 0;
				war.Ended = LastDate(root, date);
				(stoodEarly ? endedLate : finished)?.Add(war);
			}
		}
		wars.InsertRange(0, standingEarly);
		finished?.AddRange(endedLate);

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

	/// <summary>Whether the war has somebody on both sides with its history folded to the date.</summary>
	private static bool StandsAt(ClausewitzNode root, CCountries countries, CDate date)
	{
		var attackers = new HashSet<CCountryTag>();
		var defenders = new HashSet<CCountryTag>();
		foreach (ClausewitzNode entry in CHistory.Fold(root, date))
		{
			if (entry.Key is null || !CCountryTag.TryParse(entry.Value, out CCountryTag tag) || countries.Find(tag) is null)
			{
				continue;
			}
			switch (entry.Key.ToLowerInvariant())
			{
				case "add_attacker": attackers.Add(tag); break;
				case "rem_attacker": attackers.Remove(tag); break;
				case "add_defender": defenders.Add(tag); break;
				case "rem_defender": defenders.Remove(tag); break;
			}
		}
		return attackers.Count > 0 && defenders.Count > 0;
	}

	/// <summary>
	/// Each war's goals, out of its file's <c>war_goal</c> blocks dated on or before the game's day, as the original's
	/// history replay adds them - through <c>CWarGoal::AddToWar</c>, after every country's history to the date
	/// (<c>docs/mechanics/war.md</c>, section 1): a goal is added only where its type is real and its <c>is_valid</c>
	/// holds for the recipient with THIS the actor, so a goal on a country already in exile at the date, or one that
	/// owns nothing, is never added; and each one added stamps the actor's cooldown against the recipient a
	/// <c>WARGOAL_ADD_COOLDOWN</c> on from the defines' start date, the lobby's own hour - a war over by the date
	/// keeping no goal and its stamps all the same. A war is found by its name.
	/// Ours: every goal is asked of the world at the game's day - which gives the four start saves' goals and stamps
	/// exactly, Italy's on Ethiopia stamped at 1941 and not at 1939, when Ethiopia owns nothing; how the original's three
	/// passes of the history come to the same was not read.
	/// </summary>
	public static void ReadGoals(CDirectorySettings directories, CGameState state)
	{
		long lobbyHour = state.StartDate.DayNumber * (long)CGameState.HoursPerDay;
		int months = (int)(state.Defines["diplomacy", CWarGoals.Cooldown].Raw / 1000);
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
			bool over = war is null && state.FinishedWars.Exists(w => w.MadeName is null && w.Name == name);
			if (war is null && !over)
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
				if (!goal.Type.IsReal || !CCasusBellis.IsValid(state, actor, receiver, goal.Type))
				{
					continue;
				}
				if (war is not null)
				{
					if (!war.Goals.TryGetValue(actor, out List<CWarGoal>? goals))
					{
						war.Goals[actor] = goals = new List<CWarGoal>();
					}
					goals.Add(goal);
				}
				if (state.Country(actor) is { } holder && state.Country(receiver) is { } recipient)
				{
					CGameState.Status(holder, recipient).NextWarGoal = Sim.Agreements.COfferRules.AddMonths(lobbyHour, months);
				}
			}
		}
	}
}
