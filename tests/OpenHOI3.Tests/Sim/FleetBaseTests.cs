using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>A fleet's base as the original's unit loader leaves it: <c>CFleetBase</c>.</summary>
public class FleetBaseTests
{
	private static readonly CCountryTag Fra = new("FRA");
	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "naval_capacity", CFixedPoint.One);

	private static (CGameState State, CCountry France) World(params int[] navalBases)
	{
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int id = 1; id <= 5; id++)
		{
			provinces.Add(new CProvince(id, $"P{id}", isSea: false) { Owner = Fra, Controller = Fra });
		}
		foreach (int id in navalBases)
		{
			provinces[id].SetBuildingLevel(NavalBase, CFixedPoint.FromInt(3));
		}
		var france = new CCountry { Tag = Fra, Name = "France", DefinitionFile = "", Capital = 1 };
		return (new CGameState(new CDate(1936, 1, 1), provinces, new[] { france }, new[] { NavalBase }), france);
	}

	[Fact]
	public void A_base_with_no_naval_base_is_not_kept_and_the_first_of_the_countrys_bases_is_given_from_a_coast_with_no_port()
	{
		(CGameState state, CCountry france) = World(3, 4);
		var fleet = new CUnit(EUnitKind.Navy, Fra) { Location = 5, Base = 5 };

		Assert.False(CFleetBase.MayBeBase(state, fleet.Base));
		fleet.Base = 0;
		// Djibouti's case: no place in the table of distances, so every base measures nought and the first in the list wins.
		CFleetBase.Check(state, france, fleet);

		Assert.Equal(3, fleet.Base);
	}

	[Fact]
	public void A_fleet_based_where_no_enemy_holds_keeps_its_base()
	{
		(CGameState state, CCountry france) = World(3, 4);
		var fleet = new CUnit(EUnitKind.Navy, Fra) { Location = 5, Base = 4 };

		Assert.True(CFleetBase.MayBeBase(state, fleet.Base));
		CFleetBase.Check(state, france, fleet);

		Assert.Equal(4, fleet.Base);
	}

	[Fact]
	public void A_country_with_no_naval_base_and_no_faction_leaves_its_fleet_with_none()
	{
		(CGameState state, CCountry france) = World();
		var fleet = new CUnit(EUnitKind.Navy, Fra) { Location = 5, Base = 0 };

		CFleetBase.Check(state, france, fleet);

		Assert.Equal(0, fleet.Base);
		Assert.Empty(CFleetBase.NavalBases(state, Fra));
	}
}
