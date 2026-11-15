using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Agreements;

namespace OpenHOI3.Content.History;

/// <summary>
/// Reads <c>history/diplomacy</c>: who is whose puppet, who is allied to whom, who guarantees whom and
/// which pairs have a non-aggression pact, each between two dates, and the relations it sets. A
/// guarantee and a pact carry the hour they began, which their ending counts from
/// (<c>docs/mechanics/agreements.md</c>, section 6); none of the base game's pacts stands in 1936.
/// </summary>
public static class CDiplomacyReader
{
	private const string DiplomacyDirectory = "history/diplomacy";

	/// <summary>The kinds of entry these files hold that nothing here uses: none now.</summary>
	private static readonly HashSet<string> ReadPast = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Makes every country that is a puppet on <paramref name="date"/> its master's, and gives the
	/// alliances and guarantees standing on it and every relation entry. An entry counts from its
	/// first day up to, and not on, its last.
	/// </summary>
	public static CDiplomacyHistory Apply(CDirectorySettings directories, CCountries countries, CDate date,
		Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var history = new CDiplomacyHistory();
		var unknownKeys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var unknownTags = new SortedSet<string>(StringComparer.Ordinal);

		foreach (string file in directories.ListFiles(DiplomacyDirectory, "*.txt"))
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

			Read(root, countries.Find, date, history, unknownKeys, unknownTags);
		}

		if (unknownKeys.Count > 0)
		{
			warn($"diplomatic history has {unknownKeys.Count} keys that are not understood: {string.Join(", ", unknownKeys)}");
		}
		if (unknownTags.Count > 0)
		{
			warn($"diplomatic history names {unknownTags.Count} countries that are not defined: {string.Join(", ", unknownTags)}");
		}
		return history;
	}

	/// <summary>One file's entries.</summary>
	public static void Read(ClausewitzNode root, Func<string?, CCountry?> find, CDate date, CDiplomacyHistory history,
		ISet<string> unknownKeys, ISet<string> unknownTags)
	{
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key is null || !entry.IsBlock || ReadPast.Contains(entry.Key))
			{
				continue;
			}
			string kind = entry.Key.ToLowerInvariant();
			if (kind is not ("vassal" or "alliance" or "guarantee" or "nap" or "relation"))
			{
				unknownKeys.Add(entry.Key);
				continue;
			}

			CCountry? first = find(entry.GetString("first"));
			CCountry? second = find(entry.GetString("second"));
			if (first is null || second is null)
			{
				unknownTags.Add(first is null ? entry.GetString("first") : entry.GetString("second"));
				continue;
			}

			if (kind == "relation")
			{
				// Every entry: the simulation applies their dates, one side each. One with no start
				// stands from the beginning.
				if (CFixedPoint.TryParse(entry.GetString("value"), out CFixedPoint value))
				{
					CDate start = CDate.TryParse(entry.GetString("start_date"), out CDate starts) ? starts : new CDate(1, 1, 1);
					CDate? end = CDate.TryParse(entry.GetString("end_date"), out CDate ends) ? ends : null;
					history.Relations.Add(new CHistoryRelation(first.Tag, second.Tag, value, start, end));
				}
				continue;
			}
			if (!Stands(entry, date))
			{
				continue;
			}
			// Each numbered in the order it was read, which the game's numbering takes within a start date
			// (CGameStateLoader.NumberHistoryPacts); its end is only what a save writes.
			long read = ++history.PactsRead;
			long startHour = StartHour(entry, date);
			long? endHour = CDate.TryParse(entry.GetString("end_date"), out CDate endDay)
				? endDay.DayNumber * (long)CGameState.HoursPerDay
				: null;
			switch (kind)
			{
				case "vassal":
					second.Overlord = first.Tag;
					second.PuppetMade = read;
					second.PuppetStart = startHour;
					second.PuppetEnd = endHour;
					break;
				case "alliance":
					history.Alliances.Add(new CAlliance(first.Tag, second.Tag) { Made = read, Start = startHour, End = endHour });
					break;
				case "nap":
					history.Naps.Add(new CNap(first.Tag, second.Tag, startHour) { Made = read, End = endHour });
					break;
				default:
					history.Guarantees.Add(new CGuarantee(first.Tag, second.Tag, startHour) { Made = read, End = endHour });
					break;
			}
		}
	}

	/// <summary>The hour an entry began: its <c>start_date</c>, or the game's date where it gives none.</summary>
	private static long StartHour(ClausewitzNode entry, CDate date)
	{
		CDate start = CDate.TryParse(entry.GetString("start_date"), out CDate starts) ? starts : date;
		return start.DayNumber * (long)CGameState.HoursPerDay;
	}

	private static bool Stands(ClausewitzNode entry, CDate date)
	{
		if (CDate.TryParse(entry.GetString("start_date"), out CDate start) && date.CompareTo(start) < 0)
		{
			return false;
		}
		return !CDate.TryParse(entry.GetString("end_date"), out CDate end) || date.CompareTo(end) < 0;
	}
}
