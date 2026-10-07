using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

/// <summary>
/// Reads <c>history/wars</c>: one file a war, each a history of who joined and who left.
/// Folded to a date, it says which wars are being fought on it and by whom.
///
/// Only the sides are kept. A war's goals and whether it is limited are read past.
/// </summary>
public static class CWarReader
{
	private const string WarsDirectory = "history/wars";

	/// <summary>Keys a war file has that are not about who is in it.</summary>
	private static readonly HashSet<string> ReadPast = new(StringComparer.OrdinalIgnoreCase)
	{
		"name", "limited_war", "war_goal",
	};

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

			var war = new CWar(root.GetString("name"));
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
}
