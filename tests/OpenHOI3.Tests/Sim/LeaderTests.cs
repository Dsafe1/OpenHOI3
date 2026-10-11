using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Leaders against <c>docs/mechanics/leaders.md</c>: the skill their experience gives,
/// their putting in service, which units they may command, and the choice of one for a
/// unit.
/// </summary>
public class LeaderTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");

	private static CSubUnitType Type(string key, EUnitBranch branch, string speed = "4", string soft = "0", string hard = "0",
		string river = "0", bool submarine = false, bool paradrop = false)
	{
		var type = new CSubUnitType(key, branch);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, speed);
		Set(CSubUnitType.MaxStrength, "100");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CSubUnitType.CombatWidth, branch == EUnitBranch.Land ? "1" : "0");
		Set(CUnitTotals.SoftAttack, soft);
		Set(CUnitTotals.HardAttack, hard);
		var adjuster = new CModifier("river");
		adjuster.Set("attack", CFixedPoint.Parse(river));
		type.Stats.Adjusters["river"] = adjuster;
		type.Flags["is_sub"] = submarine;
		type.Flags["can_paradrop"] = paradrop;
		return type;
	}

	private static readonly CSubUnitType Infantry = Type("infantry_brigade", EUnitBranch.Land, soft: "3", hard: "1");
	private static readonly CSubUnitType Engineer = Type("engineer_brigade", EUnitBranch.Land, soft: "1", river: "0.5");
	private static readonly CSubUnitType Garrison = Type("garrison_brigade", EUnitBranch.Land, speed: "1", soft: "2");
	private static readonly CSubUnitType Militia = Type("militia_brigade", EUnitBranch.Land, speed: "3", soft: "2");
	private static readonly CSubUnitType Tank = Type("armor_brigade", EUnitBranch.Land, speed: "5", soft: "2", hard: "6");
	private static readonly CSubUnitType Paratrooper = Type("paratrooper_brigade", EUnitBranch.Land, soft: "3", paradrop: true);
	private static readonly CSubUnitType Submarine = Type("submarine", EUnitBranch.Naval, speed: "15", submarine: true);
	private static readonly CSubUnitType Destroyer = Type("destroyer", EUnitBranch.Naval, speed: "30");

	private static CGameState State(out CCountry germany, out CCountry italy)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		italy = new CCountry { Tag = Ita, Name = "Italy", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military("NAVAL_RANK_1", "6");
		Military("NAVAL_RANK_2", "12");
		Military("NAVAL_RANK_3", "18");
		Military("NAVAL_RANK_4", "30");
		Military("AIR_RANK_1", "4");
		Military("AIR_RANK_2", "8");
		Military("AIR_RANK_3", "12");
		Military("AIR_RANK_4", "16");
		Military(CLeaders.NewLeaderOrgHit, "0.5");
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger } },
			new[] { germany, italy }, defines: defines);
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, params CSubUnitType[] types)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = 1, Name = $"unit {state.LastUnitId}" };
		foreach (CSubUnitType type in types)
		{
			var subUnit = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(100),
				Organisation = CFixedPoint.FromInt(30), Name = type.Key,
			};
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		return unit;
	}

	private static CLeader Leader(CGameState state, CCountry country, int id, int rank, int skill,
		ELeaderType type = ELeaderType.Land, params CTrait[] traits)
	{
		var leader = new CLeader(id) { Country = country.Tag, Rank = rank, Skill = skill, Type = type, Name = $"leader {id}" };
		leader.Traits.AddRange(traits);
		CLeaders.SettleSkill(leader);
		state.Leaders[id] = leader;
		country.Leaders.Add(leader);
		if (rank != 0)
		{
			country.ActiveLeaders.Add(leader);
		}
		return leader;
	}

	private static CTrait Trait(string key, ETraitEffect effect)
	{
		var trait = new CTrait(key);
		trait.Effects.Add(new CTraitEffect(effect, CFixedPoint.Parse("0.1"), ""));
		return trait;
	}

	// ---- skill and experience

	[Fact]
	public void ALeaderStartsWithTheExperienceHisSkillNeeds()
	{
		var leader = new CLeader(1) { Skill = 2 };
		CLeaders.SettleSkill(leader);

		Assert.Equal(2, leader.Skill);
		Assert.Equal(CFixed15.FromInt(2000), leader.Experience);
	}

	[Fact]
	public void ExperienceAboveTheNextFloorRaisesTheSkillAndExactlyOnItDoesNot()
	{
		var on = new CLeader(1) { Skill = 2, Experience = CFixed15.FromInt(4000) };
		var above = new CLeader(2) { Skill = 2, Experience = CFixed15.FromInt(8001) };

		CLeaders.SettleSkill(on);
		CLeaders.SettleSkill(above);

		Assert.Equal(2, on.Skill);
		Assert.Equal(4, above.Skill);
	}

	[Fact]
	public void ASkillOfNothingTakesTheFilesExperienceInThousandths()
	{
		var leader = new CLeader(1) { Skill = 0, StartingExperience = 1500000 };
		CLeaders.SettleSkill(leader);

		Assert.Equal(1, leader.Skill);
		Assert.Equal(CFixed15.FromInt(1500), leader.Experience);
	}

	// ---- who may command what

	[Fact]
	public void ALandUnitAsksForOneRankExactly()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CUnit corps = Unit(state, germany, EUnitKind.Corps, Infantry);
		CUnit army = Unit(state, germany, EUnitKind.Army, Infantry);
		CUnit group = Unit(state, germany, EUnitKind.ArmyGroup, Infantry);
		CUnit theatre = Unit(state, germany, EUnitKind.Theatre, Infantry);

		for (int rank = 1; rank <= 4; rank++)
		{
			var leader = new CLeader(rank) { Rank = rank };
			Assert.Equal(rank == 1, CLeaders.MayCommand(leader, division, true));
			Assert.Equal(rank == 2, CLeaders.MayCommand(leader, corps, true));
			Assert.Equal(rank == 3, CLeaders.MayCommand(leader, army, true));
			Assert.Equal(rank == 4, CLeaders.MayCommand(leader, group, true));
			Assert.Equal(rank == 4, CLeaders.MayCommand(leader, theatre, true));
		}
	}

	[Fact]
	public void ADivisionOfOneBrigadeTakesNoLeader()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit lone = Unit(state, germany, EUnitKind.Division, Infantry);

		Assert.False(CLeaders.MayCommand(new CLeader(1) { Rank = 1 }, lone, true));
	}

	[Fact]
	public void AFleetTakesAnAdmiralOfAnyRankAndNobodyNotInServiceOrOfAnotherKind()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Destroyer);

		Assert.True(CLeaders.MayCommand(new CLeader(1) { Rank = 3, Type = ELeaderType.Sea }, fleet, true));
		Assert.False(CLeaders.MayCommand(new CLeader(2) { Rank = 0, Type = ELeaderType.Sea }, fleet, true));
		Assert.False(CLeaders.MayCommand(new CLeader(3) { Rank = 1, Type = ELeaderType.Land }, fleet, true));
	}

	[Fact]
	public void ALeaderWithAnotherUnitIsNotFree()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit first = Unit(state, germany, EUnitKind.Navy, Destroyer);
		CUnit second = Unit(state, germany, EUnitKind.Navy, Destroyer);
		var admiral = new CLeader(1) { Rank = 1, Type = ELeaderType.Sea, Unit = first };

		Assert.False(CLeaders.MayCommand(admiral, second, true));
		Assert.True(CLeaders.MayCommand(admiral, first, true));
		Assert.True(CLeaders.MayCommand(admiral, second, false));
	}

	[Fact]
	public void HavingTheRankAsksForLessThanBeingChosen()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit group = Unit(state, germany, EUnitKind.ArmyGroup, Infantry);
		CUnit army = Unit(state, germany, EUnitKind.Army, Infantry);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry);
		var three = new CLeader(1) { Rank = 3 };
		var one = new CLeader(2) { Rank = 1 };

		Assert.False(CLeaders.HasRankFor(state, group));               // nobody
		CLeaders.SetLeader(state, group, three, false, false);
		CLeaders.SetLeader(state, army, one, false, false);
		CLeaders.SetLeader(state, division, new CLeader(3) { Rank = 4 }, false, false);

		Assert.True(CLeaders.HasRankFor(state, group));                // 3 of the 4 choosing asks
		Assert.False(CLeaders.HasRankFor(state, army));                // an army wants 2
		Assert.True(CLeaders.HasRankFor(state, division));             // a division wants nothing
	}

	[Fact]
	public void AnAdmiralHasTheRankForAsManyShipsAsHisRankCommands()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit six = Unit(state, germany, EUnitKind.Navy, Destroyer, Destroyer, Destroyer, Destroyer, Destroyer, Destroyer);
		CUnit seven = Unit(state, germany, EUnitKind.Navy, Destroyer, Destroyer, Destroyer, Destroyer, Destroyer, Destroyer, Destroyer);
		CLeaders.SetLeader(state, six, new CLeader(1) { Rank = 1, Type = ELeaderType.Sea }, false, false);
		CLeaders.SetLeader(state, seven, new CLeader(2) { Rank = 1, Type = ELeaderType.Sea }, false, false);

		Assert.True(CLeaders.HasRankFor(state, six));
		Assert.False(CLeaders.HasRankFor(state, seven));
	}

	// ---- what a leader is worth to a unit

	[Fact]
	public void ALeaderIsWorthHisSkillAndOneInThousands()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CLeader leader = Leader(state, germany, 1, 1, 3);

		Assert.Equal(4000, CLeaders.Score(state, leader, division));
	}

	[Fact]
	public void ATraitThatSuitsTheUnitAddsItsSetAmountWhateverItsFigure()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit slow = Unit(state, germany, EUnitKind.Division, Garrison, Engineer);
		CUnit militia = Unit(state, germany, EUnitKind.Division, Militia, Militia);
		CUnit fast = Unit(state, germany, EUnitKind.Division, Tank, Tank);
		CLeader defender = Leader(state, germany, 1, 1, 0, ELeaderType.Land, Trait("defensive_doctrine", ETraitEffect.DefenceModifier));
		CLeader engineer = Leader(state, germany, 2, 1, 0, ELeaderType.Land, Trait("engineer", ETraitEffect.RiverAttack));
		CLeader softie = Leader(state, germany, 3, 1, 0, ELeaderType.Land, Trait("trickster", ETraitEffect.DefenderSoftness));
		CLeader attacker = Leader(state, germany, 4, 1, 0, ELeaderType.Land, Trait("offensive_doctrine", ETraitEffect.OffenceModifier));

		Assert.Equal(1000 + 10000, CLeaders.Score(state, defender, slow));
		Assert.Equal(1000, CLeaders.Score(state, defender, militia));      // under 3, and 3 is not
		Assert.Equal(1000, CLeaders.Score(state, defender, fast));
		Assert.Equal(1000 + 10000, CLeaders.Score(state, attacker, fast));    // over 4
		Assert.Equal(1000, CLeaders.Score(state, attacker, slow));
		Assert.Equal(1000 + 20000, CLeaders.Score(state, engineer, slow));
		Assert.Equal(1000, CLeaders.Score(state, engineer, fast));
		Assert.Equal(1000 + 5000, CLeaders.Score(state, softie, slow));
		Assert.Equal(1000, CLeaders.Score(state, softie, fast));
	}

	[Fact]
	public void AParatroopersLeaderGainsNothingForItThoughItIsParatroopers()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit paratroopers = Unit(state, germany, EUnitKind.Division, Paratrooper, Paratrooper);
		CLeader leader = Leader(state, germany, 1, 1, 0, ELeaderType.Land, Trait("commando", ETraitEffect.ParadropMission));

		Assert.Equal(1000, CLeaders.Score(state, leader, paratroopers));
	}

	[Fact]
	public void ASubmarineAdmiralSuitsAFleetWhoseFirstShipIsASubmarine()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit wolves = Unit(state, germany, EUnitKind.Navy, Submarine, Destroyer);
		CUnit screen = Unit(state, germany, EUnitKind.Navy, Destroyer, Submarine);
		CLeader admiral = Leader(state, germany, 1, 1, 0, ELeaderType.Sea, Trait("seawolf", ETraitEffect.SubmarineAttack));

		Assert.Equal(1000 + 20000, CLeaders.Score(state, admiral, wolves));
		Assert.Equal(1000, CLeaders.Score(state, admiral, screen));
	}

	[Fact]
	public void AnAdmiralGivenMoreShipsThanHisRankCommandsCountsForAHundredth()
	{
		CGameState state = State(out CCountry germany, out _);
		var ships = new CSubUnitType[7];
		System.Array.Fill(ships, Destroyer);
		CUnit big = Unit(state, germany, EUnitKind.Navy, ships);
		CLeader junior = Leader(state, germany, 1, 1, 4, ELeaderType.Sea);
		CLeader senior = Leader(state, germany, 2, 2, 4, ELeaderType.Sea);

		Assert.Equal(6, CLeaders.CommandSize(state, junior, big));
		Assert.Equal(50, CLeaders.Score(state, junior, big));
		Assert.Equal(5000, CLeaders.Score(state, senior, big));
	}

	// ---- the choice

	[Fact]
	public void AUnitTakesTheBestFreeLeaderAndOfEqualsTheFirstInService()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CLeader first = Leader(state, germany, 1, 1, 2);
		CLeader second = Leader(state, germany, 2, 1, 2);
		CLeader busy = Leader(state, germany, 3, 1, 5);
		busy.Unit = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);

		CLeaders.AssignBest(state, germany, division);

		Assert.Same(first, division.Leader);
		Assert.Same(division, first.Unit);
		Assert.Null(second.Unit);
	}

	[Fact]
	public void AGameBeingMadeGivesTheUnitsMadeFirstTheBestLeaders()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit first = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CUnit second = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CLeader good = Leader(state, germany, 1, 1, 1);
		CLeader better = Leader(state, germany, 2, 1, 4);

		CLeaders.SetAtStart(state);
		CLeaders.AssignAtStart(state);

		Assert.Same(better, first.Leader);
		Assert.Same(good, second.Leader);
	}

	[Fact]
	public void AnOrderOfBattlePutsItsLeaderOnWhateverHisRankOrCountry()
	{
		CGameState state = State(out CCountry germany, out CCountry italy);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CLeader italian = Leader(state, italy, 7, 0, 1);
		division.LeaderId = 7;

		CLeaders.SetAtStart(state);
		CLeaders.AssignAtStart(state);

		Assert.Same(italian, division.Leader);
		Assert.Empty(italy.ActiveLeaders);
	}

	[Fact]
	public void TheStartPutsInServiceEveryLeaderWithARankInTheOrderTheyWereRead()
	{
		CGameState state = State(out CCountry germany, out _);
		CLeader a = Leader(state, germany, 5, 2, 1);
		Leader(state, germany, 6, 0, 1);
		CLeader c = Leader(state, germany, 4, 1, 1);
		germany.ActiveLeaders.Clear();

		CLeaders.SetAtStart(state);
		CLeaders.AssignAtStart(state);

		Assert.Equal(new[] { a, c }, germany.ActiveLeaders);
	}

	// ---- the day's putting in service

	[Fact]
	public void ALeaderIsPutInServiceAtRankOneOnTheDayHisHistoryFirstGivesARank()
	{
		CGameState state = State(out CCountry germany, out _);
		CLeader leader = Leader(state, germany, 1, 0, 2);
		leader.FirstRankDate = new CDate(1936, 1, 3);

		CTick.RunHour(state);                               // 1 January, 1 o'clock
		for (int hour = 1; hour < 24; hour++)
		{
			CTick.RunHour(state);
		}
		Assert.Empty(germany.ActiveLeaders);                // 2 January
		for (int hour = 0; hour < 24; hour++)
		{
			CTick.RunHour(state);
		}

		Assert.Equal(new[] { leader }, germany.ActiveLeaders);
		Assert.Equal(1, leader.Rank);
	}

	[Fact]
	public void ALeaderWithNoRankInHisHistoryIsNeverPutInService()
	{
		CGameState state = State(out CCountry germany, out _);
		Leader(state, germany, 1, 0, 2);

		for (int hour = 0; hour < 24 * 3; hour++)
		{
			CTick.RunHour(state);
		}

		Assert.Empty(germany.ActiveLeaders);
	}

	// ---- putting one on

	[Fact]
	public void PuttingALeaderOnTakesHimOffWhatHeHadAndCanCostOrganisation()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit first = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CUnit second = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CLeader leader = Leader(state, germany, 1, 1, 1);
		CLeaders.SetLeader(state, first, leader, false, false);

		CLeaders.SetLeader(state, second, leader, false, true);

		Assert.Null(first.Leader);
		Assert.Same(leader, second.Leader);
		Assert.Same(second, leader.Unit);
		Assert.Equal(CFixedPoint.FromInt(15), second.SubUnits[0].Organisation);
		Assert.Equal(CFixedPoint.FromInt(30), first.SubUnits[0].Organisation);
	}

	[Fact]
	public void APlacedUnitGetsALeaderOnlyWhereItsCountryChoosesItsOwn()
	{
		CGameState state = State(out CCountry germany, out _);
		Leader(state, germany, 1, 1, 1);
		CUnit chosen = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		germany.Units.Remove(chosen);
		germany.Deployments.Add(chosen);
		CUnit left = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		germany.Units.Remove(left);
		germany.Deployments.Add(left);

		germany.AutoAssignLeaders = false;
		CDeployment.Place(state, germany, left, 1);
		germany.AutoAssignLeaders = true;
		CDeployment.Place(state, germany, chosen, 1);

		Assert.Null(left.Leader);
		Assert.NotNull(chosen.Leader);
	}

	[Fact]
	public void ALoneBrigadeJoiningADivisionLetsItTakeALeaderOnceItHasTwo()
	{
		CGameState state = State(out CCountry germany, out _);
		CLeader leader = Leader(state, germany, 1, 1, 1);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry);
		CUnit brigade = Unit(state, germany, EUnitKind.Division, Infantry);
		germany.Units.Remove(brigade);
		germany.Deployments.Add(brigade);

		CDeployment.Join(state, germany, brigade, division);

		Assert.Equal(2, division.SubUnits.Count);
		Assert.Same(leader, division.Leader);
	}

	[Fact]
	public void AShipJoiningAFleetKeepsTheAdmiralItWasGivenFromTheFleet()
	{
		CGameState state = State(out CCountry germany, out _);
		CLeader best = Leader(state, germany, 1, 1, 5, ELeaderType.Sea);
		CLeader next = Leader(state, germany, 2, 1, 2, ELeaderType.Sea);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Destroyer);
		CUnit ship = Unit(state, germany, EUnitKind.Navy, Destroyer);
		germany.Units.Remove(ship);
		germany.Deployments.Add(ship);

		CDeployment.Join(state, germany, ship, fleet);

		Assert.Same(next, fleet.Leader);
		Assert.Null(best.Unit);                             // free again once the empty unit is gone
		Assert.Null(ship.Leader);
	}
}
