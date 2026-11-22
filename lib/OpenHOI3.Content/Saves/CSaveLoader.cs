using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Occupation;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// A game loaded from the original's <c>.hoi3</c> save: the world the install describes, then every figure the save
/// holds of it read over that, each one the save leaves out set to what it means - and what is not read yet cleared and
/// reported, never left as the history had it. Then what the original works out again after a load, and none of a new
/// game's two days. See <c>docs/mechanics/savegame.md</c>.
/// </summary>
public static class CSaveLoader
{
	/// <summary>The keys of a country block this reads, besides the law groups, the posts, the categories and the tags.</summary>
	private static readonly HashSet<string> CountryKeys = new(StringComparer.Ordinal)
	{
		"capital", "acting_capital", "technology", "distribution", "leadership", "manpower", "officers", "diplo_influence",
		"national_unity", "dissent", "war_exhaustion", "neutrality", "effective_neutrality", "highest_threat", "ideology", "flags", "variables",
		"auto_assign_leaders", "auto_maintain_convoy", "auto_supply_convoy", "auto_resource_convoy", "modifier",
		"government_in_exile", "strategic_warfare", "land_battles_fought", "air_battles_fought", "naval_battles_fought",
		"alignment", "nukes",
		// An enhanced save's own (CSaveConversions).
		CSaveConversions.EnergyKey, CSaveConversions.CrudeOilKey,
	};

	private static readonly HashSet<string> ProvinceKeys = new(StringComparer.Ordinal)
	{
		"owner", "controller", "core", "manpower", "leadership", "revolt_risk", "points", "nationalism", "pool",
		"current_producing", "max_producing", "weather", "flags", "strategic_resource", "drawn", "last_drawn", "throughput",
		"last_throughput", "modifier", "underground_owner", "underground_action", "last_convoy_attack", "out_of_supply_days",
		// Whether it is its controller's capital or beside it, which the writer works out again (supply.md, section 7).
		"capital", "history",
	};

