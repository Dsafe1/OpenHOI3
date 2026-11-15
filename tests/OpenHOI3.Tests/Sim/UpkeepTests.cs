using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What units draw each day - supplies and fuel, the men who go home, reinforcement and
/// upgrades - against <c>docs/mechanics/upkeep.md</c>. The German figures are the
/// original's own, out of its savegames: a regular brigade of 30 on a two-year draft is
/// at 29.996 two days in, light armour is 0.024 of the way to its next level, and the
/// upgrade share runs out part of the way through the bomber wings.
/// </summary>
public class UpkeepTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CSubUnitStats Stats(params (string Statistic, string Value)[] values)
	{
		var stats = new CSubUnitStats();
		foreach ((string statistic, string value) in values)
		{
			stats.Values.Set(statistic, CFixedPoint.Parse(value));
		}
		return stats;
	}

	private static CTechnology Technology(string name, CSubUnitType type, bool change = true, bool canUpgrade = true,
		params (string Statistic, string Value)[] gives)
	{
		var technology = new CTechnology(name, 12, new CModifier(name)) { Change = change, CanUpgrade = canUpgrade };
		technology.UnitEffects[type.Key] = Stats(gives);
		type.Technologies.Add(technology);
		return technology;
	}

	private static CSubUnitType Infantry()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land) { OnCompletion = "infantry_practical" };
		type.Stats.CopyFrom(Stats(("max_strength", "30"), ("default_organisation", "30"), ("build_cost_ic", "2.33"),
			("build_cost_manpower", "3.33"), ("build_time", "95"), ("repair_cost_multiplier", "0.05"),
			("supply_consumption", "0.67"), ("fuel_consumption", "0")));
		return type;
	}

	private static CSubUnitType LightArmour()
	{
		var type = new CSubUnitType("light_armor_brigade", EUnitBranch.Land) { OnCompletion = "armour_practical" };
		type.Flags["is_armor"] = true;
		type.Stats.CopyFrom(Stats(("max_strength", "30"), ("build_cost_ic", "8.2"), ("build_cost_manpower", "2"),
			("build_time", "160"), ("repair_cost_multiplier", "0.05"), ("supply_consumption", "1"), ("fuel_consumption", "1.2")));
		return type;
	}

	private static CSubUnitType Destroyer()
	{
		var type = new CSubUnitType("destroyer", EUnitBranch.Naval) { OnCompletion = "destroyer_practical" };
		type.Stats.CopyFrom(Stats(("max_strength", "100"), ("build_cost_ic", "4.5"), ("build_cost_manpower", "1"),
			("build_time", "200"), ("repair_cost_multiplier", "0.05"), ("supply_consumption", "0.25"), ("fuel_consumption", "0.28")));
		return type;
	}

	private static CSubUnitType Bomber()
	{
		var type = new CSubUnitType("tactical_bomber", EUnitBranch.Air) { OnCompletion = "twin_engine_aircraft_practical" };
		type.Stats.CopyFrom(Stats(("max_strength", "100"), ("build_cost_ic", "16"), ("build_cost_manpower", "1.5"),
			("build_time", "220"), ("repair_cost_multiplier", "0.05"), ("supply_consumption", "2"), ("fuel_consumption", "3")));
		return type;
	}

	/// <summary>Germany on a two-year draft with advanced training, every practical at the 5 the costs are measured against.</summary>
	private static CCountry Country()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 };
		country.Modifier.Set(CUnits.ReservesPenaltySize, CFixedPoint.Parse("-0.5"));
		country.Modifier.Set(CUnits.UnitStartExperience, CFixedPoint.FromInt(15));
		country.Modifier.Set(CUnitUpkeep.Rotation, CFixedPoint.Parse("0.03"));
		foreach (string practical in new[] { "infantry_practical", "armour_practical", "destroyer_practical", "twin_engine_aircraft_practical" })
		{
			country.Abilities[practical] = CFixedPoint.FromInt(5);
		}
		country.Manpower = CFixedPoint.FromInt(500);
		country.TotalIc = 100;
		return country;
	}

	private static CGameState State(CCountry country, IEnumerable<CTechnology>? technologies = null)
	{
		var defines = new CDefines();
		defines.Set(CUnitUpkeep.CountrySection, CUnitUpkeep.LowSupplyFactor, CFixedPoint.Parse("1.25"));
		defines.Set(CManpower.MilitarySection, CReinforcement.PrideBonus, CFixedPoint.FromInt(10));
		defines.Set(CManpower.MilitarySection, CUpgrades.ShipSpeed, CFixedPoint.FromInt(4));
		defines.Set(CManpower.MilitarySection, CUpgrades.AirSpeed, CFixedPoint.One);
		defines.Set(CManpower.MilitarySection, CUpgrades.LandSpeed, CFixedPoint.One);
		defines.Set(CResources.EconomySection, CProduction.IcToSupplies, CFixedPoint.FromInt(7));
		var capital = new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger };
		return new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), capital }, new[] { country },
			technologies: technologies, defines: defines);
	}

	private static CUnit Unit(CCountry country, EUnitKind kind = EUnitKind.Division, int id = 1)
	{
		var unit = new CUnit(kind, country.Tag) { Id = id, Location = 1, Base = 1 };
		country.Units.Add(unit);
		return unit;
	}

	private static CSubUnit Brigade(CSubUnitType type, CUnit unit, string? strength = null, bool reserve = false)
	{
		var brigade = new CSubUnit(type) { Unit = unit, IsReserve = reserve };
		brigade.Strength = strength is null ? type.Stats[CSubUnitType.MaxStrength] : CFixedPoint.Parse(strength);
		brigade.Highest = brigade.Strength;
		brigade.Experience = CFixedPoint.FromInt(15);
		unit.SubUnits.Add(brigade);
		return brigade;
	}

	// ---- supplies and fuel

	[Fact]
	public void ABrigadeUsesAHundredthMoreForEveryLevelOfEverythingItHolds()
	{
		CCountry country = Country();
		CSubUnitType type = Infantry();
		Technology("smallarms", type);
		Technology("infantry_warfare", type, change: false);
		CUnit unit = Unit(country);
		CSubUnit brigade = Brigade(type, unit);
		brigade.TechnologyLevels["smallarms"] = 1;
		brigade.TechnologyLevels["infantry_warfare"] = 3;

		// 0.67 and four levels: 0.67 * 1.04, with what is left under a thousandth dropped.
		Assert.Equal(CFixedPoint.Parse("0.696"), CUnitSupply.SupplyConsumption(State(country), country, unit));
	}

	[Fact]
	public void ABrigadeUnderStrengthUsesThatMuchLessInSupplies()
	{
		CCountry country = Country();
		CUnit unit = Unit(country);
		Brigade(Infantry(), unit, strength: "15");

		Assert.Equal(CFixedPoint.Parse("0.335"), CUnitSupply.SupplyConsumption(State(country), country, unit));
	}

	[Fact]
	public void AShipUsesTheSameWhateverStateItIsIn()
	{
		CCountry country = Country();
		CUnit fleet = Unit(country, EUnitKind.Navy);
		Brigade(Destroyer(), fleet, strength: "40");

		Assert.Equal(CFixedPoint.Parse("0.25"), CUnitSupply.SupplyConsumption(State(country), country, fleet));
	}

	[Fact]
	public void FuelDoesNotGoByStrength()
	{
		CCountry country = Country();
		CUnit unit = Unit(country);
		Brigade(LightArmour(), unit, strength: "15");

		Assert.Equal(CFixedPoint.Parse("1.2"), CUnitSupply.FuelConsumption(State(country), country, unit));
	}

	[Fact]
	public void TheCountrysModifierIsOnEverythingItsUnitsUse()
	{
		CCountry country = Country();
		country.Modifier.Set(CUnitSupply.Consumption, CFixedPoint.Parse("-0.1"));
		CUnit unit = Unit(country);
		Brigade(LightArmour(), unit);

		CGameState state = State(country);
		Assert.Equal(CFixedPoint.Parse("0.9"), CUnitSupply.SupplyConsumption(state, country, unit));
		Assert.Equal(CFixedPoint.Parse("1.08"), CUnitSupply.FuelConsumption(state, country, unit));
	}

	[Fact]
	public void AUnitTakesItsSuppliesFromTheStockpileAndRemembersItWasGivenThemAll()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit unit = Unit(country);
		Brigade(Infantry(), unit);
		CGoodsPool stockpile = state.Stockpile(country)!;
		stockpile[EGoods.Supplies] = CFixedPoint.FromInt(10);

		CSupplyDraw.Draw(state, country, unit);

		Assert.Equal(CFixedPoint.Parse("9.33"), stockpile[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.One, unit.SuppliesReceived);
		Assert.Equal(CFixedPoint.Parse("0.67"), country.UnitDemand[EGoods.Supplies]);
	}

	[Fact]
	public void AUnitThatIsGivenLessRemembersHowMuchOfWhatItAskedFor()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit unit = Unit(country);
		Brigade(Infantry(), unit);
		CGoodsPool stockpile = state.Stockpile(country)!;
		stockpile[EGoods.Supplies] = CFixedPoint.Parse("0.335");

		CSupplyDraw.Draw(state, country, unit);

		Assert.Equal(CFixedPoint.Zero, stockpile[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("0.5"), unit.SuppliesReceived);
		Assert.Equal(CFixedPoint.Parse("0.67"), country.UnitDemand[EGoods.Supplies]);     // what it asked for
		Assert.Equal(CFixedPoint.Parse("0.335"), country.UnitDrawn[EGoods.Supplies]);
	}

	[Fact]
	public void AnArmyStandingStillBurnsNoFuelAndAFleetAwayFromItsPortDoes()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit army = Unit(country);
		Brigade(LightArmour(), army);
		CUnit fleet = Unit(country, EUnitKind.Navy, id: 2);
		Brigade(Destroyer(), fleet);
		fleet.Location = 0;                                   // not where it is based
		CGoodsPool stockpile = state.Stockpile(country)!;
		stockpile[EGoods.Supplies] = CFixedPoint.FromInt(10);
		stockpile[EGoods.Fuel] = CFixedPoint.FromInt(10);

		CSupplyDraw.Draw(state, country, army);
		Assert.Equal(CFixedPoint.FromInt(10), stockpile[EGoods.Fuel]);

		CSupplyDraw.Draw(state, country, fleet);
		Assert.Equal(CFixedPoint.Parse("9.72"), stockpile[EGoods.Fuel]);
	}

	[Fact]
	public void ANewGameGivesAsManyDaysOfWhatTheUnitsUseAsOfResources()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit unit = Unit(country);
		Brigade(LightArmour(), unit);

		CStockpile.AddUnitGoodsAtStart(state);

		// A country at peace starts with ninety days, and the fuel counts though nothing is moving.
		Assert.Equal(CFixedPoint.FromInt(90), state.Stockpile(country)![EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(108), state.Stockpile(country)![EGoods.Fuel]);
	}

	[Fact]
	public void TheSupplySharesNeedIsTheIcThatMakesWhatTheUnitsAskedFor()
	{
		CCountry country = Country();
		CGameState state = State(country);
		country.Modifier.Set("global_supplies", CFixedPoint.Parse("0.3"));
		country.UnitDemand[EGoods.Supplies] = CFixedPoint.Parse("91");

		// An IC makes 7 * 1.3 = 9.1 supplies, so 91 want 10 - a thousandth lost to the binary places.
		CFixedPoint need = CProduction.Needed(state, country, EProductionCategory.Supplies);
		Assert.InRange(need.Raw, 9998, 10001);
	}

	// ---- rotation

	[Fact]
	public void ADayOfRotationTakesAYearsShareOfABrigadesFullStrength()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit unit = Unit(country);
		CSubUnit regular = Brigade(Infantry(), unit);
		CSubUnit reserve = Brigade(Infantry(), unit, strength: "15", reserve: true);

		CUnitUpkeep.Rotate(state, country, unit);
		CUnitUpkeep.Rotate(state, country, unit);

		// The save's own figures two days in: 30 * 0.03 / 365 is 0.00246, and the 0.00046 is dropped.
		Assert.Equal(CFixedPoint.Parse("29.996"), regular.Strength);
		Assert.Equal(CFixedPoint.Parse("14.998"), reserve.Strength);
		Assert.Equal(CFixedPoint.FromInt(30), regular.Highest);
	}

	[Fact]
	public void ShipsAndWingsKeepTheirMen()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit fleet = Unit(country, EUnitKind.Navy);
		CSubUnit ship = Brigade(Destroyer(), fleet);

		CUnitUpkeep.Rotate(state, country, fleet);

		Assert.Equal(CFixedPoint.FromInt(100), ship.Strength);
	}

	[Fact]
	public void AUnitThatWentShortOfSuppliesLosesMenFaster()
	{
		CCountry country = Country();
		country.Modifier.Set(CUnitUpkeep.Rotation, CFixedPoint.Parse("0.1"));
		CGameState state = State(country);
		CUnit unit = Unit(country);
		unit.SuppliesReceived = CFixedPoint.Zero;

		// Given nothing, the rate is two and a quarter times itself.
		Assert.Equal(225, CUnitUpkeep.RotationRate(state, country, unit));
		unit.SuppliesReceived = CFixedPoint.Parse("0.6");
		Assert.Equal(150, CUnitUpkeep.RotationRate(state, country, unit));
	}

	// ---- reinforcement

	[Fact]
	public void ABrigadeShortByLessThanAThousandthOfItsStrengthIsLeftAlone()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CSubUnit brigade = Brigade(Infantry(), Unit(country), strength: "29.971");

		CFixedPoint spent = CReinforcement.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		Assert.Equal(CFixedPoint.Parse("29.971"), brigade.Strength);
		Assert.Equal(CFixedPoint.Zero, spent);
		Assert.Equal(CFixedPoint.Zero, country.ReinforcementCost);
	}

	[Fact]
	public void ADayBringsBackASixtiethOfABrigadesFullStrength()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CSubUnit brigade = Brigade(Infantry(), Unit(country), strength: "15");
		var charged = new List<CFixedPoint>();

		CFixedPoint spent = CReinforcement.Run(state, country, CFixedPoint.FromInt(10), apply: true, charged.Add);

		Assert.Equal(CFixedPoint.Parse("15.5"), brigade.Strength);
		// A twentieth of what it costs to build, and the men a sixtieth of it takes.
		Assert.Equal(CFixedPoint.Parse("0.116"), spent);
		Assert.Equal(new[] { CFixedPoint.Parse("0.116") }, charged);
		Assert.Equal(CFixedPoint.Parse("0.116"), country.ReinforcementCost);
		Assert.Equal(CFixedPoint.Parse("0.055"), country.ReinforcementManpowerUsed);
		Assert.Equal(CFixedPoint.Parse("499.945"), country.Manpower);
		Assert.Equal(CFixedPoint.Parse("1.665"), country.ReinforcementManpowerNeeded);   // half of 3.33
	}

	[Fact]
	public void TheNewMenWaterDownWhatTheBrigadeHasLearnt()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CSubUnit brigade = Brigade(Infantry(), Unit(country), strength: "15");
		brigade.Experience = CFixedPoint.FromInt(50);

		CReinforcement.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		// 0.016 of its full strength is new, at the 15 a new unit has.
		Assert.Equal(CFixedPoint.Parse("49.44"), brigade.Experience);
	}

	[Fact]
	public void ToppingUpDoesOnlyWhatIsMissingAndACountryThatRepairsFasterLeavesALittle()
	{
		CCountry country = Country();
		country.Modifier.Set(CReinforcement.UnitRepair, CFixedPoint.Parse("0.15"));     // ball bearings
		CGameState state = State(country);
		CSubUnit brigade = Brigade(Infantry(), Unit(country), strength: "29.97");

		CFixedPoint spent = CReinforcement.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		// 0.03 is missing; the original divides it by the speed, 1.15, and brings back 0.026.
		Assert.Equal(CFixedPoint.Parse("29.996"), brigade.Strength);
		Assert.Equal(CFixedPoint.Parse("0.005"), spent);
	}

	[Fact]
	public void WithNoIcNothingIsBroughtBackButTheNeedIsStillCounted()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CSubUnit brigade = Brigade(Infantry(), Unit(country), strength: "15");

		CFixedPoint spent = CReinforcement.Run(state, country, CFixedPoint.Zero, apply: true);

		Assert.Equal(CFixedPoint.Parse("15"), brigade.Strength);
		Assert.Equal(CFixedPoint.Zero, spent);
		Assert.Equal(CFixedPoint.Parse("0.116"), country.ReinforcementCost);
		Assert.Equal(CFixedPoint.FromInt(500), country.Manpower);
	}

	[Fact]
	public void TheShareOverspendsByAtMostTheBrigadeItRunsOutOn()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit unit = Unit(country);
		CSubUnit first = Brigade(Infantry(), unit, strength: "15");
		CSubUnit second = Brigade(Infantry(), unit, strength: "15");

		CReinforcement.Run(state, country, CFixedPoint.Parse("0.05"), apply: true);

		Assert.Equal(CFixedPoint.Parse("15.5"), first.Strength);
		Assert.Equal(CFixedPoint.Parse("15"), second.Strength);
		Assert.Equal(CFixedPoint.Parse("0.232"), country.ReinforcementCost);            // both are wanted
	}

	[Fact]
	public void WithNoMenNothingIsBroughtBack()
	{
		CCountry country = Country();
		country.Manpower = CFixedPoint.Zero;
		CGameState state = State(country);
		CSubUnit brigade = Brigade(Infantry(), Unit(country), strength: "15");

		CReinforcement.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		Assert.Equal(CFixedPoint.Parse("15"), brigade.Strength);
	}

	[Fact]
	public void AUnitThatGotNoSuppliesIsNotReinforced()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CUnit unit = Unit(country);
		unit.SuppliesReceived = CFixedPoint.Zero;
		CSubUnit brigade = Brigade(Infantry(), unit, strength: "15");

		CReinforcement.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		Assert.Equal(CFixedPoint.Parse("15"), brigade.Strength);
		Assert.Equal(CFixedPoint.Zero, country.ReinforcementCost);
	}

	[Fact]
	public void AReserveCostsFiveTimesTheShareOfItTheLawKeepsBack()
	{
		CCountry country = Country();
		CGameState state = State(country);
		Brigade(Infantry(), Unit(country), strength: "7.5", reserve: true);                // of the 15 it may have

		CReinforcement.Run(state, country, CFixedPoint.Zero, apply: false);

		// Half is kept back, so two and a half times a regular's 0.116.
		Assert.Equal(CFixedPoint.Parse("0.291"), country.ReinforcementCost);
	}

	[Fact]
	public void UnitsMarkedToGoFirstDoAndTheRestGoInTheOrderTheyWereMade()
	{
		CCountry country = Country();
		CUnit third = Unit(country, id: 30);
		CUnit first = Unit(country, id: 10);
		CUnit second = Unit(country, id: 20);
		second.IsPrioritised = true;
		third.MayReinforce = false;

		List<CUnit> order = CUnits.InWorkingOrder(country, unit => unit.MayReinforce);

		Assert.Equal(new[] { second, first }, order);
	}

	// ---- what a unit costs and how long it takes

	[Fact]
	public void APracticalAtFiveMakesNothingCheaperAndOneBelowMakesItDearer()
	{
		CCountry country = Country();
		Assert.Equal(0, CBuildCosts.CategoryDiscount(country, "armour_practical"));

		country.Abilities["armour_practical"] = CFixedPoint.Parse("2.5");
		Assert.Equal(-250, CBuildCosts.CategoryDiscount(country, "armour_practical"));

		// Past a point over, it is the original's rough square root: 5 over is 2.25, at a twentieth each.
		country.Abilities["armour_practical"] = CFixedPoint.FromInt(10);
		Assert.Equal(112, CBuildCosts.CategoryDiscount(country, "armour_practical"));
	}

	[Fact]
	public void TheLawsEfficiencyAndTheTechnologysAreAddedAndItStopsShortOfFree()
	{
		CCountry country = Country();
		country.Modifier.Set(CBuildCosts.IndustrialEfficiency, CFixedPoint.Parse("-0.15"));
		country.Technology.Set(CBuildCosts.TechnologyEfficiency, CFixedPoint.Parse("0.1"));
		Assert.Equal(-50, CBuildCosts.CategoryDiscount(country, "armour_practical"));

		country.Technology.Set(CBuildCosts.TechnologyEfficiency, CFixedPoint.FromInt(3));
		Assert.Equal(990, CBuildCosts.CategoryDiscount(country, "armour_practical"));
	}

	[Fact]
	public void BuildTimeIsTheTypesWithTheTrainingLawAndTheDiscountAndTheSpeedOfItsKind()
	{
		CCountry country = Country();
		CSubUnit armour = Brigade(LightArmour(), Unit(country));

		// Germany's: advanced training adds a tenth and four levels of efficiency take it off.
		country.Modifier.Set(CBuildCosts.RecruitmentTime, CFixedPoint.Parse("0.1"));
		country.Technology.Set(CBuildCosts.TechnologyEfficiency, CFixedPoint.Parse("0.1"));
		Assert.Equal(160, CBuildCosts.BuildTime(country, armour));

		country.Modifier.Set("tank_build_speed", CFixedPoint.Parse("-0.15"));
		Assert.Equal(136, CBuildCosts.BuildTime(country, armour));
	}

	// ---- upgrades

	[Fact]
	public void ADayMovesALevelOnByTwiceTheShareOfTheBuildTimeInWholeThousandths()
	{
		CCountry country = Country();
		CGameState state = State(country);
		CSubUnit armour = Brigade(LightArmour(), Unit(country));
		CSubUnit ship = Brigade(Destroyer(), Unit(country, EUnitKind.Navy, id: 2));

		// 1000 / 160 is 6 and a bit; the bit is dropped before it is doubled. The save has 0.024 after two days.
		Assert.Equal(12, CUpgrades.Rate(state, country, armour));
		// A ship's is four times that: 1000 / 200 is 5, doubled, times 4.
		Assert.Equal(40, CUpgrades.Rate(state, country, ship));
	}

	[Fact]
	public void ADayOfOneTechnologyCostsHalfTheBrigadeSpreadOverAllItHolds()
	{
		CCountry country = Country();
		country.Technology.Set(CBuildCosts.TechnologyEfficiency, CFixedPoint.Parse("0.1"));
		CSubUnitType type = Bomber();
		for (int i = 0; i < 17; i++)
		{
			Technology("technology" + i, type);
		}
		CSubUnit wing = Brigade(type, Unit(country, EUnitKind.Air));

		// Germany's bombers: 16 IC less a tenth, over seventeen technologies, halved.
		Assert.Equal(423, CUpgrades.DailyCost(country, wing, levelsBehind: 1));
		Assert.Equal(211, CUpgrades.DailyCost(country, wing, levelsBehind: 2));
	}

	[Fact]
	public void AnUpgradeMovesOnPaysAndIsCountedAsTheSharesNeed()
	{
		CCountry country = Country();
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type);
		Technology("lighttank_engine", type);
		CGameState state = State(country, new[] { gun });
		CSubUnit armour = Brigade(type, Unit(country));
		country.TechnologyLevels["lighttank_gun"] = 1;

		CFixedPoint spent = CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		// 8.2 over two technologies, halved.
		Assert.Equal(CFixedPoint.Parse("2.05"), spent);
		Assert.Equal(CFixedPoint.Parse("2.05"), country.UpgradeCost);
		Assert.Equal(1, country.SubUnitsUpgrading);
		Assert.Equal(CFixedPoint.Parse("0.012"), armour.TechnologyProgress["lighttank_gun"]);
		Assert.Equal(0, armour.TechnologyLevels["lighttank_gun"]);
	}

	[Fact]
	public void WhenTheLevelIsDoneTheBrigadeHasItAndItsFiguresAreWorkedOutAgain()
	{
		CCountry country = Country();
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type, gives: ("supply_consumption", "0.1"));
		CGameState state = State(country, new[] { gun });
		CSubUnit armour = Brigade(type, Unit(country));
		armour.TechnologyProgress["lighttank_gun"] = CFixedPoint.Parse("0.99");
		country.TechnologyLevels["lighttank_gun"] = 1;

		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		Assert.Equal(1, armour.TechnologyLevels["lighttank_gun"]);
		Assert.Equal(CFixedPoint.Zero, armour.TechnologyProgress["lighttank_gun"]);
		Assert.Equal(CFixedPoint.Parse("1.1"), armour.Stats["supply_consumption"]);

		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);
		Assert.Equal(CFixedPoint.Zero, country.UpgradeCost);                              // nothing is behind any more
	}

	[Fact]
	public void ADoctrineCostsNothingAndReachesABrigadeALevelADay()
	{
		CCountry country = Country();
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("infantry_warfare", type, change: false);
		CGameState state = State(country, new[] { doctrine });
		CSubUnit brigade = Brigade(type, Unit(country));
		country.TechnologyLevels["infantry_warfare"] = 3;

		CFixedPoint spent = CUpgrades.Run(state, country, CFixedPoint.Zero, apply: true);

		Assert.Equal(1, brigade.TechnologyLevels["infantry_warfare"]);
		Assert.Equal(CFixedPoint.Zero, spent);
		Assert.Equal(CFixedPoint.Zero, country.UpgradeCost);
		Assert.Equal(1, country.SubUnitsUpgrading);
	}

	[Fact]
	public void EnteringAGameMovesEveryDoctrineOnALevelAndNoEquipment()
	{
		// The original's entry runs the shares with nothing to share out: a doctrine costs nothing, so a brigade three
		// levels behind at a later start is level with its country after that and the two days.
		CCountry country = Country();
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("infantry_warfare", type, change: false);
		CTechnology rifles = Technology("smallarms_technology", type);
		CGameState state = State(country, new[] { doctrine, rifles });
		CSubUnit brigade = Brigade(type, Unit(country));
		country.TechnologyLevels["infantry_warfare"] = 3;
		country.TechnologyLevels["smallarms_technology"] = 1;

		CUpgrades.RunAtEntry(state);

		Assert.Equal(1, brigade.TechnologyLevels["infantry_warfare"]);
		Assert.Equal(0, brigade.TechnologyLevels["smallarms_technology"]);
		Assert.Equal(CFixedPoint.Zero, brigade.TechnologyProgress.GetValueOrDefault("smallarms_technology"));
	}

	[Fact]
	public void ATechnologyThatCannotBeUpgradedToIsLeftWhereItIs()
	{
		CCountry country = Country();
		CSubUnitType type = Destroyer();
		CTechnology hull = Technology("destroyer_hull", type, canUpgrade: false);
		CGameState state = State(country, new[] { hull });
		CSubUnit ship = Brigade(type, Unit(country, EUnitKind.Navy));
		country.TechnologyLevels["destroyer_hull"] = 2;

		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		Assert.Equal(0, ship.TechnologyLevels["destroyer_hull"]);
		Assert.Equal(CFixedPoint.Zero, country.UpgradeCost);
		Assert.Equal(0, country.SubUnitsUpgrading);
	}

	[Fact]
	public void TheLastOfTheShareBuysWhatItCanAndTheRestGoWithout()
	{
		CCountry country = Country();
		country.Technology.Set(CBuildCosts.TechnologyEfficiency, CFixedPoint.Parse("0.1"));
		country.Modifier.Set("air_build_speed", CFixedPoint.Parse("-0.15"));               // 220 days come to 187: 10 a day
		CSubUnitType type = Bomber();
		var technologies = new List<CTechnology>();
		for (int i = 0; i < 17; i++)
		{
			technologies.Add(Technology("technology" + i, type));
		}
		CGameState state = State(country, technologies);
		CUnit unit = Unit(country, EUnitKind.Air);
		CSubUnit first = Brigade(type, unit);
		CSubUnit second = Brigade(type, unit);
		country.TechnologyLevels["technology0"] = 1;
		country.TechnologyLevels["technology1"] = 1;

		// Enough for the first wing's two and 0.206 over - what Germany's share had left for its last wing.
		CFixedPoint spent = CUpgrades.Run(state, country, CFixedPoint.Parse("1.052"), apply: true);

		Assert.Equal(CFixedPoint.Parse("0.01"), first.TechnologyProgress["technology1"]);
		// The save's 0.008 over two days: 0.206 doubled, times the day's 0.010.
		Assert.Equal(CFixedPoint.Parse("0.004"), second.TechnologyProgress["technology0"]);
		Assert.False(second.TechnologyProgress.ContainsKey("technology1"));
		Assert.Equal(CFixedPoint.Parse("1.052"), spent);
		Assert.Equal(CFixedPoint.Parse("1.692"), country.UpgradeCost);                     // all four are wanted
	}

	[Fact]
	public void WorkingOutTheNeedMovesNothing()
	{
		CCountry country = Country();
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type);
		CGameState state = State(country, new[] { gun });
		CSubUnit armour = Brigade(type, Unit(country), strength: "15");
		country.TechnologyLevels["lighttank_gun"] = 1;

		CReinforcement.Run(state, country, CFixedPoint.Zero, apply: false);
		CUpgrades.Run(state, country, CFixedPoint.Zero, apply: false);

		Assert.Equal(CFixedPoint.Parse("15"), armour.Strength);
		Assert.False(armour.TechnologyProgress.ContainsKey("lighttank_gun"));
		Assert.Equal(CFixedPoint.Parse("4.1"), country.UpgradeCost);
		Assert.True(country.ReinforcementCost > CFixedPoint.Zero);
	}

	// ---- the builder an upgrade names

	[Fact]
	public void AWholeLevelOfEquipmentNamesTheBrigadesOwnerItsBuilder()
	{
		CCountry country = Country();
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type);
		CTechnology engine = Technology("lighttank_engine", type);
		CGameState state = State(country, new[] { gun, engine });
		CSubUnit armour = Brigade(type, Unit(country));
		armour.TechnologyProgress["lighttank_gun"] = CFixedPoint.Parse("0.98");
		country.TechnologyLevels["lighttank_gun"] = 1;
		country.TechnologyLevels["lighttank_engine"] = 1;

		// A day's 0.012 short of the whole level: still nobody's.
		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);
		Assert.Null(armour.Builder);

		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);
		Assert.Equal(1, armour.TechnologyLevels["lighttank_gun"]);
		Assert.Equal(Ger, armour.Builder);
	}

	[Fact]
	public void ADoctrineGainedNamesNobody()
	{
		CCountry country = Country();
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("infantry_warfare", type, change: false);
		CGameState state = State(country, new[] { doctrine });
		CSubUnit brigade = Brigade(type, Unit(country));
		country.TechnologyLevels["infantry_warfare"] = 1;

		CUpgrades.Run(state, country, CFixedPoint.Zero, apply: true);

		Assert.Equal(1, brigade.TechnologyLevels["infantry_warfare"]);
		Assert.Null(brigade.Builder);
	}

	[Fact]
	public void ALicencedBrigadeUpgradedIsNamedItsOwnersAfterwards()
	{
		CCountry country = Country();
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type);
		CGameState state = State(country, new[] { gun });
		CSubUnit armour = Brigade(type, Unit(country));
		armour.Builder = new CCountryTag("ITA");
		armour.TechnologyProgress["lighttank_gun"] = CFixedPoint.Parse("0.995");
		country.TechnologyLevels["lighttank_gun"] = 1;

		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);

		Assert.Equal(Ger, armour.Builder);
	}

	[Fact]
	public void AGovernmentInExileUpgradesTowardsItsFactionLeadersLevelWhereThatIsHigher()
	{
		CCountry country = Country();
		var england = new CCountry { Tag = new CCountryTag("ENG"), Name = "England", DefinitionFile = "", Capital = 1, Faction = "allies" };
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type);
		CDefines defines = State(country).Defines;
		var state = new CGameState(new CDate(1941, 6, 22),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "London", isSea: false) { Owner = england.Tag, Controller = england.Tag } },
			new[] { england, country }, technologies: new[] { gun }, defines: defines);
		CSubUnit armour = Brigade(type, Unit(country));
		armour.TechnologyProgress["lighttank_gun"] = CFixedPoint.Parse("0.995");
		england.TechnologyLevels["lighttank_gun"] = 2;
		country.Faction = "allies";

		// Not in exile: its own level, nought, leaves nothing to do.
		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);
		Assert.Equal(0, armour.TechnologyLevels.GetValueOrDefault("lighttank_gun"));
		Assert.Equal(0, CUpgrades.EquipmentLevel(state, country, "lighttank_gun"));

		// In exile, England leading its faction: brought up towards England's two, two levels behind.
		country.InExile = true;
		Assert.Equal(2, CUpgrades.EquipmentLevel(state, country, "lighttank_gun"));
		CUpgrades.Run(state, country, CFixedPoint.FromInt(10), apply: true);
		Assert.Equal(1, armour.TechnologyLevels["lighttank_gun"]);
		Assert.Equal(1, country.SubUnitsUpgrading);
	}

	[Fact]
	public void AUnitLentOutIsUpgradedByItsLenderFirstAndKeepsItsBuilder()
	{
		CCountry country = Country();
		var france = new CCountry { Tag = new CCountryTag("FRA"), Name = "France", DefinitionFile = "", Capital = 1 };
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type);
		CDefines defines = State(country).Defines;
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger } },
			new[] { country, france }, technologies: new[] { gun }, defines: defines);
		CSubUnit own = Brigade(type, Unit(country, id: 1));
		var lent = new CUnit(EUnitKind.Division, france.Tag) { Id = 2, Location = 1, Base = 1, Lender = Ger };
		france.Units.Add(lent);
		country.LentUnits.Add(lent);
		CSubUnit away = Brigade(type, lent);
		away.TechnologyProgress["lighttank_gun"] = CFixedPoint.Parse("0.995");
		country.TechnologyLevels["lighttank_gun"] = 1;

		// Enough for one brigade's day: the lent unit's comes first, though its number is the higher.
		CUpgrades.Run(state, country, CFixedPoint.Parse("4.1"), apply: true);

		Assert.Equal(1, away.TechnologyLevels["lighttank_gun"]);
		Assert.Null(away.Builder);                                                          // its owner is France, the host
		Assert.False(own.TechnologyProgress.ContainsKey("lighttank_gun"));
	}

	// ---- a whole day

	[Fact]
	public void ADayRotatesDrawsReinforcesAndUpgradesAndChargesForTheIcSpent()
	{
		CCountry country = Country();
		CSubUnitType type = LightArmour();
		CTechnology gun = Technology("lighttank_gun", type);
		CGameState state = State(country, new[] { gun });
		CSubUnit armour = Brigade(type, Unit(country), strength: "15");
		country.TechnologyLevels["lighttank_gun"] = 1;
		country.ProductionShares[(int)EProductionCategory.Reinforcement] = CFixed15.FromThousandths(CFixedPoint.Parse("0.1"));
		country.ProductionShares[(int)EProductionCategory.Upgrades] = CFixed15.FromThousandths(CFixedPoint.Parse("0.1"));
		CGoodsPool stockpile = state.Stockpile(country)!;
		foreach (EGoods goods in new[] { EGoods.Metal, EGoods.Energy, EGoods.RareMaterials, EGoods.Supplies })
		{
			stockpile[goods] = CFixedPoint.FromInt(1000);
		}

		CUnitUpkeep.RunDaily(state);
		CProduction.RunDaily(state);

		// Rotation's 0.002 comes off first, so it draws a hair under half its supplies; then reinforcement's 0.5 goes on.
		Assert.Equal(CFixedPoint.Parse("0.499"), country.UnitDemand[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("15.498"), armour.Strength);
		Assert.Equal(CFixedPoint.Parse("0.012"), armour.TechnologyProgress["lighttank_gun"]);
		Assert.Equal(CFixedPoint.Parse("0.41"), country.ReinforcementSpent);               // a twentieth of 8.2
		Assert.Equal(CFixedPoint.Parse("4.1"), country.UpgradeSpent);
		Assert.Equal(CFixedPoint.Parse("4.1"), CProduction.Needed(state, country, EProductionCategory.Upgrades));
	}
}
