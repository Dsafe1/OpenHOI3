using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.History;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

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

	/// <param name="fill">
	/// Whether the provinces are filled along the supply network and the convoys made,
	/// as a game needs before its first hour. A check that runs the original's own
	/// sequence over a new game asks for one that has not been.
	/// </param>
	/// <param name="firstDays">
	/// Whether the two days the original runs before a game's first hour are run, the
	/// filling between them: what a game that is to be played starts from. Without, a
	/// game that is filled starts from what the history files say - which is what a check
	/// that reasons about those two days one system at a time wants.
	/// </param>
	/// <param name="aiScripts">
	/// The install's Lua, which a new game asks for its relations (<c>docs/mechanics/lua.md</c>); without
	/// it they are left at 0 and reported.
	/// </param>
	/// <param name="played">
	/// The countries people play, known before the game is made: the AI's first look at the laws leaves them alone
	/// (<c>docs/compatibility.md</c>, entry 6). They are put in <see cref="CGameState.Played"/>.
	/// </param>
	/// <param name="beforeFirstDays">
	/// Told the game just before its two days are run, for a tool that wants to follow them - a trace set. Changes nothing.
	/// </param>
	public static CGameState LoadStart(CDirectorySettings directories, CMap map,
		CLocalisation localisation, CCountries countries, Action<string>? warn = null, bool fill = true,
		bool firstDays = true, IAiScripts? aiScripts = null, IEnumerable<CCountryTag>? played = null,
		Action<CGameState>? beforeFirstDays = null)
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
				Span = map.Shapes[id].IsEmpty ? 0
					: map.Shapes[id].MaxX - map.Shapes[id].MinX + 1 + map.Shapes[id].MaxY - map.Shapes[id].MinY + 1,
				PositionX = map.Positions[id].X,
				PositionY = map.Positions[id].Y,
				PortSea = map.PortSea[id],
				SeaIndex = map.SeaIndex[id],
				// The weather's start: climate.bmp's index at the unit point, 0.150 of a degree a step from 100, and the
				// map terrain's own temperature (weather.md, section 1).
				BaseTemperature = new CFixedPoint((id < map.ClimateIndex.Count ? map.ClimateIndex[id] - 100 : -100) * 150L
					+ (map.Terrain[id]?.Temperature.Raw ?? 0)),
				IsNorthern = map.Positions[id].Y >= map.Height / 2,
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
		Dictionary<string, Dictionary<string, CLaw>> lawBook = CModifierReader.ReadLaws(directories, warn);
		List<COccupationPolicy> occupationPolicies = CModifierReader.ReadOccupationPolicies(directories, warn);
		Dictionary<string, CGoodsPool> goodsGiven = CCountryHistory.Apply(directories, countries, start, warn, technologies,
			categories, lawBook, occupationPolicies.Select(policy => policy.Name).ToHashSet());

		CDefines defines = CDefinesReader.Read(directories, warn);
		CBuilding[] buildings = CBuildingReader.Read(directories, warn);
		ApplyProvinceHistory(directories, provinces, start, countries, map.Terrains, buildings, warn,
			defines.Has(CNationalism.Section, CNationalism.Years) ? defines[CNationalism.Section, CNationalism.Years] : CFixedPoint.Zero);
		// One table by name, as the original looks both files' names up in one; the first of a name wins.
		List<CModifier> staticModifiers = CModifierReader.ReadStaticModifiers(directories, warn);
		staticModifiers.AddRange(CModifierReader.ReadEventModifiers(directories, warn));
		List<CModifier> strategicResources = CModifierReader.ReadStrategicResources(directories, warn);
		List<CWar> wars = CWarReader.Read(directories, countries, start, warn);
		CDiplomacyHistory diplomacy = CDiplomacyReader.Apply(directories, countries, start, warn);

		List<CTrait> traits = CTraitReader.Read(directories, warn);
		List<CIdeologyGroup> ideologyGroups = CIdeologyReader.Read(directories, warn);
		List<CGovernmentPosition> positions = CGovernmentReader.ReadPositions(directories, warn);
		var ideologyNames = new List<string>();
		foreach (CIdeologyGroup group in ideologyGroups)
		{
			ideologyNames.AddRange(group.IdeologyOrder);
		}
		var state = new CGameState(start, provinces, countries.Declared, buildings, technologies, staticModifiers,
			defines, wars, strategicResources, categories)
		{
			TechnologyFolders = CTechnologyReader.ReadFolders(directories),
			SubUnitTypes = unitTypes,
			CombinedArmsGroups = CCombinedArmsReader.Read(directories, warn),
			Terrains = map.Terrains,
			Tactics = CCombatTacticReader.Read(directories, warn),
			Traits = traits,
			GainableTraits = CGainableTraitReader.Read(directories, traits, warn),
			NameTexts = ReadNameTexts(localisation, unitTypes.Values),
			MapWidth = map.Width,
			MapHeight = map.Height,
			Regions = map.Regions,
			Continents = map.ContinentNames,
			IdeologyGroups = ideologyGroups,
			GovernmentPositions = positions,
			LawGroups = LawGroups(lawBook),
			OccupationPolicies = occupationPolicies,
			Governments = CGovernmentReader.Read(directories, ideologyNames, positions, warn),
			StartDate = start,
			EndDate = ReadEndDate(directories, warn),
		};
		AssignRegions(state);
		state.Weather.LowPressureZones = map.LowPressureZones;
		state.Weather.HighPressureZones = map.HighPressureZones;
		state.Alliances.AddRange(diplomacy.Alliances);
		state.Guarantees.AddRange(diplomacy.Guarantees);
		state.Naps.AddRange(diplomacy.Naps);
		state.AiScripts = aiScripts;
		CRelations.SetFromHistory(state, diplomacy.Relations, start);
		COccupationPolicies.SetFromHistory(state);
		ReportUnknownStrategicResources(state, warn);
		CStockpile.FillAtStart(state, goodsGiven);
		CProduction.SetStartingShares(state);
		CLeadership.SetAtStart(state);
		CManpower.SetAtStart(state);
		CLeaderReader.Read(directories, state, start, warn);

		// Events and decisions name nearly everything above, so they are read once it is there.
		CScriptProblems scriptProblems = CScriptLoader.Load(directories, state, warn);
		if (scriptProblems.Count > 0)
		{
			warn($"events and decisions: {scriptProblems.Count} problems, which `OpenHOI3.SaveCheck script` lists");
		}
		// The rebel types' words a faction and its armies are named in (rebels.md, sections 6 and 7).
		foreach (Sim.Revolts.CRebelType type in state.RebelTypes)
		{
			type.NameText = localisation.Localise(type.Name + "_name");
			type.ArmyText = localisation.Localise(type.Name + "_army");
			type.DescText = localisation.Localise(type.Name + "_desc");
		}
		// A new game's victory conditions: each faction's primaries (factions.md, section 8).
		foreach (string faction in Sim.Wars.CVictoryConditions.ChooseAtStart(state))
		{
			warn($"victory conditions: {faction} has fewer primaries than NUMBER_OF_VICTORY_CONDITIONS; the original fills the rest " +
				"by draws of a generator of its own, which is not built");
		}

		// The units come last, as they do in the original: a brigade is made by a country
		// that already has its laws, its technology and its modifiers. The leaders the files
		// name go on with them; the rest are chosen after the stockpile is set, as the
		// original does, so what the units use in it is reckoned without them.
		CHistoricalModelReader.Read(directories, countries, unitTypes, state.Technologies,
			CHistoricalModels.DefaultCount(state), warn);
		ReadUnits(directories, state, warn);
		CUnits.SetAtStart(state);
		CBuildQueue.SetUpAtStart(state);
		CStockpile.AddUnitGoodsAtStart(state);
		CLeaders.AssignAtStart(state);
		// The parties from the history, the pool and the empty posts, the elections' dates and the seats, as the
		// original sets a country up for a new game once its order of battle is read (politics.md, section 2).
		CPolitics.SetUpAtStart(state, warn);
		// Entering the game gives the relations nobody gave, asking the install's Lua for them.
		CDiplomacy.SetUpNewGame(state, warn);
		// And every country nobody plays has its laws looked at by the AI until they stop moving, and is given a
		// politics minister (politics.md, section 12). Ours: after the step above, whose effective neutrality the laws'
		// allow blocks read; where the original's look comes beside it was not read.
		if (played is not null)
		{
			state.Played.UnionWith(played);
		}
		if (CPoliticsAi.SetUpAtStart(state) == CPoliticsAi.MostLawPasses)
		{
			warn($"politics: the AI's laws were still moving after {CPoliticsAi.MostLawPasses} passes");
		}
		// And an intelligence minister (espionage.md, section 15; ours: its draw after the politics minister's).
		Sim.Espionage.CEspionageAi.SetUpAtStart(state);
		// And a foreign, a production and a tech minister, in that order (foreignminister.md and ministers.md, section 1;
		// ours: their draws after the intelligence minister's, where the original makes them before the politics minister).
		Sim.Ai.CForeignAi.SetUpAtStart(state);
		Sim.Research.CResearchAi.SetUpAtStart(state);
		// Entering the game makes every country's AI strategy, before its weather and its two days (aistrategy.md, section 1).
		Sim.Ai.CAiStrategies.MakeAtStart(state);
		// And the two days the original has behind it on the first screen are run, with
		// the provinces filled along the supply network between them, so that no army
		// starts hungry.
		if (fill && firstDays)
		{
			beforeFirstDays?.Invoke(state);
			CTick.RunFirstDays(state);
			// Then the game is entered, and every country is brought up to date with its wars: Italy and
			// Ethiopia are mobilised for thirty days, as the start save has them (war.md, section 4).
			Sim.Wars.CWarSides.BringAllUpToDate(state);
		}
		else if (fill)
		{
			CSupplyStart.FillNewGame(state);
		}
		// And the game runs from here.
		state.IsRunning = true;
		return state;
	}

	/// <summary>
	/// The law groups in the file's order, each its laws in theirs, every law numbered from 1 across the groups as the
	/// original's law database numbers them (politics.md, section 9).
	/// </summary>
	private static List<CLawGroup> LawGroups(Dictionary<string, Dictionary<string, CLaw>> lawBook)
	{
		var groups = new List<CLawGroup>();
		int index = 1;
		foreach ((string name, Dictionary<string, CLaw> laws) in lawBook)
		{
			var group = new CLawGroup(name, groups.Count + 1);
			foreach (CLaw law in laws.Values)
			{
				law.Index = index++;
				group.Laws.Add(law);
			}
			groups.Add(group);
		}
		return groups;
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
		foreach (CCountry country in state.Countries)
		{
			country.UnitNamesInUse.Clear();
			foreach (CUnit unit in country.AllUnits())
			{
				CUnitNames.Register(country, unit.Name);
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					CUnitNames.Register(country, subUnit.Name);
				}
			}
			foreach (CConstruction construction in country.Constructions)
			{
				CUnitNames.Register(country, construction.Name);
				foreach (CBrigadeOrder order in construction.Brigades)
				{
					CUnitNames.Register(country, order.Name);
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
			Theatre = localisation.Localise(CUnitNameTexts.TheatreKey),
			NamedTheatre = localisation.Localise(CUnitNameTexts.NamedTheatreKey),
			ArmyGroup = localisation.Localise(CUnitNameTexts.ArmyGroupKey),
			Army = localisation.Localise(CUnitNameTexts.ArmyKey),
			Corps = localisation.Localise(CUnitNameTexts.CorpsKey),
			Navy = localisation.Localise(CUnitNameTexts.NavyKey),
			Air = localisation.Localise(CUnitNameTexts.AirKey),
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
	/// The end date, which the original keeps beside the start. With none to read, the
	/// start date: nothing then runs past the day a game begins.
	/// </summary>
	public static CDate ReadEndDate(CDirectorySettings directories, Action<string> warn)
	{
		string path = directories.Resolve(DefinesFile);
		if (CDefinesReader.ReadText(path).TryGetValue(("", "end_date"), out string? text)
			&& CDate.TryParse(text, out CDate date))
		{
			return date;
		}
		warn($"'{path}' has no readable end_date");
		return ReadStartDate(directories);
	}

	/// <summary>
	/// Gives each province the regions that list it, in the map's order of regions - as
	/// the original's map does once it has read them.
	/// </summary>
	private static void AssignRegions(CGameState state)
	{
		for (int index = 0; index < state.Regions.Count; index++)
		{
			foreach (int id in state.Regions[index].Provinces)
			{
				state.Provinces[id].Regions.Add(index);
			}
		}
	}

	/// <summary>
	/// Folds every province's history to the start date. The province a file belongs to
	/// is the number its name starts with; the folder and the rest of the name are for
	/// people.
	/// </summary>
	/// <param name="nationalism">
	/// <c>YEARS_OF_NATIONALISM</c>: what an owner line gives a province that already has an owner, as a change of owner
	/// does (<c>docs/mechanics/occupation.md</c>, section 5).
	/// </param>
	public static void ApplyProvinceHistory(CDirectorySettings directories, CProvince[] provinces,
		CDate start, CCountries countries, IReadOnlyList<CTerrain> terrains,
		IReadOnlyList<CBuilding> buildings, Action<string> warn, CFixedPoint nationalism = default)
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
				terrainByName, unknownTerrains, buildingByKey, nationalism);
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
		SortedSet<string> unknownTerrains, Dictionary<string, CBuilding> buildingByKey, CFixedPoint nationalism)
	{
		int badLevels = 0;
		foreach (ClausewitzNode entry in entries)
		{
			// A revolt carried out as a new game is entered: its type and size; the name and the leader are read and
			// thrown away (rebels.md, section 8). No file of Their Finest Hour or BlackICE has one.
			if (entry.Key is not null && entry.IsBlock && entry.Key.Equals("revolt", StringComparison.OrdinalIgnoreCase))
			{
				foreach (ClausewitzNode part in entry.Children)
				{
					if (part.Key is null || part.Value is null)
					{
						continue;
					}
					if (part.Key.Equals("type", StringComparison.OrdinalIgnoreCase))
					{
						province.RevoltType = part.Value;
					}
					else if (part.Key.Equals("size", StringComparison.OrdinalIgnoreCase))
					{
						province.RevoltSize = int.TryParse(part.Value, out int size) ? size : 0;
					}
				}
				continue;
			}
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
			if (entry.Key.Equals("revolt_risk", StringComparison.OrdinalIgnoreCase))
			{
				// Sets the province's revolt risk, never below nought (revolt.md, section 5); no file of Their Finest
				// Hour's has one.
				if (CFixedPoint.TryParse(entry.Value, out CFixedPoint risk))
				{
					province.RevoltRisk = CFixedPoint.Max(risk, CFixedPoint.Zero);
				}
				else
				{
					badLevels++;
				}
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
				// An owner where there was one already is a change of owner, the same one included: it sets the
				// province's nationalism (occupation.md, section 5; inferred from the start save).
				if (province.Owner is { } before && countries.Find(before) is not null)
				{
					province.Nationalism = nationalism;
				}
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
