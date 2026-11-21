using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
		Assert.Equal((10f, 10f), (buildings[0].X, buildings[0].Y));            // its label point, as it is
	}

	[Fact]
	public void APlacedBuildingAndAnUnplacedOneAreBothWorldPointsAndDrawnHalfAPixelEastAndSouthOfThem()
	{
		// Both are world points: the original stands a building's picture at its place less the label point, hung on an
		// anchor at the label point, which comes to the place itself - and the label point where the file gives none.
		Assert.Equal((12.5f, 8.25f), CMapBuildings.PlaceOf(Placed, Own, Kiel, "naval_base"));
		Assert.Equal((10f, 10f), CMapBuildings.PlaceOf(Placed, Own, Kiel, "air_base"));
		Assert.Equal((20f, 5f), CMapBuildings.PlaceOf(Placed, Own, Hamburg, "NAVAL_BASE"));     // the name without case

		// The 2D map draws a world point - a pixel's middle on whole coordinates - half a pixel east and, its rows counting
		// down from the top, half a pixel south (mapdraw.md, section 3): on a map 100 pixels high, the world's (10, 10) is
		// the middle of the 2D map's pixel (10, 89), and the placed (12.5, 8.25) half a pixel off the file's figures.
		Assert.Equal(new Vector2(10.5f, 89.5f), CWorldPoint.ToMap(10f, 10f, 100));
		Assert.Equal(new Vector2(13f, 91.25f), CWorldPoint.ToMap(12.5f, 8.25f, 100));
		Assert.Equal(new Vector2(0.5f, 99.5f), CWorldPoint.ToMap(0f, 0f, 100));     // the south-west pixel's middle
	}

	[Fact]
	public void TheAirBaseStandsWhereTheBuildingWithItsRoleIsPlacedAndNotAtAnotherOfItsName()
	{
		// As BlackICE has it: `air_base` carries nothing and is placed nowhere, `fake_air_base` carries air_capacity.
		var named = new CBuilding(0, "air_base", 10, "", CFixedPoint.Zero);
		var role = new CBuilding(1, "fake_air_base", 10, "air_capacity", CFixedPoint.One) { IsOnMap = true };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(Kiel, "Kiel", isSea: false) },
			Array.Empty<CCountry>(), buildings: new[] { named, role });
		var placed = new[] { Array.Empty<CBuildingPosition>(), new[] { new CBuildingPosition("fake_air_base", 3f, 4f) } };

		Assert.Equal((3f, 4f), CMapBuildings.PlaceOfRole(state, placed, Own, Kiel, "air_base"));
		Assert.Equal((10f, 10f), CMapBuildings.PlaceOf(placed, Own, Kiel, "air_base"));       // by the name alone: the label point
		// With no building carrying the role, the building of the name, as before.
		CGameState plain = State(out _, out _);
		Assert.Equal((12.5f, 8.25f), CMapBuildings.PlaceOfRole(plain, Placed, Own, Kiel, "naval_base"));
	}

	[Fact]
	public void ABuildingPlacedOnItsLabelPointStandsWhereOneTheFileDoesNotPlaceStands()
	{
		CGameState state = State(out _, out _);
		var onTheLabel = new[]
		{
			Array.Empty<CBuildingPosition>(),
			new[] { new CBuildingPosition("naval_base", 10f, 10f) },
			Array.Empty<CBuildingPosition>(),
			Array.Empty<CBuildingPosition>(),
		};

		List<CMapBuilding> buildings = CMapBuildings.Build(state, onTheLabel, Own);

		// Kiel's air base has no place and its naval base is placed on Kiel's label point: one point, drawn at one place.
		Assert.Equal((Kiel, "air_base"), (buildings[0].Province, buildings[0].Building));
		Assert.Equal((Kiel, "naval_base"), (buildings[1].Province, buildings[1].Building));
		Assert.Equal((buildings[0].X, buildings[0].Y), (buildings[1].X, buildings[1].Y));
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
