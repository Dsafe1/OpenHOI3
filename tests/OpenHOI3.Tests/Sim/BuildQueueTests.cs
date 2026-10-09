using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The build queue against <c>docs/mechanics/buildqueue.md</c>. The figures marked as the
/// original's are out of its savegames of one game: the Graf Spee, six days from done
/// as the game starts and finished in the middle of March with an experience of 24.940.
/// </summary>
public class BuildQueueTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Berlin = 1;
	private const int Kiel = 2;
	private const int Baltic = 3;
	private const int Posen = 4;
	private const int Lodz = 5;

	private const string Cruisers = "cruiser_practical";

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, "", CFixedPoint.Zero);

	private static CSubUnitType Type(string key, EUnitBranch branch, string cost, string days, string men,
		string practical = "", string completion = "0", string strength = "100", string organisation = "30")
	{
		var type = new CSubUnitType(key, branch) { OnCompletion = practical };
		type.Stats.Values.Set(CSubUnitStats.BuildCostIc, CFixedPoint.Parse(cost));
		type.Stats.Values.Set(CSubUnitStats.BuildTime, CFixedPoint.Parse(days));
		type.Stats.Values.Set(CSubUnitStats.BuildCostManpower, CFixedPoint.Parse(men));
		type.Stats.Values.Set(CBuildQueue.CompletionSize, CFixedPoint.Parse(completion));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.Parse(strength));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.Parse(organisation));
		type.Stats.Values.Set(CSubUnitType.Officers, CFixedPoint.FromInt(100));
		return type;
	}

	/// <summary>The original's heavy cruiser, with the seven technologies its type lists.</summary>
	private static CSubUnitType HeavyCruiser()
	{
		CSubUnitType type = Type("heavy_cruiser", EUnitBranch.Naval, "6", "280", "1.5", Cruisers, "1.6");
		for (int index = 0; index < 7; index++)
		{
			type.Technologies.Add(new CTechnology($"cruiser_technology_{index}", 12, new CModifier("")));
		}
		return type;
	}

	private static CSubUnitType Infantry() => Type("infantry_brigade", EUnitBranch.Land, "2", "100", "3", strength: "30", organisation: "40");

	private static CSubUnitType Artillery() => Type("artillery_brigade", EUnitBranch.Land, "3", "60", "1", strength: "30", organisation: "40");

	private static CSubUnitType Fighter() => Type("interceptor", EUnitBranch.Air, "4", "120", "1");

	private static CCountry Germany()
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		country.Manpower = CFixedPoint.FromInt(100);
		country.TotalIc = 100;
		country.ProductionShares[(int)EProductionCategory.Production] = CFixed15.One;
		country.Abilities[Cruisers] = CFixedPoint.FromInt(5);
		return country;
	}

	private static CGameState State(CCountry country, params CSubUnitType[] types)
	{
		var defines = new CDefines();
		defines.Set(CResearch.CountrySection, CResearch.AbilityGainDivisor, CFixedPoint.FromInt(10));
		defines.Set(CResearch.CountrySection, CResearch.MostAbility, CFixedPoint.FromInt(25));
		defines.Set(CManpower.MilitarySection, COfficers.MostRatio, CFixedPoint.Parse("1.4"));

		var berlin = new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, Neighbours = new[] { Kiel, Posen } };
		berlin.SetBuildingLevel(AirBase, CFixedPoint.FromInt(2));
		var kiel = new CProvince(Kiel, "Kiel", isSea: false) { Owner = Ger, Controller = Ger, Neighbours = new[] { Berlin, Baltic } };
		kiel.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(4));
		var baltic = new CProvince(Baltic, "Baltic", isSea: true) { Neighbours = new[] { Kiel } };
		var posen = new CProvince(Posen, "Posen", isSea: false) { Owner = new CCountryTag("POL"), Controller = new CCountryTag("POL"), Neighbours = new[] { Berlin, Lodz } };
		var lodz = new CProvince(Lodz, "Lodz", isSea: false) { Owner = Ger, Controller = Ger, Neighbours = new[] { Posen } };

		var byKey = new Dictionary<string, CSubUnitType>(System.StringComparer.OrdinalIgnoreCase);
		foreach (CSubUnitType type in types)
		{
			byKey[type.Key] = type;
		}
		TestRoads.Pave(new[] { berlin, kiel, baltic, posen, lodz });
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), berlin, kiel, baltic, posen, lodz },
			new[] { country }, buildings: new[] { NavalBase, AirBase, TestRoads.Infra }, defines: defines)
		{
			SubUnitTypes = byKey,
		};
	}

	/// <summary>A line as an order of battle gives it: one ship, of the model the save shows for the Graf Spee.</summary>
	private static CConstruction GrafSpee(CCountry country, CSubUnitType type, string daysToGo = "6")
	{
		var construction = new CConstruction(country.Tag) { Id = 238, UnitId = 239, Duration = CFixedPoint.Parse(daysToGo) };
		var order = new CBrigadeOrder(type) { Id = 242, Name = "Graf Spee" };
		order.Model.AddRange(new[] { 0, 0, 3, 2, 3, 3, 1 });
		construction.Brigades.Add(order);
		country.Constructions.Add(construction);
		return construction;
	}

	// ---- what a line costs and takes

	[Fact]
	public void EveryLevelABrigadeIsBuiltWithMakesItAHundredthDearerAndSlower()
	{
		// The original's: twelve levels between its seven technologies, 6 IC and 280 days.
		CCountry country = Germany();
		CConstruction construction = GrafSpee(country, HeavyCruiser());

		CBuildQueue.Recalculate(country, construction);

		Assert.Equal(CFixedPoint.Parse("6.720"), construction.Cost);
		Assert.Equal(CFixedPoint.FromInt(313), construction.Duration);      // 280 x 1.12, the fraction dropped
	}

	[Fact]
	public void TheLawsThatMakeAUnitDearerAndSlowerDoSoInTheQueue()
	{
		// The original's, as its start save has them: a policy that takes 15% off
		// industrial efficiency, four levels of the technology that gives 2.5% back each,
		// and a training law that adds a fifth to the time.
		CCountry country = Germany();
		country.Modifier.Set(CBuildCosts.IndustrialEfficiency, CFixedPoint.Parse("-0.15"));
		country.Technology.Set(CBuildCosts.TechnologyEfficiency, CFixedPoint.Parse("0.1"));
		country.Modifier.Set(CBuildCosts.RecruitmentTime, CFixedPoint.Parse("0.2"));
		CConstruction construction = GrafSpee(country, HeavyCruiser());

		CBuildQueue.Recalculate(country, construction);

		Assert.Equal(CFixedPoint.Parse("7.020"), construction.Cost);
		Assert.Equal(CFixedPoint.FromInt(383), construction.Duration);
	}

	[Fact]
	public void AUnitOfSeveralBrigadesTakesAsLongAsItsSlowestAndCostsWhatPaysForThemAllOverThatTime()
	{
		CCountry country = Germany();
		var construction = new CConstruction(country.Tag);
		construction.Brigades.Add(new CBrigadeOrder(Infantry()));          // 2 IC for 100 days
		construction.Brigades.Add(new CBrigadeOrder(Artillery()));         // 3 IC for 60 days

		CBuildQueue.Recalculate(country, construction);

		Assert.Equal(CFixedPoint.FromInt(100), construction.Duration);
		Assert.Equal(CFixedPoint.Parse("3.800"), construction.Cost);        // (2 x 100 + 3 x 60) / 100
	}

	[Fact]
	public void ReservesCostTheShareOfABrigadeTheirLawLeaves()
	{
		CCountry country = Germany();
		country.Modifier.Set(CUnits.ReservesPenaltySize, CFixedPoint.Parse("-0.75"));
		var construction = new CConstruction(country.Tag) { IsReserve = true };
		construction.Brigades.Add(new CBrigadeOrder(Infantry()));

		CBuildQueue.Recalculate(country, construction);

		Assert.Equal(CFixedPoint.Parse("0.500"), construction.Cost);
		Assert.Equal(CFixedPoint.Parse("0.750"), CBuildQueue.ManpowerFor(country, construction));
	}

	[Fact]
	public void AModelShorterThanItsTypesListGoesToTheFirstOfThem()
	{
		CSubUnitType type = HeavyCruiser();
		var order = new CBrigadeOrder(type);
		order.Model.AddRange(new[] { 2, 1 });

		CSubUnit ship = CBuildQueue.AsBuilt(order, isReserve: false, out long levels);

		Assert.Equal(3, levels);
		Assert.Equal(2, ship.TechnologyLevels["cruiser_technology_0"]);
		Assert.Equal(1, ship.TechnologyLevels["cruiser_technology_1"]);
		Assert.Equal(0, ship.TechnologyLevels["cruiser_technology_2"]);
	}

	[Fact]
	public void AHistoricalModelThatLeavesATechnologyOutMovesTheRestUpAPlace()
	{
		// The original's, and not what it does for a unit already in the field.
		CSubUnitType type = HeavyCruiser();
		CCountry country = Germany();
		country.HistoricalModels[type.Key] = new SortedDictionary<int, Dictionary<string, int>>
		{
			[0] = new Dictionary<string, int> { ["cruiser_technology_1"] = 4, ["cruiser_technology_3"] = 2 },
		};
		CGameState state = State(country, type);
		var order = new CBrigadeOrder(type);

		CBuildQueue.SetModelToHistorical(state, country, order, 0);

		Assert.Equal(new[] { 4, 2 }, order.Model);
		CSubUnit ship = CBuildQueue.AsBuilt(order, isReserve: false, out long _);
		Assert.Equal(4, ship.TechnologyLevels["cruiser_technology_0"]);
		Assert.Equal(2, ship.TechnologyLevels["cruiser_technology_1"]);
	}

	// ---- a new game

	[Fact]
	public void ANewGameTakesAFilesDurationAsTheDaysStillToGo()
	{
		// The original's: the Graf Spee's file says 6, and its save has it at 309 of
		// 313 days two days later.
		CCountry country = Germany();
		CConstruction construction = GrafSpee(country, HeavyCruiser());
		construction.Cost = CFixedPoint.Parse("5.6");                       // the file's, and not used
		CGameState state = State(country, HeavyCruiser());

		CBuildQueue.SetUpAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(313), construction.Duration);
		Assert.Equal(CFixedPoint.FromInt(307), construction.Progress);
		Assert.Equal(CFixedPoint.Parse("6.720"), construction.Cost);
		Assert.Equal(CFixedPoint.Parse("6.719"), country.ProductionCost);      // through the shares' kind of number
	}

	[Fact]
	public void ALineWithFurtherToGoThanItTakesStartsFromNothing()
	{
		CCountry country = Germany();
		CConstruction construction = GrafSpee(country, HeavyCruiser(), daysToGo: "400");

		CBuildQueue.SetUpAtStart(State(country, HeavyCruiser()));

		Assert.Equal(CFixedPoint.Zero, construction.Progress);
	}

	[Fact]
	public void ANewGameGivesALineItsHistoricalModel()
	{
		CSubUnitType type = HeavyCruiser();
		CCountry country = Germany();
		country.HistoricalModels[type.Key] = new SortedDictionary<int, Dictionary<string, int>>();
		var construction = new CConstruction(country.Tag) { Duration = CFixedPoint.FromInt(6) };
		construction.Brigades.Add(new CBrigadeOrder(type) { HistoricalModel = 2 });
		country.Constructions.Add(construction);
		CGameState state = State(country, type);
		state.Defines.Set(CHistoricalModels.MilitarySection, CHistoricalModels.MaxDefine, CFixedPoint.FromInt(4));

		CBuildQueue.SetUpAtStart(state);

		Assert.Equal(new[] { 2, 2, 2, 2, 2, 2, 2 }, construction.Brigades[0].Model);
	}

	// ---- a day

	[Fact]
	public void ADayOfWorkPaidInFullMovesALineOnADay()
	{
		// The original's: 3 thousandths of 313 days a day, and 15 of experience for the
		// whole building under advanced training.
		CCountry country = Germany();
		country.Modifier.Set(CUnits.UnitStartExperience, CFixedPoint.FromInt(15));
		CGameState state = State(country, HeavyCruiser());
		CConstruction construction = GrafSpee(country, HeavyCruiser());
		CBuildQueue.SetUpAtStart(state);

		CFixedPoint spent = CBuildQueue.RunDaily(state, country, CFixed15.FromInt(31));
		CBuildQueue.RunDaily(state, country, CFixed15.FromInt(31));

		Assert.Equal(CFixedPoint.Parse("6.719"), spent);                    // the cost, through the shares' kind of number
		Assert.Equal(CFixedPoint.FromInt(309), construction.Progress);
		Assert.Equal(CFixedPoint.Parse("0.006"), construction.AccumulatedProgress);
		Assert.Equal(CFixedPoint.Parse("0.090"), construction.AccumulatedExperience);
		Assert.Equal(CFixedPoint.One, construction.Status);
	}

	[Fact]
	public void TheQueueIsPaidInOrderAndTheLineTheShareRunsOutOnGetsWhatIsLeft()
	{
		CCountry country = Germany();
		CGameState state = State(country, Infantry());
		var lines = new List<CConstruction>();
		for (int index = 0; index < 3; index++)
		{
			var construction = new CConstruction(country.Tag) { Id = index + 1 };
			construction.Brigades.Add(new CBrigadeOrder(Infantry()));
			CBuildQueue.Recalculate(country, construction);                  // 2 IC a day, 100 days
			country.Constructions.Add(construction);
			lines.Add(construction);
		}

		CFixedPoint spent = CBuildQueue.RunDaily(state, country, CFixed15.FromInt(3));

		Assert.Equal(CFixedPoint.One, lines[0].Progress);
		Assert.Equal(CFixedPoint.Parse("0.500"), lines[1].Progress);
		Assert.Equal(CFixedPoint.Parse("0.500"), lines[1].Status);
		Assert.Equal(CFixedPoint.Zero, lines[2].Progress);
		Assert.Equal(CFixedPoint.Zero, lines[2].Status);
		Assert.Equal(CFixedPoint.FromInt(3), spent);
		Assert.Equal(CFixedPoint.FromInt(6), country.ProductionCost);
	}

	[Fact]
	public void ALineTheStockpileCannotPayForGetsNothingAndLeavesTheIcForTheNext()
	{
		CCountry country = Germany();
		CGameState state = State(country, Infantry(), Artillery());
		var dear = new CConstruction(country.Tag) { Id = 1 };
		dear.Brigades.Add(new CBrigadeOrder(Artillery()));                 // 3 IC
		var cheap = new CConstruction(country.Tag) { Id = 2 };
		cheap.Brigades.Add(new CBrigadeOrder(Infantry()));                 // 2 IC
		foreach (CConstruction construction in new[] { dear, cheap })
		{
			CBuildQueue.Recalculate(country, construction);
			country.Constructions.Add(construction);
		}

		// Resources for two and a half IC, and no more.
		CBuildQueue.RunDaily(state, country, CFixed15.FromInt(10), ic => ic <= CFixedPoint.Parse("2.5"));

		Assert.Equal(CFixedPoint.Zero, dear.Progress);
		Assert.Equal(CFixedPoint.Zero, dear.Status);
		Assert.Equal(CFixedPoint.One, cheap.Progress);
	}

	[Fact]
	public void TheShareIsRunWithTheOthersAndChargedLikeThem()
	{
		CCountry country = Germany();
		country.ProductionShares[(int)EProductionCategory.Production] = new CFixed15(CFixed15.Scale / 2);
		CGameState state = State(country, Infantry());
		state.Province(Berlin).Pool[EGoods.Metal] = CFixedPoint.FromInt(1000);
		state.Province(Berlin).Pool[EGoods.Energy] = CFixedPoint.FromInt(1000);
		state.Province(Berlin).Pool[EGoods.RareMaterials] = CFixedPoint.FromInt(1000);
		var construction = new CConstruction(country.Tag);
		construction.Brigades.Add(new CBrigadeOrder(Infantry()));
		CBuildQueue.Recalculate(country, construction);
		country.Constructions.Add(construction);

		CProduction.RunDaily(state);

		Assert.Equal(CFixedPoint.One, construction.Progress);
		Assert.Equal(CFixedPoint.FromInt(2), country.ProductionSpent);
		Assert.Equal(CFixedPoint.FromInt(2), CProduction.Spent(country, EProductionCategory.Production));
		Assert.Equal(CFixedPoint.FromInt(2), CProduction.Needed(state, country, EProductionCategory.Production));
		Assert.Equal(CFixedPoint.FromInt(2), country.Usage[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(4), country.Usage[EGoods.Energy]);
	}

	// ---- finishing

	[Fact]
	public void AFinishedUnitWaitsToBePlacedWithTheExperienceItsBuildingGathered()
	{
		// The original's: the Graf Spee, finished in the middle of March 1936 with 3.790
		// of experience counted over 0.154 of its building, under a training law worth
		// 25, comes out at 24.940 - and at 30 of organisation, which is its own figure and
		// not the 42 that Germany's officers would allow it once in a unit.
		CCountry country = Germany();
		country.Modifier.Set(CUnits.UnitStartExperience, CFixedPoint.FromInt(25));
		country.OfficerRatio = CFixedPoint.Parse("1.4");
		CGameState state = State(country, HeavyCruiser());
		CConstruction construction = GrafSpee(country, HeavyCruiser());
		construction.Duration = CFixedPoint.FromInt(383);
		construction.Progress = CFixedPoint.FromInt(382);
		construction.Cost = CFixedPoint.Parse("7.02");
		construction.AccumulatedProgress = CFixedPoint.Parse("0.152");
		construction.AccumulatedExperience = CFixedPoint.Parse("3.740");

		CBuildQueue.RunDaily(state, country, CFixed15.FromInt(31));

		Assert.Empty(country.Constructions);
		CUnit fleet = Assert.Single(country.Deployments);
		Assert.Equal(EUnitKind.Navy, fleet.Kind);
		Assert.Equal(239, fleet.Id);
		Assert.Equal("Graf Spee", fleet.Name);
		Assert.Equal(0, fleet.Location);
		CSubUnit ship = Assert.Single(fleet.SubUnits);
		Assert.Equal(242, ship.Id);
		Assert.Same(fleet, ship.Unit);
		Assert.Equal(CFixedPoint.Parse("24.940"), ship.Experience);
		Assert.Equal(CFixedPoint.FromInt(100), ship.Strength);
		Assert.Equal(CFixedPoint.FromInt(100), ship.Highest);
		Assert.Equal(CFixedPoint.FromInt(30), ship.Organisation);
		Assert.Equal(3, ship.TechnologyLevels["cruiser_technology_2"]);

		// And what building it taught: 1.6 of the practical, on the 5 the country had.
		Assert.Equal(CFixedPoint.Parse("6.600"), country.Ability(Cruisers));
		Assert.Empty(country.Units);
	}

	[Fact]
	public void WhatALineTeachesMakesTheRestOfTheQueueCheaperAtOnce()
	{
		CSubUnitType type = HeavyCruiser();
		CCountry country = Germany();
		CGameState state = State(country, type);
		CConstruction first = GrafSpee(country, type);
		first.Duration = CFixedPoint.One;
		first.Cost = CFixedPoint.One;
		var second = new CConstruction(country.Tag) { Id = 2, UnitId = 3 };
		second.Brigades.Add(new CBrigadeOrder(type));
		CBuildQueue.Recalculate(country, second);
		country.Constructions.Add(second);
		Assert.Equal(CFixedPoint.FromInt(6), second.Cost);

		CBuildQueue.RunDaily(state, country, CFixed15.FromInt(31));

		// 1.6 over the expected 5: its rough square root, a twentieth a point.
		Assert.Single(country.Constructions);
		Assert.True(second.Cost < CFixedPoint.FromInt(6));
		Assert.Equal(CFixedPoint.One, second.Progress);                     // the work done on it stays
	}

	[Fact]
	public void ASeriesGoesOnToItsNextWithNewNumbersAndTheWorkLeftOver()
	{
		CCountry country = Germany();
		CGameState state = State(country, Infantry());
		state.LastUnitId = 500;
		var construction = new CConstruction(country.Tag) { Id = 10, UnitId = 11, Size = 2, Name = "1. Division" };
		construction.Brigades.Add(new CBrigadeOrder(Infantry()) { Id = 12, Name = "1. Regiment" });
		CBuildQueue.Recalculate(country, construction);
		construction.Progress = CFixedPoint.Parse("99.5");
		country.Constructions.Add(construction);

		CBuildQueue.RunDaily(state, country, CFixed15.FromInt(31));

		CUnit division = Assert.Single(country.Deployments);
		Assert.Equal(EUnitKind.Division, division.Kind);
		Assert.Equal("1. Division", division.Name);
		Assert.Equal(11, division.Id);
		Assert.Same(construction, Assert.Single(country.Constructions));
		Assert.Equal(1, construction.Size);
		Assert.Equal(CFixedPoint.Parse("0.500"), construction.Progress);
		Assert.Equal(501, construction.UnitId);
		Assert.Equal(502, construction.Brigades[0].Id);
		Assert.Equal(CFixedPoint.Zero, construction.AccumulatedProgress);
	}

	[Fact]
	public void AReserveIsDeliveredAtTheStrengthItsLineWasQueuedWith()
	{
		CCountry country = Germany();
		country.Modifier.Set(CUnits.ReservesPenaltySize, CFixedPoint.Parse("-0.75"));
		CGameState state = State(country, Infantry());
		var construction = new CConstruction(country.Tag) { IsReserve = true, ReservesFactor = CBuildQueue.ReservesFactor(country) };
		construction.Brigades.Add(new CBrigadeOrder(Infantry()));
		CBuildQueue.Recalculate(country, construction);
		construction.Progress = construction.Duration;
		country.Constructions.Add(construction);

		Assert.True(CBuildQueue.Deliver(state, country, construction));

		CSubUnit brigade = country.Deployments[0].SubUnits[0];
		Assert.True(brigade.IsReserve);
		Assert.Equal(CFixedPoint.Parse("7.500"), brigade.Strength);
	}

	[Fact]
	public void ACountryThatHoldsNothingIsDeliveredNothing()
	{
		CCountry country = Germany();
		CGameState state = State(country, Infantry());
		foreach (CProvince province in state.Provinces)
		{
			province.Owner = null;
		}
		var construction = new CConstruction(country.Tag);
		construction.Brigades.Add(new CBrigadeOrder(Infantry()));

		Assert.True(CBuildQueue.Deliver(state, country, construction));

		Assert.Empty(country.Deployments);
	}

	[Fact]
	public void TheEstimatedDaysLeftGoByWhatTheLineWasLastPaid()
	{
		var construction = new CConstruction(Ger) { Duration = CFixedPoint.FromInt(100), Progress = CFixedPoint.FromInt(90) };

		Assert.Equal(10, CBuildQueue.DaysLeft(construction));

		construction.Status = CFixedPoint.Parse("0.3");
		Assert.Equal(34, CBuildQueue.DaysLeft(construction));               // 33.3, rounded up

		construction.Status = CFixedPoint.Zero;
		Assert.Null(CBuildQueue.DaysLeft(construction));

		// A line nothing has worked on yet is taken to be paid in full.
		construction.Progress = CFixedPoint.Zero;
		Assert.Equal(100, CBuildQueue.DaysLeft(construction));
	}

	[Fact]
	public void AUnitWaitingToBePlacedEatsAndCallsForOfficers()
	{
		CSubUnitType type = Infantry();
		type.Stats.Values.Set(CUnitSupply.SupplyStatistic, CFixedPoint.Parse("0.5"));
		CCountry country = Germany();
		CGameState state = State(country, type);
		state.Province(Berlin).Pool[EGoods.Supplies] = CFixedPoint.FromInt(10);
		var construction = new CConstruction(country.Tag);
		construction.Brigades.Add(new CBrigadeOrder(type));
		CBuildQueue.Deliver(state, country, construction);

		CUnitUpkeep.RunDaily(state);

		Assert.Equal(CFixedPoint.FromInt(100), COfficers.Needed(country));
		Assert.Equal(CFixedPoint.Parse("0.500"), country.UnitDrawn[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("9.500"), state.Province(Berlin).Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(30), country.Deployments[0].SubUnits[0].Strength);    // and no men go home from it
	}

	// ---- the commands

	[Fact]
	public void OrderingAUnitTakesItsMenAtOnceForTheWholeSeries()
	{
		CCountry country = Germany();
		country.TechnologyLevels["cruiser_technology_2"] = 3;
		CGameState state = State(country, HeavyCruiser(), Infantry());
		state.LastUnitId = 100;
		var simulation = new CSimulation(state);
		country.Manpower = CFixedPoint.FromInt(100);

		simulation.Post(new COrderUnitCommand(Ger, new[] { ("heavy_cruiser", "Admiral Hipper") }, size: 2));
		simulation.Pump();

		CConstruction construction = Assert.Single(country.Constructions);
		Assert.Equal(101, construction.Id);
		Assert.Equal(102, construction.UnitId);
		Assert.Equal(103, construction.Brigades[0].Id);
		Assert.Equal(2, construction.Size);
		Assert.Equal(CFixedPoint.Parse("1.500"), construction.Manpower);
		Assert.Equal(CFixedPoint.FromInt(97), country.Manpower);
		Assert.Equal(new[] { 0, 0, 3, 0, 0, 0, 0 }, construction.Brigades[0].Model);    // the country's own levels
		Assert.Equal(CFixedPoint.Parse("6.180"), construction.Cost);        // three levels: three hundredths dearer
		Assert.Equal(CFixedPoint.Zero, construction.Progress);
		Assert.Equal(CFixedPoint.Parse("6.179"), country.ProductionCost);
	}

	[Fact]
	public void AnOrderIsRefusedWithoutTheMenOrForATypeThatMayNotBeBuilt()
	{
		CSubUnitType militia = Infantry();
		militia.Flags["is_buildable"] = false;
		CSubUnitType foreign = Type("gurkha_brigade", EUnitBranch.Land, "2", "100", "3");
		foreign.UsableBy.Add("ENG");
		CCountry country = Germany();
		CGameState state = State(country, HeavyCruiser(), militia, foreign, Fighter());

		country.Manpower = CFixedPoint.One;
		Assert.False(new COrderUnitCommand(Ger, new[] { ("heavy_cruiser", "") }).MayExecute(state));
		country.Manpower = CFixedPoint.FromInt(100);
		Assert.True(new COrderUnitCommand(Ger, new[] { ("heavy_cruiser", "") }).MayExecute(state));

		Assert.False(new COrderUnitCommand(Ger, new[] { ("infantry_brigade", "") }).MayExecute(state));
		Assert.False(new COrderUnitCommand(Ger, new[] { ("gurkha_brigade", "") }).MayExecute(state));
		Assert.False(new COrderUnitCommand(Ger, new[] { ("no_such_type", "") }).MayExecute(state));
		Assert.False(new COrderUnitCommand(Ger, new[] { ("heavy_cruiser", ""), ("interceptor", "") }).MayExecute(state));
		Assert.False(new COrderUnitCommand(Ger, new (string, string)[0]).MayExecute(state));
		Assert.False(new COrderUnitCommand(Ger, new[] { ("heavy_cruiser", "") }, size: 0).MayExecute(state));
	}

	[Fact]
	public void CancellingALineGivesBackTheMenOfEveryUnitStillToCome()
	{
		CCountry country = Germany();
		CGameState state = State(country, Infantry());
		var simulation = new CSimulation(state);
		country.Manpower = CFixedPoint.FromInt(100);
		simulation.Post(new COrderUnitCommand(Ger, new[] { ("infantry_brigade", "") }, size: 3));
		simulation.Pump();
		CConstruction construction = country.Constructions[0];
		Assert.Equal(CFixedPoint.FromInt(91), country.Manpower);
		construction.Size = 2;                                              // one of the three has been delivered

		simulation.Post(new CCancelConstructionCommand(Ger, construction.Id));
		simulation.Pump();

		Assert.Empty(country.Constructions);
		Assert.Equal(CFixedPoint.FromInt(97), country.Manpower);
		Assert.Equal(CFixedPoint.Zero, country.ProductionCost);
		Assert.False(new CCancelConstructionCommand(Ger, construction.Id).MayExecute(state));
	}

	[Fact]
	public void ALineIsMovedToEitherEndOrOnePlaceEitherWay()
	{
		CCountry country = Germany();
		CGameState state = State(country, Infantry());
		for (int id = 1; id <= 4; id++)
		{
			country.Constructions.Add(new CConstruction(country.Tag) { Id = id });
		}
		int[] Order() => country.Constructions.ConvertAll(construction => construction.Id).ToArray();

		new CMoveConstructionCommand(Ger, 3, EQueueMove.First).Execute(state);
		Assert.Equal(new[] { 3, 1, 2, 4 }, Order());
		new CMoveConstructionCommand(Ger, 3, EQueueMove.Later).Execute(state);
		Assert.Equal(new[] { 1, 3, 2, 4 }, Order());
		new CMoveConstructionCommand(Ger, 4, EQueueMove.Earlier).Execute(state);
		Assert.Equal(new[] { 1, 3, 4, 2 }, Order());
		new CMoveConstructionCommand(Ger, 1, EQueueMove.Last).Execute(state);
		Assert.Equal(new[] { 3, 4, 2, 1 }, Order());

		// At the end it is sent towards, it stays.
		new CMoveConstructionCommand(Ger, 3, EQueueMove.Earlier).Execute(state);
		new CMoveConstructionCommand(Ger, 1, EQueueMove.Later).Execute(state);
		Assert.Equal(new[] { 3, 4, 2, 1 }, Order());
		Assert.False(new CMoveConstructionCommand(Ger, 9, EQueueMove.First).MayExecute(state));
	}

	[Fact]
	public void AFinishedUnitIsPlacedInTheCountrysHomeAreaAndAFleetWhereThereIsAPort()
	{
		CCountry country = Germany();
		CGameState state = State(country, HeavyCruiser(), Infantry(), Fighter());
		foreach (CSubUnitType type in new[] { HeavyCruiser(), Infantry(), Fighter() })
		{
			var construction = new CConstruction(country.Tag) { UnitId = ++state.LastUnitId };
			construction.Brigades.Add(new CBrigadeOrder(type));
			CBuildQueue.Deliver(state, country, construction);
		}
		CUnit fleet = country.Deployments[0];
		CUnit division = country.Deployments[1];
		CUnit wing = country.Deployments[2];

		Assert.True(CDeployment.MayPlace(state, country, fleet, Kiel));
		Assert.False(CDeployment.MayPlace(state, country, fleet, Berlin));         // no port
		Assert.True(CDeployment.MayPlace(state, country, wing, Berlin));
		Assert.False(CDeployment.MayPlace(state, country, wing, Kiel));            // no airfield
		Assert.True(CDeployment.MayPlace(state, country, division, Kiel));
		Assert.False(CDeployment.MayPlace(state, country, division, Posen));       // another's
		Assert.False(CDeployment.MayPlace(state, country, division, Lodz));        // its own, and cut off from the capital
		Assert.False(CDeployment.MayPlace(state, country, division, Baltic));
		Assert.False(CDeployment.MayPlace(state, country, division, 0));

		var simulation = new CSimulation(state);
		simulation.Post(new CDeployUnitCommand(Ger, fleet.Id, Kiel));
		simulation.Post(new CDeployUnitCommand(Ger, division.Id, Lodz));           // refused
		simulation.Pump();

		Assert.Equal(new[] { division, wing }, country.Deployments);
		Assert.Same(fleet, Assert.Single(country.Units));
		Assert.Equal(Kiel, fleet.Location);
		Assert.Equal(Kiel, fleet.Base);
	}

	private static CUnit InTheField(CGameState state, CCountry country, EUnitKind kind, int province, params CSubUnitType[] types)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = kind.ToString() };
		foreach (CSubUnitType type in types)
		{
			unit.SubUnits.Add(new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit });
		}
		country.Units.Add(unit);
		return unit;
	}

	private static CUnit Finished(CGameState state, CCountry country, params CSubUnitType[] types)
	{
		var construction = new CConstruction(country.Tag) { UnitId = ++state.LastUnitId };
		foreach (CSubUnitType type in types)
		{
			construction.Brigades.Add(new CBrigadeOrder(type));
		}
		CBuildQueue.Deliver(state, country, construction);
		return country.Deployments[^1];
	}

	[Fact]
	public void AFinishedUnitMayJoinOneInTheFieldThatStandsWhereItCouldBePlaced()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Infantry();
		CSubUnitType artillery = Artillery();
		CSubUnitType cruiser = HeavyCruiser();
		CSubUnitType fighter = Fighter();
		CGameState state = State(country, cruiser, infantry, artillery, fighter);
		state.Defines.Set(CManpower.MilitarySection, CUnitAvailability.BrigadesInDivision, CFixedPoint.FromInt(4));

		CUnit cutOff = InTheField(state, country, EUnitKind.Division, Lodz, infantry, infantry);
		CUnit division = InTheField(state, country, EUnitKind.Division, Berlin, infantry, infantry);
		CUnit fleet = InTheField(state, country, EUnitKind.Navy, Kiel, cruiser);
		CUnit airUnit = InTheField(state, country, EUnitKind.Air, Berlin, fighter);
		CUnit corps = InTheField(state, country, EUnitKind.Corps, Berlin, infantry);

		CUnit brigade = Finished(state, country, artillery);
		CUnit newDivision = Finished(state, country, infantry, artillery);
		CUnit ship = Finished(state, country, cruiser);
		CUnit wing = Finished(state, country, fighter);

		// By rank, the higher first: the corps was made last and is listed first. The
		// division in Lodz is cut off from the capital, so nothing could be placed there.
		Assert.Equal(new[] { corps, division }, CDeployment.Targets(state, country, brigade));
		Assert.Equal(new[] { corps }, CDeployment.Targets(state, country, newDivision));
		Assert.Equal(new[] { fleet }, CDeployment.Targets(state, country, ship));
		Assert.Equal(new[] { airUnit }, CDeployment.Targets(state, country, wing));
		Assert.False(CDeployment.MayJoin(state, country, brigade, cutOff));

		var simulation = new CSimulation(state);
		simulation.Post(new CDeployUnitCommand(Ger, ship.Id, 0, division.Id));        // refused: a ship in a division
		simulation.Post(new CDeployUnitCommand(Ger, brigade.Id, 0, division.Id));
		simulation.Post(new CDeployUnitCommand(Ger, newDivision.Id, 0, corps.Id));
		simulation.Post(new CDeployUnitCommand(Ger, ship.Id, 0, fleet.Id));
		simulation.Post(new CDeployUnitCommand(Ger, wing.Id, 0, airUnit.Id));
		simulation.Pump();

		// A lone brigade, ship or wing is handed over and its own unit is gone.
		Assert.Empty(country.Deployments);
		Assert.Equal(3, division.SubUnits.Count);
		Assert.Same(division, division.SubUnits[2].Unit);
		Assert.Same(artillery, division.SubUnits[2].Type);
		Assert.Equal(2, fleet.SubUnits.Count);
		Assert.Equal(2, airUnit.SubUnits.Count);
		Assert.DoesNotContain(brigade, country.AllUnits());
		Assert.DoesNotContain(ship, country.AllUnits());

		// Anything larger is put under the one it joins, where that one stands.
		Assert.Same(corps, newDivision.Parent);
		Assert.Contains(newDivision, corps.Children);
		Assert.DoesNotContain(newDivision, country.Units);
		Assert.Contains(newDivision, country.AllUnits());
		Assert.Equal(Berlin, newDivision.Location);
	}

	[Fact]
	public void ADivisionTakesNoMoreBrigadesThanItMayHaveAndACorpsNoMoreThanFiveUnits()
	{
		CCountry country = Germany();
		CSubUnitType infantry = Infantry();
		CGameState state = State(country, infantry);
		state.Defines.Set(CManpower.MilitarySection, CUnitAvailability.BrigadesInDivision, CFixedPoint.FromInt(4));

		CUnit full = InTheField(state, country, EUnitKind.Division, Berlin, infantry, infantry, infantry, infantry);
		CUnit corps = InTheField(state, country, EUnitKind.Corps, Berlin, infantry);
		CUnit theatre = InTheField(state, country, EUnitKind.Theatre, Berlin, infantry);
		CUnit brigade = Finished(state, country, infantry);
		CUnit division = Finished(state, country, infantry, infantry);

		Assert.False(CDeployment.MayJoin(state, country, brigade, full));
		Assert.True(CDeployment.MayJoin(state, country, brigade, corps));          // a headquarters is a land unit with room

		for (int count = 0; count < CDeployment.MostLandSubordinates; count++)
		{
			Assert.True(CDeployment.MayTakeSubordinate(corps, division));
			corps.Children.Add(new CUnit(EUnitKind.Division, Ger) { Parent = corps });
			theatre.Children.Add(new CUnit(EUnitKind.Corps, Ger) { Parent = theatre });
		}
		Assert.False(CDeployment.MayTakeSubordinate(corps, division));
		Assert.True(CDeployment.MayTakeSubordinate(theatre, division));            // a theatre takes any number
		Assert.False(CDeployment.MayTakeSubordinate(full, division));              // not of a higher rank
		Assert.False(CDeployment.MayTakeSubordinate(division, corps));
	}

	[Fact]
	public void TheProvincesAUnitMayBePlacedInAreTheHomeAreaAndForAFleetItsPorts()
	{
		CCountry country = Germany();
		CGameState state = State(country, HeavyCruiser(), Infantry());
		CUnit fleet = Finished(state, country, HeavyCruiser());
		CUnit division = Finished(state, country, Infantry(), Infantry());

		Assert.Equal(new[] { Kiel }, CDeployment.Places(state, country, fleet));
		Assert.Equal(new[] { Berlin, Kiel }, CDeployment.Places(state, country, division).OrderBy(id => id));
	}

	[Fact]
	public void AChangeOfLawPricesEveryLineAgainAndLeavesTheWorkDone()
	{
		CCountry country = Germany();
		CConstruction construction = GrafSpee(country, HeavyCruiser());
		CBuildQueue.Recalculate(country, construction);
		construction.Progress = CFixedPoint.FromInt(307);

		country.Modifier.Set(CBuildCosts.RecruitmentTime, CFixedPoint.Parse("0.2"));
		CBuildQueue.RecalculateAll(country);

		Assert.Equal(CFixedPoint.FromInt(369), construction.Duration);      // 280 x 1.32
		Assert.Equal(CFixedPoint.FromInt(307), construction.Progress);
		Assert.Equal(CFixedPoint.Parse("6.719"), country.ProductionCost);      // through the shares' kind of number
	}
}
