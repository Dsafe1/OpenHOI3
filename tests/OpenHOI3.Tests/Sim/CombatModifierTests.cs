using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What an hour makes of a unit in a battle, against <c>docs/mechanics/landcombat.md</c>,
/// section 4. Posen is fought over; Berlin, Breslau and Stettin are Germany's and each
/// touches it, and Warsaw is behind it. The defines are the base game's.
/// </summary>
public class CombatModifierTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Posen = 2;
	private const int Warsaw = 3;
	private const int Breslau = 4;
	private const int Stettin = 5;

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
		Set(CUnitTotals.Softness, "1");
		return type;
	}

	private static CProvinceEdge Edge(int to, EProvinceEdgeKind kind = EProvinceEdgeKind.Plain)
	{
		return new CProvinceEdge(to, kind, 0, CFixedPoint.One);
	}

	private static CGameState State(out CCountry germany, out CCountry poland, CTerrain? terrain = null,
		EProvinceEdgeKind berlinToPosen = EProvinceEdgeKind.Plain)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Warsaw };
		var defines = new CDefines();
		void Define(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Define(CFrontLine.BaseWidth, "10");
		Define(CFrontLine.AdditionalWidth, "5");
		Define(CCombatModifiers.RiverPenalty, "-0.6");
		Define(CCombatModifiers.LandingPenalty, "-0.7");
		Define(CCombatModifiers.FortPenalty, "-0.09");
		Define(CCombatModifiers.MultipleCombatsPenalty, "0.5");
		Define(CCombatModifiers.DissentImpact, "0.02");
		Define(CCombatModifiers.SupplyLackImpact, "0.5");
		Define(CCombatModifiers.DigInFactor, "0.02");
		Define(CCombatModifiers.StackingPenalty, "-0.1");
		Define(CCombatModifiers.NightPenalty, "-0.5");
		Define(CCombatModifiers.EnvelopmentPenalty, "0.1");
		Define(CDigIn.Cap, "10");
		Define(CReinforcement.PrideBonus, "10");

		CProvince Land(int id, string name, CCountryTag owner, int x, params CProvinceEdge[] edges)
		{
			return new CProvince(id, name, isSea: false)
			{
				Owner = owner, Controller = owner, Edges = edges, PositionX = x, PositionY = 500, CentreY = 500,
				Terrain = id == Posen ? terrain : null,
			};
		}

		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Berlin, "Berlin", Ger, 500, Edge(Posen, berlinToPosen)),
				Land(Posen, "Posen", Pol, 500, Edge(Berlin, berlinToPosen), Edge(Warsaw), Edge(Breslau), Edge(Stettin)),
				Land(Warsaw, "Warsaw", Pol, 500, Edge(Posen)),
				Land(Breslau, "Breslau", Ger, 500, Edge(Posen)),
				Land(Stettin, "Stettin", Ger, 500, Edge(Posen)),
			},
			new[] { germany, poland }, defines: defines, wars: new[] { war })
		{
			MapWidth = 1000, MapHeight = 1000,
		};
	}

	private static CUnit Division(CGameState state, CCountry country, int province)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = "Division" };
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
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static void Adjust(CUnit unit, string name, string kind, string value)
	{
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (!subUnit.Stats.Adjusters.TryGetValue(name, out CModifier? adjuster))
			{
				subUnit.Stats.Adjusters[name] = adjuster = new CModifier(name);
			}
			adjuster.Set(kind, CFixedPoint.Parse(value));
		}
	}

	/// <summary>A battle for Posen, set up and with its modifiers worked out once at the hour given.</summary>
	private static CCombat Battle(CGameState state, CUnit attacker, CUnit defender, int hour = 0)
	{
		for (int i = 0; i < hour; i++)
		{
			state.AdvanceOneHour();
		}
		var combat = new CCombat(Posen);
		state.Combats.Add(combat);
		combat.Attacker.Add(attacker);
		combat.Defender.Add(defender);
		CCombats.Begin(state, combat);
		Work(state, combat);
		return combat;
	}

	private static void Work(CGameState state, CCombat combat)
	{
		CCombatModifiers.Apply(state, combat, combat.Attacker);
		CCombatModifiers.Apply(state, combat, combat.Defender);
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

	/// <summary>The middle of the day at the middle of the map, whose clock is the game's own.</summary>
	private const int Noon = 12;

	// --- the arithmetic ---

	[Fact]
	public void WithNothingForOrAgainstItAUnitFightsAtWhatItIs()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Battle(state, attacker, defender, Noon);

		Assert.Empty(attacker.CombatModifiers);
		Assert.Equal(CFixedPoint.One, attacker.CombatAttack);
		Assert.Equal(CFixedPoint.One, defender.CombatDefence);
	}

	[Fact]
	public void EachThingIsAFactorMultipliedInAndNoneLeavesLessThanAHundredth()
	{
		var forest = new CTerrain("forest", isWater: false, default) { Attack = CFixedPoint.Parse("-0.2") };
		CGameState state = State(out CCountry germany, out CCountry poland, forest);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		foreach (CSubUnit brigade in attacker.SubUnits)
		{
			brigade.Experience = CFixedPoint.FromInt(10);
		}
		CCombat combat = Battle(state, attacker, defender, Noon);

		// A tenth more for experience, a fifth less for the forest: 1.1 by 0.8.
		Assert.Equal(CFixedPoint.Parse("0.88"), attacker.CombatAttack);
		Assert.Equal(CFixedPoint.Parse("0.88"), attacker.CombatDefence);
		Assert.Equal(new[] { ECombatModifier.Experience, ECombatModifier.Terrain },
			attacker.CombatModifiers.ConvertAll(modifier => modifier.Kind));

		Adjust(attacker, "forest", CCombatModifiers.AttackOf, "-3");
		Work(state, combat);
		Assert.Equal(CFixedPoint.Parse("0.011"), attacker.CombatAttack);     // 1.1 by a hundredth
	}

	// --- the ground ---

	[Fact]
	public void TheGroundIsItsOwnFigureAndTheUnitsWayWithItForAttackerAndDefenderEach()
	{
		var hills = new CTerrain("hills", isWater: false, default)
		{
			Attack = CFixedPoint.Parse("-0.3"), Defence = CFixedPoint.Parse("0.1"),
		};
		CGameState state = State(out CCountry germany, out CCountry poland, hills);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Adjust(attacker, "hills", CCombatModifiers.AttackOf, "0.1");
		Adjust(defender, "hills", CCombatModifiers.DefenceOf, "0.05");
		Battle(state, attacker, defender, Noon);

		Assert.Equal(CFixedPoint.Parse("-0.2"), Of(attacker, ECombatModifier.Terrain));
		Assert.Equal(CFixedPoint.Parse("0.15"), Of(defender, ECombatModifier.Terrain));
	}

	[Fact]
	public void ARiverIsAgainstTheAttackerWhoCrossesItAndNobodyElse()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, berlinToPosen: EProvinceEdgeKind.River);
		CUnit across = Division(state, germany, Berlin);
		CUnit dryShod = Division(state, germany, Breslau);
		CUnit defender = Division(state, poland, Posen);
		Adjust(across, CCombatModifiers.RiverAdjuster, CCombatModifiers.AttackOf, "0.2");
		CCombat combat = Battle(state, across, defender, Noon);
		combat.Attacker.Add(dryShod);
		Work(state, combat);

		Assert.Equal(CFixedPoint.Parse("-0.4"), Of(across, ECombatModifier.River));
		Assert.Equal(CFixedPoint.Zero, Of(dryShod, ECombatModifier.River));
		Assert.Equal(CFixedPoint.Zero, Of(defender, ECombatModifier.River));

		// A unit that makes light of rivers altogether has nothing held against it.
		Adjust(across, CCombatModifiers.RiverAdjuster, CCombatModifiers.AttackOf, "0.7");
		Work(state, combat);
		Assert.Equal(CFixedPoint.Zero, Of(across, ECombatModifier.River));
	}

	[Fact]
	public void AStraitCountsAsALandingAndIsMetByTheCoastalForts()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, berlinToPosen: EProvinceEdgeKind.Crossing);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		state.Province(Posen).Modifier.Set(CCombatModifiers.FortLevel, CFixedPoint.FromInt(5));
		state.Province(Posen).Modifier.Set(CCombatModifiers.CoastalFortLevel, CFixedPoint.FromInt(1));
		CCombat combat = Battle(state, attacker, defender, Noon);

		Assert.Equal(CFixedPoint.Parse("-0.7"), Of(attacker, ECombatModifier.Amphibious));
		Assert.Equal(CFixedPoint.Parse("-0.09"), Of(attacker, ECombatModifier.Fort));

		Adjust(attacker, CCombatModifiers.AmphibiousAdjuster, CCombatModifiers.AttackOf, "-1.1");
		Work(state, combat);
		Assert.Equal(CFixedPoint.Parse("-0.99"), Of(attacker, ECombatModifier.Amphibious));
	}

	[Fact]
	public void AFortIsAgainstTheAttackerByItsWholeLevelsAndNeverForHim()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		state.Province(Posen).Modifier.Set(CCombatModifiers.FortLevel, CFixedPoint.Parse("2.9"));
		CCombat combat = Battle(state, attacker, defender, Noon);

		Assert.Equal(CFixedPoint.Parse("-0.18"), Of(attacker, ECombatModifier.Fort));
		Assert.Equal(CFixedPoint.Zero, Of(defender, ECombatModifier.Fort));

		Adjust(attacker, CCombatModifiers.FortAdjuster, CCombatModifiers.AttackOf, "0.5");
		Work(state, combat);
		Assert.Equal(CFixedPoint.Zero, Of(attacker, ECombatModifier.Fort));
	}

	// --- the hour and the sky ---

	[Fact]
	public void TheNightIsAgainstAUnitsAttackAndLeavesItsDefenceAlone()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Adjust(defender, CCombatModifiers.NightAdjuster, CCombatModifiers.AttackOf, "0.2");
		Battle(state, attacker, defender, hour: 0);                    // midnight at the middle of the map

		Assert.Equal(CFixedPoint.Parse("0.5"), attacker.CombatAttack);
		Assert.Equal(CFixedPoint.One, attacker.CombatDefence);
		Assert.Equal(CFixedPoint.Parse("0.7"), defender.CombatAttack);
		Assert.Equal(CFixedPoint.One, defender.CombatDefence);
	}

	[Fact]
	public void ItIsNightBeforeSixAndAfterEighteenByAProvincesOwnClock()
	{
		CGameState state = State(out _, out _);
		CProvince middle = state.Province(Posen);                      // half way across: the game's own clock
		var west = new CProvince(9, "West", isSea: false) { PositionX = 0, PositionY = 500, CentreY = 500 };

		Assert.False(CDaylight.IsNight(state, middle, hour: 12, month: 0));
		Assert.False(CDaylight.IsNight(state, middle, hour: 18, month: 0));  // eighteen is still day
		Assert.True(CDaylight.IsNight(state, middle, hour: 19, month: 0));
		Assert.True(CDaylight.IsNight(state, middle, hour: 5, month: 0));
		Assert.False(CDaylight.IsNight(state, middle, hour: 6, month: 0));   // and six is day already

		// The map's western edge is twelve hours on.
		Assert.False(CDaylight.IsNight(state, west, hour: 0, month: 0));
		Assert.True(CDaylight.IsNight(state, west, hour: 12, month: 0));
	}

	[Fact]
	public void TheDayIsLongerInTheNorthernSummerAndTheSouthIsSixMonthsOn()
	{
		CGameState state = State(out _, out _);
		// The far north: June gives twelve hours more, six at each end, and December
		// takes as many away - which leaves the one hour of noon.
		var north = new CProvince(9, "North", isSea: false) { PositionX = 500, PositionY = 1000, CentreY = 1000 };
		Assert.Equal(1000, CDaylight.LatitudeNorth(state, north));
		for (int hour = 0; hour < 24; hour++)
		{
			Assert.False(CDaylight.IsNight(state, north, hour, month: 5));
			Assert.Equal(hour != 12, CDaylight.IsNight(state, north, hour, month: 11));
		}

		// Half way north, three hours more at each end in June: from three to twenty-one.
		var temperate = new CProvince(10, "Temperate", isSea: false) { PositionX = 500, PositionY = 750, CentreY = 750 };
		Assert.False(CDaylight.IsNight(state, temperate, hour: 3, month: 5));
		Assert.True(CDaylight.IsNight(state, temperate, hour: 2, month: 5));
		Assert.False(CDaylight.IsNight(state, temperate, hour: 21, month: 5));
		Assert.True(CDaylight.IsNight(state, temperate, hour: 3, month: 2));     // nothing added in March

		// South of the equator the latitude is nought, and its day is always six to eighteen.
		var south = new CProvince(11, "South", isSea: false) { PositionX = 500, PositionY = 100, CentreY = 100 };
		Assert.Equal(0, CDaylight.LatitudeNorth(state, south));
		Assert.False(CDaylight.IsNight(state, south, hour: 18, month: 5));
		Assert.True(CDaylight.IsNight(state, south, hour: 19, month: 5));
	}

	[Fact]
	public void RainColdAndHeatAreAgainstAUnitsAttack()
	{
		CGameState state = State(out CCountry germany, out _);
		void Define(string name, string value) => state.Defines.Set(CWeatherEffects.WeatherSection, name, CFixedPoint.Parse(value));
		Define(CWeatherEffects.LandRainImpactModifier, "0.005");
		Define(CWeatherEffects.LandRainImpactCap, "0.9");
		Define(CWeatherEffects.LandLowTemperatureThreshold, "-1");
		Define(CWeatherEffects.LandLowTemperatureImpact, "0.75");
		Define(CWeatherEffects.LandHighTemperatureThreshold, "20");
		Define(CWeatherEffects.LandHighTemperatureImpact, "0.07");
		var jungle = new CTerrain("jungle", isWater: false, default) { Humidity = CFixedPoint.Parse("0.2") };

		var mild = new CWeather { Temperature = CFixedPoint.FromInt(10) };
		Assert.Equal(0, CWeatherEffects.LandCombat(state, mild, null, germany));

		// Ten of rain in a wind of five: ten by ten by 0.005.
		var wet = new CWeather { Temperature = CFixedPoint.FromInt(10), Precipitation = CFixedPoint.FromInt(10), WindSpeed = CFixedPoint.FromInt(5) };
		Assert.Equal(500, CWeatherEffects.LandCombat(state, wet, null, germany));
		wet.Precipitation = CFixedPoint.FromInt(100);
		Assert.Equal(900, CWeatherEffects.LandCombat(state, wet, null, germany));       // the cap

		// Twenty below: a twentieth of twenty-one, by 0.75.
		var cold = new CWeather { Temperature = CFixedPoint.FromInt(-20) };
		Assert.Equal(787, CWeatherEffects.LandCombat(state, cold, null, germany));
		germany.Modifier.Set(CWeatherEffects.WinterEffects, CFixedPoint.Parse("0.5"));
		Assert.Equal(393, CWeatherEffects.LandCombat(state, cold, null, germany));

		var hot = new CWeather { Temperature = CFixedPoint.FromInt(30) };
		Assert.Equal(70, CWeatherEffects.LandCombat(state, hot, null, germany));
		germany.Modifier.Set(CWeatherEffects.JungleEffects, CFixedPoint.Parse("0.5"));
		Assert.Equal(70, CWeatherEffects.LandCombat(state, hot, null, germany));        // only on humid ground
		Assert.Equal(35, CWeatherEffects.LandCombat(state, hot, jungle, germany));
	}

	[Fact]
	public void TheWeatherOverTheBattleIsAgainstBothSidesAttack()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Defines.Set(CWeatherEffects.WeatherSection, CWeatherEffects.LandHighTemperatureThreshold, CFixedPoint.FromInt(20));
		state.Defines.Set(CWeatherEffects.WeatherSection, CWeatherEffects.LandHighTemperatureImpact, CFixedPoint.Parse("0.07"));
		state.Defines.Set(CWeatherEffects.WeatherSection, CWeatherEffects.LandLowTemperatureThreshold, CFixedPoint.FromInt(-1));
		state.Province(Posen).Weather.Temperature = CFixedPoint.FromInt(30);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Battle(state, attacker, defender, Noon);

		Assert.Equal(CFixedPoint.Parse("0.93"), attacker.CombatAttack);
		Assert.Equal(CFixedPoint.One, attacker.CombatDefence);
		Assert.Equal(CFixedPoint.Parse("0.93"), defender.CombatAttack);
	}

	// --- the unit and its country ---

	[Fact]
	public void ExperienceSuppliesAndDissentEachHaveTheirShare()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		attacker.SubUnits[0].Experience = CFixedPoint.FromInt(45);     // fifteen between the three
		attacker.SuppliesReceived = CFixedPoint.Parse("0.4");
		poland.Dissent = CFixedPoint.FromInt(50);
		Battle(state, attacker, defender, Noon);

		Assert.Equal(CFixedPoint.Parse("0.15"), Of(attacker, ECombatModifier.Experience));
		Assert.Equal(CFixedPoint.Parse("-0.3"), Of(attacker, ECombatModifier.LackOfSupplies));   // half of the six tenths missing
		Assert.Equal(CFixedPoint.Parse("-0.01"), Of(defender, ECombatModifier.Dissent));         // 0.02 by half a point
		Assert.Equal(CFixedPoint.Zero, Of(attacker, ECombatModifier.Dissent));
	}

	[Fact]
	public void AUnitFightsBetterOnItsCountrysCoresByTheCountrysOwnFigure()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Province(Posen).Cores.Add(Pol);
		poland.Modifier.Set(CCombatModifiers.TerritorialPride, CFixedPoint.Parse("0.1"));
		germany.Modifier.Set(CCombatModifiers.TerritorialPride, CFixedPoint.Parse("0.1"));
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		Battle(state, attacker, defender, Noon);

		Assert.Equal(CFixedPoint.Parse("0.1"), Of(defender, ECombatModifier.TerritorialPride));
		Assert.Equal(CFixedPoint.Zero, Of(attacker, ECombatModifier.TerritorialPride));
	}

	// --- digging in ---

	[Fact]
	public void AUnitAtWarThatStaysPutDigsInALevelADayUpToTheCap()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit unit = Division(state, germany, Berlin);
		for (int day = 0; day < 12; day++)
		{
			CDigIn.RunDaily(state);
		}
		Assert.Equal(CFixedPoint.FromInt(10), unit.DigIn);

		unit.Path.Add(Breslau);
		CDigIn.RunHour(state, germany, unit);
		Assert.Equal(CFixedPoint.Zero, unit.DigIn);
		CDigIn.RunDaily(state);
		Assert.Equal(CFixedPoint.Zero, unit.DigIn);                    // nor does a unit on the march dig

		unit.Path.Clear();
		CDigIn.RunDaily(state);
		Assert.Equal(CFixedPoint.One, unit.DigIn);
		state.Wars.Clear();
		CDigIn.RunDaily(state);
		Assert.Equal(CFixedPoint.Zero, unit.DigIn);                    // a country at peace has nobody dug in
	}

	[Fact]
	public void BeingDugInHelpsADefenderAndNotAnAttacker()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		attacker.DigIn = CFixedPoint.FromInt(5);
		defender.DigIn = CFixedPoint.FromInt(5);
		Battle(state, attacker, defender, Noon);

		Assert.Equal(CFixedPoint.Parse("0.1"), Of(defender, ECombatModifier.DugIn));
		Assert.Equal(CFixedPoint.Zero, Of(attacker, ECombatModifier.DugIn));
	}

	// --- how many, and from where ---

	[Fact]
	public void ASideIsCrowdedByEachUnitPastThreeForEachDirectionOfTheAttack()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit first = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Battle(state, first, defender, Noon);
		for (int i = 0; i < 4; i++)
		{
			combat.Attacker.Add(Division(state, germany, Berlin));     // into reserve: the front is set up
		}
		Work(state, combat);

		// One on the front and four who could fight behind it, less three: two, so once.
		Assert.Equal(100, CCombatModifiers.Stacking(state, combat.Attacker, 1));
		Assert.Equal(CFixedPoint.Parse("-0.1"), Of(first, ECombatModifier.Stacking));
		Assert.Equal(0, CCombatModifiers.Stacking(state, combat.Defender, 1));

		// A sixth makes three: twice, a tenth and then a tenth of what is left.
		combat.Attacker.Add(Division(state, germany, Berlin));
		Assert.Equal(190, CCombatModifiers.Stacking(state, combat.Attacker, 1));

		// From a second direction three more are free.
		Assert.Equal(0, CCombatModifiers.Stacking(state, combat.Attacker, 2));
	}

	[Fact]
	public void ADefenderAttackedFromMoreThanTwoProvincesIsEnveloped()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit first = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Battle(state, first, defender, Noon);
		combat.Attacker.Add(Division(state, germany, Breslau));
		Work(state, combat);
		Assert.Equal(2, CCombatModifiers.Directions(combat));
		Assert.Equal(CFixedPoint.Zero, Of(defender, ECombatModifier.Envelopment));

		combat.Attacker.Add(Division(state, germany, Stettin));
		Work(state, combat);
		Assert.Equal(3, CCombatModifiers.Directions(combat));
		Assert.Equal(CFixedPoint.Parse("-0.1"), Of(defender, ECombatModifier.Envelopment));
		Assert.Equal(CFixedPoint.Zero, Of(first, ECombatModifier.Envelopment));
	}

	[Fact]
	public void ADefenderInTwoBattlesAtOnceFightsEachAtHalf()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Battle(state, attacker, defender, Noon);
		Assert.Equal(CFixedPoint.Zero, Of(defender, ECombatModifier.MultipleCombats));

		var elsewhere = new CCombat(Warsaw);
		elsewhere.Attacker.Add(defender);
		Work(state, combat);
		Assert.Equal(CFixedPoint.Parse("-0.5"), Of(defender, ECombatModifier.MultipleCombats));
	}

	// --- what it comes to ---

	[Fact]
	public void AUnitFiresAsManyShotsAsItsAttackTimesWhatTheHourMakesOfIt()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		void Define(string name, string value) => state.Defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Define(CLandFire.OrganisationDice, "1");
		Define(CLandFire.StrengthDice, "1");
		Define(CLandFire.OrganisationDamage, "1");
		Define(CLandFire.StrengthDamage, "0");
		Define(CLandFire.ChanceToAvoid, "0");                          // every shot lands
		Define(CLandFire.ChanceToAvoidUndefended, "0");
		CUnit attacker = Division(state, germany, Berlin);
		CUnit defender = Division(state, poland, Posen);
		CCombat combat = Battle(state, attacker, defender, Noon);
		state.SeedHour();

		// Six of soft attack: six shots, each a two for a unit at full strength, of
		// which a brigade of three takes a third, cut.
		CLandFire.Fire(state, combat, combat.Attacker, new CCombatStream(1), attacker, combat.Defender);
		Assert.Equal(6 * 666, defender.SubUnits[0].PendingOrganisationDamage);

		defender.SubUnits.ForEach(brigade => brigade.PendingOrganisationDamage = 0);
		attacker.CombatAttack = CFixedPoint.Parse("0.5");              // the night, say
		CLandFire.Fire(state, combat, combat.Attacker, new CCombatStream(1), attacker, combat.Defender);
		Assert.Equal(3 * 666, defender.SubUnits[0].PendingOrganisationDamage);
	}
}
