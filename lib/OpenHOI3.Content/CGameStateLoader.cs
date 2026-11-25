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
	/// <param name="date">
	/// The hour the game begins at, a bookmark's - the defines' <c>start_date</c> where none is given
	/// (<c>docs/mechanics/bookmarks.md</c>).
	/// </param>
	/// <param name="seed">
	/// The game's seed, set before anything of the new game is drawn; the state's own, 1, where none is given - which the
	/// savegame tool's checks keep (<c>docs/mechanics/frontend.md</c>, *Ours*).
	/// </param>
	public static CGameState LoadStart(CDirectorySettings directories, CMap map,
		CLocalisation localisation, CCountries countries, Action<string>? warn = null, bool fill = true,
		bool firstDays = true, IAiScripts? aiScripts = null, IEnumerable<CCountryTag>? played = null,
		Action<CGameState>? beforeFirstDays = null, CDate? date = null, uint? seed = null)
	{
		warn ??= CDiagnostics.Problem;
		CGameState state = BuildWorld(directories, map, localisation, countries, warn, aiScripts, date);
		if (seed is { } given)
		{
			state.Seed = given;
		}
		IReadOnlyDictionary<string, CSubUnitType> unitTypes = state.SubUnitTypes;
		return SetUpNewGame(directories, state, countries, unitTypes, warn, fill, firstDays, played, beforeFirstDays);
	}

	/// <summary>
	/// The world an install describes, as far as a new game and a loaded one share it: the provinces with the map's
	/// figures, every database - units, technologies, laws, buildings, modifiers, traits, ideologies, posts - the defines,
	/// the leaders and ministers of the files, the events and decisions, and the start's history folded in. A new game goes
	/// on from it (<see cref="SetUpNewGame"/>); a loaded one fills its every dynamic figure from the save over it, as the
	/// original refills the objects its startup made (<c>docs/mechanics/savegame.md</c>, section 2).
	/// </summary>
	/// <param name="date">
	/// The hour the history is folded to and the game's clock set at: a bookmark's, or the defines' <c>start_date</c> where
	/// none is given. The defines' date stays <see cref="CGameState.StartDate"/> whatever the bookmark, as the original
	/// keeps it for the technology head start and the date widgets (<c>docs/mechanics/bookmarks.md</c>, section 2).
	/// <paramref name="countries"/> must be freshly read: the history is written onto them.
	/// </param>
	public static CGameState BuildWorld(CDirectorySettings directories, CMap map, CLocalisation localisation,
		CCountries countries, Action<string> warn, IAiScripts? aiScripts, CDate? date = null)
	{
		CDate start = ReadStartDate(directories);
		CDate at = date ?? start;

		var provinces = new CProvince[map.ProvinceCount];
		for (int id = 0; id < provinces.Length; id++)
		{
			provinces[id] = new CProvince(id, id == 0 ? "" : localisation.Localise($"PROV{id}"), map.IsSea(id))
			{
				Neighbours = map.Neighbours[id],
				Continent = map.Continents[id],
				Terrain = map.Terrain[id],
				Edges = map.Edges[id],
				// The centre the original measures distances from: province 0's at (1, 1) (alignment.md, section 5).
				CentreX = map.Shapes[id].GameCentre(id).X,
				CentreY = map.Shapes[id].GameCentre(id).Y,
				Span = map.Shapes[id].IsEmpty || id == 0 ? 0
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
		// Every declaration goes to the game state, which lists a name declared twice by its later; the history names the later.
		List<CTechnology> declarations = CTechnologyReader.Read(directories, warn, categories, unitTypes, terrainNames);
		List<CTechnology> technologies = declarations.Where(technology => !technology.IsShadowed).ToList();
		Dictionary<string, Dictionary<string, CLaw>> lawBook = CModifierReader.ReadLaws(directories, warn);
		List<COccupationPolicy> occupationPolicies = CModifierReader.ReadOccupationPolicies(directories, warn);
		var ministerTypes = new List<CMinisterType>();
		Dictionary<string, Dictionary<EGoods, CFixedPoint>> goodsGiven = CCountryHistory.Apply(directories, countries, at, warn, technologies,
			categories, lawBook, occupationPolicies.Select(policy => policy.Name).ToHashSet(), definesStart: start,
			ministerTypeOrder: ministerTypes);

		CDefines defines = CDefinesReader.Read(directories, warn);
		CBuilding[] buildings = CBuildingReader.Read(directories, warn);
		var captures = new List<(CCountryTag Taker, CCountryTag From)>();
		var resourceLines = new Dictionary<int, (string File, int Line)>();
		ApplyProvinceHistory(directories, provinces, at, countries, map.Terrains, buildings, warn,
			defines.Has(CNationalism.Section, CNationalism.Years) ? defines[CNationalism.Section, CNationalism.Years] : CFixedPoint.Zero,
			definesStart: start, captures: captures, resourceLines: resourceLines);
		// One table by name, as the original looks both files' names up in one; the first of a name wins, the name matched
		// case and all - so an event modifier differing from a static one only in case is its own (effects.md, section 6).
		List<CModifier> staticModifiers = CModifierReader.ReadStaticModifiers(directories, warn);
		staticModifiers.AddRange(CModifierReader.ReadEventModifiers(directories, warn));
		List<CModifier> strategicResources = CModifierReader.ReadStrategicResources(directories, warn);
		var finishedWars = new List<CWar>();
		var joins = new List<(CCountryTag Joiner, CCountryTag Enemy)>();
		List<CWar> wars = CWarReader.Read(directories, countries, at, warn, finishedWars, joins, definesStart: start);
		CDiplomacyHistory diplomacy = CDiplomacyReader.Apply(directories, countries, at, warn);

		List<CTrait> traits = CTraitReader.Read(directories, warn);
		List<CIdeologyGroup> ideologyGroups = CIdeologyReader.Read(directories, warn);
		List<CGovernmentPosition> positions = CGovernmentReader.ReadPositions(directories, warn);
		var ideologyNames = new List<string>();
		foreach (CIdeologyGroup group in ideologyGroups)
		{
			ideologyNames.AddRange(group.IdeologyOrder);
		}
		CUnitUpgradeReader.Read(directories, unitTypes, warn);
		var state = new CGameState(at, provinces, countries.Declared, buildings, declarations, staticModifiers,
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
			CoastlineRegions = map.CoastlineRegions,
			Continents = map.ContinentNames,
			IdeologyGroups = ideologyGroups,
			GovernmentPositions = positions,
			MinisterTypes = ministerTypes,
			LawGroups = LawGroups(lawBook),
			OccupationPolicies = occupationPolicies,
			Governments = CGovernmentReader.Read(directories, ideologyNames, positions, warn),
			StartDate = start,
			EndDate = ReadEndDate(directories, warn),
		};
		// The rules the game is played by: the enhanced mode's where a layer turns it on (enhanced.md, The shape).
		state.Rules.CopyFrom(Rules.CRulesReader.Read(directories, warn));
		AssignRegions(state);
		// A faction's members as the original's history passes reached them: those of the defines' start date in the
		// countries' order, then those the bookmark's pass made members, in the countries' order too (war.md, section 13).
		foreach (CCountry country in state.Countries)
		{
			if (country.HistoryJoinedLate && country.Faction.Length > 0)
			{
				country.FactionSeat = ++state.LastFactionSeat;
			}
		}
		state.Weather.LowPressureZones = map.LowPressureZones;
		state.Weather.HighPressureZones = map.HighPressureZones;
		state.Alliances.AddRange(diplomacy.Alliances);
		state.Guarantees.AddRange(diplomacy.Guarantees);
		state.Naps.AddRange(diplomacy.Naps);
		NumberHistoryPacts(state);
		state.FinishedWars.AddRange(finishedWars);
		state.AiScripts = aiScripts;
		CRelations.SetFromHistory(state, diplomacy.Relations, at);
		// The policies the history's changes of controller took, every province's before any country's history line, as
		// the original replays the provinces' history before the countries' (occupation.md, section 2). The policies' allow
		// blocks, which a capture asks of civil_war, are read for it ahead of the rest of the script; the script's own
		// reading below reads them again and reports what is wrong with them.
		COccupationPolicyAllowReader.Read(directories, state, new CScriptContext(CScriptNamesReader.Read(directories, state, _ => { })));
		foreach ((CCountryTag taker, CCountryTag from) in captures)
		{
			COccupationPolicies.Capture(state, taker, from);
		}
		COccupationPolicies.SetFromHistory(state);
		// What the history's wars left as they were replayed: who is mobilised, and the last_war of each who joined (war.md,
		// section 4).
		CWarSides.SetFromHistory(state, joins);
		ReportUnknownStrategicResources(state, warn, resourceLines);
		// Each country's going into exile or coming back that its history says, every line in turn, in the countries' order,
		// its government to its faction leader's seat (war.md, section 14). Ours: once the history stands, where the
		// original goes at the line's place in the history.
		foreach (CCountry country in state.Countries)
		{
			foreach (bool exile in country.HistoryExile)
			{
				if (exile)
				{
					CExile.Enter(state, country);
				}
				else
				{
					CExile.Leave(state, country);
				}
			}
		}
		// The goods the history sets, in the pool of the seat each government sits in - an exile's its host's, as the start
		// saves have London holding the exiles' (stockpile.md, section 2); the stockpile's days of IC are added as a new game
		// is set up (SetUpNewGame).
		Sim.Modifiers.CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);
		CStockpile.SetFromHistory(state, goodsGiven);
		CProduction.SetStartingShares(state);
		// What each country starts with of war exhaustion, by how long it has been at war or at peace (warexhaustion.md,
		// section 3).
		Sim.Wars.CWarExhaustion.SetAtStart(state);
		CLeadership.SetAtStart(state);
		CManpower.SetAtStart(state);
		CLeaderReader.Read(directories, state, at, warn);

		// Events and decisions name nearly everything above, so they are read once it is there.
		CScriptProblems scriptProblems = CScriptLoader.Load(directories, state, warn);
		// Each a line of its own at its file and line, its kind after it (docs/mechanics/logs.md, section 4).
		foreach (CScriptProblem problem in scriptProblems.All)
		{
			warn(problem.Report);
		}
		// The rebel types' words a faction and its armies are named in (rebels.md, sections 6 and 7).
		foreach (Sim.Revolts.CRebelType type in state.RebelTypes)
		{
			type.NameText = localisation.Localise(type.Name + "_name");
			type.ArmyText = localisation.Localise(type.Name + "_army");
			type.DescText = localisation.Localise(type.Name + "_desc");
		}
		return state;
	}

	/// <summary>
	/// A new game from a world <see cref="BuildWorld"/> made, as the lobby's Play makes one of the world it shows
	/// (<c>docs/mechanics/frontend.md</c>, *Ours*): the played countries, then the rest of a new game.
	/// </summary>
	/// <param name="chooseVictoryConditions">
	/// Whether the victory conditions are chosen: as a bookmark chosen in the lobby chooses them, and not for a Quick Start,
	/// which chooses none (<c>docs/mechanics/bookmarks.md</c>, section 4).
	/// </param>
	/// <param name="seed">The game's seed, set before anything of the new game is drawn; the world's own where none is given.</param>
	public static CGameState StartNewGame(CDirectorySettings directories, CGameState world, CCountries countries,
		IEnumerable<CCountryTag> played, Action<string>? warn = null, bool chooseVictoryConditions = true, uint? seed = null)
	{
		if (seed is { } given)
		{
			world.Seed = given;
		}
		return SetUpNewGame(directories, world, countries, world.SubUnitTypes, warn ?? CDiagnostics.Problem, fill: true,
			firstDays: true, played, beforeFirstDays: null, chooseVictoryConditions);
	}

	/// <summary>A new game from the world: the start's victory conditions, laws, technology, units, politics, AI and two days.</summary>
	private static CGameState SetUpNewGame(CDirectorySettings directories, CGameState state, CCountries countries,
		IReadOnlyDictionary<string, CSubUnitType> unitTypes, Action<string> warn, bool fill, bool firstDays,
		IEnumerable<CCountryTag>? played, Action<CGameState>? beforeFirstDays, bool chooseVictoryConditions = true)
	{
		// Making the game's screen first gives every country that does not own its history's capital a capital of its own,
		// as each day's head does - BlackICE's Shanxi, whose history names China's Beiping, gets Taiyuan; the government stays
		// where it was until the hour moves it (war.md, section 14).
		Sim.Countries.CActingCapital.EnsureCapitals(state);
		// A new game's victory conditions: each faction's primaries (factions.md, section 8).
		IEnumerable<string> shortOf = chooseVictoryConditions ? Sim.Wars.CVictoryConditions.ChooseAtStart(state) : Array.Empty<string>();
		foreach (string faction in shortOf)
		{
			warn($"victory conditions: {faction} has fewer primaries than NUMBER_OF_VICTORY_CONDITIONS; the rest are drawn, as the " +
				"original draws them, on a generator seeded from the game's seed where the original's is seeded from the clock");
		}

		// Entering a new game brings every country forward, the player's too, before its order of battle is read: its laws
		// to the latest each group allows, then a head start in technology bought with its core provinces' leadership
		// (politics.md, section 13; research.md, section 9). The levels' effects come with the modifier made again. The
		// original's entry has worked out every country's effective neutrality by then, which the laws' allow blocks read.
		foreach (CCountry country in state.Countries)
		{
			Sim.Diplomacy.CNeutrality.UpdateEffective(state, country);
			// And its triggered modifiers tested, as the original's entry tests them for each country before its new-game
			// set-up - so France's population crisis is in the most manpower it starts with (manpower.md, section 4).
			Sim.Modifiers.CTriggeredModifiers.Update(state, country);
		}
		// Each country's manpower law as the history left it, which the countries set up after it are weighed by when
		// the original works out its most in its turn (manpower.md, section 4).
		Dictionary<string, CFixedPoint> manpowerLawsBefore = CManpower.BeforeSetUp(state);
		// The original sets each country up in its turn (politics.md, section 13; diplomacy.md, section 5.3). Its most
		// threatening country is the lobby's, elected with nobody threatened - the first that may be, in the countries'
		// order. In a country's turn its IC is worked out - one whose turn has not come has none yet, so a law's
		// enemy_ic_ratio is false while any enemy comes later - and its effective neutrality, against the threat of the
		// countries set up before it; then its laws and technology; and its turn ends with the threat its history gives.
		// The parties' figures are the history's by then, which a law's popularity reads.
		Sim.Diplomacy.CThreat.UpdateMostThreatening(state);
		int[] worked = new int[state.Countries.Count];
		foreach (CCountry country in state.Countries)
		{
			CPolitics.SetPartiesFromHistory(state, country);
			worked[country.Index] = country.BaseIc;
			country.BaseIc = 0;
		}
		foreach (CCountry country in state.Countries)
		{
			country.BaseIc = worked[country.Index];
			// Brought up to date with its wars, as the entry does each country: mobilised where at war (war.md, section 4).
			Sim.Wars.CWarSides.BringUpToDate(state, country, allowDemobilise: true);
			Sim.Diplomacy.CNeutrality.UpdateEffective(state, country);
			Sim.Politics.CStartLaws.Move(state, country);
			Sim.Research.CStartTechnology.Buy(state, country);
			Sim.Diplomacy.CDiplomacy.SpreadHistoryThreat(state, country);
		}
		state.MobilisedByHistory.Clear();
		Sim.Modifiers.CCountryModifiers.Rebuild(state);

		// The units come last, as they do in the original: a brigade is made by a country
		// that already has its laws, its technology and its modifiers. The leaders the files
		// name go on with them; the rest are chosen after the stockpile is set, as the
		// original does, so what the units use in it is reckoned without them.
		CHistoricalModelReader.Read(directories, countries, unitTypes, state.Technologies,
			CHistoricalModels.DefaultCount(state), warn);
		ReadUnits(directories, state, warn);
		CUnits.SetAtStart(state);
		// Each country's set-up for a new game begins by working out its IC, before the effects of its history's
		// decisions: so the dissent the Soviet Union's purge gives it is not in the IC its shares are set by
		// (production.md, section 1). That figure stands until its first day - and for good for a country that owns no
		// province, under its history's ministers and without those the filling of empty posts gives (ic.md, section 3).
		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.UpdateAtSetUp(state);
		var icAtSetUp = new (int Total, int Max)[state.Countries.Count];
		foreach (CCountry country in state.Countries)
		{
			icAtSetUp[country.Index] = (country.TotalIc, country.IcBeforeCap);
		}
		// Then the effects of the decisions its history says it has taken (events.md, section 13), and its manpower
		// started at its most under the laws and technology brought forward and what those decisions gave (manpower.md,
		// section 4).
		foreach (string unknown in Sim.Events.CDecisions.TakeHistoryAtNewGame(state))
		{
			warn(unknown);
		}
		// Then the stockpile's days of base IC, once those decisions' effects are in it: money only where there is none,
		// so France, which the Popular Front has given 35 by 1939, is given none for its IC (stockpile.md, section 2).
		// And its diplomatic influence from the same base IC.
		CStockpile.FillAtStart(state);
		CLeadership.SetInfluenceAtStart(state);
		CManpower.SetAtStart(state, manpowerLawsBefore);
		CBuildQueue.SetUpAtStart(state);
		// The shares of IC a new game starts with, once the units are read and counted and before any empty post is
		// filled: so under the laws brought forward, with the standing army's term, and without the ministers the
		// filling gives (production.md, section 1) - and by the IC the set-up's head worked out.
		foreach (CCountry country in state.Countries)
		{
			(country.TotalIc, country.IcBeforeCap) = icAtSetUp[country.Index];
		}
		CProduction.SetStartingShares(state);
		// A country at a time: its order of battle's leaders, its stockpile's days of what its units use, and its leaders
		// chosen, so that a later country's file takes a leader off an earlier one's unit after its choosing (leaders.md,
		// section 5).
		CLeaders.AssignAtStart(state, country => CStockpile.AddUnitGoodsAtStart(state, country));
		// The parties from the history, the pool and the empty posts, the elections' dates and the seats, as the
		// original sets a country up for a new game once its order of battle is read (politics.md, section 2).
		CPolitics.SetUpAtStart(state, warn);
		// Entering the game gives the relations nobody gave, asking the install's Lua for them.
		CDiplomacy.SetUpNewGame(state, warn);
		// And every country has its laws looked at by the install's Lua, the player's too (entry 6 of
		// docs/compatibility.md, reversed 2026-11-02), what it posts held until the two days are run, and every country
		// nobody plays is given a politics minister (politics.md, section 12). Ours: after the step above, whose effective
		// neutrality the laws' allow blocks read.
		if (played is not null)
		{
			state.Played.UnionWith(played);
		}
		// Every country's five ministers are made first, as the original makes them before a game is entered: the
		// placeholder country's five draws, then each country's foreign, production, intelligence, tech and politics
		// minister in turn, the AI generator's first draws (ministers.md, section 1).
		Sim.Settings.CAutomation.MakeMinisters(state);
		CPoliticsAi.SetUpAtStart(state, draw: false);
		// And the foreign ministers are told of the wars being fought (foreignminister.md, section 1).
		Sim.Ai.CForeignAi.SetUpAtStart(state, draw: false);
		// Entering the game makes every country's AI strategy, before its weather and its two days (aistrategy.md, section 1).
		Sim.Ai.CAiStrategies.MakeAtStart(state);
		// And the two days the original has behind it on the first screen are run, with
		// the provinces filled along the supply network between them, so that no army
		// starts hungry. The changes of law the Lua posted above are run after the days, as the original's queue runs
		// them (politics.md, section 12).
		if (fill && firstDays)
		{
			beforeFirstDays?.Invoke(state);
			CTick.RunFirstDays(state);
		}
		else
		{
			// A game made without its days, for a check that reasons about them one system at a time: the changes made
			// at once, while it is still being made, so that nobody is held to a ceiling a running game would have.
			bool was = state.IsBeingMade;
			state.IsBeingMade = true;
			CPoliticsAi.RunNewGameLaws(state);
			state.IsBeingMade = was;
			if (fill)
			{
				CSupplyStart.FillNewGame(state);
			}
		}
		// And the game runs from here. What was made until now, its two days' too, is numbered as the history's and
		// what is made from here on as the player's: the original's start save has the one count up to here, type 41,
		// and nothing of its own minting (savegame.md, section 9).
		state.HistoryUnitNext = state.LastUnitId + 1;
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
			country.UnitNamesInUse.Clear();
			country.UnitNameCursors.Clear();
		}
		state.LoadOrderOfBattle = (country, file) => COrderOfBattleReader.ReadInto(directories, state, country, file);
		var unnamed = new List<(CCountry Country, CUnit Unit)>();
		// Only a country that owns a province, or the rebels, has its order of battle read, as the original's new game reads
		// it: at a later start Austria keeps its undated file and gets no units (bookmarks.md; FINDINGS-bookmarks.md, 2.4).
		COwnedCounts owned = COwnedCounts.Of(state);
		IEnumerable<CCountry> reading = state.Countries.Where(country =>
			country.Tag.Value == "REB" || owned.Count(country.Tag) > 0);
		state.LastUnitId = COrderOfBattleReader.Read(directories, reading, state.SubUnitTypes,
			state.Technologies, state.Provinces.Count, warn,
			(country, type) => CUnitNames.ForSubUnit(state, country, type), unnamed, state.Leaders, state);
		CheckFleetBases(state, reading);
		RegisterUnitNames(state);
		NameUnnamedUnits(state, unnamed);
	}

	/// <summary>
	/// Each fleet's base as the original's unit loader leaves it as the block closes: a base with no naval base dropped, and
	/// a fleet with none given the nearest of its country's (<see cref="CFleetBase"/>) - BlackICE's French fleet at
	/// Djibouti, whose province has none, gets Guadeloupe, the first of France's.
	/// </summary>
	public static void CheckFleetBases(CGameState state, IEnumerable<CCountry> countries)
	{
		foreach (CCountry country in countries)
		{
			COrderOfBattleReader.CheckFleets(state, country, country.Units);
		}
	}

	/// <summary>
	/// A new game's units the files leave unnamed, named once every file has been read - so a name anywhere in the
	/// country's file can take a number - in the order they joined the country's list, each the first free number of its
	/// kind's pattern, <c>$TYPE$</c> its first brigade's: a carrier's air group "1st Air wing". The original's walk over a
	/// country's units as a new game is entered (<c>docs/mechanics/units.md</c>, section 3).
	/// </summary>
	private static void NameUnnamedUnits(CGameState state, List<(CCountry Country, CUnit Unit)> unnamed)
	{
		foreach ((CCountry country, CUnit unit) in unnamed)
		{
			if (unit.Name.Length != 0)
			{
				continue;
			}
			unit.Name = unit.Kind == EUnitKind.Theatre
				? CUnitNames.ForTheatre(state, country, null)
				: CUnitNames.ForKind(state, country, unit.Kind, unit.SubUnits.Count > 0 ? unit.SubUnits[0].Type : null);
			CUnitNames.Register(country, unit.Name);
		}
	}

	/// <summary>
	/// Every name the orders of battle - or a save - gave is in use from the start, so that a unit
	/// ordered later is not called what one already is.
	/// </summary>
	public static void RegisterUnitNames(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.UnitNamesInUse.Clear();
			foreach (CUnit unit in country.AllUnits().Concat(country.Deployments))
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
	/// original would say nothing about it. Each is said at the history line that named it (logs.md, section 4).
	/// </summary>
	private static void ReportUnknownStrategicResources(CGameState state, Action<string> warn,
		IReadOnlyDictionary<int, (string File, int Line)> resourceLines)
	{
		foreach (CProvince province in state.Provinces)
		{
			if (province.StrategicResource.Length != 0
				&& state.FindStrategicResource(province.StrategicResource) is null)
			{
				(string file, int line) = resourceLines.TryGetValue(province.Id, out (string File, int Line) at) ? at : ("", 0);
				warn(CDiagnostics.Located(file, line, $"'{province.StrategicResource}' is a strategic resource that is not defined"));
				province.StrategicResource = "";
			}
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
		warn(CDiagnostics.Located(path, 0, "has no readable end_date"));
		return ReadStartDate(directories);
	}

	/// <summary>
	/// Gives each province the regions that list it, in the map's order of regions - as
	/// the original's map does once it has read them.
	/// </summary>
	/// <summary>
	/// The history's standing pacts numbered as the original makes them, a day of the history at a time: by their start,
	/// and within a start in the order they were read - the files' order, then each file's (<c>savegame.md</c>, section 10).
	/// </summary>
	public static void NumberHistoryPacts(CGameState state)
	{
		var pacts = new List<(long Start, long Read, Action<long> Number)>();
		for (int i = 0; i < state.Alliances.Count; i++)
		{
			int at = i;
			pacts.Add((state.Alliances[i].Start, state.Alliances[i].Made, made => state.Alliances[at] = state.Alliances[at] with { Made = made }));
		}
		for (int i = 0; i < state.Guarantees.Count; i++)
		{
			int at = i;
			pacts.Add((state.Guarantees[i].Start ?? 0, state.Guarantees[i].Made, made => state.Guarantees[at] = state.Guarantees[at] with { Made = made }));
		}
		for (int i = 0; i < state.Naps.Count; i++)
		{
			int at = i;
			pacts.Add((state.Naps[i].Start, state.Naps[i].Made, made => state.Naps[at] = state.Naps[at] with { Made = made }));
		}
		foreach (CCountry country in state.Countries.Where(country => country.Overlord is not null))
		{
			pacts.Add((country.PuppetStart, country.PuppetMade, made => country.PuppetMade = made));
		}
		foreach ((long _, long _, Action<long> number) in pacts.OrderBy(pact => pact.Start).ThenBy(pact => pact.Read))
		{
			number(state.NextPact());
		}
	}

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
	/// <param name="definesStart">The defines' <c>start_date</c>, which a bookmark's replay begins from; the date folded to where none.</param>
	/// <param name="captures">
	/// Given, every change of controller the history makes, in the order made: the new controller and the old, which the
	/// original's controller setter takes an occupation policy by (<c>docs/mechanics/occupation.md</c>, section 2).
	/// </param>
	public static void ApplyProvinceHistory(CDirectorySettings directories, CProvince[] provinces,
		CDate start, CCountries countries, IReadOnlyList<CTerrain> terrains,
		IReadOnlyList<CBuilding> buildings, Action<string> warn, CFixedPoint nationalism = default, CDate? definesStart = null,
		List<(CCountryTag Taker, CCountryTag From)>? captures = null,
		Dictionary<int, (string File, int Line)>? resourceLines = null)
	{
		var lastPass = new int[provinces.Length];
		var buildingByKey = new Dictionary<string, CBuilding>(StringComparer.OrdinalIgnoreCase);
		foreach (CBuilding building in buildings)
		{
			buildingByKey.TryAdd(building.Key, building);
		}
		// The building with the naval base's role, which the history never gives a province with no port (CBuildingRoles).
		CBuilding? navalBase = CBuildingRoles.Find(buildings, Sim.Movement.CMovementRules.NavalBase);
		var terrainByName = new Dictionary<string, CTerrain>(StringComparer.OrdinalIgnoreCase);
		foreach (CTerrain terrain in terrains)
		{
			terrainByName.TryAdd(terrain.Name, terrain);
		}
		var filesRead = new int[provinces.Length];
		// Each province's files in path order, for the report of a province given more than one.
		var filesOf = new Dictionary<int, List<string>>();
		// Each fault a line of its own at its place, once: the three passes never give one entry twice (logs.md, section 4).
		var said = new HashSet<string>(StringComparer.Ordinal);

		bool wordAfterKey = Rules.CRulesReader.Has(directories, Sim.Rules.EFix.KeyWithoutEquals);
		foreach (CDirectorySettings.LayeredFile file in
			directories.ListFilesRecursive(ProvinceHistoryDirectory, "*.txt"))
		{
			if (!TryReadLeadingId(Path.GetFileName(file.RelativePath), out int id)
				|| id <= 0 || id >= provinces.Length)
			{
				warn(CDiagnostics.Located(file.AbsolutePath, 0, "the name does not start with a province id on this map"));
				continue;
			}

			ClausewitzNode root;
			try
			{
				root = ClausewitzParser.ParseFile(file.AbsolutePath, wordAfterKey);
			}
			catch (IOException e)
			{
				warn(CDiagnostics.Located(file.AbsolutePath, 0, $"could not read the file: {e.Message}"));
				continue;
			}

			filesRead[id]++;
			if (!filesOf.TryGetValue(id, out List<string>? paths))
			{
				filesOf[id] = paths = new List<string>();
			}
			paths.Add(file.AbsolutePath);
			string path = file.AbsolutePath;
			void Report(int line, string message)
			{
				string located = CDiagnostics.Located(path, line, message);
				if (said.Add(located))
				{
					warn(located);
				}
			}
			// In the original's three passes - to year 2, to the defines' start date, to the bookmark's - each province's
			// controller taken from one country and put on the end of the other's list of the provinces it controls as the pass
			// reaches it, provinces in the order of their numbers (CProvince.ControlOrder; supply.md, section 2).
			CProvince province = provinces[id];
			CDate defines = definesStart ?? start;
			CDate second = defines.CompareTo(start) < 0 ? defines : start;
			var passes = new[]
			{
				CHistory.FoldBetween(root, null, CHistory.FirstPassEnd),
				CHistory.FoldBetween(root, CHistory.FirstPassEnd, second),
				CHistory.FoldBetween(root, second, start),
			};
			for (int pass = 0; pass < passes.Length; pass++)
			{
				bool changedBefore = province.ControllerChanged;
				province.ControllerChanged = false;
				Apply(province, passes[pass], countries, terrainByName, buildingByKey, nationalism, captures, navalBase, Report,
					resourceLines is null ? null : line => resourceLines[id] = (path, line));
				if (province.ControllerChanged)
				{
					lastPass[id] = pass + 1;
				}
				province.ControllerChanged |= changedBefore;
			}
		}
		for (int id = 1; id < provinces.Length; id++)
		{
			provinces[id].ControlOrder = (long)lastPass[id] * provinces.Length + id;
		}

		// Two files for one province are read in path order and the later one's lines
		// land on top. What the original does with them has not been established.
		for (int id = 1; id < filesRead.Length; id++)
		{
			if (filesRead[id] > 1)
			{
				List<string> paths = filesOf[id];
				for (int later = 1; later < paths.Count; later++)
				{
					warn(CDiagnostics.Located(paths[later], 0, $"province {id} has more than one history file; this one, " +
						$"later in path order than '{paths[later - 1]}', was applied last"));
				}
			}
		}
	}

	/// <summary>
	/// Applies one province's folded history, handing each fault to <paramref name="report"/> with its line, and each
	/// <c>strategic_resource</c> line's number to <paramref name="resourceLine"/>.
	/// </summary>
	private static void Apply(CProvince province, List<ClausewitzNode> entries, CCountries countries,
		Dictionary<string, CTerrain> terrainByName, Dictionary<string, CBuilding> buildingByKey, CFixedPoint nationalism,
		List<(CCountryTag Taker, CCountryTag From)>? captures, CBuilding? navalBase, Action<int, string> report,
		Action<int>? resourceLine)
	{
		void Bad(ClausewitzNode entry) => report(entry.Line, $"'{entry.Key} = {entry.Value}' is not a number and was ignored");
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
						report(part.Line, $"'revolt/{part.Key ?? part.Value ?? "{ }"}' is no key of a province's and no " +
							"building; passed over");
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
					else if (!part.Key.Equals("name", StringComparison.OrdinalIgnoreCase)
						&& !part.Key.Equals("leader", StringComparison.OrdinalIgnoreCase))
					{
						report(part.Line, $"'revolt/{part.Key}' is no key of a province's and no building; passed over");
					}
				}
				continue;
			}
			if (entry.Key is null)
			{
				// A bare word: the parser has said where it is (clausewitz.md, section 2).
				continue;
			}
			if (entry.Value is null)
			{
				report(entry.Line, $"'{entry.Key} = {{ }}' is no key of a province's and no building; passed over");
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
					Bad(entry);
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
					Bad(entry);
				}
				continue;
			}
			if (entry.Key.Equals("strategic_resource", StringComparison.OrdinalIgnoreCase))
			{
				province.StrategicResource = entry.Value;        // checked against the list once all are read
				resourceLine?.Invoke(entry.Line);
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
					Bad(entry);
				}
				continue;
			}
			if (entry.Key.Equals("points", StringComparison.OrdinalIgnoreCase))
			{
				if (int.TryParse(entry.Value, out int points))
				{
					province.Points = points;
				}
				else
				{
					Bad(entry);
				}
				continue;
			}

			// A building's key sets its level here. A later line replaces an earlier one. The naval base is never given to a
			// province with no port on the map, where a shipyard is (measured: BlackICE's Djibouti, 9867, and two more keep
			// none; buildinglines.md, section 1.1).
			if (buildingByKey.TryGetValue(entry.Key, out CBuilding? building))
			{
				if (CFixedPoint.TryParse(entry.Value, out CFixedPoint level))
				{
					if (!ReferenceEquals(building, navalBase) || province.PortSea > 0)
					{
						province.SetBuildingLevel(building, level);
					}
				}
				else
				{
					Bad(entry);
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
					report(entry.Line, $"'{entry.Value}' is a terrain that is not defined");
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
				// Said, where nothing was: the original takes any key it does not name for a building's (clausewitz.md, 3).
				report(entry.Line, $"'{entry.Key}' is no key of a province's and no building; passed over");
				continue;
			}

			if (!CCountryTag.TryParse(entry.Value, out CCountryTag tag))
			{
				report(entry.Line, $"'{entry.Value}' is a country that is not defined");
				continue;
			}
			if (countries.Find(tag) is null)
			{
				report(entry.Line, $"'{tag.Value}' is a country that is not defined");
			}

			if (isOwner)
			{
				// The original's change of owner: the new owner takes control first, then owns it; and where there was an
				// owner already, the same one included, the province's nationalism is set (occupation.md, section 5).
				CCountryTag? before = province.Owner;
				TakeControl(province, tag, countries, captures);
				province.Owner = tag;
				if (before is { } old && countries.Find(old) is not null)
				{
					province.Nationalism = nationalism;
				}
			}
			else if (isController)
			{
				TakeControl(province, tag, countries, captures);
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

	/// <summary>
	/// The history's change of controller, as the original's controller setter makes it with no consequences: nothing
	/// where the controller is the same; otherwise the change noted for the occupation policy it takes - the new
	/// controller's towards the old - and the controller set; and an unowned province given to a country but the rebels
	/// owned by it as well (<c>docs/mechanics/occupation.md</c>, section 2).
	/// </summary>
	private static void TakeControl(CProvince province, CCountryTag tag, CCountries countries,
		List<(CCountryTag Taker, CCountryTag From)>? captures)
	{
		if (province.Controller == tag)
		{
			return;
		}
		if (province.Controller is { } old)
		{
			captures?.Add((tag, old));
		}
		province.Controller = tag;
		if (province.Owner is null && tag.Value != "REB" && countries.Find(tag) is not null)
		{
			province.Owner = tag;
		}
		// The setter gives the province a depot and counts its days out of supply, and as the history is replayed it is
		// given none: one day for every change, the first included (supply.md, section 18).
		province.OutOfSupplyDays++;
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
