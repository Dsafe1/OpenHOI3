using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The confirm-build dialog's words, a building line's name and counter, and the placing map's colours, against
/// <c>docs/mechanics/buildinglines.md</c>, sections 2, 6 and 7, on <see cref="RebelWorld"/>.
/// </summary>
public class BuildingLineTextTests
{
	private static string Localise(string key) => key switch
	{
		"DAYS" => "Days",
		"MONTHS" => "Months",
		"air_base" => "Air Base",
		_ => key,
	};

	[Fact]
	public void The_dialog_writes_parallel_times_the_cost_and_serial_times_the_days()
	{
		CGameState state = RebelWorld.State();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		germany.Abilities["construction_practical"] = CFixedPoint.FromInt(5);
		(CFixedPoint cost, int days) = CConfirmBuildText.Price(germany, RebelWorld.AirBase);
		Assert.Equal(CFixedPoint.FromInt(4), cost);
		Assert.Equal(90, days);
		Assert.Equal("12.00", CConfirmBuildText.Cost(3, cost));
		Assert.Equal("360 Days", CConfirmBuildText.Time(Localise, 4, days));
		Assert.Equal("15 Months", CConfirmBuildText.Time(Localise, 5, days));
		Assert.Equal("+0.1", CConfirmBuildText.Impact(RebelWorld.AirBase));
		Assert.Equal("picture_air_base", CConfirmBuildText.Picture(RebelWorld.AirBase));
		Assert.Equal("icon_construction_practical", CConfirmBuildText.CategoryIcon(RebelWorld.AirBase));
		// No IC for the share: nothing spare, so red.
		Assert.Equal("selected_brigade_red", CConfirmBuildText.Mark(germany, 1, cost));
	}

	[Fact]
	public void A_building_line_is_named_by_its_building_and_province_and_drawn_by_its_icon()
	{
		CGameState state = RebelWorld.State();
		var placed = new CConstruction(RebelWorld.Ger) { Building = RebelWorld.AirBase, Location = 2 };
		var unplaced = new CConstruction(RebelWorld.Ger) { Building = RebelWorld.AirBase };
		Assert.Equal("Air Base (p2)", CBuildQueueText.BuildingName(Localise, state, placed));
		Assert.Equal("Air Base", CBuildQueueText.BuildingName(Localise, state, unplaced));
		Assert.Equal("icon_build_air_base", CBuildQueueText.CounterSprite(placed));
		Assert.Equal("production_queue_buildings", CBuildQueueText.KindElement(placed));
	}

	[Fact]
	public void The_placing_map_is_grey_and_green_where_the_building_may_go()
	{
		CGameState state = RebelWorld.State();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		germany.TechnologyLevels[RebelWorld.AirTech.Name] = 1;
		Assert.Equal(CDeploymentColours.Green, CDeploymentColours.Of(state, germany, RebelWorld.AirBase, state.Province(2)));
		Assert.Equal(CDeploymentColours.Grey, CDeploymentColours.Of(state, germany, RebelWorld.AirBase, state.Province(5)));
	}
}
