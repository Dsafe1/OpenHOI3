using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// One country, as far as the front end needs it: what it is called, what colour it
/// flies, and where its definition lives.
/// </summary>
public sealed class CCountry
{
	public required CCountryTag Tag { get; init; }

	/// <summary>Localised name; falls back to the tag when the string is missing.</summary>
	public required string Name { get; init; }

	/// <summary>Game-relative path from <c>common/countries.txt</c>.</summary>
	public required string DefinitionFile { get; init; }

	public CColor Color { get; init; } = CColor.Gray;

	public string GraphicalCulture { get; init; } = "";

	/// <summary>The id of the capital province, or 0 for a country that has none.</summary>
	public int Capital { get; set; }

	/// <summary>Whether its own file calls it a major power.</summary>
	public bool IsMajor { get; init; }

	/// <summary>The faction it is in, by the faction's name, or nothing for a country in none.</summary>
	public string Faction { get; set; } = "";

	/// <summary>
	/// The country it is a puppet of, or null for a country that is nobody's. A puppet
	/// and its master are on one side for supply: see <c>docs/mechanics/supply.md</c>,
	/// section 3.
	/// </summary>
	public CCountryTag? Overlord { get; set; }

	/// <summary>
	/// How much of each strategic resource it has the use of, by the resource's name:
	/// 1 for a whole one. Within a game it only ever rises. Written by
	/// <see cref="CStrategicResources"/>.
	/// </summary>
	public Dictionary<string, CFixedPoint> StrategicResources { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The level it has of each technology, by the technology's name. Absent means none.</summary>
	public Dictionary<string, int> TechnologyLevels { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// What its technologies are worth in all: each one's effect times its level. Kept
	/// apart from <see cref="Modifier"/>, as the original keeps it, because the two are
	/// applied differently. Rebuilt with it.
	/// </summary>
	public CModifier Technology { get; } = new();

	/// <summary>What it produces at home in a day. Written by <see cref="CResources"/>.</summary>
	public CGoodsPool HomeProduced { get; } = new();

	/// <summary>The law in force from each group, by the group's name.</summary>
	public Dictionary<string, CLaw> Laws { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Everyone the country's file declares, by the number it gives them.</summary>
	public Dictionary<int, CMinister> Ministers { get; } = new();

	/// <summary>Who holds each position, by the position's name.</summary>
	public Dictionary<string, CMinister> Government { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The sum of everything that currently applies to the country. Rebuilt, never
	/// adjusted: see <see cref="CCountryModifiers"/>.
	/// </summary>
	public CModifier Modifier { get; } = new();

	/// <summary>
	/// What <see cref="Modifier"/> was summed from, in the order each was added: what the
	/// original's tooltips list as where a figure comes from. Rebuilt with it.
	/// </summary>
	public List<CModifierSource> ModifierSources { get; } = new();

	/// <summary>
	/// The IC of its provinces, in whole IC, before any of the country's own bonuses.
	/// Written by <see cref="CIndustrialCapacity"/>.
	/// </summary>
	public int BaseIc { get; set; }

	/// <summary>
	/// The IC it has to spend, in whole IC. At least 1 as calculated, though a day's
	/// shortage of resources can cut it to nothing.
	/// </summary>
	public int TotalIc { get; set; }

	/// <summary>
	/// The IC it would have with resources enough: <see cref="TotalIc"/> as calculated,
	/// before a day's shortage cuts it. Written by <see cref="CIndustrialCapacity"/>.
	/// </summary>
	public int MaxIc { get; set; }

	/// <summary>
	/// The IC of its own ground at home, in thousandths: the provinces it owns, holds
	/// and has a core on that are in its capital's area. A day's supplies and fuel are
	/// made in those provinces by their share of this. Written by
	/// <see cref="CIndustrialCapacity"/>.
	/// </summary>
	public long CoreHomeIc { get; set; }

	/// <summary>
	/// The fraction of its IC that goes to each of the six things IC is spent on, by
	/// <see cref="EProductionCategory"/>. Changed only by a command.
	/// </summary>
	public CFixed15[] ProductionShares { get; } = new CFixed15[CProduction.CategoryCount];

	/// <summary>The resources its industry used today. Cleared and counted again each day.</summary>
	public CGoodsPool Usage { get; } = new();

	/// <summary>What turning one good into another used today: energy, and crude oil.</summary>
	public CGoodsPool ConversionUsed { get; } = new();

	/// <summary>And what it made: crude oil, and fuel.</summary>
	public CGoodsPool ConversionMade { get; } = new();

	/// <summary>How unhappy its people are. Never below nothing.</summary>
	public CFixedPoint Dissent { get; set; }

	/// <summary>
	/// How far its people will go before giving up, from 1 to 100. Read from its history,
	/// and 1 for a country whose history gives none; nothing moves it yet.
	/// </summary>
	public CFixedPoint NationalUnity { get; set; } = CFixedPoint.One;

	/// <summary>The men it has to put in uniform. Never below nothing.</summary>
	public CFixedPoint Manpower { get; set; }

	/// <summary>What a month adds to its manpower. Written by <see cref="CManpower"/>.</summary>
	public CFixedPoint MonthlyManpower { get; set; }

	/// <summary>
	/// So many years of what it gets, which is what a new game starts it with. At least 1.
	/// The pool is not held to it. Written by <see cref="CManpower"/>.
	/// </summary>
	public CFixedPoint MaxManpower { get; set; }

	/// <summary>
	/// The leadership it has to divide each day. At least 1. Written by
	/// <see cref="CLeadership"/>.
	/// </summary>
	public CFixedPoint TotalLeadership { get; set; }

	/// <summary>
	/// The fraction of its leadership that goes to each of the four things it is spent
	/// on, by <see cref="ELeadershipCategory"/>. Changed only by a command.
	/// </summary>
	public CFixed15[] LeadershipShares { get; } = new CFixed15[CLeadership.CategoryCount];

	/// <summary>The officers it has. Never below nothing.</summary>
	public CFixedPoint Officers { get; set; }

	/// <summary>
	/// What its history gives as <c>officers_ratio</c>, or nothing. Read once, when a
	/// new game counts the officers its units come with.
	/// </summary>
	public CFixedPoint OfficersRatio { get; set; }

	/// <summary>
	/// The officers its brigades, ships and wings call for between them. Counted again
	/// every hour by <see cref="COfficers"/>.
	/// </summary>
	public CFixedPoint OfficersNeeded { get; set; }

	/// <summary>
	/// How many of the officers its units call for it has, where 1 is all of them: the
	/// figure the top bar shows, and one of the things a brigade's organisation is held
	/// to. Never above the define's most, and 1 for a country with no units. Written by
	/// <see cref="COfficers"/>; not to be confused with what its history gives, above.
	/// </summary>
	public CFixedPoint OfficerRatio { get; set; } = CFixedPoint.One;

	/// <summary>
	/// The order of battle its history names, relative to <c>history/units</c>, or an
	/// empty string for a country that starts with nothing.
	/// </summary>
	public string OrderOfBattleFile { get; set; } = "";

	/// <summary>
	/// Whether its reserves have been called up. A country at war as a game starts is.
	/// </summary>
	public bool IsMobilised { get; set; }

	/// <summary>
	/// The units nothing of its own commands, in the order its order of battle gives
	/// them. Everything else it has is under one of these.
	/// </summary>
	public List<CUnit> Units { get; } = new();

	/// <summary>
	/// The historical models its files spell out, by unit type and then by number: the
	/// level of each technology the model lists. A model no file gives is worked out by
	/// <see cref="CHistoricalModels"/>.
	/// </summary>
	public Dictionary<string, SortedDictionary<int, Dictionary<string, int>>> HistoricalModels { get; } =
		new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The divisions it keeps ready to order, as its file's <c>default_templates</c>
	/// gives them and as the player has changed them since.
	/// </summary>
	public List<CDivisionTemplate> Templates { get; } = new();

	/// <summary>
	/// The names its file gives new units, by unit type: a division's are under the type
	/// of its first brigade, a ship's and a wing's under their own.
	/// </summary>
	public Dictionary<string, List<string>> UnitNames { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// For trying things out: the country may order any unit type that can be built at
	/// all, whatever technology would have to unlock it and whichever countries its file
	/// keeps it to. Nothing in a game sets it; <c>CUnlockEveryUnitCommand</c> does.
	/// </summary>
	public bool EveryUnitUnlocked { get; set; }

	/// <summary>Where it has got to in each of those lists: the place of the name it last took.</summary>
	public Dictionary<string, int> UnitNameCursors { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Its leaders, in service or not, in the order they were read. See
	/// <c>docs/mechanics/leaders.md</c>, section 1.
	/// </summary>
	public List<CLeader> Leaders { get; } = new();

	/// <summary>
	/// The leaders it has put in service, in the order it did so: those its history gives
	/// a rank by the start, then each as his day comes. A leader is chosen for a unit out
	/// of these, the first of equals winning.
	/// </summary>
	public List<CLeader> ActiveLeaders { get; } = new();

	/// <summary>
	/// Whether a unit it places, joins or makes a headquarters of is given a leader of
	/// its own choosing. The original starts every country with it on.
	/// </summary>
	public bool AutoAssignLeaders { get; set; } = true;

	/// <summary>Every unit it has, at every level, each before the units under it.</summary>
	public IEnumerable<CUnit> AllUnits()
	{
		foreach (CUnit top in Units)
		{
			foreach (CUnit unit in top.WithAllBelow())
			{
				yield return unit;
			}
		}
	}

	/// <summary>
	/// Every unit it has in the order the original keeps them, which is the order they
	/// were made in and so the order of their numbers. Reinforcement and upgrades work
	/// through them this way, and it decides who goes without when a share runs out.
	/// </summary>
	public List<CUnit> UnitsInOrder()
	{
		var units = new List<CUnit>(AllUnits());
		units.Sort((a, b) => a.Id.CompareTo(b.Id));
		return units;
	}

	/// <summary>
	/// What it is building, in the order the production share works through it: the
	/// first line is paid for first. Changed only by a command, and by a unit being
	/// finished. See <c>docs/mechanics/buildqueue.md</c>.
	/// </summary>
	public List<CConstruction> Constructions { get; } = new();

	/// <summary>
	/// The units it has finished and not yet put anywhere. They are its units in that
	/// they eat and call for officers, and not in that nothing reinforces or upgrades
	/// them and they are in no province.
	/// </summary>
	public List<CUnit> Deployments { get; } = new();

	/// <summary>The IC a full day's work on everything in its queue takes: the production share's need.</summary>
	public CFixedPoint ProductionCost { get; set; }

	/// <summary>The IC its production share spent today.</summary>
	public CFixedPoint ProductionSpent { get; set; }

	/// <summary>
	/// What the units drawing on its ground asked for today, supplies and fuel, whoever
	/// they belong to: a unit is counted by the country that holds the province it draws
	/// in. Cleared and counted again each day.
	/// </summary>
	public CGoodsPool UnitDemand { get; } = new();

	/// <summary>
	/// The supplies and fuel that left its stockpile for units today: what the units
	/// standing in its capital were given, what the provinces beside the capital took
	/// out of it, and what went to a depot across the sea.
	/// </summary>
	public CGoodsPool UnitDrawn { get; } = new();

	/// <summary>
	/// The supplies and fuel it made today in provinces other than its capital. They are
	/// part of what it produced and are not in its stockpile until they have travelled there.
	/// </summary>
	public CGoodsPool SentTo { get; } = new();

	/// <summary>What came back into its stockpile today from provinces that held more than they needed.</summary>
	public CGoodsPool SentBack { get; } = new();

	/// <summary>
	/// What it paid today for its units drawing on a friend's depot: it comes out of its
	/// stockpile and goes into the friend's. See <c>docs/mechanics/supply.md</c>, section 13.
	/// </summary>
	public CGoodsPool TradedAway { get; } = new();

	/// <summary>What it was paid today for a friend's units drawing on its depots.</summary>
	public CGoodsPool TradedFor { get; } = new();

	/// <summary>
	/// Its convoys, in the order they were made - which is the order its transports are
	/// shared out in. See <c>docs/mechanics/convoys.md</c>.
	/// </summary>
	public List<CConvoy> Convoys { get; } = new();

	/// <summary>The transports it has that are on no convoy.</summary>
	public int Transports { get; set; }

	/// <summary>The escorts it has that are with no convoy.</summary>
	public int Escorts { get; set; }

	/// <summary>The transports its running convoys would have between them, as last shared out.</summary>
	public int TransportsNeeded { get; set; }

	/// <summary>
	/// Whether its transports and escorts are shared out among its convoys for it each
	/// day, whether its supply convoys are made and unmade for it, and whether its
	/// resource convoys are. A new game has all three on for every country.
	/// </summary>
	public bool AutoMaintainConvoys { get; set; } = true;

	public bool AutoSupplyConvoys { get; set; } = true;

	public bool AutoResourceConvoys { get; set; } = true;

	/// <summary>What its convoys carried out of its stockpile today to land across the sea.</summary>
	public CGoodsPool ConvoyedOut { get; } = new();

	/// <summary>What convoys landed today in harbours it holds and in its stockpile.</summary>
	public CGoodsPool ConvoyedIn { get; } = new();

	/// <summary>
	/// The IC a day of bringing its units back to strength costs: the reinforcement
	/// share's need. Worked out afresh each day by <see cref="CReinforcement"/>.
	/// </summary>
	public CFixedPoint ReinforcementCost { get; set; }

	/// <summary>The IC its reinforcement share spent today.</summary>
	public CFixedPoint ReinforcementSpent { get; set; }

	/// <summary>The manpower that would bring every brigade back to strength.</summary>
	public CFixedPoint ReinforcementManpowerNeeded { get; set; }

	/// <summary>The manpower reinforcement took today.</summary>
	public CFixedPoint ReinforcementManpowerUsed { get; set; }

	/// <summary>
	/// The IC a day of re-equipping its units costs: the upgrade share's need. Worked out
	/// afresh each day by <see cref="CUpgrades"/>.
	/// </summary>
	public CFixedPoint UpgradeCost { get; set; }

	/// <summary>The IC its upgrade share spent today.</summary>
	public CFixedPoint UpgradeSpent { get; set; }

	/// <summary>How many of its brigades, ships and wings are behind in something they can be brought up in.</summary>
	public int SubUnitsUpgrading { get; set; }

	/// <summary>What it has to spend on diplomacy. Never below nothing.</summary>
	public CFixedPoint DiplomaticInfluence { get; set; }

	/// <summary>Spies trained and not yet sent anywhere. A whole spy is 1.</summary>
	public CFixedPoint SpiesInTraining { get; set; }

	/// <summary>
	/// Its level in each technology category - its theories and practicals - by the
	/// category's name. Absent means none.
	/// </summary>
	public Dictionary<string, CFixedPoint> Abilities { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The technologies it is researching, in the order they are given leadership.
	/// Changed by a command, and by a level being finished.
	/// </summary>
	public List<string> ResearchQueue { get; } = new();

	/// <summary>How far each technology's next level has got, where 32768 is the whole level.</summary>
	public Dictionary<string, CFixed15> ResearchProgress { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The leadership each technology was given today. Cleared and filled again each day.</summary>
	public Dictionary<string, CFixedPoint> ResearchLeadership { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Its ability in a category, which is nothing for one it has never had any in.</summary>
	public CFixedPoint Ability(string category)
	{
		return Abilities.TryGetValue(category, out CFixedPoint level) ? level : CFixedPoint.Zero;
	}

	/// <summary>Game-relative path to the flag, which is named after the tag.</summary>
	public string FlagFile => $"gfx/flags/{Tag}.tga";

	public override string ToString()
	{
		return $"{Tag} ({Name})";
	}
}
