using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A land battle against <c>docs/mechanics/landcombat.md</c>, on a border:
///
///     1 Berlin ---- 2 Posen ---- 3 Warsaw
///         |        /    \          |
///     4 Breslau --+      5 Lodz ---+
///
/// Berlin and Breslau are Germany's, the other three Poland's. The brigades are the base
/// game's infantry and its defines.
/// </summary>
public class LandCombatTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Sov = new("SOV");

	private const int Berlin = 1;
	private const int Posen = 2;
	private const int Warsaw = 3;
	private const int Breslau = 4;
	private const int Lodz = 5;

	private static CSubUnitType Infantry()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		Set(type, CSubUnitType.MaxStrength, "30");
		Set(type, CSubUnitType.DefaultOrganisation, "30");
		Set(type, CSubUnitType.Officers, "100");
		Set(type, CSubUnitType.CombatWidth, "1");
		Set(type, CSubUnitStats.BuildCostManpower, "2.33");
		Set(type, CMovementSpeed.MaximumSpeed, "4");
		Set(type, CUnitTotals.SoftAttack, "2");
		Set(type, CUnitTotals.HardAttack, "0.67");
		Set(type, CUnitTotals.Defensiveness, "4");
		Set(type, CSubUnitStats.Toughness, "3");
		Set(type, CUnitTotals.Softness, "1");
		return type;
	}

	private static void Set(CSubUnitType type, string statistic, string value)
	{
		type.Stats.Values.Set(statistic, CFixedPoint.Parse(value));
	}

	private static CProvinceEdge Edge(int to, string distance = "1")
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.Parse(distance));
	}

	private static CProvince Land(int id, string name, CCountryTag owner, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: false) { Owner = owner, Controller = owner, Edges = edges };
	}

	private static CWar War(CCountryTag attacker, CCountryTag defender)
	{
		var war = new CWar("war");
		war.Attackers.Add(attacker);
		war.Defenders.Add(defender);
		return war;
	}

	private static CGameState State(out CCountry germany, out CCountry poland, bool atWar = true,
		CBuilding[]? buildings = null)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Warsaw };
		var soviets = new CCountry { Tag = Sov, Name = "Soviet Union", DefinitionFile = "" };
		var defines = new CDefines();
		void Define(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Define(CMovementSpeed.LandSpeedModifier, "0.05");
		Define(CMovementSpeed.CombatMovementSpeed, "0.33");
		Define(CFrontLine.BaseWidth, "10");
		Define(CFrontLine.AdditionalWidth, "5");
		Define(CLandFire.OrganisationDice, "4");
		Define(CLandFire.StrengthDice, "2");
		Define(CLandFire.OrganisationDiceOnSoft, "5");
		Define(CLandFire.StrengthDiceOnSoft, "2");
		Define(CLandFire.OrganisationDeflection, "0.5");
		Define(CLandFire.StrengthDeflection, "0.5");
		Define(CLandFire.OrganisationDamage, "7.5");
		Define(CLandFire.StrengthDamage, "0.6");
		Define(CLandFire.ChanceToAvoid, "70");
		Define(CLandFire.ChanceToAvoidUndefended, "48");
		Define(CCombatDamage.OfficerLoss, "0.007");
		Define(CCombatRetreat.RetreatProgress, "0.9");
		Define(CAttackDelay.Delay, "168");
		Define(CAttackDelay.DelayPeriod, "12");
		Define(CAttackDelay.DelayModify, "10");
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Berlin, "Berlin", Ger, Edge(Posen), Edge(Breslau)),
				Land(Posen, "Posen", Pol, Edge(Berlin), Edge(Warsaw), Edge(Breslau), Edge(Lodz)),
				Land(Warsaw, "Warsaw", Pol, Edge(Posen), Edge(Lodz)),
				Land(Breslau, "Breslau", Ger, Edge(Berlin), Edge(Posen)),
				Land(Lodz, "Lodz", Pol, Edge(Posen), Edge(Warsaw)),
			},
			new[] { germany, poland, soviets }, defines: defines,
			wars: atWar ? new[] { War(Ger, Pol) } : null, buildings: CTestRoads.With(buildings ?? new CBuilding[0])).Paved();
	}

	private static CUnit Division(CGameState state, CCountry country, int province, int brigades = 3,
		string organisation = "30", string strength = "30")
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = "Division" };
		for (int i = 0; i < brigades; i++)
		{
			CSubUnitType type = Infantry();
			var subUnit = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit,
				Strength = CFixedPoint.Parse(strength), Highest = CFixedPoint.Parse(strength),
				Organisation = CFixedPoint.Parse(organisation), Experience = CFixedPoint.Zero,
			};
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	/// <summary>One hour of what concerns a battle: the units' pass, then the battles'.</summary>
	private static void Hour(CGameState state)
	{
		state.AdvanceOneHour();
		state.SeedHour();
		CMovement.RunHourly(state);
		CCombats.RunHourly(state);
	}

	private static CCombat Fight(CGameState state, CUnit attacker, int province)
	{
		attacker.Path.Add(province);
		Hour(state);
		return Assert.Single(state.Combats);
	}

	// --- chance ---

	[Fact]
	public void TheGeneratorNeverDrawsBelowNothingAndDrawsTheSameFromTheSameSeed()
	{
		var first = new CRandom(42);
		var second = new CRandom(42);
		var other = new CRandom(43);
		bool differs = false;
		for (int i = 0; i < 2000; i++)
		{
			int draw = first.Next();
			Assert.True(draw >= 0);
			Assert.Equal(draw, second.Next());
			differs |= draw != other.Next();
		}
		Assert.True(differs);
		Assert.Equal(2000, first.Draws);
	}

	[Fact]
	public void AnHoursRollsStartFromTheGamesSeedAndTheHour()
	{
		CGameState state = State(out _, out _);
		state.SeedHour();
		int now = state.Random.Next();
		state.SeedHour();
		Assert.Equal(now, state.Random.Next());

		state.AdvanceOneHour();
		state.SeedHour();
		int later = state.Random.Next();
		state.Seed = 7;
		state.SeedHour();
		Assert.NotEqual(later, state.Random.Next());
		Assert.NotEqual(now, later);
	}

	[Fact]
	public void ASidesOwnRollsAreTheProductOfTwoStepsOfItsGenerator()
	{
		// The first two steps from a seed of nothing are 38 and 7719.
		Assert.Equal(38L * 7719, new CCombatStream(0).Next());
	}

	// --- what starts a battle ---

	[Fact]
	public void AUnitWalkingTowardsAnEnemyStartsABattleThereAndStaysWhereItIs()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);

		CCombat combat = Fight(state, attacker, Posen);

		Assert.Equal(Posen, combat.Province);
		Assert.Equal(new[] { attacker }, combat.Attacker.Units);
		Assert.Equal(new[] { defender }, combat.Defender.Units);
		Assert.Equal(Berlin, attacker.Location);
		Assert.True(attacker.IsFighting && defender.IsFighting);
	}

	[Fact]
	public void NothingStartsBetweenCountriesAtPeace()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, atWar: false);
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		attacker.Path.Add(Posen);

		Hour(state);
		Assert.Empty(state.Combats);
	}

	[Fact]
	public void AUnitThatWasGivenNoSuppliesStartsNoBattle()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		attacker.SuppliesReceived = CFixedPoint.Zero;
		attacker.Path.Add(Posen);

		Hour(state);
		Assert.Empty(state.Combats);
		Assert.False(attacker.IsMoving);                               // nor does it walk onto enemy ground
	}

	[Fact]
	public void ASecondAttackerJoinsTheBattleThatIsThereAndWaitsInReserve()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit first = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		CCombat combat = Fight(state, first, Posen);

		CUnit second = Division(state, germany, Breslau);
		second.Path.Add(Posen);
		Hour(state);

		Assert.Single(state.Combats);
		Assert.Equal(new[] { first, second }, combat.Attacker.Units);
		Assert.Contains(second, combat.Attacker.Reserves);             // the front was set up without it
	}

	[Fact]
	public void AUnitJoinsTheSideWhoseEnemiesAreAllItsOwn()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CCountry soviets = state.Country(Sov)!;
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);

		// At war with nobody: it is offered to neither side.
		CUnit bystander = Division(state, soviets, Posen);
		Assert.False(CCombats.Offer(state, combat, bystander));

		// At war with Poland only: the defenders would all fight it, the attackers would not.
		state.Wars.Add(War(Sov, Pol));
		Assert.True(CCombats.Offer(state, combat, bystander));
		Assert.Contains(bystander, combat.Attacker.Units);

		// A countryman of somebody on a side is not that side's enemy, so a second
		// Soviet unit follows the first even with its country at war with Germany too.
		CUnit second = Division(state, soviets, Posen);
		state.Wars.Add(War(Sov, Ger));
		Assert.True(CCombats.Offer(state, combat, second));

		// At war with both and with nobody of its own there: it joins neither.
		CCombats.LeaveAll(state, bystander);
		CCombats.LeaveAll(state, second);
		Assert.False(CCombats.Offer(state, combat, second));
	}

	// --- the front ---

	[Fact]
	public void ABattleIsAsWideAsTheBaseAndFiveMoreForEachFurtherProvinceTheAttackersComeFrom()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit first = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		CCombat combat = Fight(state, first, Posen);
		Assert.Equal(CFixedPoint.FromInt(10), CFrontLine.Width(state, combat));

		CUnit sameWay = Division(state, germany, Berlin);
		combat.Attacker.Add(sameWay);
		Assert.Equal(CFixedPoint.FromInt(10), CFrontLine.Width(state, combat));

		CUnit otherWay = Division(state, germany, Breslau);
		combat.Attacker.Add(otherWay);
		Assert.Equal(CFixedPoint.FromInt(15), CFrontLine.Width(state, combat));
	}

	[Fact]
	public void AUnitTakesItsBrigadesWidthsByItsCountrysModifierAndNeverLessThanOne()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin, brigades: 4);
		Assert.Equal(CFixedPoint.FromInt(4), CUnitTotals.Width(germany, unit));

		germany.Modifier.Set(CUnitTotals.WidthModifier, CFixedPoint.Parse("-0.25"));
		Assert.Equal(CFixedPoint.FromInt(3), CUnitTotals.Width(germany, unit));

		germany.Modifier.Set(CUnitTotals.WidthModifier, CFixedPoint.Parse("-1"));
		Assert.Equal(CFixedPoint.One, CUnitTotals.Width(germany, unit));
	}

	[Fact]
	public void TheFrontTakesTheBestFirstUntilTheWidthIsUsedAndMayEndOneUnitOver()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit weak = Division(state, poland, Posen, brigades: 4, organisation: "10");
		CUnit strong = Division(state, poland, Posen, brigades: 4);
		CUnit middling = Division(state, poland, Posen, brigades: 4, organisation: "20");
		CUnit late = Division(state, poland, Posen, brigades: 4, organisation: "5");
		CUnit spent = Division(state, poland, Posen, brigades: 4, organisation: "0.5");

		attacker.Path.Add(Posen);
		state.AdvanceOneHour();
		state.SeedHour();
		CMovement.RunHourly(state);
		CCombat combat = Assert.Single(state.Combats);
		CCombats.Begin(state, combat);

		// Width 10, four each: 0, 4 and 8 are in use when the first three are asked.
		Assert.Equal(new[] { strong, middling, weak }, combat.Defender.Front);
		Assert.Equal(new[] { spent, late }, combat.Defender.Reserves);
		Assert.True(combat.Defender.IsFrontSetUp);
	}

	[Fact]
	public void AUnitCanFightWhileItsBrigadesThatHoldFrontageAverageATenthOfStrengthAndOneOfOrganisation()
	{
		CGameState state = State(out CCountry germany, out _);
		Assert.True(CUnitTotals.CanFight(Division(state, germany, Berlin, organisation: "1")));
		Assert.False(CUnitTotals.CanFight(Division(state, germany, Berlin, organisation: "0.999")));
		Assert.False(CUnitTotals.CanFight(Division(state, germany, Berlin, strength: "0.099")));
	}

	[Fact]
	public void NobodyComesUpFromReserveInABattlesFirstHourAndAChanceScalesWithOrganisation()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);
		CUnit reserve = Division(state, germany, Berlin);
		combat.Attacker.Add(reserve);

		combat.Hour = 0;
		Assert.Null(CFrontLine.PickReinforcement(state, combat, combat.Attacker));

		// 10 thousandths by an organisation of 30 over a hundred: 3.
		Assert.Equal(new CFixedPoint(3), CFrontLine.ReinforceChance(state, reserve, isAttacker: true));
		germany.Technology.Set(CFrontLine.TechnologyReinforceChance, CFixedPoint.Parse("0.1"));
		germany.Modifier.Set(CFrontLine.AttackReinforceChance, CFixedPoint.Parse("0.1"));
		Assert.Equal(new CFixedPoint(63), CFrontLine.ReinforceChance(state, reserve, isAttacker: true));
		Assert.Equal(new CFixedPoint(33), CFrontLine.ReinforceChance(state, reserve, isAttacker: false));

		// With a chance of a hundred per cent the roll cannot fail.
		germany.Modifier.Set(CFrontLine.AttackReinforceChance, CFixedPoint.FromInt(10));
		combat.Hour = 1;
		Assert.Same(reserve, CFrontLine.PickReinforcement(state, combat, combat.Attacker));
	}

	// --- the round ---

	[Fact]
	public void AUnitsOwnStrengthAddsAHundredToItsDamageForEachTenthItHasAndNeverLess()
	{
		CGameState state = State(out CCountry germany, out _);
		Assert.Equal(1000, CLandFire.StrengthFactor(Division(state, germany, Berlin)));
		Assert.Equal(500, CLandFire.StrengthFactor(Division(state, germany, Berlin, strength: "15")));
		Assert.Equal(500, CLandFire.StrengthFactor(Division(state, germany, Berlin, strength: "17.9")));
		Assert.Equal(100, CLandFire.StrengthFactor(Division(state, germany, Berlin, strength: "1")));
	}

	[Fact]
	public void ADefenceBuysAsManyShotsAsItsWholePartAndTheRestAreJudgedByTheWorseChance()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit target = Division(state, germany, Berlin);
		// Nothing avoids a shot the unit can meet; everything but a roll of 99 avoids one it cannot.
		state.Defines.Set(CManpower.MilitarySection, CLandFire.ChanceToAvoid, CFixedPoint.Zero);
		state.Defines.Set(CManpower.MilitarySection, CLandFire.ChanceToAvoidUndefended, CFixedPoint.FromInt(200));
		state.SeedHour();

		Assert.True(CLandFire.Hits(state, target, 2000));
		Assert.True(CLandFire.Hits(state, target, 2000));
		Assert.Equal(2, target.DefencesUsed);

		int landed = 0;
		for (int i = 0; i < 50; i++)
		{
			landed += CLandFire.Hits(state, target, 2000) ? 1 : 0;
		}
		Assert.Equal(2, target.DefencesUsed);                          // no more to spend
		Assert.True(landed < 10);
	}

	[Fact]
	public void AHitIsSpreadOverTheBrigadesAndAHundredthOfItIsTakenOffAtTheEndOfTheHour()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);

		CCombatDamage.Take(state, unit, strength: 3000, organisation: 6000);
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			// 333 thousandths of each: the strength by its share of the unit's, the organisation evenly.
			Assert.Equal(999, brigade.PendingStrengthDamage);
			Assert.Equal(1998, brigade.PendingOrganisationDamage);
		}
		Assert.False(CCombatDamage.IsOutOfTheFight(unit));

		long men = CCombatDamage.Settle(unit.SubUnits[0]);
		Assert.Equal(CFixedPoint.Parse("29.991"), unit.SubUnits[0].Strength);
		Assert.Equal(CFixedPoint.Parse("29.981"), unit.SubUnits[0].Organisation);
		Assert.Equal(900, men);                                        // 0.009 of a strength is 0.9 of a man
		Assert.Equal(0, unit.SubUnits[0].PendingStrengthDamage);
	}

	[Fact]
	public void NeitherStrengthNorOrganisationIsTakenBelowNothing()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin, organisation: "0.5", strength: "0.2");
		CCombatDamage.Take(state, unit, strength: 30_000_000, organisation: 30_000_000);
		Assert.True(CCombatDamage.IsOutOfTheFight(unit));

		CCombatDamage.Settle(unit.SubUnits[0]);
		Assert.Equal(CFixedPoint.Zero, unit.SubUnits[0].Strength);
		Assert.Equal(CFixedPoint.Zero, unit.SubUnits[0].Organisation);
	}

	[Fact]
	public void TheMenLostCostOfficersAndSomeComeBack()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		germany.Officers = CFixedPoint.FromInt(100);
		germany.Manpower = CFixedPoint.FromInt(10);
		germany.Modifier.Set(CCombatDamage.Trickleback, CFixedPoint.Parse("0.3"));
		CCombatDamage.Take(state, unit, strength: 300_000, organisation: 0);     // 0.999 off each brigade

		long men = CCombatDamage.SettleUnit(state, unit);
		Assert.Equal(299_700, men);
		// 299.7 men, by a hundred officers a brigade over a hundred, by 0.007.
		Assert.Equal(CFixedPoint.Parse("97.903"), germany.Officers);
		// 299.7 men at 6.99 manpower for 90 strength, three tenths of it, over a hundred.
		Assert.Equal(CFixedPoint.Parse("10.069"), germany.Manpower);
	}

	[Fact]
	public void AUnitShattersUnderHalfItsStrengthOrLessForACountryWithOfficersToSpare()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		Assert.Equal(500, CCombatDamage.ShatterThreshold(state, germany, unit));

		germany.OfficerRatio = CFixedPoint.Parse("1.4");
		Assert.Equal(300, CCombatDamage.ShatterThreshold(state, germany, unit));

		germany.OfficerRatio = CFixedPoint.Parse("0.5");               // short of officers: no worse than a half
		Assert.Equal(500, CCombatDamage.ShatterThreshold(state, germany, unit));

		foreach (CSubUnit brigade in unit.SubUnits)
		{
			brigade.Experience = CFixedPoint.FromInt(10);
		}
		Assert.Equal(470, CCombatDamage.ShatterThreshold(state, germany, unit));
	}

	[Fact]
	public void AShatteredUnitLeavesItsBattleAndStandsAtItsCapital()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);

		CCombatDamage.Take(state, defender, strength: 6_000_000, organisation: 0);   // 20 off each of 30
		CCombatDamage.SettleUnit(state, defender);

		Assert.Equal(Warsaw, defender.Location);
		Assert.False(defender.IsFighting);
		Assert.Empty(combat.Defender.Units);
		Assert.Contains(defender, state.UnitsIn(Warsaw));
	}

	// --- the end ---

	[Fact]
	public void ASideHasLostWithNobodyOnItsFrontOrAFrontWithNoOrganisationLeft()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);
		Assert.False(CCombats.HasLost(combat.Defender));

		foreach (CSubUnit brigade in defender.SubUnits)
		{
			brigade.Organisation = CFixedPoint.One;
		}
		Assert.True(CCombats.HasLost(combat.Defender));

		foreach (CSubUnit brigade in defender.SubUnits)
		{
			brigade.Organisation = CFixedPoint.FromInt(30);
		}
		combat.Defender.Front.Clear();
		combat.Defender.Reserves.Add(defender);                        // a reserve does not save it
		Assert.True(CCombats.HasLost(combat.Defender));
	}

	[Fact]
	public void ABattleIsNotDecidedBeforeItsFourthHour()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen, organisation: "0.5");   // cannot fight at all

		CCombat combat = Fight(state, attacker, Posen);                // its first hour
		Assert.Equal(1, combat.Duration);
		Hour(state);
		Hour(state);
		Assert.Single(state.Combats);

		Hour(state);                                                   // the fourth
		Assert.Empty(state.Combats);
		Assert.True(combat.IsConcluded && !combat.AttackerLost);
		Assert.False(defender.IsFighting);
	}

	[Fact]
	public void ABeatenDefenderFallsBackAndTheAttackerWalksInAndTakesTheProvince()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen, organisation: "0.5");

		Fight(state, attacker, Posen);
		for (int i = 0; i < 3; i++)
		{
			Hour(state);
		}
		Assert.Empty(state.Combats);
		Assert.True(defender.IsRetreating);
		Assert.True(defender.IsMoving);
		Assert.Equal(Pol, state.Province(Posen).Controller);           // nobody has walked in yet

		for (int i = 0; i < 40 && attacker.Location != Posen; i++)
		{
			Hour(state);
		}
		Assert.Equal(Posen, attacker.Location);
		Assert.Equal(Ger, state.Province(Posen).Controller);
		Assert.Equal(Pol, state.Province(Posen).Owner);
		Assert.True(attacker.AttackDelay > CFixedPoint.Zero);

		for (int i = 0; i < 40 && defender.IsMoving; i++)
		{
			Hour(state);
		}
		Assert.False(defender.IsRetreating);                           // it stops falling back where it arrives
		Assert.NotEqual(Posen, defender.Location);
	}

	[Fact]
	public void ABeatenAttackerStopsWhereItStands()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin, organisation: "0.5");
		Division(state, poland, Posen);

		Fight(state, attacker, Posen);
		for (int i = 0; i < 3; i++)
		{
			Hour(state);
		}

		Assert.Empty(state.Combats);
		Assert.Equal(Berlin, attacker.Location);
		Assert.False(attacker.IsMoving);
		Assert.False(attacker.IsRetreating);
		Assert.Equal(Pol, state.Province(Posen).Controller);
	}

	[Fact]
	public void TwoDivisionsFightABattleToItsEndAndBothAreWornByIt()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen, organisation: "6");

		Fight(state, attacker, Posen);
		int hours = 1;
		for (; hours < 2000 && state.Combats.Count > 0; hours++)
		{
			Hour(state);
		}

		Assert.Empty(state.Combats);
		Assert.True(hours > CCombats.LeastHours);                      // it took fighting, not a rule
		Assert.True(CUnitTotals.AverageOrganisation(attacker) < CFixedPoint.FromInt(30));
		Assert.True(CUnitTotals.Strength(attacker) < CFixedPoint.FromInt(90));
		Assert.True(CUnitTotals.Strength(defender) < CFixedPoint.FromInt(90));
	}

	[Fact]
	public void TheSameGameFightsTheSameBattle()
	{
		long Run(uint seed)
		{
			CGameState state = State(out CCountry germany, out CCountry poland);
			state.Seed = seed;
			CUnit attacker = Division(state, germany, Berlin);
			CUnit defender = Division(state, poland, Posen);
			Fight(state, attacker, Posen);
			for (int i = 0; i < 48; i++)
			{
				Hour(state);
			}
			return CUnitTotals.AverageOrganisation(attacker).Raw * 100_000 + CUnitTotals.AverageOrganisation(defender).Raw;
		}

		Assert.Equal(Run(1), Run(1));
		Assert.NotEqual(Run(1), Run(2));
	}

	// --- falling back ---

	[Fact]
	public void TheRetreatButtonSendsEachSideWhereABeatenUnitGoesOutOfTheBattle()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);

		// The defender to the best province beside Posen, falling back: the attackers are still there.
		new CFallBackCommand(Pol, defender.Id).Execute(state);
		Assert.Equal(Warsaw, defender.Path[^1]);
		Assert.DoesNotContain(combat, defender.Combats);
		Assert.True(defender.IsRetreating);

		// The attacker, fighting from Berlin, goes to the best province beside Berlin - not stopped where it stands, as a
		// beaten attacker is - and leaves the battle with no order; with nobody left against it, not falling back.
		var attackers = new CFallBackCommand(Ger, attacker.Id);
		Assert.True(attackers.MayExecute(state));
		attackers.Execute(state);
		Assert.Equal(Breslau, attacker.Path[^1]);
		Assert.DoesNotContain(combat, attacker.Combats);
		Assert.Equal(EUnitOrder.None, attacker.Order);
		Assert.False(attacker.IsRetreating);
		Assert.False(attackers.MayExecute(state));                      // it is in no battle now
	}

	[Fact]
	public void TheRetreatButtonLeavesAUnitWithNowhereToGoWhereItIs()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Division(state, germany, Warsaw);
		Division(state, germany, Lodz);
		CCombat combat = Fight(state, attacker, Posen);
		foreach (CSubUnit brigade in defender.SubUnits)
		{
			brigade.Organisation = CFixedPoint.Parse("0.9");
		}

		// Nowhere to fall back to: nothing happens, where a beaten unit with nowhere is removed from the game.
		new CFallBackCommand(Pol, defender.Id).Execute(state);
		Assert.Contains(combat, defender.Combats);
		Assert.Contains(defender, poland.Units);
		Assert.Empty(defender.Path);
	}

	[Fact]
	public void AUnitFallsBackToTheNeighbourWorthMostAndNotToOneAnEnemyStandsIn()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit defender = Division(state, poland, Posen);

		// Warsaw and Lodz each touch Posen and one another: both are worth two thousand,
		// and of two worth the same the first found is kept. Berlin and Breslau, the
		// enemy's, are worth five hundred: one friendly neighbour, halved for the other.
		Assert.Equal(Warsaw, CCombatRetreat.FindRetreatProvince(state, defender));

		Division(state, germany, Warsaw);
		Assert.Equal(Lodz, CCombatRetreat.FindRetreatProvince(state, defender));

		// With its own country's provinces closed it falls back onto the enemy's
		// undefended ground, while it has the organisation to.
		Division(state, germany, Lodz);
		Assert.Equal(Berlin, CCombatRetreat.FindRetreatProvince(state, defender));
		foreach (CSubUnit brigade in defender.SubUnits)
		{
			brigade.Organisation = CFixedPoint.Parse("0.9");
		}
		Assert.Equal(0, CCombatRetreat.FindRetreatProvince(state, defender));
	}

	[Fact]
	public void AUnitWithNowhereToFallBackToIsRemovedFromTheGame()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen, organisation: "0.5");
		Division(state, germany, Warsaw);
		Division(state, germany, Lodz);

		Fight(state, attacker, Posen);
		for (int i = 0; i < 3; i++)
		{
			Hour(state);
		}

		Assert.True(defender.IsRemoved);
		Assert.DoesNotContain(defender, poland.Units);
		Assert.DoesNotContain(defender, state.UnitsIn(Posen));
	}

	[Fact]
	public void AUnitOrderedAwayFromABattleItIsHoldingFallsBack()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);

		var order = new CMoveUnitCommand(Pol, defender.Id, Warsaw);
		Assert.True(order.MayExecute(state));
		order.Execute(state);

		Assert.True(defender.IsRetreating);
		Assert.False(defender.IsFighting);
		Assert.True(combat.IsConcluded && !combat.AttackerLost);       // it was the only defender
	}

	[Fact]
	public void HaltingAnAttackerBreaksTheAttackOff()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);

		Assert.True(attacker.IsMoving);
		new CCancelMovementCommand(Ger, attacker.Id).Execute(state);

		Assert.False(attacker.IsFighting);
		Assert.Empty(combat.Attacker.Units);
		Assert.True(combat.AttackerLost);
		// Its way and its order go with the attack.
		Assert.False(attacker.IsMoving);
		Assert.Equal(CFixedPoint.Zero, attacker.MovementProgress);
		Assert.Equal(EUnitOrder.None, attacker.Order);
	}

	// --- taking a province ---

	[Fact]
	public void GroundTakenFromAnEnemyGoesToWhoeverWalkedInAndGroundTakenBackToTheFriendWhoOwnsIt()
	{
		CGameState state = State(out _, out _);
		CProvince posen = state.Province(Posen);

		Assert.True(CProvinceControl.Take(state, posen, Ger));
		Assert.Equal(Ger, posen.Controller);
		Assert.False(CProvinceControl.Take(state, posen, Ger));        // it already holds it

		// Poland walks back in: its own ground.
		Assert.True(CProvinceControl.Take(state, posen, Pol));
		Assert.Equal(Pol, posen.Controller);

		// The Soviets, at war with nobody, change nothing by standing there.
		Assert.False(CProvinceControl.Take(state, posen, Sov));

		// At war with Germany, they hand a province Germany holds back to its Polish owner.
		posen.Controller = Ger;
		state.Wars.Add(War(Sov, Ger));
		Assert.True(CProvinceControl.Take(state, posen, Sov));
		Assert.Equal(Pol, posen.Controller);
	}

	[Fact]
	public void APuppetsConquestGoesToItsMasterWhileTheMasterIsAtWarWithTheHolder()
	{
		CGameState state = State(out _, out _);
		state.Country(Sov)!.Overlord = Ger;
		state.Wars[0].Attackers.Add(Sov);

		Assert.True(CProvinceControl.Take(state, state.Province(Warsaw), Sov));
		Assert.Equal(Ger, state.Province(Warsaw).Controller);

		// In a war of its own, its master at peace with Poland, it keeps what it takes.
		state.Wars.Clear();
		state.Wars.Add(War(Sov, Pol));
		Assert.True(CProvinceControl.Take(state, state.Province(Lodz), Sov));
		Assert.Equal(Sov, state.Province(Lodz).Controller);
	}

	[Fact]
	public void GroundNoneOfWhoseNeighboursTheTakerHoldsGoesToAFightingFriendWhoHoldsOne()
	{
		CGameState state = State(out _, out _);
		state.Wars[0].Attackers.Add(Sov);
		state.Province(Posen).Controller = Ger;

		// Lodz touches Posen, which Germany holds, and Warsaw, which Poland does.
		Assert.True(CProvinceControl.Take(state, state.Province(Lodz), Sov));
		Assert.Equal(Ger, state.Province(Lodz).Controller);

		// Holding Warsaw beside it, the Soviets keep it.
		state.Province(Lodz).Controller = Pol;
		state.Province(Warsaw).Controller = Sov;
		Assert.True(CProvinceControl.Take(state, state.Province(Lodz), Sov));
		Assert.Equal(Sov, state.Province(Lodz).Controller);
	}

	[Fact]
	public void AnExilesConquestGoesToTheHeadOfItsFactionOnlyWhereTheHeadIsAnEnemyOfTheOwner()
	{
		CGameState state = State(out _, out CCountry poland);
		CCountry soviets = state.Country(Sov)!;
		soviets.Faction = poland.Faction = "comintern";
		soviets.FactionSeat = 1;
		poland.FactionSeat = 2;
		poland.InExile = true;
		CProvince breslau = state.Province(Breslau);

		Assert.True(CProvinceControl.Take(state, breslau, Pol));
		Assert.Equal(Pol, breslau.Controller);

		breslau.Controller = Ger;
		state.Wars[0].Defenders.Add(Sov);
		Assert.True(CProvinceControl.Take(state, breslau, Pol));
		Assert.Equal(Sov, breslau.Controller);
	}

	[Fact]
	public void ACapitalTakenInBattleIsLootedOfAQuarter()
	{
		CGameState state = State(out _, out _);
		state.Province(Warsaw).Pool[EGoods.Money] = CFixedPoint.FromInt(40);

		Assert.True(CProvinceControl.Take(state, state.Province(Warsaw), Ger));
		Assert.Equal(CFixedPoint.FromInt(30), state.Province(Warsaw).Pool[EGoods.Money]);
		Assert.Equal(CFixedPoint.FromInt(10), state.Province(Berlin).Pool[EGoods.Money]);
	}

	[Fact]
	public void ADefendingFrontUnitThatFallsBackMayDamageTheProvincesBuildings()
	{
		var industry = new CBuilding(0, "industry", 10, "", default);
		CGameState state = State(out CCountry germany, out CCountry poland, buildings: new[] { industry });
		state.Defines.Set(CProvinceHands.Section, "COMBAT_PUSHBACK_CHANCE_FOR_DAMAGE", CFixedPoint.FromInt(100));
		state.Defines.Set(CProvinceHands.Section, "COMBAT_PUSHBACK_DAMAGE", CFixedPoint.One);
		state.Province(Posen).SetBuildingLevel(industry, CFixedPoint.FromInt(3));
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Fight(state, attacker, Posen);

		// The attacker giving way leaves the buildings be.
		attacker.SubUnits.ForEach(brigade => brigade.Organisation = CFixedPoint.Zero);
		CFrontLine.Update(state, combat, combat.Attacker);
		Assert.Equal(CFixedPoint.FromInt(3), state.Province(Posen).BuildingLevel(industry));

		defender.SubUnits.ForEach(brigade => brigade.Organisation = CFixedPoint.Zero);
		CFrontLine.Update(state, combat, combat.Defender);
		Assert.Equal(CFixedPoint.FromInt(2), state.Province(Posen).BuildingLevel(industry));
		Assert.Equal(CFixedPoint.FromInt(3), state.Province(Posen).BuildingMaxLevel(industry));
	}

	[Fact]
	public void AUnitStandingInAnUndefendedEnemyProvinceTakesItTheHourItArrives()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		unit.Path.Add(Posen);

		for (int i = 0; i < 10 && unit.Location != Posen; i++)
		{
			Hour(state);
		}
		Assert.Equal(Posen, unit.Location);
		Assert.Equal(Ger, state.Province(Posen).Controller);
		Assert.Empty(state.Combats);
	}

	// --- the attack delay ---

	[Fact]
	public void AnAttackStartsADelayOfAWeekOrSoMoreForACountryShortOfOfficersAndLessForItsDoctrine()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		state.SeedHour();

		CAttackDelay.Start(state, germany, unit);
		Assert.InRange(unit.AttackDelay.Raw, 168_000, 177_000);
		long whole = unit.AttackDelay.Raw;

		state.SeedHour();
		germany.OfficerRatio = CFixedPoint.Parse("0.5");
		CAttackDelay.Start(state, germany, unit);
		Assert.Equal(whole + 36_000, unit.AttackDelay.Raw);

		state.SeedHour();
		germany.Technology.Set(CAttackDelay.TechnologyAttackDelay, CFixedPoint.FromInt(48));
		CAttackDelay.Start(state, germany, unit);
		Assert.Equal(whole + 36_000 - 48_000, unit.AttackDelay.Raw);

		germany.Technology.Set(CAttackDelay.TechnologyAttackDelay, CFixedPoint.FromInt(1000));
		CAttackDelay.Start(state, germany, unit);
		Assert.Equal(CFixedPoint.FromInt(12), unit.AttackDelay);
	}

	[Fact]
	public void AfterAShortAttackTheDelayIsCutToTheShareOfTwelveHoursItLasted()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		unit.AttackDelay = CFixedPoint.FromInt(170);
		unit.HoursAttacking = 6;

		CAttackDelay.RunHour(state, germany, unit);
		Assert.Equal(CFixedPoint.Parse("84.5"), unit.AttackDelay);     // 169 by six twelfths
		Assert.Equal(0, unit.HoursAttacking);

		CAttackDelay.RunHour(state, germany, unit);
		Assert.Equal(CFixedPoint.Parse("83.5"), unit.AttackDelay);     // an hour an hour from here

		unit.AttackDelay = CFixedPoint.FromInt(170);
		unit.HoursAttacking = 13;                                      // a long attack is not cut
		CAttackDelay.RunHour(state, germany, unit);
		Assert.Equal(CFixedPoint.FromInt(169), unit.AttackDelay);
	}

	[Fact]
	public void AnHourOfAttackingTakesAQuarterOfAnHourOffTheDelay()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		Fight(state, attacker, Posen);
		Assert.Equal(CFixedPoint.Zero, attacker.AttackDelay);          // it had not taken a step when its delay was seen to

		Hour(state);
		long started = attacker.AttackDelay.Raw;
		Assert.InRange(started, 168_000 - 250, 177_000 - 250);
		Assert.Equal(1, attacker.HoursAttacking);

		Hour(state);
		Assert.Equal(started - 250, attacker.AttackDelay.Raw);
	}

	[Fact]
	public void AUnitWithADelayRunningDoesNotTakeItsFirstStep()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		unit.AttackDelay = CFixedPoint.FromInt(3);
		unit.Path.Add(Breslau);

		Hour(state);
		Hour(state);
		Assert.Equal(CFixedPoint.Zero, unit.MovementProgress);

		Hour(state);                                                   // the delay is run out before it moves
		Assert.True(unit.MovementProgress > CFixedPoint.Zero);
	}

	// --- speed and organisation ---

	[Fact]
	public void AnAttackerWalksOnAtAThirdOfItsSpeedAndAUnitFallingBackATenthFaster()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Assert.Equal(CFixedPoint.Parse("0.2"), CMovementSpeed.Speed(state, germany, attacker));

		Fight(state, attacker, Posen);
		Assert.Equal(330, CMovementSpeed.Modifier(state, germany, attacker));
		Assert.Equal(CFixedPoint.Parse("0.066"), CMovementSpeed.Speed(state, germany, attacker));

		// A defender given somewhere to go without leaving the battle all but stands.
		defender.Path.Add(Warsaw);
		Assert.Equal(10, CMovementSpeed.Modifier(state, poland, defender));

		CCombats.LeaveAll(state, defender);
		defender.IsRetreating = true;
		Assert.Equal(1100, CMovementSpeed.Modifier(state, poland, defender));
	}

	[Fact]
	public void TheTechnologiesAttackMovementSpeedQuickensAnAttackersWalk()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		Fight(state, attacker, Posen);
		germany.Technology.Set(CAttackDelay.TechnologyAttackMovementSpeed, CFixedPoint.Parse("0.1"));

		// 1.1 x 0.33, as the country's combat movement speed would be.
		Assert.Equal(363, CMovementSpeed.Modifier(state, germany, attacker));
	}

	[Fact]
	public void TheSpeedTooltipsTermsGiveADefenderOneWhereTheModifierGivesAHundredth()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Fight(state, attacker, Posen);
		defender.Path.Add(Warsaw);

		Assert.Equal(330, CMovementSpeed.Terms(state, germany, attacker).Combat);
		Assert.Equal(10, CMovementSpeed.Modifier(state, poland, defender));
		Assert.Equal(1000, CMovementSpeed.Terms(state, poland, defender).Combat);

		CCombats.LeaveAll(state, defender);
		defender.IsRetreating = true;
		Assert.Equal(1100, CMovementSpeed.Terms(state, poland, defender).Retreating);
	}

	[Fact]
	public void AnAttackersWalkIsHeldAtTheFarEndWhileItFights()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		Division(state, poland, Posen);
		Fight(state, attacker, Posen);

		for (int i = 0; i < 40 && state.Combats.Count > 0; i++)
		{
			Hour(state);
			Assert.Equal(Berlin, attacker.Location);
			Assert.True(attacker.MovementProgress <= CFixedPoint.One);
		}
	}

	[Fact]
	public void NoOrganisationComesBackWhileAUnitFightsOrMarchesOntoAnEnemysGround()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		Assert.False(COrganisation.IsMovingOntoEnemyGround(state, unit));

		unit.Path.Add(Breslau);
		Assert.False(COrganisation.IsMovingOntoEnemyGround(state, unit));

		unit.Path[0] = Posen;
		Assert.True(COrganisation.IsMovingOntoEnemyGround(state, unit));

		unit.AttackDelay = CFixedPoint.One;                            // held back, it is not on its way yet
		Assert.False(COrganisation.IsMovingOntoEnemyGround(state, unit));
	}

	// --- the command that makes a war ---

	[Fact]
	public void TheWarCommandPutsTwoCountriesAtWarOnce()
	{
		CGameState state = State(out _, out _, atWar: false);
		var command = new CStartWarCommand(Ger, Pol);
		Assert.True(command.MayExecute(state));
		command.Execute(state);

		Assert.True(CMovementRules.IsEnemy(state, Ger, Pol));
		Assert.False(command.MayExecute(state));
		Assert.False(new CStartWarCommand(Pol, Ger).MayExecute(state));
		Assert.False(new CStartWarCommand(Ger, Ger).MayExecute(state));
	}

	[Fact]
	public void AUnitTrespassesWhereItsCountryMayNotGoAndNeverARebelUnit()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, atWar: false);
		CUnit abroad = Division(state, germany, Posen);
		CUnit home = Division(state, germany, Berlin);
		var rebels = new CUnit(EUnitKind.Division, new CCountryTag("REB")) { Id = ++state.LastUnitId, Location = Posen };

		Assert.True(CTrespassing.Is(state, abroad));
		Assert.False(CTrespassing.Is(state, home));
		Assert.False(CTrespassing.Is(state, rebels));

		// A war is itself leave to enter.
		state.Wars.Add(War(Ger, Pol));
		Assert.False(CTrespassing.Is(state, abroad));
	}

	[Fact]
	public void ATrespasserStartsNoFightAndIsFoughtOnlyInAWar()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, atWar: false);
		var soviets = state.Country(Sov)!;
		state.Wars.Add(War(Ger, Sov));
		CUnit trespasser = Division(state, germany, Posen);
		CUnit enemy = Division(state, soviets, Posen);

		// Both stand on Poland's ground uninvited: neither may be the one that asks.
		Assert.False(CCombats.MayEngage(state, trespasser, enemy));
		Assert.False(CCombats.MayEngage(state, enemy, trespasser));

		// On Germany's own ground the two fight: the Soviet division is there by its war.
		trespasser.Location = Berlin;
		enemy.Location = Berlin;
		state.ForgetPositions();
		Assert.True(CCombats.MayEngage(state, trespasser, enemy));
		Assert.True(CCombats.MayEngage(state, enemy, trespasser));
	}

	[Fact]
	public void ATrespasserMayWalkOnThroughItsHostsLand()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, atWar: false);
		CUnit abroad = Division(state, germany, Posen);
		CUnit home = Division(state, germany, Berlin);

		Assert.True(CMovementRules.MayStandIn(state, germany, abroad, state.Province(Lodz)));
		Assert.False(CMovementRules.MayStandIn(state, germany, home, state.Province(Posen)));
	}

	[Fact]
	public void ATrespasserIsNotSlowedForWantOfFuel()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, atWar: false);
		CUnit abroad = Division(state, germany, Posen);
		CUnit home = Division(state, germany, Berlin);
		foreach (CUnit unit in new[] { abroad, home })
		{
			unit.FuelReceived = CFixedPoint.Zero;
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				brigade.Stats.Values.Set(CUnitSupply.FuelStatistic, CFixedPoint.One);
			}
		}

		long slowed = CMovementSpeed.Modifier(state, germany, home);
		Assert.Equal(slowed * 10, CMovementSpeed.Modifier(state, germany, abroad));
	}
}
