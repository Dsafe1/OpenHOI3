using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A building line ordered, priced, worked, finished and placed, against <c>docs/mechanics/buildinglines.md</c>, on
/// <see cref="RebelWorld"/>: Germany builds, its land 1 to 3 and Warsaw held.
/// </summary>
public class BuildingLineTests
{
	private static readonly CBuilding Fort = new(4, "land_fort", 10, "fort_level", CFixedPoint.One)
	{
		Cost = CFixedPoint.FromInt(5), Time = CFixedPoint.FromInt(180), OnCompletion = "construction_practical",
		CompletionSize = CFixedPoint.Parse("0.33"),
	};

	private static CBuilding AirBase => RebelWorld.AirBase;

	private static CGameState World()
	{
		CGameState state = RebelWorld.State();
		// Construction at 5.0 gives no discount; the air base costs 4 a day for 90 days.
		CCountry germany = state.Country(RebelWorld.Ger)!;
		germany.Abilities["construction_practical"] = CFixedPoint.FromInt(5);
		state.Defines.Set("country", CResearch.MostAbility, CFixedPoint.FromInt(99));
		state.Defines.Set(CProvinceRepair.EconomySection, CProvinceRepair.RepairSpeed, CFixedPoint.Parse("0.1"));
		return state;
	}

	private static CBuilding WithPrice(CGameState state)
	{
		return state.Buildings.First(building => building.Key == AirBase.Key);
	}

	[Fact]
	public void A_line_costs_its_buildings_cost_and_time_by_the_category_discount_and_nothing_else()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		var line = new CConstruction(germany.Tag) { Building = Fort, Size = 5 };
		CBuildingLines.Price(germany, line);
		Assert.Equal(CFixedPoint.FromInt(5), line.Cost);
		Assert.Equal(CFixedPoint.FromInt(180), line.Duration);

