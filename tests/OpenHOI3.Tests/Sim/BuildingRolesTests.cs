using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>The five buildings known by their role: <c>CBuildingRoles</c>.</summary>
public class BuildingRolesTests
{
	private static CGameState State(params CBuilding[] buildings)
	{
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "P1", isSea: false) };
		return new CGameState(new CDate(1936, 1, 1), provinces, System.Array.Empty<CCountry>(), buildings);
	}

	[Fact]
	public void The_air_base_is_the_building_that_carries_air_capacity_whatever_its_name()
	{
		// BlackICE's: `air_base` carries nothing, `fake_air_base` the capacity.
		var airBase = new CBuilding(0, "air_base", 10, "", CFixedPoint.Zero);
		var fake = new CBuilding(1, "fake_air_base", 10, "air_capacity", CFixedPoint.One);
		CGameState state = State(airBase, fake);
		state.Province(1).SetBuildingLevel(airBase, CFixedPoint.FromInt(10));

		Assert.Same(fake, CBuildingRoles.Find(state, CMovementRules.AirBase));
		Assert.Equal(CFixedPoint.Zero, CMovementRules.BuildingLevel(state, state.Province(1), CMovementRules.AirBase));
		state.Province(1).SetBuildingLevel(fake, CFixedPoint.FromInt(2));
		Assert.Equal(CFixedPoint.FromInt(2), CMovementRules.BuildingLevel(state, state.Province(1), CMovementRules.AirBase));
	}

	[Fact]
	public void Of_two_buildings_carrying_a_roles_modifier_the_last_in_the_file_has_it()
	{
		var first = new CBuilding(0, "naval_base", 10, "naval_capacity", CFixedPoint.One);
		var second = new CBuilding(1, "harbour", 10, "naval_capacity", CFixedPoint.One);
		CGameState state = State(first, second);

		Assert.Same(second, CBuildingRoles.Find(state, CMovementRules.NavalBase));
		Assert.True(CBuildingRoles.Is(state, second, CMovementRules.NavalBase));
		Assert.False(CBuildingRoles.Is(state, first, CMovementRules.NavalBase));
	}

	[Fact]
	public void A_building_with_no_role_is_found_by_its_name_and_a_role_nobody_carries_falls_back_to_the_name()
	{
		var infra = new CBuilding(0, "infra", 10, "infrastructure", CFixedPoint.Parse("0.1"));
		var airBase = new CBuilding(1, "air_base", 10, "", CFixedPoint.Zero);
		CGameState state = State(infra, airBase);

		Assert.Same(infra, CBuildingRoles.Find(state, "infra"));
		Assert.Same(airBase, CBuildingRoles.Find(state, CMovementRules.AirBase));
		Assert.Null(CBuildingRoles.Find(state, "nothing"));
	}
}