	/// <param name="played">The country played; null, the one the save says was.</param>
	public static (CGameState State, CSaveReport Report) Load(CDirectorySettings directories, CMap map,
		CLocalisation localisation, CCountries countries, string path, CCountryTag? played = null,
		IAiScripts? aiScripts = null, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;
		ClausewitzNode root = ClausewitzParser.ParseFile(path);
		CGameState state = CGameStateLoader.BuildWorld(directories, map, localisation, countries, warn, aiScripts);
		var report = new CSaveReport();
		ClearUnread(state);
		// What `load_oob` reads a file with, as a new game is given it: without it the effect did nothing in a loaded game.
		state.LoadOrderOfBattle = (country, file) => History.COrderOfBattleReader.ReadInto(directories, state, country, file);

		foreach (ClausewitzNode node in root.All("battle_plan"))
		{
			state.LoadedBattlePlans.Add(BattlePlans.CBattlePlanFile.Read(node));
		}

		// The ids first: the history's counter and the highest minted in play, which every unit's number is made from.
		var ids = new CSaveIds();
		foreach (ClausewitzNode node in root.Children)
		{
			if (node.Key == "unit")
			{
				ids.SetHistoryNext((int)CSaveValues.Whole(node.Value));
			}
			else if (node.Key == "id" && node.IsBlock)
			{
				ids.SetHighest(node.GetInt("id"), node.GetInt("type"));
			}
		}
		state.LastUnitId = ids.Reserved;
		state.HistoryUnitNext = ids.HistoryNext;
		var units = new CSaveUnitReader(state, ids, report);
		var routes = new List<(Sim.Trade.CTradeRoute Route, int Convoy)>();
		ClausewitzNode? combats = null;
		bool automateTrade = false;

		foreach (ClausewitzNode node in root.Children)
		{
			if (node.Key is null)
			{
				continue;
			}
			if (int.TryParse(node.Key, out int id))
			{
				if (id > 0 && id < state.Provinces.Count && node.IsBlock)
				{
					ReadProvince(state, state.Province(id), node, report);
				}
				else
				{
					report.Unread("top", "a province that is none of the map's");
				}
				continue;
			}
			if (node.IsBlock && CCountryTag.TryParse(node.Key, out CCountryTag tag) && state.Country(tag) is { } country)
			{
				ReadCountry(state, country, node, report, ids, units);
				continue;
			}
			switch (node.Key)
			{
				case "date":
					if (CSaveValues.TryHours(node.Value, out long hours))
					{
						state.SetClock(hours);
					}
					break;
				case "player":
					report.Player = node.Value?.Trim('"') ?? "";
					break;
				// The game's generator goes on where the save left it, where the original throws both away
				// (docs/compatibility.md, entry 15): seeded, then moved on by the draws the save counts.
				case "seed":
					state.Seed = unchecked((uint)CSaveValues.Whole(node.Value));
					break;
				case "count":
					state.Random.Skip(CSaveValues.Whole(node.Value));
					break;
				// The history's counter and the highest id were read first.
				case "unit":
				case "id":
					break;
				case "rebel_faction" when node.IsBlock:
					CSaveRebelFactions.Read(state, node, ids, report);
					break;
				case "faction":
					ReadFactions(state, node);
					break;
				// The wars and pacts (savegame.md, section 6); a route's convoy is found once every country is read.
				case "diplomacy" when node.IsBlock:
					routes.AddRange(CSaveDiplomacy.Read(state, node, report));
					break;
				case "active_war" when node.IsBlock:
					state.Wars.Add(CSaveWars.Read(state, node, report, localisation.Localise));
					break;
				case "undeclared_war" when node.IsBlock:
					var undeclared = new Sim.Wars.CUndeclaredWar();
					foreach (ClausewitzNode entry in node.Children)
					{
						switch (entry.Key)
						{
							case "attacker":
								undeclared.Attackers.Add(new CCountryTag(entry.Value ?? ""));
								break;
							case "defender":
								undeclared.Defenders.Add(new CCountryTag(entry.Value ?? ""));
								break;
							case "region":
								undeclared.Regions.Add(entry.Value ?? "");
								break;
						}
					}
					state.UndeclaredWars.Add(undeclared);
					break;
				case "previous_war" when node.IsBlock:
					state.FinishedWars.Add(CSaveWars.Read(state, node, report, localisation.Localise));
					break;
				// The next convoy's number.
				case "convoy" when !node.IsBlock:
					state.LastConvoyId = Math.Max(state.LastConvoyId, (int)CSaveValues.Whole(node.Value) - 1);
					break;
				// Every ship that has left the game, each filed under its owner's tag (savegame.md, section 7).
				case "sunk_ships" when node.IsBlock:
					ReadSunkShips(state, node, units, report);
					break;
				// The battles are read once every unit is, since their sides name units.
				case "combat" when node.IsBlock:
					combats = node;
					break;
				// The local player's, given to whoever is played once that is known (savegame.md, section 14).
				case "automate_trade":
					automateTrade = node.Value == "yes";
					break;
				default:
					// The counters, the game's flags, the victory conditions, the fired events and the weather's fronts.
					if (!CSaveTop.Read(state, node, report))
					{
						report.Unread("top", node.Key);
					}
					break;
			}
		}

		units.Finish();
		Sim.Units.CBrigadeUpgrades.RegisterLines(state);
		state.LastUnitId = Math.Max(state.LastUnitId, ids.Highest);
		// The rebels' armies and brigades, numbered on their counter, keep that number for the save (rebels.md, section 6).
		foreach (((int serial, int type), int id) in ids.Others)
		{
			if (type == CSaveRebelFactions.RebelIdType)
			{
				state.RebelSerials[id] = serial;
			}
		}
		var convoys =state.Countries.SelectMany(country => country.Convoys).ToDictionary(convoy => convoy.Id);
		foreach ((Sim.Trade.CTradeRoute route, int convoy) in routes)
		{
			route.Convoy = convoys.GetValueOrDefault(convoy);
			state.TradeRoutes.Add(route);
		}
		if (combats is not null)
		{
			CSaveCombats.Read(state, combats, units.ById, ids, report);
		}
		// Entering the game, a unit in a battle starts its count towards breaking off again from nought.
		foreach (Sim.Combat.CCombat combat in state.Combats)
		{
			foreach (Sim.Units.CUnit unit in combat.Attacker.Units.Concat(combat.Defender.Units))
			{
				unit.Disengage = 0;
				unit.DisengageTo = 0;
			}
		}

		CCountryTag? player = played ?? (CCountryTag.TryParse(report.Player, out CCountryTag saved) ? saved : null);
		if (player is { } who)
		{
			state.Played.Add(who);
			if (automateTrade)
			{
				state.AutomateTrade.Add(who);
			}
		}
		// The historical models are the install's, for what is built from now on; a save's brigades never take theirs.
		CHistoricalModelReader.Read(directories, countries, state.SubUnitTypes, state.Technologies,
			Sim.Units.CHistoricalModels.DefaultCount(state), warn);
		// Entering the game brings every country up to date with its wars, a loaded one as a new one: each pair at war
		// stamps last_war the hour now, so a truce follows any peace for a hundred days (war.md, section 4). Before the
		// game runs, so no strategy is made again by it: entering makes them all. A country mobilised at peace stays so, the
		// original passing loaded_from_save to keep it (reversing/findings/FINDINGS-war.md, A.9).
		Sim.Wars.CWarSides.BringAllUpToDate(state, loaded: true);
		SetUpLoadedGame(state);
		foreach (string line in report.Lines())
		{
			warn($"save '{System.IO.Path.GetFileName(path)}': not loaded yet - {line}");
		}
		// Entering a loaded game gives every province a depot, none being saved, and counts its days out of supply once more
		// (supply.md, section 18).
		Sim.Supply.COutOfSupplyDays.Enter(state);
		Sim.Wars.CSurrender.CountVictoryPoints(state);
		// The original has run its AI passes by the time a loaded game stands paused at its hour: two of them, every agent
		// made before the first (measured in the running game, reversing/findings/FINDINGS-liveread.md, section 12). Entering
		// the game runs them here; a load marks them as being no first pass.
		state.LoadedFromSave = true;
		foreach (CCountry country in state.Countries)
		{
			// A loaded game is no first pass and waits for no theatre: FINDINGS-agents.md, the pass, step 5.
			country.UnitAi.Waiting = false;
			country.UnitAi.UnitPassDone = true;
			country.Strategy.TheatresDirty = false;
		}
		return (state, report);
	}

