using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.History;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

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
				Terrain = map.Terrain[id],
				Edges = map.Edges[id],
				CentreX = map.Shapes[id].IsEmpty ? 0 : map.Shapes[id].CentreX,
				CentreY = map.Shapes[id].IsEmpty ? 0 : map.Shapes[id].CentreY,
				PositionX = map.Positions[id].X,
				PositionY = map.Positions[id].Y,
				PortSea = map.PortSea[id],
				SeaIndex = map.SeaIndex[id],
			};
		}

		var terrainNames = new List<string>();
		foreach (CTerrain terrain in map.Terrains)
		{
			terrainNames.Add(terrain.Name);
		}
		var unitTypes = new Dictionary<string, CSubUnitType>(StringComparer.OrdinalIgnoreCase);
		foreach (CSubUnitType type in CSubUnitTypeReader.Read(directories, terrainNames, warn))
		{
			unitTypes[type.Key] = type;
		}

		List<CTechnologyCategory> categories = CTechnologyReader.ReadCategories(directories, warn);
		List<CTechnology> technologies = CTechnologyReader.Read(directories, warn, categories, unitTypes, terrainNames);
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
			SubUnitTypes = unitTypes,
			CombinedArmsGroups = CCombinedArmsReader.Read(directories, warn),
			NameTexts = ReadNameTexts(localisation, unitTypes.Values),
			MapWidth = map.Width,
			MapHeight = map.Height,
		};
		ReportUnknownStrategicResources(state, warn);
		CStockpile.FillAtStart(state, goodsGiven);
		CProduction.SetStartingShares(state);
		CLeadership.SetAtStart(state);
		CManpower.SetAtStart(state);

		// The units come last, as they do in the original: a brigade is made by a country
		// that already has its laws, its technology and its modifiers.
		CHistoricalModelReader.Read(directories, countries, unitTypes, state.Technologies,
			CHistoricalModels.DefaultCount(state), warn);
		ReadUnits(directories, state, warn);
		CUnits.SetAtStart(state);
		CBuildQueue.SetUpAtStart(state);
		CStockpile.AddUnitGoodsAtStart(state);
		return state;
	}

	/// <summary>
	/// Gives every country the units its order of battle declares, and the ones it is
	/// building, as the file gives them and in place of any it had. What they then
	/// start with is <see cref="CUnits.SetAtStart"/> and
	/// <see cref="CBuildQueue.SetUpAtStart"/>, which depend on the country's laws - so
	/// a caller that changes those can read the units again and set them up afresh.
	/// </summary>
	public static void ReadUnits(CDirectorySettings directories, CGameState state, Action<string>? warn = null)
	{
		foreach (CCountry country in state.Countries)
		{
			country.Units.Clear();
			country.Constructions.Clear();
			country.Deployments.Clear();
		}
		state.LastUnitId = COrderOfBattleReader.Read(directories, state.Countries, state.SubUnitTypes,
			state.Technologies, state.Provinces.Count, warn);
		RegisterUnitNames(state);
	}

	/// <summary>
	/// Every name the orders of battle gave is in use from the start, so that a unit
	/// ordered later is not called what one already is.
	/// </summary>
	private static void RegisterUnitNames(CGameState state)
	{
		state.UnitNamesInUse.Clear();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				CUnitNames.Register(state, unit.Name);
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					CUnitNames.Register(state, subUnit.Name);
				}
			}
			foreach (CConstruction construction in country.Constructions)
			{
				CUnitNames.Register(state, construction.Name);
				foreach (CBrigadeOrder order in construction.Brigades)
				{
					CUnitNames.Register(state, order.Name);
				}
			}
		}
	}

	/// <summary>
	/// The words a new unit's name is made of once its country's own lists run out: the
	/// three patterns, the four ordinal endings, and every unit type's short name. A
	/// text the localisation has not got is its key, as it would be in the original.
	/// </summary>
	private static CUnitNameTexts ReadNameTexts(CLocalisation localisation, IEnumerable<CSubUnitType> types)
	{
		var ordinals = new string[CUnitNameTexts.OrdinalKeys.Length];
		for (int index = 0; index < ordinals.Length; index++)
		{
			ordinals[index] = localisation.Localise(CUnitNameTexts.OrdinalKeys[index]);
		}
		var texts = new CUnitNameTexts
		{
			Division = localisation.Localise(CUnitNameTexts.DivisionKey),
			Brigade = localisation.Localise(CUnitNameTexts.BrigadeKey),
			SubUnit = localisation.Localise(CUnitNameTexts.SubUnitKey),
			Ordinals = ordinals,
		};
		foreach (CSubUnitType type in types)
		{
			texts.ShortNames[type.Key] = localisation.Localise(type.Key + CUnitNameTexts.ShortSuffix);
		}
		return texts;
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
