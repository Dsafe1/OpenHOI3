using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The buildings on the map and the click on a port or an airfield, against
/// <c>docs/mechanics/mapunits.md</c>, section 9. Kiel has a naval base, an air base and
/// industry, which does not show on the map; Hamburg an air base; the bay is sea.
/// </summary>
public class MapBuildingsTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Eng = new("ENG");

	private const int Kiel = 1;
	private const int Hamburg = 2;
	private const int Bay = 3;

	private static readonly CBuilding AirBase = new(0, "air_base", 10, "", CFixedPoint.Zero) { IsOnMap = true };
	private static readonly CBuilding NavalBase = new(1, "naval_base", 10, "", CFixedPoint.Zero) { IsOnMap = true };
	private static readonly CBuilding Industry = new(2, "industry", 10, "", CFixedPoint.Zero);

	private static readonly CBuildingPosition[][] Placed =
	{
		Array.Empty<CBuildingPosition>(),
		new[] { new CBuildingPosition("naval_base", 12.5f, 8.25f) },
		Array.Empty<CBuildingPosition>(),
		Array.Empty<CBuildingPosition>(),
	};

	private static readonly CMapPosition[] Own = { default, new(10, 10), new(20, 5), new(15, 15) };

	private static CGameState State(out CCountry germany, out CCountry england)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		england = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" };
		var kiel = new CProvince(Kiel, "Kiel", isSea: false) { Owner = Ger, Controller = Ger };
		kiel.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(2));
		kiel.SetBuildingLevel(AirBase, CFixedPoint.FromInt(1));
		kiel.SetBuildingLevel(Industry, CFixedPoint.FromInt(3));
		var hamburg = new CProvince(Hamburg, "Hamburg", isSea: false) { Owner = Ger, Controller = Ger };
		hamburg.SetBuildingLevel(AirBase, CFixedPoint.FromInt(4));
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), kiel, hamburg, new CProvince(Bay, "Bay", isSea: true) },
			new[] { germany, england }, buildings: new[] { AirBase, NavalBase, Industry });
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int location, int home)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = location, Base = home, Name = "Unit" };
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void AProvinceShowsEveryBuildingItHasThatIsDeclaredToShowOnTheMap()
	{
		CGameState state = State(out _, out _);

		List<CMapBuilding> buildings = CMapBuildings.Build(state, Placed, Own);

		Assert.Equal(new[] { (Kiel, "air_base"), (Kiel, "naval_base"), (Hamburg, "air_base") },
			buildings.Select(building => (building.Province, building.Building)));
		Assert.Equal(new[] { EMapBuilding.Airfield, EMapBuilding.Port, EMapBuilding.Airfield },
			buildings.Select(building => building.Kind));
	}

	[Fact]
	public void ABuildingStandsWhereThePositionsFilePutsItOrAtTheProvincesOwnPoint()
	{
		CGameState state = State(out _, out _);

		List<CMapBuilding> buildings = CMapBuildings.Build(state, Placed, Own);

		Assert.Equal((12.5f, 8.25f), (buildings[1].X, buildings[1].Y));        // as the file has it
		Assert.Equal((10.5f, 10.5f), (buildings[0].X, buildings[0].Y));        // the middle of its label point's pixel
	}

	[Fact]
	public void WhatThePlayerKnowsOfAProvinceDecidesWhichOfItsBuildingsShowAndWhetherABaseIsInUse()
	{
		var visibleNavalBase = new CBuilding(1, "naval_base", 10, "", CFixedPoint.Zero) { IsOnMap = true, IsAlwaysVisible = true };
		CGameState state = State(out CCountry germany, out CCountry england);
		state = new CGameState(state.Date, state.Provinces, new[] { germany, england },
			buildings: new[] { AirBase, visibleNavalBase, Industry });
		state.Province(Kiel).SetBuildingLevel(visibleNavalBase, CFixedPoint.FromInt(2));
		Unit(state, germany, EUnitKind.Navy, Kiel, Kiel);
		state.Province(Hamburg).Intel[england.Index] = 1;

		// England knows Kiel not at all: only the naval base, declared visibility, shows, and as not in use.
		List<CMapBuilding> buildings = CMapBuildings.Build(state, Placed, Own, Eng);
		Assert.Equal(new[] { (Kiel, "naval_base", 0), (Hamburg, "air_base", 0) },
			buildings.Select(building => (building.Province, building.Building, building.Frame)));

		// At 2 the fleet in port shows.
		state.Province(Kiel).Intel[england.Index] = 2;
		Assert.Equal(1, CMapBuildings.Build(state, Placed, Own, Eng).Single(building => building.Building == "naval_base").Frame);
	}

	[Fact]
	public void ABasesPictureChangesWhenAFleetLiesThereOrAnAirUnitIsAtHome()
	{
		CGameState state = State(out CCountry germany, out _);
		Assert.All(CMapBuildings.Build(state, Placed, Own), building => Assert.Equal(0, building.Frame));

		Unit(state, germany, EUnitKind.Navy, Kiel, Kiel);
		CUnit wing = Unit(state, germany, EUnitKind.Air, Bay, Hamburg);      // based at Hamburg and out over the bay
		List<CMapBuilding> buildings = CMapBuildings.Build(state, Placed, Own);
		Assert.Equal(new[] { 0, 1, 0 }, buildings.Select(building => building.Frame));

		wing.Location = Hamburg;
		Assert.Equal(new[] { 0, 1, 1 }, CMapBuildings.Build(state, Placed, Own).Select(building => building.Frame));
	}

	[Fact]
	public void AClickOnAPortGoesThroughThePlayersFleetsThere()
	{
		CGameState state = State(out CCountry germany, out CCountry england);
		CUnit first = Unit(state, germany, EUnitKind.Navy, Kiel, Kiel);
		CUnit foreign = Unit(state, england, EUnitKind.Navy, Kiel, Kiel);
		CUnit second = Unit(state, germany, EUnitKind.Navy, Kiel, Kiel);
		CUnit atSea = Unit(state, germany, EUnitKind.Navy, Bay, Kiel);

		// The list is turned by one before the first is taken; then round and round.
		Assert.Same(second, CMapBuildings.Next(state, Ger, Kiel, EMapBuilding.Port, null));
		Assert.Same(first, CMapBuildings.Next(state, Ger, Kiel, EMapBuilding.Port, second));
		Assert.Same(second, CMapBuildings.Next(state, Ger, Kiel, EMapBuilding.Port, first));
		Assert.Same(foreign, CMapBuildings.Next(state, Eng, Kiel, EMapBuilding.Port, null));
		Assert.DoesNotContain(atSea, CMapBuildings.UnitsOf(state, Kiel, EMapBuilding.Port));
		Assert.Null(CMapBuildings.Next(state, Ger, Hamburg, EMapBuilding.Port, null));
	}

	[Fact]
	public void AClickOnAnAirfieldGoesThroughTheAirUnitsBasedThereWhereverTheyAre()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit home = Unit(state, germany, EUnitKind.Air, Hamburg, Hamburg);
		CUnit away = Unit(state, germany, EUnitKind.Air, Bay, Hamburg);
		Unit(state, germany, EUnitKind.Air, Hamburg, Kiel);                  // here, and based elsewhere

		Assert.Equal(new[] { home, away }, CMapBuildings.UnitsOf(state, Hamburg, EMapBuilding.Airfield));
		Assert.Same(away, CMapBuildings.Next(state, Ger, Hamburg, EMapBuilding.Airfield, home));
		Assert.Same(home, CMapBuildings.Next(state, Ger, Hamburg, EMapBuilding.Airfield, away));
	}
}
