using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the window a leader is chosen in lists and in what order, its trait filter, and
/// when the unit panel's button opens it, against <c>docs/mechanics/leaders.md</c>,
/// section 12, and <c>docs/mechanics/unitpanel.md</c>, section 3.
/// </summary>
public class LeaderWindowTextTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static readonly CTrait Panzer = new("panzer_leader") { AllowedSea = false, AllowedAir = false };
	private static readonly CTrait Sea = new("sea_wolf") { AllowedLand = false, AllowedAir = false };
	private static readonly CTrait Logistics = new("logistics_wizard");

	private static readonly CSubUnitType Infantry = new("infantry_brigade", EUnitBranch.Land);

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Warszawa", isSea: false) { Owner = Pol, Controller = Pol },
				new CProvince(3, "Baltic", isSea: true),
			},
			new[] { germany, poland }) { Traits = new[] { Panzer, Sea, Logistics } };
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind = EUnitKind.Division, int brigades = 2)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = 1, Name = $"unit {state.LastUnitId}" };
		for (int i = 0; i < brigades; i++)
		{
			unit.SubUnits.Add(new CSubUnit(Infantry) { Id = ++state.LastUnitId, Unit = unit });
		}
		country.Units.Add(unit);
		return unit;
	}

	private static CLeader Leader(CCountry country, int id, string name, int rank, int skill,
		ELeaderType type = ELeaderType.Land, params CTrait[] traits)
	{
		var leader = new CLeader(id) { Country = country.Tag, Name = name, Rank = rank, Skill = skill, Type = type };
		leader.Traits.AddRange(traits);
		country.ActiveLeaders.Add(leader);
		return leader;
	}

	private static List<string> Names(IEnumerable<CLeader> leaders)
	{
		return leaders.Select(leader => leader.Name).ToList();
	}

	private static CLeaderTraitFilter AllOn(CGameState state)
	{
		return CLeaderTraitFilter.Load(null, state.Traits);
	}

	[Fact]
	public void ItListsTheOwnersLeadersOfTheUnitsKindWhoAreFreeOrItsOwn()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany);
		CUnit other = Unit(state, germany);
		Leader(germany, 1, "Bock", 1, 2);
		Leader(germany, 2, "Raeder", 1, 4, ELeaderType.Sea);
		CLeader busy = Leader(germany, 3, "Rundstedt", 3, 3);
		busy.Unit = other;
		CLeader own = Leader(germany, 4, "Kluge", 2, 1);
		own.Unit = unit;
		Leader(germany, 5, "Hoth", 0, 5);                              // not in service

		List<CLeader> leaders = CLeaderWindowText.Leaders(state, unit, AllOn(state), ELeaderSort.Name, false);

		Assert.Equal(new[] { "Bock", "Kluge" }, Names(leaders));       // a corps' rank is listed for a division
		Assert.False(CLeaderWindowText.MayChoose(own, unit));          // and cannot be chosen
		Assert.True(CLeaderWindowText.MayChoose(leaders[0], unit));
	}

	[Fact]
	public void NamesGoUpAndTheRestComeDownAndASecondClickTurnsThemRound()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany);
		Leader(germany, 1, "Reichenau", 1, 2, ELeaderType.Land, Panzer);
		Leader(germany, 2, "Blaskowitz", 2, 4);
		Leader(germany, 3, "Bock", 4, 3, ELeaderType.Land, Panzer, Logistics);

		Assert.Equal(new[] { "Blaskowitz", "Bock", "Reichenau" },
			Names(CLeaderWindowText.Leaders(state, unit, AllOn(state), ELeaderSort.Name, false)));
		Assert.Equal(new[] { "Blaskowitz", "Bock", "Reichenau" },
			Names(CLeaderWindowText.Leaders(state, unit, AllOn(state), ELeaderSort.Skill, false)));
		Assert.Equal(new[] { "Bock", "Reichenau", "Blaskowitz" },
			Names(CLeaderWindowText.Leaders(state, unit, AllOn(state), ELeaderSort.Traits, false)));
		Assert.Equal(new[] { "Reichenau", "Blaskowitz", "Bock" },
			Names(CLeaderWindowText.Leaders(state, unit, AllOn(state), ELeaderSort.Rank, true)));

		Assert.Equal((ELeaderSort.Skill, true), CLeaderWindowText.Resort(ELeaderSort.Skill, false, ELeaderSort.Skill));
		Assert.Equal((ELeaderSort.Rank, false), CLeaderWindowText.Resort(ELeaderSort.Skill, true, ELeaderSort.Rank));
	}

	[Fact]
	public void TheFilterStartsWithEverythingOnAndShowsALeaderByAnyOfHisTraits()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany);
		Leader(germany, 1, "Bock", 1, 2);
		Leader(germany, 2, "Guderian", 1, 3, ELeaderType.Land, Panzer, Logistics);
		CLeaderTraitFilter filter = AllOn(state);

		filter.Set(Panzer, false);
		Assert.Equal(new[] { "Bock", "Guderian" }, Names(CLeaderWindowText.Leaders(state, unit, filter, ELeaderSort.Name, false)));
		filter.Set(Logistics, false);
		Assert.Equal(new[] { "Bock" }, Names(CLeaderWindowText.Leaders(state, unit, filter, ELeaderSort.Name, false)));
		filter.Set(null, false);
		Assert.Empty(CLeaderWindowText.Leaders(state, unit, filter, ELeaderSort.Name, false));
	}

	[Fact]
	public void OnlyThisTraitAndBackToEverything()
	{
		CGameState state = State(out _);
		CLeaderTraitFilter filter = AllOn(state);

		filter.Solo(Panzer);
		Assert.True(filter.IsOn(Panzer));
		Assert.False(filter.IsOn(Logistics));
		Assert.False(filter.IsOn(null));

		filter.Solo(Panzer);                                           // the only one on: all of them
		Assert.True(filter.IsOn(Logistics));
		Assert.True(filter.IsOn(null));

		filter.Solo(null);
		Assert.True(filter.IsOn(null));
		Assert.False(filter.IsOn(Sea));
	}

	[Fact]
	public void WhatIsKeptIsWhatWasOnAndNothingKeptMeansEverything()
	{
		CGameState state = State(out _);
		CLeaderTraitFilter filter = AllOn(state);
		filter.Solo(Logistics);

		List<CTrait?> kept = filter.Save();
		CLeaderTraitFilter again = CLeaderTraitFilter.Load(kept, state.Traits);

		Assert.Equal(new CTrait?[] { Logistics }, kept);
		Assert.True(again.IsOn(Logistics));
		Assert.False(again.IsOn(Panzer));                              // a trait never kept is off
		Assert.False(again.IsOn(null));

		again.Set(Logistics, false);
		Assert.True(CLeaderTraitFilter.Load(again.Save(), state.Traits).IsOn(Panzer));   // all off is forgotten
	}

	[Fact]
	public void TheFilterListsNoTraitFirstAndTheTraitsMeantForTheKind()
	{
		CGameState state = State(out _);

		Assert.Equal(new CTrait?[] { null, Panzer, Logistics }, CLeaderWindowText.FilterTraits(state, ELeaderType.Land));
		Assert.Equal(new CTrait?[] { null, Sea, Logistics }, CLeaderWindowText.FilterTraits(state, ELeaderType.Sea));
		Assert.Equal("NO_TRAIT_FILTER", CLeaderWindowText.FilterName(null, key => key));
		Assert.Equal("icon_panzer_leader", CLeaderWindowText.TraitSprite(Panzer));
	}

	[Fact]
	public void AnEnhancedGameWritesTheSkillAsItsLocalisationSaysWithTheHighestBesideIt()
	{
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) },
			new[] { new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" } });
		var leader = new CLeader(1) { Skill = 3, MaxSkill = 5 };
		string Given(string key) => key == CLeaderWindowText.SkillKey ? "$SKILL$ ($MAX$)" : key;

		// A faithful game's is the number, whatever the localisation holds.
		Assert.Equal("3", CLeaderWindowText.Skill(state, leader, Given));

		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		Assert.Equal("3 (5)", CLeaderWindowText.Skill(state, leader, Given));
		// With no such text, and for the empty leader, the number alone.
		Assert.Equal("3", CLeaderWindowText.Skill(state, leader, key => key));
		Assert.Equal("0", CLeaderWindowText.Skill(state, null, Given));
	}

	[Fact]
	public void TheHeadShowsTheEmptyLeaderForAUnitWithNone()
	{
		Assert.Equal("No Leader", CLeaderWindowText.Name(null, key => key == "NO_LEADER" ? "No Leader" : key));
		Assert.Equal("0", CLeaderWindowText.Skill(null));
		Assert.Equal(0, CLeaderWindowText.RankFrame(null));
		Assert.Equal("leader_entry_naval", CLeaderWindowText.EntryName(new CLeader(1) { Type = ELeaderType.Sea }));
	}

	[Fact]
	public void TheButtonIsRefusedFallingBackInABattleAtSeaAndOnEnemyGroundWhoeverOwnsTheUnit()
	{
		CGameState state = State(out CCountry germany);
		CCountry poland = state.Country(Pol)!;
		CUnit unit = Unit(state, germany);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy);
		CUnit polish = Unit(state, poland);
		polish.Location = 2;

		Assert.True(CUnitPanelText.MayChangeLeader(state, unit, Ger));
		Assert.True(CUnitPanelText.MayChangeLeader(state, fleet, Ger));             // in port
		Assert.True(CUnitPanelText.MayChangeLeader(state, polish, Ger));            // nobody asks whose

		var war = new CWar("Fall Weiss");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		state.Wars.Add(war);
		Assert.False(CUnitPanelText.MayChangeLeader(state, polish, Ger));           // on ground an enemy holds

		fleet.Location = 3;
		Assert.False(CUnitPanelText.MayChangeLeader(state, fleet, Ger));
		unit.IsRetreating = true;
		Assert.False(CUnitPanelText.MayChangeLeader(state, unit, Ger));
		unit.IsRetreating = false;
		unit.Combats.Add(new CCombat(1));
		Assert.False(CUnitPanelText.MayChangeLeader(state, unit, Ger));
	}

	[Fact]
	public void TheKindOfLeaderIsTheFrameAfterIt()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany);

		Assert.Equal(1, CUnitPanelText.LeaderTypeFrame(unit));
		unit.Leader = new CLeader(1) { Type = ELeaderType.Air };
		Assert.Equal(3, CUnitPanelText.LeaderTypeFrame(unit));
	}
}
