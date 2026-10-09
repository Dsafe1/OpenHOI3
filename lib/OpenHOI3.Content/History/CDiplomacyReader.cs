using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Content.History;

/// <summary>
/// Reads <c>history/diplomacy</c>: who is whose puppet and who is allied to whom, each
/// between two dates. Guarantees, pacts and relations are in the same files and are
/// read past: nothing asks about them yet.
/// </summary>
public static class CDiplomacyReader
{
	private const string DiplomacyDirectory = "history/diplomacy";

	/// <summary>The kinds of entry these files hold that nothing here uses.</summary>
	private static readonly HashSet<string> ReadPast = new(StringComparer.OrdinalIgnoreCase)
	{
		"guarantee", "nap", "relation",
	};

	/// <summary>
	/// Makes every country that is a puppet on <paramref name="date"/> its master's, and
	/// gives the alliances standing on it. An entry counts from its first day up to,
	/// and not on, its last.
	/// </summary>
	public static List<CAlliance> Apply(CDirectorySettings directories, CCountries countries, CDate date,
		Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		var alliances = new List<CAlliance>();
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

			Read(root, countries.Find, date, alliances, unknownKeys, unknownTags);
		}

		if (unknownKeys.Count > 0)
		{
			warn($"diplomatic history has {unknownKeys.Count} keys that are not understood: {string.Join(", ", unknownKeys)}");
		}
		if (unknownTags.Count > 0)
		{
			warn($"diplomatic history names {unknownTags.Count} countries that are not defined: {string.Join(", ", unknownTags)}");
		}
		return alliances;
	}

	/// <summary>One file's entries: the puppets are made their masters', the alliances added to the list.</summary>
	public static void Read(ClausewitzNode root, Func<string?, CCountry?> find, CDate date, List<CAlliance> alliances,
		ISet<string> unknownKeys, ISet<string> unknownTags)
	{
		foreach (ClausewitzNode entry in root.Children)
		{
			if (entry.Key is null || !entry.IsBlock || ReadPast.Contains(entry.Key))
			{
				continue;
			}
			bool vassal = entry.Key.Equals("vassal", StringComparison.OrdinalIgnoreCase);
			if (!vassal && !entry.Key.Equals("alliance", StringComparison.OrdinalIgnoreCase))
			{
				unknownKeys.Add(entry.Key);
				continue;
			}
			if (!Stands(entry, date))
			{
				continue;
			}

			CCountry? first = find(entry.GetString("first"));
			CCountry? second = find(entry.GetString("second"));
			if (first is null || second is null)
			{
				unknownTags.Add(first is null ? entry.GetString("first") : entry.GetString("second"));
				continue;
			}
			if (vassal)
			{
				second.Overlord = first.Tag;
			}
			else
			{
				alliances.Add(new CAlliance(first.Tag, second.Tag));
			}
		}
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
