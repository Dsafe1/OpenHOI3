using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content;

/// <summary>
/// Builds the one start: the state of the world on the start date, from an install.
///
/// So far that is the provinces - their names, their neighbours, who holds them and
/// what is built in them - the countries' laws, governments and technology, the wars
/// being fought, and what each country holds. The rules are in
/// <c>docs/mechanics/map.md</c>, sections 2 and 7, <c>docs/mechanics/ic.md</c>,
/// section 1, and <c>docs/mechanics/stockpile.md</c>, section 2.
/// </summary>
public static class CGameStateLoader
{
	private const string DefinesFile = "common/defines.lua";
	private const string ProvinceHistoryDirectory = "history/provinces";

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
				Continent = map.Continents[id],
				Terrain = map.MajorityTerrain[id],
			};
		}

		List<CTechnologyCategory> categories = CTechnologyReader.ReadCategories(directories, warn);
		List<CTechnology> technologies = CTechnologyReader.Read(directories, warn, categories);
		Dictionary<string, CGoodsPool> goodsGiven =
			CCountryHistory.Apply(directories, countries, start, warn, technologies, categories);

		CBuilding[] buildings = CBuildingReader.Read(directories, warn);
		ApplyProvinceHistory(directories, provinces, start, countries, map.Terrains, buildings, warn);
		List<CModifier> staticModifiers = CModifierReader.ReadStaticModifiers(directories, warn);
		List<CModifier> strategicResources = CModifierReader.ReadStrategicResources(directories, warn);
		CDefines defines = CDefinesReader.Read(directories, warn);
		List<CWar> wars = CWarReader.Read(directories, countries, start, warn);

		var state = new CGameState(start, provinces, countries.Declared, buildings, technologies, staticModifiers,
			defines, wars, strategicResources, categories)
		{
			TechnologyFolders = CTechnologyReader.ReadFolders(directories),
		};
		ReportUnknownStrategicResources(state, warn);
		CStockpile.FillAtStart(state, goodsGiven);
		CProduction.SetStartingShares(state);
		CLeadership.SetAtStart(state);
		CManpower.SetAtStart(state);
		return state;
	}

	/// <summary>
	/// A province naming a strategic resource nothing declares is left with none. The
	/// original would say nothing about it.
	/// </summary>
	private static void ReportUnknownStrategicResources(CGameState state, Action<string> warn)
	{
		var unknown = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (CProvince province in state.Provinces)
		{
			if (province.StrategicResource.Length != 0
				&& state.FindStrategicResource(province.StrategicResource) is null)
			{
				unknown.Add(province.StrategicResource);
				province.StrategicResource = "";
			}
		}
		if (unknown.Count > 0)
		{
			warn($"province history names {unknown.Count} strategic resources that are not defined: " +
				string.Join(", ", unknown));
		}
	}

	/// <summary>The start date, which the original keeps with its defines.</summary>
	public static CDate ReadStartDate(CDirectorySettings directories)
	{
		string path = directories.Resolve(DefinesFile);
		if (!CDefinesReader.ReadText(path).TryGetValue(("", "start_date"), out string? text)
			|| !CDate.TryParse(text, out CDate date))
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
		CDate start, CCountries countries, IReadOnlyList<CTerrain> terrains,
		IReadOnlyList<CBuilding> buildings, Action<string> warn)
	{
		var buildingByKey = new Dictionary<string, CBuilding>(StringComparer.OrdinalIgnoreCase);
		foreach (CBuilding building in buildings)
		{
			buildingByKey.TryAdd(building.Key, building);
		}
		int badLevels = 0;
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
			badLevels += Apply(provinces[id], CHistory.Fold(root, start), countries, unknownTags,
				terrainByName, unknownTerrains, buildingByKey);
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

		if (badLevels > 0)
		{
			warn($"{badLevels} building levels and amounts in province history are not numbers and were ignored");
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

	/// <summary>Applies one province's folded history and returns how many building levels were unreadable.</summary>
	private static int Apply(CProvince province, List<ClausewitzNode> entries, CCountries countries,
		SortedSet<string> unknownTags, Dictionary<string, CTerrain> terrainByName,
		SortedSet<string> unknownTerrains, Dictionary<string, CBuilding> buildingByKey)
	{
		int badLevels = 0;
		foreach (ClausewitzNode entry in entries)
		{
			if (entry.Key is null || entry.Value is null)
			{
				continue;
			}

			// What the province yields. It starts producing all it can.
			if (CGoodsPool.TryParseKey(entry.Key, out EGoods goods))
			{
				if (CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
				{
					province.MaxProducing[goods] = amount;
					province.CurrentProducing[goods] = amount;
				}
				else
				{
					badLevels++;
				}
				continue;
			}
			if (entry.Key.Equals("manpower", StringComparison.OrdinalIgnoreCase)
				|| entry.Key.Equals("leadership", StringComparison.OrdinalIgnoreCase))
			{
				if (CFixedPoint.TryParse(entry.Value, out CFixedPoint amount))
				{
					if (entry.Key.Equals("manpower", StringComparison.OrdinalIgnoreCase))
					{
						province.Manpower = amount;
					}
					else
					{
						province.Leadership = amount;
					}
				}
				else
				{
					badLevels++;
				}
				continue;
			}
			if (entry.Key.Equals("strategic_resource", StringComparison.OrdinalIgnoreCase))
			{
				province.StrategicResource = entry.Value;        // checked against the list once all are read
				continue;
			}
			if (entry.Key.Equals("points", StringComparison.OrdinalIgnoreCase))
			{
				if (int.TryParse(entry.Value, out int points))
				{
					province.Points = points;
				}
				continue;
			}

			// A building's key sets its level here. A later line replaces an earlier one.
			if (buildingByKey.TryGetValue(entry.Key, out CBuilding? building))
			{
				if (CFixedPoint.TryParse(entry.Value, out CFixedPoint level))
				{
					province.SetBuildingLevel(building, level);
				}
				else
				{
					badLevels++;
				}
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
		return badLevels;
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
