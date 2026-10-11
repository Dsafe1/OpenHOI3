using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What leaders do in a land battle, against <c>docs/mechanics/landcombat.md</c>: the
/// leader's bonus and the traits in the hour's modifiers, the theatre's commander and
/// crowding, the order of a defender's front, a corps commander and the reserves, shattering
/// and a side's leader for the tactics. Germany attacks Posen out of Berlin.
/// </summary>
public class CombatLeaderTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Posen = 2;
	private const int Noon = 12;

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
		Set(CSubUnitStats.Toughness, "3");
		Set(CCommandReach.RadioStrength, "2");
		return type;
	}

	private static CGameState State(out CCountry germany, out CCountry poland, CTerrain? terrain = null)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Posen };
		var defines = new CDefines();
		void Define(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Define(CFrontLine.BaseWidth, "10");
		Define(CFrontLine.AdditionalWidth, "5");
		Define(CCombatModifiers.StackingPenalty, "-0.1");
		Define(CCombatModifiers.DigInFactor, "0.02");
		Define(CCombatModifiers.LeaderImpact, "0.05");
		Define("RADIO_CORPS_LEADER_DISTANCE", "100");

		CProvince Land(int id, string name, CCountryTag owner, int other)
		{
			return new CProvince(id, name, isSea: false)
			{
				Owner = owner, Controller = owner, PositionX = 500, PositionY = 500, CentreX = 500, CentreY = 500,
				Edges = new[] { new CProvinceEdge(other, EProvinceEdgeKind.Plain, 0, CFixedPoint.One) },
				Terrain = id == Posen ? terrain : null,
			};
		}

		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), Land(Berlin, "Berlin", Ger, Posen), Land(Posen, "Posen", Pol, Berlin) },
			new[] { germany, poland }, defines: defines, wars: new[] { war })
		{
			MapWidth = 1000, MapHeight = 1000,
		};
	}

	private static CUnit Unit(CGameState state, CCountry country, int province, EUnitKind kind = EUnitKind.Division,
		CUnit? parent = null, CLeader? leader = null)
	{
		var unit = new CUnit(kind, country.Tag)
		{
			Id = ++state.LastUnitId, Location = province, Name = kind.ToString(), Parent = parent, Leader = leader,
		};
		parent?.Children.Add(unit);
		for (int i = 0; i < 3; i++)
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
		state.ForgetPositions();
		return unit;
	}

	private static CLeader Leader(int skill, int rank = 1, params CTraitEffect[] effects)
	{
		var leader = new CLeader(skill * 10 + rank) { Skill = skill, Rank = rank, Name = "leader" };
		var trait = new CTrait("a_trait");
		trait.Effects.AddRange(effects);
		leader.Traits.Add(trait);
		return leader;
	}

	private static CTraitEffect Effect(ETraitEffect kind, string value, string terrain = "")
	{
		return new CTraitEffect(kind, CFixedPoint.Parse(value), terrain);
	}

	private static CCombat Battle(CGameState state, CUnit attacker, CUnit defender)
	{
		for (int i = 0; i < Noon; i++)
		{
			state.AdvanceOneHour();
		}
		var combat = new CCombat(Posen);
		state.Combats.Add(combat);
		combat.Attacker.Add(attacker);
		combat.Defender.Add(defender);
		CCombats.Begin(state, combat);
		CCombatModifiers.Apply(state, combat, combat.Attacker);
		CCombatModifiers.Apply(state, combat, combat.Defender);
		return combat;
	}

	private static CFixedPoint Of(CUnit unit, ECombatModifier kind)
	{
		foreach (CCombatModifier modifier in unit.CombatModifiers)
		{
			if (modifier.Kind == kind)
			{
				return modifier.Attack;
			}
		}
		return CFixedPoint.Zero;
	}

	// --- the leader's bonus ---

	[Fact]
	public void AnAttackersLeaderAddsHisSkillHisOffenceAndHisTraitForTheGround()
	{
		var forest = new CTerrain("forest", isWater: false, default);
		CGameState state = State(out CCountry germany, out CCountry poland, forest);
		CLeader leader = Leader(2, 1, Effect(ETraitEffect.OffenceModifier, "0.1"),
			Effect(ETraitEffect.TerrainAttack, "0.05", "forest"), Effect(ETraitEffect.TerrainAttack, "0.3", "plains"),
			Effect(ETraitEffect.DefenceModifier, "0.4"));
		CUnit attacker = Unit(state, germany, Berlin, leader: leader);
		CUnit defender = Unit(state, poland, Posen);
		Battle(state, attacker, defender);

		// 2 x 0.05, and 0.1, and the forest's 0.05: a quarter, on its attack and its defence alike.
		Assert.Equal(CFixedPoint.Parse("0.25"), Of(attacker, ECombatModifier.Leader));
		Assert.Equal(CFixedPoint.Parse("1.25"), attacker.CombatDefence);
	}

	[Fact]
	public void ADefendersBonusIsMadeMoreByItsCountrysLeaderDefence()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		poland.Modifier.Set(CCombatModifiers.LeaderDefence, CFixedPoint.Parse("0.2"));
		CUnit attacker = Unit(state, germany, Berlin);
		CUnit defender = Unit(state, poland, Posen, leader: Leader(3, 1, Effect(ETraitEffect.DefenceModifier, "0.1")));
		Battle(state, attacker, defender);

		Assert.Equal(CFixedPoint.Parse("0.3"), Of(defender, ECombatModifier.Leader));   // (0.15 + 0.1) x 1.2
	}

	[Fact]
	public void ALeaderWithNoSkillBringsNoneOfHisTraitsAndNeitherDoesAUnitWithNone()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit corps = Unit(state, germany, Berlin, EUnitKind.Corps, leader: Leader(4, 2, Effect(ETraitEffect.OffenceModifier, "0.2")));
		CUnit unled = Unit(state, germany, Berlin, parent: corps);
		CUnit unskilled = Unit(state, germany, Berlin, parent: corps, leader: Leader(0, 1, Effect(ETraitEffect.OffenceModifier, "0.2")));
		CUnit defender = Unit(state, poland, Posen);
		CCombat combat = Battle(state, unled, defender);
		combat.Attacker.Add(unskilled);
		CCombatModifiers.Apply(state, combat, combat.Attacker);

		Assert.Equal(CFixedPoint.Zero, Of(unled, ECombatModifier.Leader));
		Assert.Equal(CFixedPoint.Zero, Of(unskilled, ECombatModifier.Leader));
	}

	[Fact]
	public void TraitsComeIntoTheModifiersTheyNameAndDownTheChain()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Unit(state, germany, Berlin);
		CUnit corps = Unit(state, poland, Posen, EUnitKind.Corps, leader: Leader(1, 2, Effect(ETraitEffect.DiginBonus, "1")));
		CUnit defender = Unit(state, poland, Posen, parent: corps);
		defender.DigIn = CFixedPoint.FromInt(5);
		Battle(state, attacker, defender);

		// Five levels at 0.02 is 0.1, and half the corps commander's doubling: 0.15.
		Assert.Equal(CFixedPoint.Parse("0.15"), Of(defender, ECombatModifier.DugIn));
	}

	// --- crowding ---

	[Fact]
	public void TheTheatresCommanderTakesAHundredthAPointOffTheCrowding()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit theatre = Unit(state, germany, Berlin, EUnitKind.Theatre, leader: Leader(4, 4));
		var attackers = new CUnit[5];
		for (int i = 0; i < attackers.Length; i++)
		{
			attackers[i] = Unit(state, germany, Berlin, parent: theatre);
		}
		CUnit defender = Unit(state, poland, Posen);
		CCombat combat = Battle(state, attackers[0], defender);
		for (int i = 1; i < attackers.Length; i++)
		{
			combat.Attacker.Add(attackers[i]);
		}
		CFrontLine.SetUp(state, combat, combat.Attacker);
		CCombatModifiers.Apply(state, combat, combat.Attacker);

		long crowding = CCombatModifiers.Stacking(state, combat.Attacker, 1);
		Assert.True(crowding > 40);
		Assert.Equal(new CFixedPoint(-(crowding - 40)), Of(attackers[0], ECombatModifier.Stacking));
	}

	// --- the front, the reserves, shattering ---

	[Fact]
	public void ADefendersLeadersSkillWholeGoesIntoTheOrderOfItsFront()
	{
		CGameState state = State(out CCountry _, out CCountry poland);
		CUnit unled = Unit(state, poland, Posen);
		CUnit led = Unit(state, poland, Posen, leader: Leader(2, 0));

		// Soft attack 6, defensiveness 12 and one: 19 without him, 21 with.
		Assert.Equal(CFrontLine.FrontOrder(unled, false) * 21 / 19, CFrontLine.FrontOrder(led, false));
		Assert.Equal(CFrontLine.FrontOrder(unled, true), CFrontLine.FrontOrder(led, true));
	}

	[Fact]
	public void ACorpsCommanderNearByMakesItsDivisionsComeUpFromReserveSooner()
	{
		CGameState state = State(out CCountry germany, out CCountry _);
		CUnit corps = Unit(state, germany, Berlin, EUnitKind.Corps, leader: Leader(3, 2));
		CUnit near = Unit(state, germany, Berlin, parent: corps);
		CUnit alone = Unit(state, germany, Berlin);

		// (10 + 3 x 10) by an organisation of 30 over a hundred, against 10 by the same.
		Assert.Equal(new CFixedPoint(12), CFrontLine.ReinforceChance(state, near, isAttacker: true));
		Assert.Equal(new CFixedPoint(3), CFrontLine.ReinforceChance(state, alone, isAttacker: true));
		Assert.Equal(new CFixedPoint(12), CFrontLine.ReinforceChance(state, corps, isAttacker: true));
	}

	[Fact]
	public void AUnitsOwnLeaderTakesAHundredthAPointOffWhereItShatters()
	{
		CGameState state = State(out CCountry germany, out CCountry _);
		CUnit led = Unit(state, germany, Berlin, leader: Leader(3, 0));

		Assert.Equal(470, CCombatDamage.ShatterThreshold(state, germany, led));
	}

	// --- a side's leader ---

	[Fact]
	public void ASidesLeaderIsItsHighestRankedTheFirstOfEquals()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CLeader first = Leader(2, 2);
		CLeader second = Leader(5, 2);
		CUnit a = Unit(state, germany, Berlin, leader: Leader(6, 1));
		CUnit b = Unit(state, germany, Berlin, leader: first);
		CUnit c = Unit(state, germany, Berlin, leader: second);
		first.Traits.Add(new CTrait("panzer_leader"));
		CCombat combat = Battle(state, a, Unit(state, poland, Posen));
		combat.Attacker.Add(b);
		combat.Attacker.Add(c);

		Assert.Same(first, CCombatLeaders.HighestRanked(combat.Attacker));
		Assert.Equal(2, CCombatLeaders.Skill(combat.Attacker));
		Assert.True(CCombatLeaders.HasTrait(combat.Attacker, "panzer_leader"));
		Assert.Null(CCombatLeaders.HighestRanked(combat.Defender));
	}
}
