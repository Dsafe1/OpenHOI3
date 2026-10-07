using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

/// <summary>
/// Builds the one start: the state of the world on the start date, from an install.
///
/// So far that is the provinces - their names, their neighbours and who holds them.
/// The rules are in <c>docs/mechanics/map.md</c>, sections 2 and 7.
/// </summary>
public static class CGameStateLoader
{
	private const string DefinesFile = "common/defines.lua";
	private const string ProvinceHistoryDirectory = "history/provinces";

	private static readonly Regex StartDatePattern =
		new(@"\bstart_date\s*=\s*['""]([^'""]+)['""]", RegexOptions.Compiled);

	public static CGameState LoadStart(CDirectorySettings directories, CMap map,
		CLocalisation localisation, CCountries countries, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;

		CDate start = ReadStartDate(directories);

		var provinces = new CProvince[map.ProvinceCount];
		for (int id = 0; id < provinces.Length; id++)
		{
			provinces[id] = new CProvince(id, id == 0 ? "" : localisation.Localise($"PROV{id}"), map.IsSea(id))
			{
				Neighbours = map.Neighbours[id],
				Terrain = map.MajorityTerrain[id],
			};
		}

		ApplyProvinceHistory(directories, provinces, start, countries, map.Terrains, warn);
		return new CGameState(start, provinces, countries.All);
	}

	/// <summary>
	/// The start date, which the original keeps in a Lua table. Only the one line is
	/// wanted, so it is matched rather than run.
	/// </summary>
	public static CDate ReadStartDate(CDirectorySettings directories)
	{
		string path = directories.Resolve(DefinesFile);
		string text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
		Match match = StartDatePattern.Match(text);
		if (!match.Success || !CDate.TryParse(match.Groups[1].Value, out CDate date))
		{
			throw new InvalidDataException($"'{path}' has no readable start_date");
		}
		return date;
	}

	/// <summary>
	/// Folds every province's history to the start date. The province a file belongs to
	/// is the number its name starts with; the folder and the rest of the name are for
	/// people.
	/// </summary>
	public static void ApplyProvinceHistory(CDirectorySettings directories, CProvince[] provinces,
		CDate start, CCountries countries, IReadOnlyList<CTerrain> terrains, Action<string> warn)
	{
		var terrainByName = new Dictionary<string, CTerrain>(StringComparer.OrdinalIgnoreCase);
		foreach (CTerrain terrain in terrains)
		{
			terrainByName.TryAdd(terrain.Name, terrain);
		}
		var unknownTerrains = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		var filesRead = new int[provinces.Length];
		var unknownTags = new SortedSet<string>(StringComparer.Ordinal);
		int unreadable = 0;

		foreach (CDirectorySettings.LayeredFile file in
			directories.ListFilesRecursive(ProvinceHistoryDirectory, "*.txt"))
		{
			if (!TryReadLeadingId(Path.GetFileName(file.RelativePath), out int id)
				|| id <= 0 || id >= provinces.Length)
			{
				warn($"'{file.AbsolutePath}': the name does not start with a province id on this map");
				continue;
			}

			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file.AbsolutePath);
			}
			catch (IOException)
			{
				unreadable++;
				continue;
			}

			filesRead[id]++;
			Apply(provinces[id], CHistory.Fold(root, start), countries, unknownTags, terrainByName,
				unknownTerrains);
		}

		if (unreadable > 0)
		{
			warn($"{unreadable} province history files could not be read");
		}
		if (unknownTags.Count > 0)
		{
			warn($"province history names {unknownTags.Count} countries that are not defined: " +
				string.Join(", ", unknownTags));
		}

		if (unknownTerrains.Count > 0)
		{
			warn($"province history names {unknownTerrains.Count} terrains that are not defined: " +
				string.Join(", ", unknownTerrains));
		}

		// Two files for one province are read in path order and the later one's lines
		// land on top. What the original does with them has not been established.
		var doubled = new List<int>();
		for (int id = 1; id < filesRead.Length; id++)
		{
			if (filesRead[id] > 1)
			{
				doubled.Add(id);
			}
		}
		if (doubled.Count > 0)
		{
			warn($"{doubled.Count} provinces have more than one history file; the later file in path order " +
				$"was applied last. First few: {string.Join(", ", doubled.GetRange(0, Math.Min(8, doubled.Count)))}");
		}
	}

	private static void Apply(CProvince province, List<ClausewitzNode> entries, CCountries countries,
		SortedSet<string> unknownTags, Dictionary<string, CTerrain> terrainByName,
		SortedSet<string> unknownTerrains)
	{
		foreach (ClausewitzNode entry in entries)
		{
			if (entry.Key is null || entry.Value is null)
			{
				continue;
			}

			// The file's word on terrain replaces what the bitmap's majority said.
			if (entry.Key.Equals("terrain", StringComparison.OrdinalIgnoreCase))
			{
				if (terrainByName.TryGetValue(entry.Value, out CTerrain? terrain))
				{
					province.Terrain = terrain;
				}
				else
				{
					unknownTerrains.Add(entry.Value);
				}
				continue;
			}

			// Keys are matched without regard to case, as the files themselves are not
			// consistent about it.
			bool isOwner = entry.Key.Equals("owner", StringComparison.OrdinalIgnoreCase);
			bool isController = entry.Key.Equals("controller", StringComparison.OrdinalIgnoreCase);
			bool isAddCore = entry.Key.Equals("add_core", StringComparison.OrdinalIgnoreCase);
			bool isRemoveCore = entry.Key.Equals("remove_core", StringComparison.OrdinalIgnoreCase);
			if (!isOwner && !isController && !isAddCore && !isRemoveCore)
			{
				continue;
			}

			if (!CCountryTag.TryParse(entry.Value, out CCountryTag tag))
			{
				unknownTags.Add(entry.Value);
				continue;
			}
			if (countries.Find(tag) is null)
			{
				unknownTags.Add(tag.Value);
			}

			if (isOwner)
			{
				province.Owner = tag;
			}
			else if (isController)
			{
				province.Controller = tag;
			}
			else if (isAddCore)
			{
				if (!province.Cores.Contains(tag))
				{
					province.Cores.Add(tag);
				}
			}
			else
			{
				province.Cores.Remove(tag);
			}
		}
	}

	private static bool TryReadLeadingId(string fileName, out int id)
	{
		id = 0;
		int i = 0;
		while (i < fileName.Length && char.IsWhiteSpace(fileName[i]))
		{
			i++;
		}
		int start = i;
		while (i < fileName.Length && char.IsAsciiDigit(fileName[i]) && i - start < 9)
		{
			id = id * 10 + (fileName[i] - '0');
			i++;
		}
		return i > start;
	}
}
