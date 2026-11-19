using System.Linq;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a battle gives, against <c>docs/mechanics/leaders.md</c>, section 11: the experience
/// of an hour in one, a leader's skill out of it, and the hours a leader banks towards a
/// trait and has counted. Germany attacks Posen out of Berlin.
/// </summary>
public class BattleGainTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Posen = 2;

	private static readonly CTerrain Desert = new("desert", isWater: false, default);

	private static CSubUnitType Infantry()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		void Set(string statistic, string value) => type.Stats.Values.Set(statistic, CFixedPoint.Parse(value));
		Set(CSubUnitType.MaxStrength, "30");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CSubUnitType.CombatWidth, "1");
		Set(CMovementSpeed.MaximumSpeed, "4");
		Set(CUnitTotals.SoftAttack, "2");
		Set(CUnitTotals.Defensiveness, "4");
		return type;
	}

	private static CGameState State(out CCountry germany, out CCountry poland, CGainableTraits? gainable = null)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Posen };
		var defines = new CDefines();
		void Define(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Define(CCombatExperience.GainLand, "0.4");
		Define(CCombatExperience.GainNaval, "5");
		Define(CCombatExperience.GainAir, "0.25");
		Define(CCombatExperience.GainDivision, "1.8");
		Define(CCombatExperience.GainLeader, "240");
		Define(CCombatExperience.GainConvoy, "0.25");

		CProvince Land(int id, string name, CCountryTag owner, int other)
		{
			return new CProvince(id, name, isSea: false)
			{
				Owner = owner, Controller = owner, PositionX = 500, PositionY = 500, CentreX = 500, CentreY = 500,
				Edges = new[] { new CProvinceEdge(other, EProvinceEdgeKind.Plain, 0, CFixedPoint.One) },
				Terrain = id == Posen ? Desert : null,
			};
		}

		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), Land(Berlin, "Berlin", Ger, Posen), Land(Posen, "Posen", Pol, Berlin) },
			new[] { germany, poland }, defines: defines, wars: new[] { war })
		{
			MapWidth = 1000, MapHeight = 1000, GainableTraits = gainable ?? CGainableTraits.Empty,
		};
	}

	private static CUnit Unit(CGameState state, CCountry country, int province, EUnitKind kind = EUnitKind.Division,
		CUnit? parent = null, CLeader? leader = null, int brigades = 3)
	{
		var unit = new CUnit(kind, country.Tag)
		{
			Id = ++state.LastUnitId, Location = province, Name = kind.ToString(), Parent = parent, Leader = leader,
		};
		parent?.Children.Add(unit);
		for (int i = 0; i < brigades; i++)
		{
			CSubUnitType type = Infantry();
			var subUnit = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(30), Highest = CFixedPoint.FromInt(30),
				Organisation = CFixedPoint.FromInt(30), Experience = CFixedPoint.Zero,
			};
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		if (parent is null)
		{
			country.Units.Add(unit);
		}
		if (leader is not null)
		{
			leader.Unit = unit;
		}
		state.ForgetPositions();
		return unit;
	}

	private static CLeader Leader(int skill, int maxSkill, ELeaderType type = ELeaderType.Land)
	{
		var leader = new CLeader(skill * 100 + maxSkill) { Skill = skill, MaxSkill = maxSkill, Rank = 1, Name = "leader", Type = type };
		leader.Experience = CLeaders.Floor(skill);
		return leader;
	}

	private static CCombat Battle(CGameState state, CUnit attacker, CUnit defender)
	{
		var combat = new CCombat(Posen);
		state.Combats.Add(combat);
		combat.Attacker.Add(attacker);
		combat.Defender.Add(defender);
		return combat;
	}

	// --- a leader's experience ---

	[Fact]
	public void ALeaderLearnsFasterTheFurtherHeIsBelowHisMostAndAThirdAsFastAtIt()
	{
		CLeader far = Leader(2, 5);
		CLeader at = Leader(5, 5);
		CFixed15 before = far.Experience;
		CLeaders.AddExperience(far, CFixed15.FromInt(1));
		CLeaders.AddExperience(at, CFixed15.FromInt(1));

		// One and nine fifths: 32768 + 294912 / 5, whole in the 15 places.
		Assert.Equal(91750, (far.Experience - before).Raw);
		// A third of one.
		Assert.Equal(10922, (at.Experience - CLeaders.Floor(5)).Raw);
	}

	[Fact]
	public void ALeaderRisesALevelOnceHisExperienceIsAboveTheNextFloorAndOneAtATime()
	{
		CLeader leader = Leader(2, 2);
		leader.Experience = CLeaders.Floor(3);
		CLeaders.AddExperience(leader, CFixed15.Zero);
		Assert.Equal(2, leader.Skill);

		// Far past the floors of 3 and 4: one level for the one gain.
		CLeaders.AddExperience(leader, CFixed15.FromInt(10000));
		Assert.Equal(3, leader.Skill);
	}

	// --- an hour's experience ---

	[Fact]
	public void AnHoursExperienceGoesToTheBrigadesTheLeaderAndAShareToEachCommanderAbove()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit corps = Unit(state, germany, Berlin, EUnitKind.Corps, leader: Leader(4, 4), brigades: 1);
		CUnit attacker = Unit(state, germany, Berlin, parent: corps, leader: Leader(4, 4));
		Unit(state, germany, Berlin, parent: corps);
		Unit(state, germany, Berlin, parent: corps);
		Battle(state, attacker, Unit(state, poland, Posen));
		CFixed15 led = attacker.Leader!.Experience;
		CFixed15 commanded = corps.Leader!.Experience;

		CCombatExperience.Grant(state, attacker, convoyRaid: false);

		long gain = (attacker.Leader.Experience - led).Raw;
		Assert.True(gain > 0);
		// The corps has three units under it, a third; both leaders learn alike.
		Assert.InRange((corps.Leader.Experience - commanded).Raw, gain / 3 - 1, gain / 3 + 1);
		Assert.All(attacker.SubUnits, brigade => Assert.True(brigade.Experience > CFixedPoint.Zero));
		Assert.All(corps.SubUnits, brigade => Assert.Equal(CFixedPoint.Zero, brigade.Experience));
	}

	[Fact]
	public void AUnitFacingNobodyGainsNothingAndRollsNoDie()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CGameState twin = State(out _, out _);
		CUnit attacker = Unit(state, germany, Berlin, leader: Leader(1, 3));
		CUnit defender = Unit(state, poland, Posen);
		CCombat combat = Battle(state, attacker, defender);
		combat.Defender.Units.Clear();
		CFixed15 before = attacker.Leader!.Experience;

		CCombatExperience.Grant(state, attacker, convoyRaid: false);

		Assert.Equal(before, attacker.Leader.Experience);
		Assert.Equal(twin.Random.Next(1000), state.Random.Next(1000));
	}

	[Fact]
	public void ABrigadesExperienceStopsAtAHundred()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Unit(state, germany, Berlin);
		attacker.SubUnits[0].Experience = CFixedPoint.Parse("99.999");
		Battle(state, attacker, Unit(state, poland, Posen));

		CCombatExperience.Grant(state, attacker, convoyRaid: false);

		Assert.Equal(CFixedPoint.FromInt(100), attacker.SubUnits[0].Experience);
	}

	// --- traits a battle earns ---

	private static CGainableTraits DesertFox(out CTrait trait, int hours = 710)
	{
		trait = new CTrait("desert_fox") { AllowedSea = false, AllowedAir = false };
		var condition = new CCombatCondition(ECombatCondition.All);
		condition.Children.Add(new CCombatCondition(ECombatCondition.Terrain, name: "desert"));
		return new CGainableTraits(new List<CGainableTrait>
		{
			new(ECombatKind.Land, trait) { HoursRequired = hours, Condition = condition },
		});
	}

	[Fact]
	public void AnHourIsBankedOnceHoweverManyBattlesAskAndCountedAgainstTheHoursATraitWants()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, DesertFox(out CTrait fox));
		CLeader leader = Leader(2, 4);
		CUnit attacker = Unit(state, germany, Berlin, leader: leader);
		CCombat combat = Battle(state, attacker, Unit(state, poland, Posen));
		CTraitGain.BankHour(state, combat.Attacker);
		CTraitGain.BankHour(state, combat.Attacker);
		Assert.Equal(1, leader.TraitProgress.Duration);

		leader.TraitProgress.Duration = 355;
		CTraitGain.Count(state, combat, combat.Attacker);
		Assert.Equal(50000, leader.TraitProgress.Of(state.GainableTraits.All.Single(gainable => gainable.Trait == fox)));
		Assert.Equal(0, leader.TraitProgress.Duration);
		Assert.DoesNotContain(fox, leader.Traits);

		leader.TraitProgress.Duration = 400;
		CTraitGain.Count(state, combat, combat.Attacker);
		Assert.Equal(CTraitProgress.Whole, leader.TraitProgress.Of(state.GainableTraits.All.Single(gainable => gainable.Trait == fox)));
		Assert.Contains(fox, leader.Traits);
	}

	[Fact]
	public void HoursWhereTheConditionFailsAreSpentForNothingAndNoOtherKindOfLeaderEarnsIt()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, DesertFox(out CTrait fox));
		state.Province(Posen).Terrain = null;
		CLeader leader = Leader(2, 4);
		CUnit attacker = Unit(state, germany, Berlin, leader: leader);
		CCombat combat = Battle(state, attacker, Unit(state, poland, Posen));
		leader.TraitProgress.Duration = 710;

		CTraitGain.Count(state, combat, combat.Attacker);

		Assert.Equal(0, leader.TraitProgress.Of(state.GainableTraits.All.Single(gainable => gainable.Trait == fox)));
		Assert.Equal(0, leader.TraitProgress.Duration);
		Assert.Empty(state.GainableTraits.For(ECombatKind.Land, ELeaderType.Sea));
	}

	[Fact]
	public void AUnitLeavingASideHasEveryLeaderOnItCountedAndTheEndCountsBoth()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, DesertFox(out CTrait fox, hours: 0));
		CLeader staying = Leader(1, 3);
		CLeader leaving = Leader(1, 3);
		CUnit first = Unit(state, germany, Berlin, leader: staying);
		CUnit second = Unit(state, germany, Berlin, leader: leaving);
		CCombat combat = Battle(state, first, Unit(state, poland, Posen));
		combat.Attacker.Add(second);
		CTraitGain.BankHour(state, combat.Attacker);

		CCombats.Leave(state, combat, second);

		// A trait wanting no hours is earned at the first count of any.
		Assert.Contains(fox, staying.Traits);
		Assert.Contains(fox, leaving.Traits);
	}

	[Fact]
	public void IsWinnerHoldsOnceTheBattleIsOverForASideThatDidNotLoseIt()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CCombat combat = Battle(state, Unit(state, germany, Berlin), Unit(state, poland, Posen));
		var winner = new CCombatCondition(ECombatCondition.IsWinner);

		Assert.False(winner.Holds(state, combat, combat.Attacker));
		CCombats.Conclude(state, combat, attackerLost: true);
		combat.HasEnded = true;
		Assert.False(winner.Holds(state, combat, combat.Attacker));
		Assert.True(winner.Holds(state, combat, combat.Defender));
	}

	[Fact]
	public void ASideHadAModifierWhereOneOfItsUnitsHadItLastHourAndATemperatureIsBelowItsFigure()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Unit(state, germany, Berlin);
		CCombat combat = Battle(state, attacker, Unit(state, poland, Posen));
		attacker.CombatModifiers.Add(new CCombatModifier(ECombatModifier.Fort, CFixedPoint.Zero, CFixedPoint.Zero));
		state.Province(Posen).Weather.Temperature = CFixedPoint.Parse("-4.5");

		Assert.True(new CCombatCondition(ECombatCondition.HasCombatModifier, name: "BM_FORT_MODIFIER").Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.HasCombatModifier, name: "bm_fort_modifier").Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.HasCombatModifier, name: "BM_FORT_MODIFIER").Holds(state, combat, combat.Defender));
		Assert.True(new CCombatCondition(ECombatCondition.ProvinceTemperature, amount: -4).Holds(state, combat, combat.Attacker));
		Assert.False(new CCombatCondition(ECombatCondition.ProvinceTemperature, amount: -5).Holds(state, combat, combat.Attacker));
	}

	[Fact]
	public void ALeaderAPlayerPutsOnAUnitThrowsAwayTheHoursHeHadBanked()
	{
		CGameState state = State(out CCountry germany, out _);
		CLeader leader = Leader(1, 3);
		leader.TraitProgress.Duration = 40;
		leader.TraitProgress.CurrentHour = 5;

		CLeaders.SetLeader(state, Unit(state, germany, Berlin), leader, resetTraitHours: true, organisationHit: false);

		Assert.Equal(0, leader.TraitProgress.Duration);
		Assert.Equal(-1, leader.TraitProgress.CurrentHour);
	}

	[Fact]
	public void ALeaderTheChoosingPutsOnAUnitKeepsTheHoursHeHadBanked()
	{
		CGameState state = State(out CCountry germany, out _);
		CLeader leader = Leader(1, 3);
		leader.TraitProgress.Duration = 40;
		leader.TraitProgress.CurrentHour = 5;

		CLeaders.SetLeader(state, Unit(state, germany, Berlin), leader, resetTraitHours: false, organisationHit: false);

		Assert.Equal(40, leader.TraitProgress.Duration);
		Assert.Equal(5, leader.TraitProgress.CurrentHour);
	}
}