	/// <summary>
	/// What the world holds from the history that a save gives: cleared, so that what the save gives is all there is -
	/// and what it gives that is not read yet is nothing, never answered from the history in a loaded game
	/// (<c>CLAUDE.md</c>'s rule on what is not built).
	/// </summary>
	private static void ClearUnread(CGameState state)
	{
		foreach (Sim.Leaders.CLeader leader in state.Leaders.Values)
		{
			leader.Unit = null;
		}
		foreach (CCountry country in state.Countries)
		{
			country.Units.Clear();
			country.Constructions.Clear();
			country.Deployments.Clear();
			country.BuildingDeployments.Clear();
			country.ActiveLeaders.Clear();
			country.Convoys.Clear();
			country.Faction = "";
			country.Overlord = null;
			country.PuppetEnd = null;
			country.EventModifiers.Clear();
			country.Mobilised = false;
		}
		state.Wars.Clear();
		state.FinishedWars.Clear();
		state.Alliances.Clear();
		state.Guarantees.Clear();
		state.Naps.Clear();
		state.Embargoes.Clear();
		state.Influences.Clear();
		state.Aligns.Clear();
		state.TradeRoutes.Clear();
		state.PactsMade = 0;
		state.LastConvoyId = 0;
		state.Combats.Clear();
		state.CombatHistory.Clear();
		state.SunkShips.Clear();
		state.Theatres.Clear();
		foreach (CProvince province in state.Provinces)
		{
			province.TheatreId = 0;
		}
		state.AutomateTrade.Clear();
		state.ClearFiredOnce();
		state.Weather.Fronts.Clear();
		state.GameFinished = false;
	}

	/// <summary>
	/// <c>sunk_ships</c>: each ship under its owner's tag, which is all that says whose it was - one under a tag of no
	/// country is kept with none.
	/// </summary>
	private static void ReadSunkShips(CGameState state, ClausewitzNode block, CSaveUnitReader units, CSaveReport report)
	{
		foreach (ClausewitzNode entry in block.Children)
		{
			if (entry.Key is null || !entry.IsBlock)
			{
				continue;
			}
			CCountryTag? owner = CCountryTag.TryParse(entry.Key, out CCountryTag tag) && state.Country(tag) is not null ? tag : null;
			if (units.ReadSunkShip(entry, state.Country(owner)) is { } ship)
			{
				Sim.Units.CSunkShips.Add(state, owner, ship);
			}
		}
	}