		// Nothing practised: a discount of -0.5, so 7.5 a day for 270 days - and its size plays no part.
		germany.Abilities["construction_practical"] = CFixedPoint.Zero;
		CBuildingLines.Price(germany, line);
		Assert.Equal(CFixedPoint.Parse("7.5"), line.Cost);
		Assert.Equal(CFixedPoint.FromInt(270), line.Duration);
	}

	[Fact]
	public void An_order_without_a_province_makes_a_new_line_each_time_and_is_never_refused()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		var order = new CConstructBuildingCommand(germany.Tag, AirBase.Key, 0, 150);
		Assert.True(order.MayExecute(state));
		order.Execute(state);
		order.Execute(state);
		Assert.Equal(2, germany.Constructions.Count);
		Assert.All(germany.Constructions, line => Assert.Equal(150, line.Size));
	}

	[Fact]
	public void An_order_in_a_province_joins_the_last_line_there_up_to_the_most_level()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		germany.TechnologyLevels["air_tech"] = 1;
		state.Province(1).SetBuildingLevel(AirBase, CFixedPoint.FromInt(7));
		var order = new CConstructBuildingCommand(germany.Tag, AirBase.Key, 1, 2);
		Assert.True(order.MayExecute(state));
		order.Execute(state);
		order.Execute(state);
		CConstruction line = Assert.Single(germany.Constructions);
		Assert.Equal(3, line.Size);
		Assert.Equal(1, line.Location);
		// Seven built and three queued reach the most: no more is allowed there, and none where Germany does not hold.
		Assert.False(order.MayExecute(state));
		Assert.False(new CConstructBuildingCommand(germany.Tag, AirBase.Key, 5, 1).MayExecute(state));
	}

	[Fact]
	public void A_day_of_work_moves_a_building_line_on_and_counts_none_of_it_towards_experience()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		var line = new CConstruction(germany.Tag) { Building = AirBase, Size = 1, Location = 2 };
		CBuildingLines.Price(germany, line);

		CBuildQueue.AddProgress(germany, line, 1000);

		// What a save writes for a unit line alone, and the original keeps on its military line alone.
		Assert.Equal(CFixedPoint.One, line.Progress);
		Assert.Equal(CFixedPoint.Zero, line.AccumulatedProgress);
		Assert.Equal(CFixedPoint.Zero, line.AccumulatedExperience);
	}

	[Fact]
	public void A_finished_line_builds_a_level_that_does_not_work_teaches_in_full_and_tells_the_holder()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;
		var line = new CConstruction(germany.Tag) { Building = AirBase, Size = 2, Location = 2, PracticalFactor = CFixedPoint.Zero };
		CBuildingLines.Price(germany, line);
		line.Progress = line.Duration;

		Assert.False(CBuildingLines.Finish(state, germany, line));
		CProvince frankfurt = state.Province(2);
		Assert.Equal(CFixedPoint.One, frankfurt.BuildingMaxLevel(AirBase));
		Assert.Equal(CFixedPoint.Zero, frankfurt.BuildingLevel(AirBase));
		Assert.Equal(1, line.Size);
		Assert.Equal(CFixedPoint.Zero, line.Progress);
		Assert.True(germany.Ability("construction_practical") > CFixedPoint.FromInt(5));
		Assert.Equal(RebelWorld.Ger, Assert.Single(messages, message => message.We == "BUILDING_DONE").Told);

		// The day's repair fills it.
		CProvinceRepair.Repair(state, frankfurt, CProvinceRepair.Speed(state));
		Assert.True(frankfurt.BuildingLevel(AirBase) > CFixedPoint.Zero);
	}

	[Fact]
	public void A_line_with_no_province_waits_in_the_deploy_menu_and_is_placed_where_the_country_holds()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		germany.TechnologyLevels["air_tech"] = 1;
		var line = new CConstruction(germany.Tag) { Id = 40, Building = AirBase, Size = 1 };
		CBuildingLines.Price(germany, line);
		line.Progress = line.Duration;

		Assert.True(CBuildingLines.Finish(state, germany, line));
		CBuildingDeployment waiting = Assert.Single(germany.BuildingDeployments);
		Assert.Equal(41, waiting.Id);

		// Germany holds 1 to 4; Lodz is Poland's.
		Assert.True(CBuildingLines.CanDeployIn(state, germany, AirBase, state.Province(4)));
		Assert.False(CBuildingLines.CanDeployIn(state, germany, AirBase, state.Province(5)));
		// An air base already a whole level is no place for a deployed one.
		state.Province(3).SetBuildingLevel(AirBase, CFixedPoint.One);
		Assert.False(CBuildingLines.CanDeployIn(state, germany, AirBase, state.Province(3)));

		var place = new CDeployBuildingCommand(germany.Tag, waiting.Id, 4);
		Assert.True(place.MayExecute(state));
		place.Execute(state);
		Assert.Empty(germany.BuildingDeployments);
		Assert.Equal(CFixedPoint.One, state.Province(4).BuildingMaxLevel(AirBase));
	}

	[Fact]
	public void A_line_in_a_province_its_country_loses_goes_silently()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		var line = new CConstruction(germany.Tag) { Building = AirBase, Size = 1, Location = 4 };
		CBuildingLines.Price(germany, line);
		germany.Constructions.Add(line);
		state.Province(4).Controller = RebelWorld.Pol;

		CBuildQueue.RunDaily(state, germany, CFixed15.FromInt(10));

		Assert.Empty(germany.Constructions);
	}

	[Fact]
	public void Entering_a_loaded_game_takes_a_line_in_lost_land_out_and_counts_the_queue_without_moving_the_rest()
	{
		// The original's entry runs the production share with nothing to spend: the lines that may no longer be built go,
		// nothing is paid, and the need is counted (buildqueue.md, section 4).
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		var lost = new CConstruction(germany.Tag) { Building = AirBase, Size = 1, Location = 4 };
		var kept = new CConstruction(germany.Tag) { Building = AirBase, Size = 1, Location = 2 };
		CBuildingLines.Price(germany, lost);
		CBuildingLines.Price(germany, kept);
		germany.Constructions.Add(lost);
		germany.Constructions.Add(kept);
		state.Province(4).Controller = RebelWorld.Pol;

		CBuildQueue.RunAtEntry(state, germany);

		Assert.Equal(new[] { kept }, germany.Constructions);
		Assert.Equal(CFixedPoint.Zero, kept.Progress);
		Assert.Equal(CFixed15.FromThousandths(kept.Cost).ToThousandths(), germany.ProductionCost);
		Assert.True(germany.ProductionCost > CFixedPoint.Zero);
	}

	[Fact]
	public void A_building_is_available_by_the_technology_that_names_it()
	{
		CGameState state = World();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		Assert.False(CBuildingLines.IsAvailable(state, germany, AirBase));
		germany.TechnologyLevels["air_tech"] = 1;
		Assert.True(CBuildingLines.IsAvailable(state, germany, AirBase));
	}
}
