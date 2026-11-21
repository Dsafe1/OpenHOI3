using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Which units have a counter, how they stack and what a counter is made of, against
/// <c>docs/mechanics/mapunits.md</c>.
/// </summary>
public class MapCounterTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Berlin = 1;
	private const int Kiel = 2;
	private const int Baltic = 3;

	private static readonly CSubUnitType Infantry = Type("infantry_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Headquarters = Type("hq_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Destroyer = Type("destroyer", EUnitBranch.Naval);
	private static readonly CSubUnitType Fighter = Type("interceptor", EUnitBranch.Air);

	private static CSubUnitType Type(string key, EUnitBranch branch)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set("combat_width", CFixedPoint.One);
		type.Stats.Values.Set("soft_attack", CFixedPoint.FromInt(6));
		type.Stats.Values.Set("defensiveness", CFixedPoint.FromInt(16));
		return type;
	}

	private static CGameState State(out CCountry country)
	{
		country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin, Color = new CColor(0.4f, 0.5f, 0.6f) };
		return new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false), new CProvince(Berlin, "Berlin", isSea: false),
			new CProvince(Kiel, "Kiel", isSea: false), new CProvince(Baltic, "Baltic", isSea: true),
		}, new[] { country });
	}

	private static CUnit Unit(CCountry country, int id, EUnitKind kind, int location, params CSubUnitType[] types)
	{
		var unit = new CUnit(kind, country.Tag) { Id = id, Location = location };
		foreach (CSubUnitType type in types)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Unit = unit });
		}
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void ACountersSizeIsItsUnitsRank()
	{
		CGameState state = State(out CCountry country);

		Assert.Equal(5, CCounterText.Size(Unit(country, 1, EUnitKind.Theatre, Berlin, Headquarters)));
		Assert.Equal(4, CCounterText.Size(Unit(country, 2, EUnitKind.ArmyGroup, Berlin, Headquarters)));
		Assert.Equal(3, CCounterText.Size(Unit(country, 3, EUnitKind.Army, Berlin, Headquarters)));
		Assert.Equal(2, CCounterText.Size(Unit(country, 4, EUnitKind.Corps, Berlin, Headquarters)));
		Assert.Equal(1, CCounterText.Size(Unit(country, 5, EUnitKind.Division, Berlin, Infantry, Infantry)));
		Assert.Equal(0, CCounterText.Size(Unit(country, 6, EUnitKind.Division, Berlin, Infantry)));
		Assert.Equal(1, CCounterText.Size(Unit(country, 7, EUnitKind.Navy, Baltic, Destroyer)));
		Assert.Equal(0, CCounterText.Size(Unit(country, 8, EUnitKind.Air, Berlin, Fighter)));

		// An interface icon's frames are counted from one.
		Assert.Equal(6, CCounterText.SizeFrame(country.Units[0]));
		Assert.Equal(1, CCounterText.SizeFrame(country.Units[5]));
	}

	[Fact]
	public void ACounterShowsItsFirstBrigadesKindAndItsTwoFigures()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, 1, EUnitKind.Division, Berlin, Headquarters, Infantry);
		CUnit empty = Unit(country, 2, EUnitKind.Division, Berlin);

		Assert.Equal("hq_brigade", CCounterText.TypeKey(division));
		Assert.Equal("GFX_map_counter_hq_brigade", CCounterText.Sprite(CCounterText.NearSprite, division));
		Assert.Equal("GFX_small_counter_hq_brigade", CCounterText.Sprite(CCounterText.FarSprite, division));
		Assert.Equal("GFX_counter_hq_brigade", CDeployMenuText.CounterSprite(division));
		Assert.Equal("", CCounterText.Sprite(CCounterText.NearSprite, empty));

		division.Name = "1. Panzer-Division";
		empty.Name = "Stab";
		Assert.Equal("1. Panze", CCounterText.Name(division, true));   // the first eight letters
		Assert.Equal("Stab", CCounterText.Name(empty, true));

		Assert.Equal("2 - 2", CCounterText.Figures(division));         // 12 over 6, and 32 over 16
		Assert.Equal("0 - 0", CCounterText.Figures(empty));
		Assert.Equal("? - 3", CCounterText.Figures(-1, 3));
		Assert.Equal("Berlin", CDeployMenuText.Location(state, division));
	}

	[Fact]
	public void ACountersNameIsItsUnitsOrItsLeadersByTheShowUnitNamesSetting()
	{
		CGameState state = State(out CCountry country);
		CUnit led = Unit(country, 1, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit unled = Unit(country, 2, EUnitKind.Division, Kiel, Infantry, Infantry);
		led.Name = "1. Panzer-Division";
		unled.Name = "2. Infanterie-Division";
		led.Leader = new CLeader(7) { Name = "Guderian, Heinz" };

		// On: the unit's own, cut to eight letters.
		Assert.Equal("1. Panze", CCounterText.Name(led, true));
		Assert.Equal("2. Infan", CCounterText.Name(unled, true));
		// Off: its leader's, cut the same, and nothing for a unit with none.
		Assert.Equal("Guderian", CCounterText.Name(led, false));
		Assert.Equal("", CCounterText.Name(unled, false));

		// The counters take the setting, off unless told.
		Assert.Equal("Guderian", CMapCounters.Build(state).Single(stack => stack.Province == Berlin).Top.Name);
		Assert.Equal("", CMapCounters.Build(state).Single(stack => stack.Province == Kiel).Top.Name);
		Assert.Equal("1. Panze", CMapCounters.Build(state, null, true).Single(stack => stack.Province == Berlin).Top.Name);
	}

	[Fact]
	public void BelowIntelligenceFiveACountersNameIsQuestionMarksWhateverTheSetting()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin, Color = new CColor(0.4f, 0.5f, 0.6f) };
		var poland = new CCountry { Tag = new CCountryTag("POL"), Name = "Poland", DefinitionFile = "", Capital = Kiel, Color = new CColor(0.9f, 0.1f, 0.1f) };
		var state = new CGameState(new CDate(1936, 1, 1), new[]
		{
			new CProvince(0, "", isSea: false), new CProvince(Berlin, "Berlin", isSea: false),
			new CProvince(Kiel, "Kiel", isSea: false), new CProvince(Baltic, "Baltic", isSea: true),
		}, new[] { germany, poland });
		CUnit polish = Unit(poland, 1, EUnitKind.Division, Kiel, Infantry, Infantry);
		polish.Name = "1 Dywizja";
		polish.Leader = new CLeader(9) { Name = "Rydz-Smigly" };
		state.Province(Kiel).Intel[germany.Index] = 4;

		Assert.Equal("???", CMapCounters.Build(state, Ger, true).Single(stack => stack.Province == Kiel).Top.Name);
		Assert.Equal("???", CMapCounters.Build(state, Ger, false).Single(stack => stack.Province == Kiel).Top.Name);
		state.Province(Kiel).Intel[germany.Index] = 5;
		Assert.Equal("1 Dywizj", CMapCounters.Build(state, Ger, true).Single(stack => stack.Province == Kiel).Top.Name);
		Assert.Equal("Rydz-Smi", CMapCounters.Build(state, Ger, false).Single(stack => stack.Province == Kiel).Top.Name);
	}

	private static HashSet<CUnit> Set(params CUnit[] units)
	{
		return new HashSet<CUnit>(units);
	}

	[Fact]
	public void TheSelectedUnitWearsOneRingAndEveryUnitUnderItAnother()
	{
		State(out CCountry country);
		CUnit army = Unit(country, 1, EUnitKind.Army, Berlin, Headquarters);
		CUnit corps = Unit(country, 2, EUnitKind.Corps, Berlin, Headquarters);
		CUnit division = Unit(country, 3, EUnitKind.Division, Kiel, Infantry);
		CUnit other = Unit(country, 4, EUnitKind.Division, Kiel, Infantry);
		corps.Parent = army;
		division.Parent = corps;

		Assert.Equal(ECounterRing.None, CMapCounters.RingOf(army, Set()));

		Assert.Equal(ECounterRing.Selected, CMapCounters.RingOf(army, Set(army)));
		Assert.Equal(ECounterRing.Under, CMapCounters.RingOf(corps, Set(army)));
		Assert.Equal(ECounterRing.Under, CMapCounters.RingOf(division, Set(army)));     // however far down
		Assert.Equal(ECounterRing.None, CMapCounters.RingOf(other, Set(army)));

		// An AI in command of the selected unit, or of one above it, turns its own ring green and no other.
		army.Agent = new OpenHOI3.Sim.Agents.CAgent(army.Owner, 0);
		Assert.Equal(ECounterRing.SelectedUnderAi, CMapCounters.RingOf(army, Set(army)));
		Assert.Equal(ECounterRing.SelectedUnderAi, CMapCounters.RingOf(division, Set(division)));
		Assert.Equal(ECounterRing.Under, CMapCounters.RingOf(corps, Set(army)));
		Assert.Equal(ECounterRing.Selected, CMapCounters.RingOf(other, Set(other)));
		army.Agent = null;

		// Nothing above the selected unit is marked.
		Assert.Equal(ECounterRing.None, CMapCounters.RingOf(army, Set(corps)));
		Assert.Equal(ECounterRing.Selected, CMapCounters.RingOf(corps, Set(corps)));
		Assert.Equal(ECounterRing.Under, CMapCounters.RingOf(division, Set(corps)));
	}

	[Fact]
	public void AFleetInPortAndAWingAtItsBaseHaveNoCounter()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, 1, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit inPort = Unit(country, 2, EUnitKind.Navy, Kiel, Destroyer);
		CUnit atSea = Unit(country, 3, EUnitKind.Navy, Baltic, Destroyer);
		CUnit atBase = Unit(country, 4, EUnitKind.Air, Berlin, Fighter);
		atBase.Base = Berlin;
		CUnit flying = Unit(country, 5, EUnitKind.Air, Kiel, Fighter);
		flying.Base = Berlin;
		CUnit nowhere = Unit(country, 6, EUnitKind.Division, 0, Infantry);
		CUnit aboard = Unit(country, 7, EUnitKind.Air, Baltic, Fighter);
		aboard.CarriedBy = atSea.SubUnits[0];

		Assert.True(CMapCounters.HasCounter(state, division));
		Assert.False(CMapCounters.HasCounter(state, inPort));
		Assert.True(CMapCounters.HasCounter(state, atSea));
		Assert.False(CMapCounters.HasCounter(state, atBase));
		Assert.True(CMapCounters.HasCounter(state, flying));
		Assert.False(CMapCounters.HasCounter(state, nowhere));
		Assert.False(CMapCounters.HasCounter(state, aboard));

		Assert.Equal(EMapStack.Standing, CMapCounters.StackOf(state, division));
		Assert.Equal(EMapStack.Standing, CMapCounters.StackOf(state, atSea));
		Assert.Equal(EMapStack.Air, CMapCounters.StackOf(state, flying));
	}

	[Fact]
	public void AProvincesUnitsStackWithTheHigherRankOnTopAndAircraftApart()
	{
		CGameState state = State(out CCountry country);
		CUnit corps = Unit(country, 1, EUnitKind.Corps, Berlin, Headquarters);
		CUnit first = Unit(country, 2, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit second = Unit(country, 3, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit brigade = Unit(country, 4, EUnitKind.Division, Berlin, Infantry);
		CUnit flying = Unit(country, 5, EUnitKind.Air, Berlin, Fighter);
		CUnit elsewhere = Unit(country, 6, EUnitKind.Division, Kiel, Infantry, Infantry);

		var stacks = CMapCounters.Build(state);

		Assert.Equal(3, stacks.Count);
		CMapStack berlin = stacks.Single(stack => stack.Province == Berlin && stack.Kind == EMapStack.Standing);
		// The bottom first: a lone brigade is a division as the original ranks it, and of one rank the first stands higher.
		Assert.Equal(new[] { brigade, second, first, corps }, berlin.Counters.Select(counter => counter.Unit));
		Assert.Same(corps, berlin.Top.Unit);
		Assert.Equal(2, berlin.Top.Size);
		Assert.Equal("hq_brigade", berlin.Top.TypeKey);
		Assert.Equal(country.Color, berlin.Top.Colour);
		Assert.Same(flying, stacks.Single(stack => stack.Kind == EMapStack.Air).Top.Unit);
		Assert.Same(elsewhere, stacks.Single(stack => stack.Province == Kiel).Top.Unit);
	}

	[Fact]
	public void ThePlayersOwnUnitsComeFirstAndAtSeaFleetsBeforePassengers()
	{
		CGameState state = State(out CCountry country);
		var polishCorps = new CUnit(EUnitKind.Corps, new CCountryTag("POL")) { Id = 1 };
		CUnit division = Unit(country, 2, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit fleet = Unit(country, 3, EUnitKind.Navy, Baltic, Destroyer);
		CUnit passenger = Unit(country, 4, EUnitKind.Corps, Baltic, Headquarters);
		CUnit flying = Unit(country, 5, EUnitKind.Air, Baltic, Fighter);
		var carried = new CUnit(EUnitKind.Division, Ger) { Id = 6, Plane = flying };

		// The player's own first, whatever the rank.
		Assert.True(CProvinceUnitOrder.Before(division, polishCorps, Ger, landProvince: true));
		Assert.False(CProvinceUnitOrder.Before(polishCorps, division, Ger, landProvince: true));
		// With nobody playing, by rank.
		Assert.True(CProvinceUnitOrder.Before(polishCorps, division, null, landProvince: true));
		// At sea a fleet before the corps it carries; in a land province the other way.
		Assert.True(CProvinceUnitOrder.Before(fleet, passenger, Ger, landProvince: false));
		Assert.True(CProvinceUnitOrder.Before(passenger, fleet, Ger, landProvince: true));
		// An air unit before a land unit it carries, and otherwise after land by rank.
		Assert.True(CProvinceUnitOrder.Before(flying, carried, Ger, landProvince: true));
		Assert.False(CProvinceUnitOrder.Before(carried, flying, Ger, landProvince: true));
		Assert.True(CProvinceUnitOrder.Before(division, flying, Ger, landProvince: true));
	}

	[Fact]
	public void ASelectedUnitsPlaceStartsItsProvincesStacks()
	{
		CGameState state = State(out CCountry country);
		CUnit corps = Unit(country, 1, EUnitKind.Corps, Berlin, Headquarters);
		CUnit first = Unit(country, 2, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit second = Unit(country, 3, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit third = Unit(country, 4, EUnitKind.Division, Berlin, Infantry, Infantry);
		CMapStack berlin = CMapCounters.Build(state).Single(stack => stack.Province == Berlin);

		Assert.Equal(new[] { corps, first, second, third }, CMapCounters.TopDown(berlin, 0).Select(counter => counter.Unit));
		// The second selected: it on top, the third under it, and the corps and the first after them, round the list.
		int cursor = berlin.Counters.Single(counter => counter.Unit == second).ListIndex;
		Assert.Equal(new[] { second, third, corps, first }, CMapCounters.TopDown(berlin, cursor).Select(counter => counter.Unit));
	}

	/// <summary>A battle in a province, with Germany on the side given and Poland on the other.</summary>
	private static CCombat Battle(CGameState state, int province, bool germanyAttacks, ECombatKind kind = ECombatKind.Land)
	{
		var combat = new CCombat(province, kind);
		(germanyAttacks ? combat.Attacker : combat.Defender).AddCountry(Ger);
		(germanyAttacks ? combat.Defender : combat.Attacker).AddCountry(new CCountryTag("POL"));
		state.Combats.Add(combat);
		return combat;
	}

	[Fact]
	public void AUnitInABattleStandsWithItsSideAheadOfItsPath()
	{
		CGameState state = State(out CCountry country);
		CUnit attacker = Unit(country, 1, EUnitKind.Division, Kiel, Infantry, Infantry);
		CUnit defender = Unit(country, 2, EUnitKind.Division, Berlin, Infantry, Infantry);
		attacker.Combats.Add(Battle(state, Berlin, germanyAttacks: true));
		attacker.Path.Add(Berlin);
		defender.Combats.Add(Battle(state, Berlin, germanyAttacks: false));
		CUnit flying = Unit(country, 3, EUnitKind.Air, Berlin, Fighter);
		flying.Combats.Add(state.Combats[0]);

		// Attacking from Kiel, with no battle in Kiel: its own first battle, where Germany attacks - and ahead of its path.
		Assert.Equal(EMapStack.Attacking, CMapCounters.StackOf(state, attacker));
		// In Berlin the first land battle there is asked, whichever the unit is in, and Germany attacks in it.
		Assert.Equal(EMapStack.Attacking, CMapCounters.StackOf(state, defender));
		// An air unit stays with the air units, in a battle or not.
		Assert.Equal(EMapStack.Air, CMapCounters.StackOf(state, flying));
	}

	[Fact]
	public void AnAttackerWhoseOwnProvinceIsAttackedStandsWithTheDefenders()
	{
		CGameState state = State(out CCountry country);
		CUnit division = Unit(country, 1, EUnitKind.Division, Kiel, Infantry, Infantry);
		CCombat attackOnKiel = Battle(state, Kiel, germanyAttacks: false);
		CCombat attackFromKiel = Battle(state, Berlin, germanyAttacks: true);
		division.Combats.Add(attackFromKiel);
		division.Combats.Add(attackOnKiel);

		// The battle in its own province is asked first, whichever the unit joined first.
		Assert.Equal(EMapStack.Defending, CMapCounters.StackOf(state, division));
		Assert.False(CMapCounters.IsAttackerHere(state, division));
	}

	[Fact]
	public void AStackStandsOnTheProvincesPointButOnTheMoveAndASeaBattlesSides()
	{
		Assert.Equal(Vector2.Zero, CCounterLayout.StackOffset(EMapStack.Standing, sea: false));
		Assert.Equal(Vector2.Zero, CCounterLayout.StackOffset(EMapStack.Air, sea: true));
		Assert.Equal(Vector2.Zero, CCounterLayout.StackOffset(EMapStack.Attacking, sea: false));
		Assert.Equal(Vector2.Zero, CCounterLayout.StackOffset(EMapStack.Defending, sea: false));
		Assert.Equal(new Vector2(-2.4f, -2.4f), CCounterLayout.StackOffset(EMapStack.Attacking, sea: true));
		Assert.Equal(new Vector2(2.4f, 2.4f), CCounterLayout.StackOffset(EMapStack.Defending, sea: true));
		Assert.Equal(CCounterLayout.MovingStack, CCounterLayout.StackOffset(EMapStack.Moving, sea: false));
	}

	[Fact]
	public void TwoFleetsInASeaBattleStandInTwoStacks()
	{
		CGameState state = State(out CCountry country);
		CUnit ours = Unit(country, 1, EUnitKind.Navy, Baltic, Destroyer);
		var theirs = new CUnit(EUnitKind.Navy, new CCountryTag("POL")) { Id = 2, Location = Baltic };
		theirs.SubUnits.Add(new CSubUnit(Destroyer) { Unit = theirs });
		country.Units.Add(theirs);
		CCombat battle = Battle(state, Baltic, germanyAttacks: true, ECombatKind.Naval);
		ours.Combats.Add(battle);
		theirs.Combats.Add(battle);

		var stacks = CMapCounters.Build(state);

		Assert.Same(ours, stacks.Single(stack => stack.Kind == EMapStack.Attacking).Top.Unit);
		Assert.Same(theirs, stacks.Single(stack => stack.Kind == EMapStack.Defending).Top.Unit);
	}

	[Fact]
	public void WhatThePlayerKnowsOfAProvinceDecidesWhatItsForeignStacksShow()
	{
		CGameState state = State(out CCountry germany);
		var poland = new CCountry { Tag = new CCountryTag("POL"), Name = "Poland", DefinitionFile = "" };
		var italy = new CCountry { Tag = new CCountryTag("ITA"), Name = "Italy", DefinitionFile = "" };
		state = new CGameState(new CDate(1936, 1, 1), state.Provinces, new[] { germany, poland, italy });
		CUnit unseen = Unit(poland, 1, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit glimpsed = Unit(poland, 2, EUnitKind.Division, Kiel, Infantry, Infantry);
		CUnit glimpsedToo = Unit(poland, 3, EUnitKind.Corps, Kiel, Headquarters);
		CUnit atSea = Unit(poland, 4, EUnitKind.Division, Baltic, Infantry, Infantry);
		CUnit ally = Unit(italy, 5, EUnitKind.Division, Baltic, Infantry, Infantry);
		state.Province(Berlin).Intel[germany.Index] = 1;
		state.Province(Kiel).Intel[germany.Index] = 2;
		state.Province(Baltic).Intel[germany.Index] = 6;
		italy.Faction = germany.Faction = "axis";

		var stacks = CMapCounters.Build(state, Ger);

		// At 1 Poland's stack in Berlin is not there at all.
		Assert.DoesNotContain(stacks, stack => stack.Province == Berlin);
		// At 2 one counter, not fanned, the unknown picture, the name ??? and the figures ?.
		CMapStack kiel = stacks.Single(stack => stack.Province == Kiel);
		Assert.False(kiel.Fanned);
		Assert.Same(glimpsedToo, kiel.Top.Unit);
		Assert.Equal(CUnitIntel.UnknownType, kiel.Top.TypeKey);
		Assert.Equal("???", kiel.Top.Name);
		Assert.Equal("? - ?", kiel.Top.Figures);
		Assert.Contains(kiel.Counters, counter => counter.Unit == glimpsed);
		// At 6 the picture and the name, the figures still ?; Italy's, the same faction's, known whole.
		CMapStack baltic = stacks.Single(stack => stack.Province == Baltic);
		Assert.True(baltic.Fanned);
		Assert.Equal(CUnitIntel.Full, baltic.Counters.Single(counter => counter.Unit == ally).Intel);
		CMapCounter polish = baltic.Counters.Single(counter => counter.Unit == atSea);
		// Neither is the player's, both are divisions: the first in the province's list is on top.
		Assert.Same(atSea, baltic.Top.Unit);
		Assert.Equal("infantry_brigade", polish.TypeKey);
		Assert.NotEqual("???", polish.Name);
		Assert.Equal("? - ?", polish.Figures);
		// With nobody playing, everything.
		Assert.Contains(CMapCounters.Build(state).Single(stack => stack.Province == Berlin).Counters, counter => counter.Unit == unseen);
		Assert.Equal(CUnitIntel.Full, CUnitIntel.Level(state, atSea, null));
	}

	[Fact]
	public void ClickingAStackSelectsItsTopUnitAndThenEachUnitUnderItInTurn()
	{
		CGameState state = State(out CCountry country);
		CUnit corps = Unit(country, 1, EUnitKind.Corps, Berlin, Headquarters);
		CUnit first = Unit(country, 2, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit second = Unit(country, 3, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit elsewhere = Unit(country, 4, EUnitKind.Division, Kiel, Infantry, Infantry);
		var stacks = CMapCounters.Build(state);
		CMapStack berlin = stacks.Single(stack => stack.Province == Berlin);
		CMapStack kiel = stacks.Single(stack => stack.Province == Kiel);

		Assert.Same(corps, berlin.Next(null));
		Assert.Same(corps, berlin.Next(elsewhere));            // a unit of another stack is none of this one's
		// The unit after the selected one in the province's list: the corps, then its divisions in their order.
		Assert.Same(first, berlin.Next(corps));
		Assert.Same(second, berlin.Next(first));
		Assert.Same(corps, berlin.Next(second));               // round to the top again
		Assert.Same(elsewhere, kiel.Next(elsewhere));          // a stack of one stays on it
	}

	[Fact]
	public void ACountersPlacesAndSizesAreInTenthsOfItsTypesScaleAndGrowWithItsRank()
	{
		// The base game's two, as its file gives them.
		var far = new CCounterType { Name = "unit_counter", Scale = 3f, LevelScale = 0.2f, TextSize = 6f, FiguresOffset = new Vector3(-4.5f, 0f, -6.5f) };
		var near = new CCounterType
		{
			Name = "unit_counter_close", Scale = 2.5f, TextSize = 4.5f, HasFlag = true, FlagWidth = 0.45f, FlagHeight = 0.3f,
			FlagOffset = new Vector3(2.5f, 0f, -5f), FiguresOffset = new Vector3(-7f, 0f, -6.4f),
		};

		Assert.Equal(0.8f, CCounterLayout.Scale(far, 0), 4);
		Assert.Equal(1f, CCounterLayout.Scale(far, 1), 4);
		Assert.Equal(1.8f, CCounterLayout.Scale(far, 5), 4);
		Assert.Equal(1f, CCounterLayout.Scale(near, 5), 4);            // the near one does not grow
		// counter_scale multiplies the growth alone: a theatre 2.6 at 2, every counter a division's at 0.
		Assert.Equal(2.6f, CCounterLayout.Scale(far, 5, 2f), 4);
		Assert.Equal(1f, CCounterLayout.Scale(far, 5, 0f), 4);
		Assert.Equal(1f, CCounterLayout.Scale(far, 1, 2f), 4);
		Assert.Equal(1f, CCounterLayout.Scale(near, 5, 2f), 4);
		Assert.Equal(7.8f, CCounterLayout.HalfHeight(far, 5, 2f), 4);

		Assert.Equal(3f, CCounterLayout.HalfHeight(far, 1), 4);
		Assert.Equal(5.4f, CCounterLayout.HalfHeight(far, 5), 4);
		Assert.Equal(2.5f, CCounterLayout.HalfHeight(near, 3), 4);

		// East and north of the middle: the figures start at the lower left, the flag is
		// at the lower right and the name starts at the upper right.
		Vector2 figures = CCounterLayout.FiguresPlace(near, 1);
		Assert.Equal(-1.75f, figures.X, 4);
		Assert.Equal(-1.6f, figures.Y, 4);
		Vector2 flag = CCounterLayout.FlagPlace(near, 1);
		Assert.Equal(0.625f, flag.X, 4);
		Assert.Equal(-1.25f, flag.Y, 4);
		Assert.Equal(new Vector2(1.125f, 0.75f), CCounterLayout.FlagSize(near));
		Assert.Equal(0.0625f, CCounterLayout.FlagFrame(near), 4);
		near = new CCounterType { Name = "unit_counter_close", Scale = 2.5f, TextSize = 4.5f, NameOffset = new Vector3(5.8f, 0f, 5.5f) };
		Vector2 name = CCounterLayout.NamePlace(near, 1);
		Assert.Equal(1.45f, name.X, 4);
		Assert.Equal(1.375f, name.Y, 4);
		Assert.Equal(-1.95f, CCounterLayout.FiguresPlace(far, 1).Y, 4);

		// A line of text is nine tenths of the size its type gives: 4.5 and 3.5 tenths of
		// a scale of 2.5 for the near counter's figures and name, 6 tenths of 3 for the far one's.
		Assert.Equal(1.0125f, CCounterLayout.FiguresHeight(near, 1), 4);
		Assert.Equal(0.7875f, CCounterLayout.NameHeight(near, 1), 4);
		Assert.Equal(1.62f, CCounterLayout.FiguresHeight(far, 1), 4);

		// A stack shows its top six, each a little west and north of the one under it.
		Assert.Equal(1, CCounterLayout.Drawn(1));
		Assert.Equal(6, CCounterLayout.Drawn(9));
		Assert.Equal(Vector2.Zero, CCounterLayout.FanOffset(0));
		Assert.Equal(new Vector2(-0.6f, 0.6f), CCounterLayout.FanOffset(2));
	}

	[Fact]
	public void ACountersCornerIsRedForSupplyBeforeYellowForItsAttackDelayAndOnlyThePlayersOwn()
	{
		State(out CCountry country);
		CUnit unit = Unit(country, 1, EUnitKind.Division, Berlin, Infantry);

		Assert.Equal(ECounterMark.None, CCounterShading.MarkOf(unit, Ger));

		unit.AttackDelay = CFixedPoint.FromInt(3);
		Assert.Equal(ECounterMark.AttackDelay, CCounterShading.MarkOf(unit, Ger));
		Assert.Equal(ECounterMark.None, CCounterShading.MarkOf(unit, new CCountryTag("ENG")));
		Assert.Equal(ECounterMark.None, CCounterShading.MarkOf(unit, null));
		unit.IsRetreating = true;
		Assert.Equal(ECounterMark.None, CCounterShading.MarkOf(unit, Ger));
		unit.IsRetreating = false;

		// Under a twentieth of its fuel, and not at a twentieth of its supplies.
		unit.FuelReceived = new CFixedPoint(49);
		Assert.Equal(ECounterMark.Supply, CCounterShading.MarkOf(unit, Ger));
		unit.FuelReceived = CFixedPoint.One;
		unit.SuppliesReceived = new CFixedPoint(50);
		Assert.Equal(ECounterMark.AttackDelay, CCounterShading.MarkOf(unit, Ger));

		Assert.Equal(new CColor(1f, 0f, 0f), CCounterShading.ColourOf(ECounterMark.Supply));
		Assert.Equal(new CColor(1f, 0.5f, 0f), CCounterShading.ColourOf(ECounterMark.Upgrade));
		Assert.Equal(new CColor(1f, 1f, 0f), CCounterShading.ColourOf(ECounterMark.AttackDelay));
		Assert.Equal(0f, CCounterShading.ColourOf(ECounterMark.None).A);
	}

	[Fact]
	public void ACountersRingPulsesBetweenHalfAndFullAndItsSizeStripIsReadAsTheTextureHasIt()
	{
		Assert.Equal(0.5f, CCounterShading.SelectionIntensity(0), 4);
		Assert.Equal(1f, CCounterShading.SelectionIntensity(Math.PI / 4), 4);
		Assert.Equal(0.5f, CCounterShading.SelectionIntensity(Math.PI / 2), 4);

		// The original's strip, 384 wide in a texture of 512, and the same strip as a texture of its own.
		Assert.Equal(0.125f, CCounterShading.SizeOffset(384, 512, 6), 5);
		Assert.Equal(1f / 6f, CCounterShading.SizeOffset(384, 384, 6), 5);
	}

	[Fact]
	public void AMovingUnitsPointerIsItsOffsetAwayInTheDirectionTheUnitIsGoing()
	{
		// The base game's: a square two map units across, four and a half from the counter.
		var pointer = new CBillboardType { Name = CBillboardTypes.CounterArrow, Scale = 1f, Offset = 4.5f, Frames = 2 };
		Vector2 east = CCounterLayout.PointerPlace(pointer, 0f);
		Assert.Equal(4.5f, east.X, 4);
		Assert.Equal(0f, east.Y, 4);
		Vector2 north = CCounterLayout.PointerPlace(pointer, MathF.PI / 2f);
		Assert.Equal(0f, north.X, 4);
		Assert.Equal(4.5f, north.Y, 4);
		// A counter's own scale reaches neither: the original hands it the pointer and the draw never reads it.
		Assert.Equal(1f, CCounterLayout.PointerHalfSize(pointer), 4);
	}

	[Fact]
	public void TheDirectionAUnitIsGoingIsMeasuredTheShortWayRoundTheMap()
	{
		Assert.Equal(0f, CCounterLayout.Heading(10, 10, 20, 10, 100), 4);                  // east
		Assert.Equal(MathF.PI / 2f, CCounterLayout.Heading(10, 10, 10, 20, 100), 4);       // north
		Assert.Equal(MathF.PI / 4f, CCounterLayout.Heading(10, 10, 20, 20, 100), 4);
		Assert.Equal(MathF.PI, MathF.Abs(CCounterLayout.Heading(5, 10, 95, 10, 100)), 4);  // west, over the edge
		Assert.Equal(0f, CCounterLayout.Heading(95, 10, 5, 10, 100), 4);                   // east, over it
	}

	// --- the arrow of a selected unit ---

	[Fact]
	public void AnArrowIsCutTwentyTimesALegAndStopsATwentiethShortOfItsEnd()
	{
		var straight = CArrowBand.Build(new[] { new Vector2(0, 0), new Vector2(20, 0) });
		Assert.Equal(20, straight.Count);
		Assert.Equal(40, CArrowBand.Build(new[] { new Vector2(0, 0), new Vector2(20, 0), new Vector2(40, 10) }).Count);

		// Along a straight line the cuts run from one end towards the other, the middle one in
		// the middle - they bunch a little towards the ends - and the band is four wide. The last
		// is at a twentieth of the leg short of the end, so the head's tip is cut.
		Assert.Equal(0f, Middle(straight[0]).X, 3);
		Assert.Equal(10f, Middle(straight[10]).X, 3);
		Assert.InRange(Middle(straight[^1]).X, 18.5f, 19.9f);
		Assert.All(straight, cut => Assert.Equal(2f * CArrowBand.HalfWidth, Vector2.Distance(cut.Left, cut.Right), 3));
		Assert.All(straight, cut => Assert.Equal(0f, Middle(cut).Y, 3));

		// A cut's place along the arrow is its number over the number of cuts: never one.
		Assert.Equal(0f, straight[0].Progress);
		Assert.Equal(0.5f, straight[10].Progress, 4);
		Assert.Equal(19f / 20f, straight[^1].Progress, 4);
	}

	private static Vector2 Middle(CArrowSample cut) => (cut.Left + cut.Right) * 0.5f;

	[Fact]
	public void TheHeadOfTheArrowsPictureLiesOverItsLastFourUnits()
	{
		var band = CArrowBand.Build(new[] { new Vector2(0, 0), new Vector2(20, 0) });

		Assert.Equal(0.5f, band[0].Along);            // the shaft's column
		Assert.InRange(band[^1].Along, 0.75f, 0.99f);  // near the tip, which the last cut falls short of
		foreach (CArrowSample cut in band)
		{
			// Four from the end the head begins, and it is half way through two from it.
			float left = 20f - Middle(cut).X;
			float expected = left >= 4f ? 0.5f : 0.5f + 0.5f * (1f - left / 4f);
			Assert.Equal(expected, cut.Along, 4);
		}
		Assert.Contains(band, cut => cut.Along > 0.5f && cut.Along < 1f);
	}

	[Fact]
	public void OnlyTheLastHundredAndNineteenCutsAreWeighedForTheHead()
	{
		// Out and back along the same line: the start lies within four of the end, and seven legs make 140 cuts, so the
		// first twenty-one are not weighed and keep the shaft's column though they lie near the end.
		var points = new List<Vector2> { new(1, 0) };
		for (int leg = 0; leg < 7; leg++)
		{
			points.Add(new Vector2(leg % 2 == 0 ? 20 : 0, 0.2f * (leg + 1)));
		}
		points[^1] = new Vector2(0, 1.6f);
		var band = CArrowBand.Build(points);

		Assert.Equal(140, band.Count);
		Assert.Equal(0.5f, band[0].Along);
		Assert.True(band[^1].Along > 0.5f);
	}

	[Fact]
	public void TheArrowIsACurveThroughEveryPointOfThePath()
	{
		var points = new[] { new Vector2(0, 0), new Vector2(20, 0), new Vector2(20, 20) };
		var band = CArrowBand.Build(points);

		// It goes through the corner and does not cut it: the first cut of each leg is at its point.
		Assert.Equal(0f, Vector2.Distance(points[0], Middle(band[0])), 3);
		Assert.Equal(0f, Vector2.Distance(points[1], Middle(band[20])), 3);
		Assert.InRange(Vector2.Distance(points[2], Middle(band[^1])), 0.5f, 1.5f);
		// And it bends on the way there, swinging outside the straight line.
		Assert.True(Middle(band[15]).Y < 0f);
	}

	[Fact]
	public void APointWithinATenthOfTheNextIsLeftOutAndSoIsTheNext()
	{
		Assert.Empty(CArrowBand.Build(new[] { new Vector2(3, 3) }));
		Assert.Empty(CArrowBand.Build(new[] { new Vector2(3, 3), new Vector2(3.05f, 3) }));
		// The first point the same as the second: both go, and one point is left.
		Assert.Empty(CArrowBand.Build(new[] { new Vector2(0, 0), new Vector2(0, 0), new Vector2(9, 0) }));
		// A point near the next in the middle of the way: both go, and the way is one leg.
		Assert.Equal(20, CArrowBand.Build(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10.05f, 0.05f), new Vector2(30, 0) }).Count);
		// Within a tenth on one axis only is not near.
		Assert.Equal(60, CArrowBand.Build(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10.05f, 5), new Vector2(30, 0) }).Count);
	}
}