	private static void ReadProvince(CGameState state, CProvince province, ClausewitzNode node, CSaveReport report)
	{
		province.Owner = Tag(node.Get("owner")?.Value);
		province.Controller = Tag(node.Get("controller")?.Value);
		// Its place in its controller's list: a save keeps no such order, so a loaded game's lists are in the order of the
		// provinces' numbers, the same place for every province ('CProvince.ControlOrder'; ours, not read).
		province.ControlOrder = 0;
		// A place set without a change of hands: the controlled lists kept until one are made again.
		state.ControlledLists = null;
		// Its underground's country, and what an underground here does - build up strength where none is written, as the
		// original leaves that one out (undergrounds.md, section 6).
		province.UndergroundOwner = Tag(node.Get("underground_owner")?.Value);
		province.UndergroundAction = node.Get("underground_action")?.Value is { } action
			? (int)CSaveValues.Whole(action)
			: Sim.Revolts.CUndergrounds.BuildUp;
		// The hour a raid last fought a convoy in its water, written where it is a date (navalmissions.md, section 9).
		province.LastConvoyAttackHour = CSaveOrders.Hour(node.Get("last_convoy_attack")?.Value) ?? CProvince.NoConvoyAttack;
		province.Cores.Clear();
		foreach (ClausewitzNode core in node.All("core"))
		{
			if (Tag(core.Value) is { } tag)
			{
				province.Cores.Add(tag);
			}
		}
		province.Manpower = CSaveValues.Fixed(node, "manpower");
		province.Leadership = CSaveValues.Fixed(node, "leadership");
		province.RevoltRisk = CSaveValues.Fixed(node, "revolt_risk");
		province.Points = (int)CSaveValues.Whole(node, "points");
		province.Nationalism = CSaveValues.Fixed(node, "nationalism");
		province.StrategicResource = node.Get("strategic_resource")?.Value?.Trim('"') ?? "";
		ReadPool(province.Pool, node.Get("pool"));
		// What it held when the pass last came to it is not saved, and a loaded game starts with nothing there, as the
		// original's does: the first midnight's carrying hands nothing on from one province to the next, so supplies reach
		// the far end of a network a day after a load (savegame.md, section 4; FINDINGS-liveread.md, section 116).
		province.LastPool.Clear();
		ReadPool(province.CurrentProducing, node.Get("current_producing"));
		ReadPool(province.MaxProducing, node.Get("max_producing"));
		// What was asked of it and what passed through it, today and yesterday: the day's carrying works from yesterday's
		// (supply.md, section 12).
		ReadPool(province.SupplyDrawn, node.Get("drawn"));
		ReadPool(province.LastSupplyDrawn, node.Get("last_drawn"));
		ReadPool(province.SupplyThroughput, node.Get("throughput"));
		ReadPool(province.LastSupplyThroughput, node.Get("last_throughput"));
		ReadWeather(state, province.Weather, node.Get("weather"));
		province.OutOfSupplyDays = (int)CSaveValues.Whole(node, "out_of_supply_days");
		// The changes made to it in play, a dated block each - a country quoted, a figure not (savegame.md, section 4).
		province.History.Clear();
		foreach (ClausewitzNode dated in node.Get("history")?.Children ?? new List<ClausewitzNode>())
		{
			if (!dated.IsBlock || !CSaveValues.TryHours(dated.Key, out long hour))
			{
				report.Unread("province", "a history entry with no date");
				continue;
			}
			foreach (ClausewitzNode change in dated.Children)
			{
				if (change.Key is not { } key || change.Value is not { } value)
				{
					continue;
				}
				province.History.Add(change.IsQuoted
					? new Sim.Provinces.CProvinceChange(hour, key, new CCountryTag(value.Trim('"')), CFixedPoint.Zero)
					: new Sim.Provinces.CProvinceChange(hour, key, null, CSaveValues.Fixed(value)));
			}
		}
		ReadFlags(province.Flags, node.Get("flags"));
		ReadEventModifiers(state, province.EventModifiers, node, report);
		// Every building: the level in working order first, what was built second; none written is none built.
		var buildings = new HashSet<string>(StringComparer.Ordinal);
		foreach (CBuilding building in state.Buildings)
		{
			buildings.Add(building.Key);
			ClausewitzNode? pair = node.Get(building.Key);
			bool written = pair is { IsBlock: true, Children.Count: >= 2 };
			province.SetBuildingMaxLevel(building, written ? CSaveValues.Fixed(pair!.Children[1].Value) : CFixedPoint.Zero);
			province.SetBuildingCurrentLevel(building, written ? CSaveValues.Fixed(pair!.Children[0].Value) : CFixedPoint.Zero);
		}
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is { } key && !ProvinceKeys.Contains(key) && !buildings.Contains(key))
			{
				report.Unread("province", key);
			}
		}
	}

	private static void ReadCountry(CGameState state, CCountry country, ClausewitzNode node, CSaveReport report,
		CSaveIds ids, CSaveUnitReader units)
	{
		country.Capital = (int)CSaveValues.Whole(node, "capital");
		// None written, or nought, is a country with no acting capital of its own, which follows its capital - as one that
		// has never had one does in a running game.
		int acting = (int)CSaveValues.Whole(node, "acting_capital");
		if (acting > 0)
		{
			country.ActingCapital = acting;
		}
		else
		{
			country.FollowCapital();
		}

		var laws = new HashSet<string>(StringComparer.Ordinal);
		foreach (CLawGroup group in state.LawGroups)
		{
			laws.Add(group.Name);
			if (node.Get(group.Name)?.Value is { } name && group.Laws.FirstOrDefault(law => law.Name == name) is { } law)
			{
				country.Laws[group.Name] = law;
			}
		}
		// The posts: a minister of the country's by the id the save gives - or, as the history may seat another country's
		// (politics.md, section 2), the first country's of that id - none where it gives none.
		var posts = new HashSet<string>(StringComparer.Ordinal);
		country.Government.Clear();
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			posts.Add(position.Name);
			int id = (int)CSaveValues.Whole(node, position.Name);
			CMinister? minister = country.Ministers.GetValueOrDefault(id);
			if (minister is null && id > 0)
			{
				minister = state.Countries.Select(other => other.Ministers.GetValueOrDefault(id)).FirstOrDefault(found => found is not null);
			}
			if (minister is not null)
			{
				country.Government[position.Name] = minister;
			}
		}
		string ideology = node.Get("ideology")?.Value ?? "";
		country.RulingIdeology = ideology == "noIdeology" ? "" : ideology;

		// Technology: every entry its level and the progress towards the next.
		country.TechnologyLevels.Clear();
		country.ResearchProgress.Clear();
		country.NullTechnologyLevel = 0;
		foreach (ClausewitzNode entry in node.Get("technology")?.Children ?? new List<ClausewitzNode>())
		{
			// The original's built-in null technology, its level alone.
			if (entry.Key == CSaveWriter.NullTechnology && entry.IsBlock && entry.Children.Count >= 1)
			{
				country.NullTechnologyLevel = (int)CSaveValues.Whole(entry.Children[0].Value);
				continue;
			}
			// A technology the install does not have - the original skips it too - is reported.
			if (entry.Key is { } unknown && !state.Technologies.ContainsKey(unknown))
			{
				report.Unread("technology", unknown);
				continue;
			}
			if (entry.Key is not null && entry.IsBlock && entry.Children.Count >= 2)
			{
				country.TechnologyLevels[entry.Key] = (int)CSaveValues.Whole(entry.Children[0].Value);
				CFixed15 progress = CSaveValues.Fixed15(entry.Children[1].Value);
				if (progress != CFixed15.Zero)
				{
					country.ResearchProgress[entry.Key] = progress;
				}
			}
		}
		var categories = new HashSet<string>(StringComparer.Ordinal);
		country.Abilities.Clear();
		// The null category first, which a technology with no on_completion teaches (research.md, section 4).
		foreach (string category in state.TechnologyCategories.Select(category => category.Name).Prepend(Sim.Research.CResearch.NullCategory))
		{
			categories.Add(category);
			if (node.Get(category)?.Value is { } text)
			{
				country.Abilities[category] = CSaveValues.Fixed(text);
			}
		}
		ReadShares(country.ProductionShares, node.Get("distribution"));
		ReadShares(country.LeadershipShares, node.Get("leadership"));
		// The research queue and the politics (savegame.md, section 12).
		CSavePolitics.Read(state, country, node, report);
		// Its spies, in it and in every other country (savegame.md, section 13).
		CSaveEspionage.Read(state, country, node);
		// Whether it is in exile, its pools of goods, its battles fought, its strategic warfare, where it stands on the
		// triangle and its lend-lease (section 15); a count of battles is written only above nought.
		country.InExile = node.GetBool("government_in_exile");
		CSaveLedgers.Read(country, node);
		country.LandBattlesFought = (int)CSaveValues.Whole(node, "land_battles_fought");
		country.AirBattlesFought = (int)CSaveValues.Whole(node, "air_battles_fought");
		country.NavalBattlesFought = (int)CSaveValues.Whole(node, "naval_battles_fought");
		CSaveStrategicWarfare.Read(country, node.Get("strategic_warfare"));
		ClausewitzNode? triangle = node.Get("alignment")?.Get("position");
		country.AlignmentX = CSaveValues.Fixed(triangle, "x").Raw;
		country.AlignmentY = CSaveValues.Fixed(triangle, "y").Raw;
		CSaveLendLease.Read(state, country, node, report);
		// Its AI's strategy and theatres (section 16).
		CSaveAiStrategy.Read(state, country, node, ids, report);

		country.Manpower = CSaveValues.Fixed(node, "manpower");
		country.Nukes = CSaveValues.Fixed(node, "nukes");
		country.Officers = CSaveValues.Fixed(node, "officers");
		country.DiplomaticInfluence = CSaveValues.Fixed(node, "diplo_influence");
		country.NationalUnity = CSaveValues.Fixed(node, "national_unity");
		country.Dissent = CSaveValues.Fixed(node, "dissent");
		country.WarExhaustion = CSaveValues.Fixed(node, "war_exhaustion");
		country.Neutrality = CSaveValues.Fixed(node, "neutrality");
		country.EffectiveNeutrality = CSaveValues.Fixed(node, "effective_neutrality");
		// The country it fears most, which its effective neutrality is worked out against again; "---" none.
		country.MostThreatening = Tag(node.Get("highest_threat")?.Value);
		country.AutoAssignLeaders = node.GetBool("auto_assign_leaders", true);
		country.AutoMaintainConvoys = node.GetBool("auto_maintain_convoy", true);
		country.AutoSupplyConvoys = node.GetBool("auto_supply_convoy", true);
		country.AutoResourceConvoys = node.GetBool("auto_resource_convoy", true);
		ReadFlags(country.Flags, node.Get("flags"));
		ReadEventModifiers(state, country.EventModifiers, node, report);
		country.Variables.Clear();
		foreach (ClausewitzNode variable in node.Get("variables")?.Children ?? new List<ClausewitzNode>())
		{
			if (variable.Key is { } name)
			{
				country.Variables[name] = CSaveValues.Fixed(variable.Value);
			}
		}
		CSaveConversions.Read(country, node);

		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key is not { } key)
			{
				continue;
			}
			// A block under another country's tag is what the two hold of each other.
			if (child.IsBlock && CCountryTag.TryParse(key, out CCountryTag otherTag) && state.Country(otherTag) is { } other)
			{
				CSaveStatus.Read(state, CGameState.Status(country, other), child, report);
				continue;
			}
			// An offer on its list, under its kind's key (section 19).
			if (child.IsBlock && CSaveOffers.IsOffer(key))
			{
				CSaveOffers.Read(state, country, child, ids, report);
				continue;
			}
			// The military part (savegame.md, section 5): the units at the top of its tree, the queue's lines in order,
			// what waits to be placed, the leaders in service and the templates.
			if (child.IsBlock && CSaveUnitReader.UnitKinds.TryGetValue(key, out Sim.Units.EUnitKind kind))
			{
				if (units.Read(child, kind, country, parent: null, carriedBy: null) is { } unit)
				{
					country.Units.Add(unit);
				}
				continue;
			}
			switch (key)
			{
				case "military_construction" when child.IsBlock:
					CSaveLines.ReadMilitary(state, country, child, ids, report);
					continue;
				case "building_construction" when child.IsBlock:
					CSaveLines.ReadBuilding(state, country, child, ids, report);
					continue;
				case "convoy_construction" when child.IsBlock:
					CSaveLines.ReadConvoy(state, country, child, ids, report);
					continue;
				case "unit_deployment" when child.IsBlock:
					CSaveLines.ReadUnitDeployment(country, child, ids, units, report);
					continue;
				case "building_deployment" when child.IsBlock:
					CSaveLines.ReadBuildingDeployment(state, country, child, ids, report);
					continue;
				case "active_leaders" when child.IsBlock:
					CSaveLeaders.Read(state, country, child, report);
					continue;
				case "saved_templates" when child.IsBlock:
					CSaveLines.ReadTemplates(country, child);
					continue;
				// Its convoys, and the transports and escorts in its pool (savegame.md, section 6).
				case "convoy" when child.IsBlock:
					CSaveConvoys.Read(state, country, child, report);
					continue;
				case "convoys":
					country.Transports = (int)CSaveValues.Whole(child.Value);
					continue;
				case "escorts":
					country.Escorts = (int)CSaveValues.Whole(child.Value);
					continue;
				// Written only while it is mobilised.
				case "mobilize":
					country.Mobilised = child.Value == "yes";
					continue;
				// The production screen's slider locks, the played country's alone written, any block's read (sliders.md, section 3).
				case "slider_locks" when child.IsBlock:
					country.SliderLocks = child.Children.Select(value => (int)CSaveValues.Whole(value.Value)).ToArray();
					continue;
				// Its historical friends, written only where it has any (aistrategy.md, section 9.2).
				case "historical_friends" when child.IsBlock:
					country.HistoricalFriends.AddRange(child.Children.Select(tag => new CCountryTag(tag.Value)));
					continue;
			}
			if (!CountryKeys.Contains(key) && !laws.Contains(key) && !posts.Contains(key) && !categories.Contains(key)
				&& !CSavePolitics.Keys.Contains(key) && !CSaveEspionage.Keys.Contains(key) && !CSaveLedgers.Keys.Contains(key)
				&& !CSaveLendLease.Keys.Contains(key) && !CSaveAiStrategy.Keys.Contains(key))
			{
				report.Unread("country", key);
			}
		}
	}

	/// <summary>The top-level <c>faction = { axis = { progress = .. country = "GER" ... } }</c>: its progress, and each country named a member.</summary>
	private static void ReadFactions(CGameState state, ClausewitzNode node)
	{
		foreach (ClausewitzNode faction in node.Children)
		{
			if (faction.Key is null || !faction.IsBlock)
			{
				continue;
			}
			state.FactionProgress[faction.Key] = (int)CSaveValues.Fixed(faction, "progress").Raw;
			foreach (ClausewitzNode member in faction.All("country"))
			{
				if (Tag(member.Value) is { } tag && state.Country(tag) is { } country)
				{
					country.Faction = faction.Key;
				}
			}
		}
	}

	private static void ReadPool(CGoodsPool pool, ClausewitzNode? node)
	{
		foreach (EGoods goods in Enum.GetValues<EGoods>())
		{
			pool[goods] = CSaveValues.Fixed(node?.Get(CGoodsPool.KeyOf(goods))?.Value);
		}
	}

	private static void ReadShares(CFixed15[] shares, ClausewitzNode? node)
	{
		List<ClausewitzNode> values = node?.Children ?? new List<ClausewitzNode>();
		if (values.Count != shares.Length)
		{
			return;
		}
		for (int i = 0; i < shares.Length; i++)
		{
			shares[i] = CSaveValues.Fixed15(values[i].Value);
		}
	}

	/// <summary>A province's weather, each figure the save leaves out nought - and the pressure its define.</summary>
	private static void ReadWeather(CGameState state, CWeather weather, ClausewitzNode? node)
	{
		weather.Humidity = CSaveValues.Fixed(node, "humidity");
		weather.Pressure = node?.Get("pressure")?.Value is { } pressure ? CSaveValues.Fixed(pressure)
			: state.Defines[CProvinceWeather.Section, CProvinceWeather.PressureDefault];
		weather.WindSpeed = CSaveValues.Fixed(node, "windspeed");
		weather.Temperature = CSaveValues.Fixed(node, "temperature");
		weather.Precipitation = CSaveValues.Fixed(node, "precipitation");
		weather.Muddyness = CSaveValues.Fixed(node, "muddyness");
		weather.Frozen = CSaveValues.Fixed(node, "frozen");
		weather.CloudCoverage = CSaveValues.Fixed(node, "cloud_coverage");
	}

	/// <summary>A flags block: every flag written is set, as the original writes only those that are.</summary>
	private static void ReadFlags(Sim.Script.CFlags flags, ClausewitzNode? node)
	{
		foreach (ClausewitzNode flag in node?.Children ?? new List<ClausewitzNode>())
		{
			if (flag.Key is { } name && flag.Value == "yes")
			{
				flags.Set(name);
			}
		}
	}

	/// <summary>
	/// The modifiers events have given, each <c>modifier = { modifier = "name" date = "the hour it lasts to" }</c>, in the
	/// order given. A name that is no static modifier is reported.
	/// </summary>
	private static void ReadEventModifiers(CGameState state, List<CTimedModifier> into, ClausewitzNode node, CSaveReport report)
	{
		into.Clear();
		foreach (ClausewitzNode entry in node.All("modifier"))
		{
			string name = entry.Get("modifier")?.Value ?? "";
			if (state.FindStaticModifier(name) is not { } modifier || !CSaveValues.TryHours(entry.Get("date")?.Value, out long until))
			{
				report.Unread("modifier", $"a modifier of no name known, {name}");
				continue;
			}
			into.Add(new CTimedModifier(modifier, until));
		}
	}

	private static CCountryTag? Tag(string? text)
	{
		return CCountryTag.TryParse(text?.Trim('"'), out CCountryTag tag) ? tag : null;
	}

	/// <summary>
	/// What the original works out again after a load, before the first hour (<c>savegame.md</c>, section 2): the
	/// modifiers, resources, IC, manpower and leadership, neutrality, and a fresh AI for every country - and none of a
	/// new game's two days, the filling or the first hour.
	/// </summary>
	private static void SetUpLoadedGame(CGameState state)
	{
		// Each unit's leader, as the save names him, of whatever country, rank or kind - as the files' are put on.
		foreach (CCountry country in state.Countries)
		{
			foreach (Sim.Units.CUnit unit in country.UnitsInOrder())
			{
				if (unit.LeaderId != 0 && state.Leaders.TryGetValue(unit.LeaderId, out Sim.Leaders.CLeader? leader))
				{
					Sim.Leaders.CLeaders.SetLeader(state, unit, leader, false, false);
				}
				// What an intercept watches is made from its provinces, and not saved.
				if (unit.Order == Sim.Units.EUnitOrder.AirIntercept)
				{
					Sim.Missions.CAirIntercept.BuildWatch(state, unit);
				}
			}
			Sim.Construction.CBuildQueue.UpdateCost(country);
			// The seats, from the popularity the save gave (savegame.md, section 12).
			CCabinet.Allot(state, country);
		}
		// Each fleet's base as the original's unit loader leaves it, a save's as a file's (CFleetBase).
		CGameStateLoader.CheckFleetBases(state, state.Countries);
		CGameStateLoader.RegisterUnitNames(state);
		state.ForgetPositions();
		CProvinceModifiers.Rebuild(state);
		CCountryModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);
		// Ours: the save keeps no IC, so a country owning no province keeps the figure worked out here, without its
		// ministers, as the IC a new game's set-up gives one that never had any (ic.md, section 3).
		foreach (CCountry country in state.Countries)
		{
			country.IcWorkedOut = true;
		}
		CStrategicResources.Update(state);
		CCountryModifiers.Rebuild(state);
		// What a triggered modifier's `lost_national` asks: worked out before they are tested, or a country that has lost its
		// land would go without its modifier until the next midnight (seen in the running game, desperate_defence).
		Sim.Wars.CSurrender.CountVictoryPoints(state);
		// And the war record and lost_IC, which no save keeps: entering counts them again (triggers.md, section 5.1).
		Sim.Countries.CWarRecord.CountDaily(state);
		CCountryModifiers.RebuildDaily(state);
		CManpower.Update(state);
		CLeadership.Update(state);
		// The officer ratio, which every brigade's ceiling of organisation is made with: before it, the first hour would
		// hold every brigade to its type's own.
		Sim.Units.COfficers.Update(state);
		foreach (CCountry country in state.Countries)
		{
			CNeutrality.UpdateEffective(state, country);
		}
		// Every province's season and night for the saved hour, as entering a loaded game makes them (weather.md, section 4).
		Sim.Weather.CWeatherPass.AtLoad(state);
		// The game's screen made and entered gives a country that does not own its capital one it owns, as each day's head
		// does (war.md, section 14).
		Sim.Countries.CActingCapital.EnsureCapitals(state);
		// The countries the save's wars mobilised as they were read are a new game's set-up's alone, and empty after it.
		state.MobilisedByHistory.Clear();
		// A fresh AI for every country, as the original makes one: the ministers' first hours, in its order.
		Sim.Settings.CAutomation.MakeMinistersOfLoad(state);
		CForeignAi.TellOfWars(state);
		// The strategy the save gave is kept until the game is entered, which makes every one again; one never made is
		// made now (savegame.md, section 16).
		long madeHour = (long)state.StartDate.DayNumber * CGameState.HoursPerDay;
		foreach (CCountry country in state.Countries)
		{
			country.Strategy.MadeHour = madeHour;
			if (!country.Strategy.IsMade)
			{
				CAiStrategies.Make(state, country);
			}
		}
		// Entering takes every country owning no province and not in exile off its wars and out of its faction, a loaded game
		// as a new one (war.md, section 4; reversing/findings/FINDINGS-savegame.md, A.5).
		Sim.Wars.CWarSides.RemoveLandless(state);
		state.IsRunning = true;
	}
}
