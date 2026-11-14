using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Diplomacy;
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

	/// <summary>
	/// Localised adjective, <c>&lt;TAG&gt;_ADJ</c>, read as the game is made; the tag where the string is
	/// missing. A war's name is made from it (<c>war.md</c>, section 7).
	/// </summary>
	public string Adjective { get; init; } = "";

	/// <summary>Game-relative path from <c>common/countries.txt</c>.</summary>
	public required string DefinitionFile { get; init; }

	public CColor Color { get; set; } = CColor.Gray;

	/// <summary>The console's <c>aipause</c>: while set, none of this country's AI runs.</summary>
	public bool AiPaused { get; set; }

	/// <summary>What it was paid as tribute today (<c>docs/mechanics/stockpile.md</c>, section 7).</summary>
	public Economy.CGoodsPool TributeReceived { get; } = new();

	/// <summary>What it paid as tribute today, held below nought.</summary>
	public Economy.CGoodsPool TributeSent { get; } = new();

	/// <summary>The nuclear bombs it holds, in thousandths: a thousand is one (<c>docs/mechanics/nukes.md</c>).</summary>
	public CFixedPoint Nukes { get; set; }

	/// <summary>The units this country has lent, which it goes on reinforcing and officering.</summary>
	public List<CUnit> LentUnits { get; } = new();

	public string GraphicalCulture { get; init; } = "";

	/// <summary>
	/// The id of its declared capital province, or 0 for a country that has none. It moves only when the country
	/// stops owning it. See <c>docs/mechanics/war.md</c>, section 14.
	/// </summary>
	public int Capital { get; set; }

	/// <summary>
	/// Where its government sits, and so whose pool is its stockpile: its capital, until somebody else holds that,
	/// when the government moves at once. The declared capital until something sets it. See
	/// <see cref="CActingCapital"/>.
	/// </summary>
	public int ActingCapital
	{
		get => _actingCapital ?? Capital;
		set => _actingCapital = value;
	}

	private int? _actingCapital;

	/// <summary>Leaves it with no acting capital of its own, so that it follows its declared capital: a save that gives none.</summary>
	public void FollowCapital() => _actingCapital = null;

	/// <summary>The history's first capital, where a government whose capital fell goes back to once it holds it again.</summary>
	public int OriginalCapital { get; set; }

	/// <summary>Whether its own file calls it a major power.</summary>
	public bool IsMajor { get; init; }

	/// <summary>The faction it is in, by the faction's name, or nothing for a country in none.</summary>
	public string Faction { get; set; } = "";

	/// <summary>
	/// Where it stands among its faction's members, which are listed by this and then in the countries'
	/// order: nought for the history's members, more for each country to join since, less for one put at the
	/// front. The first member leads. See <see cref="CFactionMembership"/>.
	/// </summary>
	public long FactionSeat { get; set; }

	/// <summary>
	/// The country it is a puppet of, or null for a country that is nobody's. A puppet
	/// and its master are on one side for supply: see <c>docs/mechanics/supply.md</c>,
	/// section 3.
	/// </summary>
	public CCountryTag? Overlord { get; set; }

	/// <summary>The number of the pact that made it a puppet, among the pacts (<see cref="CGameState.NextPact"/>).</summary>
	public long PuppetMade { get; set; }

	/// <summary>The hour it became a puppet.</summary>
	public long PuppetStart { get; set; }

	/// <summary>
	/// The end the history or a save gave its being a puppet, which a save writes; none where it became one in play.
	/// Nothing ends it on that date here (<c>savegame.md</c>, section 10).
	/// </summary>
	public long? PuppetEnd { get; set; }

	/// <summary>Its place among the game state's countries, in the order they were declared.</summary>
	public int Index { get; internal set; }

	/// <summary>
	/// What it holds towards each country, by that country's <see cref="Index"/> - itself included.
	/// See <c>docs/mechanics/diplomacy.md</c>, section 1.
	/// </summary>
	public CDiplomacyStatus[] Diplomacy { get; internal set; } = Array.Empty<CDiplomacyStatus>();

	/// <summary>The form of government its history gives it - <c>national_socialism</c> - or nothing.</summary>
	public string GovernmentType { get; set; } = "";

	/// <summary>
	/// Its neutrality, held between 0 and 100 each day: 50 until its history sets one. See
	/// <c>docs/mechanics/diplomacy.md</c>, section 6.
	/// </summary>
	public CFixedPoint Neutrality { get; set; } = CFixedPoint.FromInt(50);

	/// <summary>Neutrality less the threat it feels from <see cref="MostThreatening"/>, between 0 and 100.</summary>
	public CFixedPoint EffectiveNeutrality { get; set; }

	/// <summary>The country it feels the most threat from, or null for nobody (section 5.4).</summary>
	public CCountryTag? MostThreatening { get; set; }

	/// <summary>The threat its history gives it, spread from its own capital as a new game is made.</summary>
	public CFixedPoint HistoryThreat { get; set; }

	/// <summary>
	/// The occupation policies its history sets, each by name towards a country, in the order written: put on its records
	/// once they exist (<c>docs/mechanics/occupation.md</c>, section 2).
	/// </summary>
	public List<(string Policy, CCountryTag Occupied)> HistoryOccupationPolicies { get; } = new();

	/// <summary>
	/// Where it stands on the alignment triangle, thousandths: its history's point, then where its
	/// drift takes it. See <c>docs/mechanics/alignment.md</c>.
	/// </summary>
	public long AlignmentX { get; set; }

	public long AlignmentY { get; set; }

	/// <summary>
	/// The hour after which its next drift step comes: none yet, so a new game's first day steps.
	/// Not saved in the original, so a load steps at once.
	/// </summary>
	public long NextAlignmentStep { get; set; } = long.MinValue;

	/// <summary>The ideology of its ruling party, by name - its history's <c>ideology</c> - or nothing.</summary>
	public string RulingIdeology { get; set; } = "";

	/// <summary>
	/// How much of each strategic resource it has the use of, by the resource's name:
	/// 1 for a whole one. Within a game it only ever rises. Written by
	/// <see cref="CStrategicResources"/>.
	/// </summary>
	public Dictionary<string, CFixedPoint> StrategicResources { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The level it has of each technology, by the technology's name. Absent means none.</summary>
	public Dictionary<string, int> TechnologyLevels { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The level of the original's built-in null technology, which no file declares and which does nothing: 1 where a new
	/// game's head start bought it, as a save keeps it (<c>docs/mechanics/research.md</c>, section 9).
	/// </summary>
	public int NullTechnologyLevel { get; set; }

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
	/// The ministers it may appoint, in the order they joined: those whose start date has come, in its file's
	/// order. Nothing ever takes one out but script. See <c>docs/mechanics/politics.md</c>, section 7.
	/// </summary>
	public List<CMinister> PossibleMinisters { get; } = new();

	/// <summary>Each party's popularity, by its ideology's number; made as a game is set up (politics.md, section 2).</summary>
	public Politics.CPartyFigures Popularity { get; set; } = new(0);

	/// <summary>Each party's organisation, the same way.</summary>
	public Politics.CPartyFigures Organisation { get; set; } = new(0);

	/// <summary>Each party's seats in the cabinet, by its ideology's number (politics.md, section 4).</summary>
	public int[] Cabinet { get; set; } = Array.Empty<int>();

	/// <summary>
	/// The cabinet a loaded save gave, kept behind <see cref="Cabinet"/> and written after it, as the original appends a
	/// save's seats to a cabinet already made; nothing reads it. Empty in a new game (savegame.md, section 12).
	/// </summary>
	public int[] CabinetLoaded { get; set; } = Array.Empty<int>();

	/// <summary>The history's party figures, by the ideology's name, until a game is set up with them.</summary>
	public Dictionary<string, CFixedPoint> HistoryPopularity { get; } = new(StringComparer.OrdinalIgnoreCase);

	public Dictionary<string, CFixedPoint> HistoryOrganisation { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>When it last held an election; <see cref="CDate.Zero"/> for never (politics.md, section 2).</summary>
	public CDate LastElection { get; set; } = CDate.Zero;

	/// <summary>Its own term in months, which overrides its government's where it is not 0.</summary>
	public int ElectionDuration { get; set; }

	/// <summary>What was done to it or for it at war that moves its national unity (politics.md, section 6).</summary>
	public Politics.CStrategicWarfare StrategicWarfare { get; } = new();

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
	/// The modifiers events and decisions have given it, each to its hour, in the order given:
	/// step 13 of its modifier. See <c>docs/mechanics/effects.md</c>, section 6.
	/// </summary>
	public List<CTimedModifier> EventModifiers { get; } = new();

	/// <summary>
	/// The triggered modifiers it has, in the order they came on: step 14 of its modifier. See
	/// <c>docs/mechanics/modifiers.md</c>, section 5.
	/// </summary>
	public List<CTriggeredModifier> TriggeredModifiers { get; } = new();

	/// <summary>
	/// The events its day's pass asks, made again each month and when a game is entered:
	/// <c>docs/mechanics/events.md</c>, section 1.
	/// </summary>
	public List<Script.CEvent> EventCandidates { get; } = new();

	/// <summary>
	/// How many land, air and sea battles the country has fought to a decision, counted as each is
	/// decided: <c>docs/mechanics/events.md</c>, section 8.
	/// </summary>
	public int LandBattlesFought { get; set; }

	public int AirBattlesFought { get; set; }

	public int NavalBattlesFought { get; set; }

	/// <summary>The flags script has set on it: <c>docs/mechanics/effects.md</c>, section 3.</summary>
	public Script.CFlags Flags { get; } = new();

	/// <summary>
	/// The numbers script keeps on it by name, in thousandths, names matched without case:
	/// <c>docs/mechanics/effects.md</c>, section 3.
	/// </summary>
	public Dictionary<string, CFixedPoint> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

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

	/// <summary>
	/// Which of the production screen's sliders the player has locked, 1 or 0, in that screen's list order - upgrades,
	/// reinforcement, supplies, production, consumer goods, lend-lease, the reverse of <see cref="EProductionCategory"/>.
	/// The original's mirror of its sliders' locks: nothing in the game reads it; the screens are made from it, the
	/// technology screen from its first four, and a save keeps it for the played country as <c>slider_locks</c>.
	/// Changed only by a command (<c>docs/mechanics/sliders.md</c>, section 3).
	/// </summary>
	public int[] SliderLocks { get; set; } = new int[CProduction.CategoryCount];

	/// <summary>The resources its industry used today. Cleared and counted again each day.</summary>
	public CGoodsPool Usage { get; } = new();

	/// <summary>What turning one good into another used today: energy, and crude oil.</summary>
	public CGoodsPool ConversionUsed { get; } = new();

	/// <summary>And what it made: crude oil, and fuel.</summary>
	public CGoodsPool ConversionMade { get; } = new();

	/// <summary>How unhappy its people are. Never below nothing.</summary>
	public CFixedPoint Dissent { get; set; }

	/// <summary>
	/// What its wars have worn it down by: banked each month it is at war and paid back to its neutrality in the first month
	/// of peace. Never below nothing. See <c>docs/mechanics/warexhaustion.md</c>.
	/// </summary>
	public CFixedPoint WarExhaustion { get; set; }

	/// <summary>
	/// How far its people will go before giving up, from 1 to 100. Read from its history, and moved each day
	/// (politics.md, section 6). 1 where the history gives none: the original's is nought until its first day lifts
	/// it to the floor, before anything reads it.
	/// </summary>
	public CFixedPoint NationalUnity { get; set; } = CFixedPoint.One;

	/// <summary>
	/// The share of its owned cores it does not hold, in thousandths, as the day's count left it: what <c>lost_national</c>
	/// asks. Nought with none owned. See <c>docs/mechanics/triggers.md</c>, section 5.
	/// </summary>
	public CFixedPoint LostNationalProvinces { get; set; }

	/// <summary>
	/// The victory points of its cores it owns, a thousand to a point, as the day's count left them. See
	/// <c>docs/mechanics/war.md</c>, section 12.
	/// </summary>
	public long OwnedCoreVictoryPoints { get; set; }

	/// <summary>Of those, the ones it also holds.</summary>
	public long HeldCoreVictoryPoints { get; set; }

	/// <summary>The victory points of every province it owns, cores or not, a thousand to a point, as the day's count left them.</summary>
	public long OwnedVictoryPoints { get; set; }

	/// <summary>The hour it last surrendered; long before any game, the save's "1.1.1.0", where it never has.</summary>
	public long LastSurrender { get; set; } = long.MinValue / 2;

	/// <summary>
	/// Whether it is a government in exile: its stockpile its own, its government in its faction leader's capital,
	/// never surrendering. See <c>docs/mechanics/war.md</c>, section 14.
	/// </summary>
	public bool InExile { get; set; }

	/// <summary>An exile's own stockpile, which every use of its stockpile reads and fills while it is one.</summary>
	public Economy.CGoodsPool PoolInExile { get; } = new();

	/// <summary>The supplies and fuel governments in exile sitting in its land have handed it today. See <c>war.md</c>, section 14.</summary>
	public Economy.CGoodsPool ReceivedFromExiles { get; } = new();

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
	/// Its land brigades in service: every brigade of a land unit, those waiting to be placed among them, but a reserve's
	/// while it is not mobilised. Counted again every hour by <see cref="COfficers"/>, as the original counts it with the
	/// officers; the consumer goods its army calls for at peace are reckoned from it (<c>production.md</c>, section 3).
	/// </summary>
	public int LandBrigadesInService { get; set; }

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
	/// Every name its units, brigades, ships and wings have been given: a new one of its own is never called what one of
	/// them already is. Another country's may be (the original keeps a set of them for each country; theatres.md, section 10).
	/// </summary>
	public HashSet<string> UnitNamesInUse { get; } = new(StringComparer.Ordinal);

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

	/// <summary>How many selection groups a country has: one for each digit.</summary>
	public const int SelectionGroupCount = 10;

	/// <summary>
	/// The selection groups of the digits, each the numbers of its units in the order they were selected. Kept in the
	/// game state, as the original keeps them, and set only by a command. See <c>docs/mechanics/selection.md</c>,
	/// section 7.
	/// </summary>
	public List<int>[] SelectionGroups { get; } = NewSelectionGroups();

	private static List<int>[] NewSelectionGroups()
	{
		var groups = new List<int>[SelectionGroupCount];
		for (int group = 0; group < groups.Length; group++)
		{
			groups[group] = new List<int>();
		}
		return groups;
	}

	/// <summary>Every unit it has, at every level, each before the units under it.</summary>
	public IEnumerable<CUnit> AllUnits()
	{
		var stack = new Stack<CUnit>(Units.Count + 16);
		for (int i = Units.Count - 1; i >= 0; i--)
		{
			stack.Push(Units[i]);
		}
		foreach (CUnit unit in CUnit.Walk(stack))
		{
			yield return unit;
		}
	}

	/// <summary>
	/// Every unit it has in the order the original keeps them, which is the order they
	/// were made in and so the order of their numbers - and in a loaded game the order the
	/// save's tree gave them (<see cref="CUnit.ListOrder"/>), those made since after them.
	/// Reinforcement and upgrades work through them this way, and it decides who goes
	/// without when a share runs out.
	/// </summary>
	public List<CUnit> UnitsInOrder()
	{
		var units = new List<CUnit>(AllUnits());
		units.Sort((a, b) => a.ListOrder != b.ListOrder ? a.ListOrder.CompareTo(b.ListOrder) : a.Id.CompareTo(b.Id));
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

	/// <summary>
	/// The buildings it has finished and not yet put anywhere, in the order they were finished
	/// (<c>docs/mechanics/buildinglines.md</c>, section 6).
	/// </summary>
	public List<Construction.CBuildingDeployment> BuildingDeployments { get; } = new();

	/// <summary>
	/// The provinces its radar sees from, by id: each added as it comes to control one with radar, and none ever taken
	/// off (<c>docs/mechanics/intelligence.md</c>, section 4).
	/// </summary>
	public List<int> RadarProvinces { get; } = new();

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
	/// What its trade routes handed over today, without what a friend's units paid: trade credits it
	/// beside <see cref="TradedAway"/>. See <c>docs/mechanics/trade.md</c>, section 6.
	/// </summary>
	public CGoodsPool TradedAwaySansAlliedSupply { get; } = new();

	/// <summary>What its trade routes brought it today: <see cref="TradedFor"/> without allied supply.</summary>
	public CGoodsPool TradedForSansAlliedSupply { get; } = new();

	/// <summary>
	/// The money it repaid on its debts today, cleared with the day's traded figures - ours: whether the
	/// original clears it was not established (<c>docs/mechanics/aid.md</c>, section 1).
	/// </summary>
	public CFixedPoint RepaidAway { get; set; }

	/// <summary>The money it has been repaid on debts owed to it, the other side of <see cref="RepaidAway"/>.</summary>
	public CFixedPoint IncomeFromDebt { get; set; }

	/// <summary>Who it gives lend-lease to, in the order it began (<c>docs/mechanics/aid.md</c>, section 3).</summary>
	public List<Aid.CLendLeaseGift> LendLeaseGiving { get; } = new();

	/// <summary>Who gives it lend-lease, in the order they began, with what each has sent and is not yet IC.</summary>
	public List<Aid.CLendLeaseReceipt> LendLeaseReceived { get; } = new();

	/// <summary>Lend-lease received and not yet made IC, in thousandths of IC.</summary>
	public long LendLeaseIncome { get; set; }

	/// <summary>The whole IC its lend-lease made today, part of <see cref="TotalIc"/>, needing no resources.</summary>
	public int LendLeaseIc { get; set; }

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
	/// Its transports on convoys, and all its transports, as the day's sharing found them before it moved any - what
	/// the production script reads as <c>GetTotalNeededConvoyTransports</c> (with <see cref="TransportsNeeded"/>) and
	/// <c>GetTotalConvoyTransports</c> (<c>ministers.md</c>, section 7).
	/// </summary>
	public int TransportsOnConvoysAtSharing { get; set; }

	public int TransportsAtSharing { get; set; }

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

	/// <summary>
	/// The offers waiting on this country: proposals to it and answers to its own, in the order they
	/// came (<c>docs/mechanics/agreements.md</c>, section 1).
	/// </summary>
	public List<Agreements.COffer> PendingOffers { get; } = new();

	/// <summary>
	/// Mobilised: its reserves called up, so that none of them is kept under strength or under organisation. Made so
	/// by a war, with <c>initial_mobilization</c>, when the country is brought up to date at war, and only undone after
	/// a peace (<c>war.md</c>, section 4); and by the politics screen's buttons and the AI
	/// (<c>politics.md</c>, section 11). The original's one byte for all three.
	/// </summary>
	public bool Mobilised { get; set; }

	/// <summary>
	/// Hours until its politics minister first works, from 1 to 3, drawn when a new game makes one for every country nobody
	/// plays; 0 for a country with none (<c>politics.md</c>, section 12).
	/// </summary>
	public int PoliticsCountdown { get; set; }

	/// <summary>
	/// Hours until its intelligence minister first works, from 1 to 10, drawn when a new game makes one for every country
	/// nobody plays; 0 for a country with none (<c>espionage.md</c>, section 15).
	/// </summary>
	public int IntelligenceCountdown { get; set; }

	/// <summary>
	/// Hours until its production minister first works, and then its tech minister, from 1 to 3 each, drawn when a new game
	/// makes them for every country nobody plays; 0 for a country with none (<c>ministers.md</c>, section 1).
	/// </summary>
	public int ProductionCountdown { get; set; }

	public int TechCountdown { get; set; }

	/// <summary>
	/// Hours until its foreign minister first works, from 1 to 3, drawn when a new game makes one for every country nobody
	/// plays, before the production minister's; 0 for a country with none (<c>foreignminister.md</c>, section 1).
	/// </summary>
	public int ForeignCountdown { get; set; }

	/// <summary>What its foreign minister keeps between its days (<c>foreignminister.md</c>, section 7).</summary>
	public Ai.CForeignMinister ForeignMinister { get; } = new();

	/// <summary>
	/// The wars its AI has prepared, in the order they were first prepared - each a target and the decision tied to it, if
	/// any (<c>foreignminister.md</c>, section 4; the original's strategy's <c>WarTargets</c>).
	/// </summary>
	public List<Ai.CWarTarget> WarTargets { get; } = new();

	/// <summary>
	/// What its AI makes of every other country - its personality and its weighted lists - made again every ten days
	/// and at a change of war (<c>aistrategy.md</c>; the original's merged strategy).
	/// </summary>
	public Ai.CAiStrategy Strategy { get; } = new();

	/// <summary>Its AI's unit agents and the flags of their pass (<c>docs/mechanics/agents.md</c>).</summary>
	public Agents.CUnitAi UnitAi { get; } = new();

	/// <summary>
	/// What its production minister last made of its desperation, 0 to 1100 thousandths, made again on the 1st, the 15th and
	/// the 29th of a month; -1 before the first time (<c>ministers.md</c>, section 4.1).
	/// </summary>
	public int MinisterDesperation { get; set; } = -1;

	/// <summary>
	/// The manpower it has to spare once every brigade is refilled, made at the end of its day and after every unit order
	/// (<see cref="Economy.CSpareManpower"/>).
	/// </summary>
	public CFixedPoint SpareManpower { get; set; }

	/// <summary>
	/// Each building's levels built in the owned provinces it holds a core in, and a whole level for every line of it in
	/// the queue, by the building's place in its file - made again each day and on a change of hands
	/// (<c>ministers.md</c>, section 7; the original's <c>GetTotalCoreBuildingLevels</c>).
	/// </summary>
	public List<CFixedPoint> CoreBuildingLevels { get; } = new();

	/// <summary>Spies trained and not yet placed anywhere, with no ceiling (<c>spies</c>). A whole spy is 1.</summary>
	public CFixedPoint FreeSpies { get; set; }

	/// <summary>
	/// What it has in each country, by that country's <see cref="Index"/> - itself included, where its counter-espionage
	/// at home is. See <c>docs/mechanics/espionage.md</c>, section 1.
	/// </summary>
	public Espionage.CSpyPresence[] Espionage { get; internal set; } = Array.Empty<Espionage.CSpyPresence>();

	/// <summary>The spies it has caught at home this month (<c>spiescaught</c>); nothing but the screen reads it.</summary>
	public int SpiesCaught { get; set; }

	/// <summary>
	/// Where the day's placing of spies starts in each priority band, 1 to 3, by a country's <see cref="Index"/>; entry 0
	/// is never used. Never saved (<c>espionage.md</c>, section 4).
	/// </summary>
	public int[] SpyCursors { get; } = new int[4];

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

	/// <summary>
	/// Its level in a category as everything reads it: its own, or where another shares the category with it
	/// and that one's own is higher, that. See <c>docs/mechanics/aid.md</c>, section 2.
	/// </summary>
	public CFixedPoint Ability(string category)
	{
		CFixedPoint own = OwnAbility(category);
		if (TechnologySharedFrom.TryGetValue(category, out CCountry? sharer))
		{
			CFixedPoint theirs = sharer.OwnAbility(category);
			return theirs > own ? theirs : own;
		}
		return own;
	}

	/// <summary>Its own ability in a category, which is nothing for one it has never had any in.</summary>
	public CFixedPoint OwnAbility(string category)
	{
		return Abilities.TryGetValue(category, out CFixedPoint level) ? level : CFixedPoint.Zero;
	}

	/// <summary>Who shares each category with it, by the category's name: one sharer a category.</summary>
	public Dictionary<string, CCountry> TechnologySharedFrom { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>What it shares, in the order it began: the receiver and the category.</summary>
	public List<(CCountry Receiver, string Category)> TechnologyShares { get; } = new();

	/// <summary>Game-relative path to the flag, which is named after the tag.</summary>
	public string FlagFile => $"gfx/flags/{Tag}.tga";

	public override string ToString()
	{
		return $"{Tag} ({Name})";
	}
}
