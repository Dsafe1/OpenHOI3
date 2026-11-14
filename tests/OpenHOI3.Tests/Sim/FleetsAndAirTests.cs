using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Fleets and air units against <c>docs/mechanics/fleetsandair.md</c>, on a small coast:
///
///     5 Kiel Bay ------ 6 Baltic ------ 8 Gulf
///         |            /     \
///     1 Kiel      3 Danzig - 4 Gdynia (Poland's)
///         |
///     2 Hamburg                          7 a lake
///
/// Kiel, Danzig and Gdynia have ports; Kiel's opens onto Kiel Bay and the other two onto
/// the Baltic. A step by sea is four times the pixels between the two provinces' centres.
/// </summary>
public class FleetsAndAirTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Hamburg = 2;
	private const int Danzig = 3;
	private const int Gdynia = 4;
	private const int KielBay = 5;
	private const int Baltic = 6;
	private const int Lake = 7;
	private const int Gulf = 8;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, "", CFixedPoint.Zero);

	private static CProvinceEdge Edge(int to, string distance = "10")
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.Parse(distance));
	}

	private static CProvince Land(int id, string name, CCountryTag owner, int x, int y, int portSea, int seaIndex,
		int navalBase, int airBase, params CProvinceEdge[] edges)
	{
		var province = new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = y, PortSea = portSea, SeaIndex = seaIndex, Edges = edges,
		};
		province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(navalBase));
		province.SetBuildingLevel(AirBase, CFixedPoint.FromInt(airBase));
		return province;
	}

	private static CProvince Sea(int id, string name, int x, int y, int seaIndex, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: true) { CentreX = x, CentreY = y, SeaIndex = seaIndex, Edges = edges };
	}

	private static CGameState State(out CCountry germany, IEnumerable<CWar>? wars = null)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CMovementSpeed.NavalSpeedModifier, CFixedPoint.Parse("0.5"));
		defines.Set(CManpower.MilitarySection, CMovementSpeed.AirSpeedModifier, CFixedPoint.Parse("0.3"));
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Kiel, "Kiel", Ger, 10, 10, KielBay, 0, navalBase: 2, airBase: 1, Edge(Hamburg), Edge(KielBay), Edge(Baltic)),
				Land(Hamburg, "Hamburg", Ger, 10, 0, 0, -1, navalBase: 0, airBase: 3, Edge(Kiel)),
				Land(Danzig, "Danzig", Ger, 40, 10, Baltic, 1, navalBase: 1, airBase: 0, Edge(Baltic), Edge(Gdynia)),
				Land(Gdynia, "Gdynia", Pol, 40, 0, Baltic, 2, navalBase: 1, airBase: 1, Edge(Baltic), Edge(Danzig)),
				Sea(KielBay, "Kiel Bay", 10, 20, 3, Edge(Kiel), Edge(Baltic)),
				Sea(Baltic, "Baltic", 40, 20, 4, Edge(KielBay), Edge(Kiel), Edge(Danzig), Edge(Gdynia), Edge(Gulf)),
				Sea(Lake, "Lake", 90, 90, -1),
				Sea(Gulf, "Gulf", 70, 20, 5, Edge(Baltic)),
			},
			new[] { germany, poland }, buildings: new[] { NavalBase, AirBase }, defines: defines, wars: wars)
		{
			MapWidth = 1000,
		};
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, string speed, string range)
	{
		var type = new CSubUnitType(kind == EUnitKind.Navy ? "destroyer" : "interceptor",
			kind == EUnitKind.Navy ? EUnitBranch.Naval : EUnitBranch.Air);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.Parse(speed));
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.Parse(range));
		// The most organisation it may have: an air unit away from its base with none is sent home.
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(100));
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Base = province, Name = "Unit" };
		var subUnit = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit };
		subUnit.Stats.CopyFrom(type.Stats);
		unit.SubUnits.Add(subUnit);
		country.Units.Add(unit);
		return unit;
	}

	private static CUnit Fleet(CGameState state, CCountry country, int province, string range = "1000")
	{
		return Unit(state, country, EUnitKind.Navy, province, "20", range);
	}

	private static CUnit Wing(CGameState state, CCountry country, int province, string range = "1000")
	{
		return Unit(state, country, EUnitKind.Air, province, "200", range);
	}

	// --- distances ---

	[Fact]
	public void AStraightLineIsFourTimesThePixelsBetweenTwoCentresTheShortWayRound()
	{
		CGameState state = State(out _);
		Assert.Equal(40, CMapDistance.Between(state.Province(Kiel), state.Province(KielBay), 1000));
		Assert.Equal(120, CMapDistance.Between(state.Province(KielBay), state.Province(Baltic), 1000));
		// 30 across and 10 up: the root of 16 * 1000, rounded down.
		Assert.Equal(126, CMapDistance.Between(state.Province(Kiel), state.Province(Baltic), 1000));
		// On a map 35 wide the two are 5 apart the other way round.
		Assert.Equal(20, CMapDistance.Between(state.Province(KielBay), state.Province(Baltic), 35));
	}

	[Fact]
	public void ADistanceBySeaIsTheShortestSumOfStepsBetweenPortsAndSeas()
	{
		CGameState state = State(out _);
		CNavalDistances distances = state.NavalDistances;

		Assert.Equal(6, distances.Count);
		// Kiel touches the Baltic too, and the straight step is shorter than the way round by its bay.
		Assert.Equal(126, distances.Between(state.Province(Kiel), state.Province(Baltic)));
		Assert.Equal(166, distances.Between(state.Province(Kiel), state.Province(Danzig)));
		// Two ports that touch are joined, so the way to Gdynia may go through Danzig's.
		Assert.Equal(40, distances.Between(state.Province(Danzig), state.Province(Gdynia)));
		Assert.Equal(distances.Between(state.Province(Kiel), state.Province(Danzig)),
			distances.Between(state.Province(Danzig), state.Province(Kiel)));
		Assert.Equal(0, distances.Between(state.Province(Kiel), state.Province(Kiel)));
	}

	[Fact]
	public void AProvinceWithNoPlaceInTheTableIsNoDistanceAway()
	{
		CGameState state = State(out _);
		// A lake has no number, and a land province with no port has none to borrow.
		Assert.Equal(0, state.NavalDistances.Between(state.Province(Kiel), state.Province(Lake)));
		Assert.Equal(0, state.NavalDistances.Between(state.Province(Hamburg), state.Province(Baltic)));
		Assert.Equal(-1, state.NavalDistances.IndexOf(state.Province(Hamburg)));
	}

	// --- where a fleet may be ---

	[Fact]
	public void AFleetLiesOnLandOnlyInAFriendsPortThatHasANavalBase()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel);

		Assert.True(CMovementRules.MayStandIn(state, country, fleet, state.Province(Danzig)));
		Assert.True(CMovementRules.MayStandIn(state, country, fleet, state.Province(Baltic)));
		Assert.False(CMovementRules.MayStandIn(state, country, fleet, state.Province(Hamburg)));   // no port
		Assert.False(CMovementRules.MayStandIn(state, country, fleet, state.Province(Gdynia)));    // no friend's

		state.Province(Danzig).SetBuildingLevel(NavalBase, CFixedPoint.Zero);
		Assert.False(CMovementRules.MayStandIn(state, country, fleet, state.Province(Danzig)));    // no naval base
	}

	[Fact]
	public void AFleetGoesBetweenLandAndSeaOnlyThroughThePortsOwnSea()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel);
		CProvince kiel = state.Province(Kiel);

		// Kiel touches the Baltic as well, but its port opens onto Kiel Bay.
		Assert.True(CMovementRules.MayStep(state, country, fleet, kiel, state.Province(KielBay), Edge(KielBay)));
		Assert.False(CMovementRules.MayStep(state, country, fleet, kiel, state.Province(Baltic), Edge(Baltic)));
		Assert.False(CMovementRules.MayStep(state, country, fleet, state.Province(Baltic), kiel, Edge(Kiel)));
		Assert.False(CMovementRules.MayStep(state, country, fleet, kiel, state.Province(Hamburg), Edge(Hamburg)));
		Assert.Equal(new[] { KielBay, Baltic, Danzig }, CRouteFinder.Find(state, country, fleet, Kiel, Danzig));
	}

	[Fact]
	public void AFleetPutsToSeaFromAPortItMayNoLongerLieIn()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Gdynia);

		// Gdynia is no friend's port, and the fleet in it may leave all the same.
		Assert.False(CMovementRules.MayStandIn(state, country, fleet, state.Province(Gdynia)));
		Assert.True(CMovementRules.MayStep(state, country, fleet, state.Province(Gdynia), state.Province(Baltic), Edge(Baltic)));
	}

	[Fact]
	public void AWaterTheDefinesNameIsClosedWhileAnEnemyHoldsItsBlocker()
	{
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		CGameState state = State(out CCountry country, new[] { war });
		CUnit fleet = Fleet(state, country, Kiel);

		Assert.True(CMovementRules.MayStandIn(state, country, fleet, state.Province(Gulf)));
		state.Defines.Set(CMovementRules.MapSection, "SUEZ", CFixedPoint.FromInt(Gulf));
		state.Defines.Set(CMovementRules.MapSection, "SUEZ_BLOCKER", CFixedPoint.FromInt(Gdynia));
		Assert.False(CMovementRules.MayStandIn(state, country, fleet, state.Province(Gulf)));
		Assert.Null(CRouteFinder.Find(state, country, fleet, Kiel, Gulf));

		// Held by the fleet's own country, the blocker closes nothing.
		state.Province(Gdynia).Controller = Ger;
		Assert.True(CMovementRules.MayStandIn(state, country, fleet, state.Province(Gulf)));
	}

	// --- range and the orders ---

	[Fact]
	public void AUnitReachesAsFarAsItsShortestRangedShipFromItsBase()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel, range: "160");
		CUnit wing = Wing(state, country, Kiel, range: "120");

		Assert.Equal(CFixedPoint.FromInt(160), CUnitOrders.RangeOf(fleet));
		Assert.True(CUnitOrders.IsInRange(state, fleet, EUnitOrder.Move, state.Province(Baltic)));     // 126
		Assert.False(CUnitOrders.IsInRange(state, fleet, EUnitOrder.Move, state.Province(Danzig)));    // 166
		// A change of base is never out of reach, nor anything for a unit with no base.
		Assert.True(CUnitOrders.IsInRange(state, fleet, EUnitOrder.Rebase, state.Province(Danzig)));
		fleet.Base = 0;
		Assert.True(CUnitOrders.IsInRange(state, fleet, EUnitOrder.Move, state.Province(Danzig)));

		// An air unit's is a straight line: Danzig is 120 from Kiel and the Baltic 126.
		Assert.Equal(CFixedPoint.FromInt(120), CUnitOrders.DistanceFromBase(state, wing, state.Province(Danzig)));
		Assert.True(CUnitOrders.IsInRange(state, wing, EUnitOrder.Move, state.Province(Danzig)));
		Assert.False(CUnitOrders.IsInRange(state, wing, EUnitOrder.Move, state.Province(Baltic)));
	}

	[Fact]
	public void AMoveIsGivenWithinRangeAndARebaseWhereverThereIsAFriendsBase()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel, range: "160");

		Assert.True(CUnitOrders.MayGive(state, country, fleet, EUnitOrder.Move, state.Province(Baltic)));
		Assert.False(CUnitOrders.MayGive(state, country, fleet, EUnitOrder.Move, state.Province(Danzig)));    // out of range
		Assert.True(CUnitOrders.MayGive(state, country, fleet, EUnitOrder.Rebase, state.Province(Danzig)));
		Assert.False(CUnitOrders.MayGive(state, country, fleet, EUnitOrder.Rebase, state.Province(Baltic)));  // no base there
		Assert.False(CUnitOrders.MayGive(state, country, fleet, EUnitOrder.Rebase, state.Province(Gdynia)));  // no friend's
		Assert.False(CUnitOrders.MayGive(state, country, fleet, EUnitOrder.RebaseAir, state.Province(Danzig)));

		// The window ticks a rebase where the province has a base for one, and a move otherwise.
		Assert.Equal(EUnitOrder.Rebase, CUnitOrders.Suggested(state, country, fleet, state.Province(Danzig)));
		Assert.Equal(EUnitOrder.Move, CUnitOrders.Suggested(state, country, fleet, state.Province(Baltic)));
		Assert.Equal(EUnitOrder.None, CUnitOrders.Suggested(state, country, fleet, state.Province(Gulf)));    // 246 away
	}

	[Fact]
	public void ARebaseMakesThePortTheFleetsBaseTheFirstHourAndIsDoneWhenTheFleetIsThere()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel);
		var simulation = new CSimulation(state);

		simulation.Post(new CSetUnitOrderCommand(Ger, fleet.Id, EUnitOrder.Rebase, Danzig));
		simulation.Pump();
		Assert.Equal(EUnitOrder.Rebase, fleet.Order);
		Assert.Equal(new[] { KielBay, Baltic, Danzig }, fleet.Path);
		Assert.Equal(Kiel, fleet.Base);                                 // not until the order is first looked at

		// At 20 and half of it an hour, each edge of 10 is one hour.
		CMovement.RunHourly(state);
		Assert.Equal(Danzig, fleet.Base);
		Assert.Equal(KielBay, fleet.Location);
		CMovement.RunHourly(state);
		CMovement.RunHourly(state);
		Assert.Equal(Danzig, fleet.Location);
		Assert.Equal(EUnitOrder.Rebase, fleet.Order);                   // it arrived after this hour's look
		CMovement.RunHourly(state);
		Assert.Equal(EUnitOrder.None, fleet.Order);
		Assert.Equal(0, fleet.OrderProvince);
	}

	[Fact]
	public void AMoveOrderIsDroppedWhenTheFleetArrivesOrHasNoWayLeft()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel);

		CUnitOrders.Give(state, country, fleet, EUnitOrder.Move, Baltic);
		Assert.Equal(new[] { KielBay, Baltic }, fleet.Path);
		CMovement.Cancel(fleet);
		CMovement.RunHourly(state);
		Assert.Equal(EUnitOrder.None, fleet.Order);
		Assert.Equal(Kiel, fleet.Base);                                 // a move leaves the base alone
	}

	[Fact]
	public void AnAirUnitChangesItsBaseAtOnceAndLandsWithHalfItsOrganisation()
	{
		CGameState state = State(out CCountry country);
		CUnit wing = Wing(state, country, Kiel);
		wing.SubUnits[0].Organisation = CFixedPoint.FromInt(60);

		Assert.True(CUnitOrders.MayGive(state, country, wing, EUnitOrder.RebaseAir, state.Province(Hamburg)));
		Assert.False(CUnitOrders.MayGive(state, country, wing, EUnitOrder.RebaseAir, state.Province(Danzig)));   // no air base
		Assert.False(CUnitOrders.MayGive(state, country, wing, EUnitOrder.RebaseAir, state.Province(Gdynia)));   // no friend's
		Assert.False(CUnitOrders.MayGive(state, country, wing, EUnitOrder.RebaseAir, state.Province(Baltic)));   // the sea
		Assert.False(CUnitOrders.MayGive(state, country, wing, EUnitOrder.Move, state.Province(Hamburg)));       // no such order for it

		CUnitOrders.Give(state, country, wing, EUnitOrder.RebaseAir, Hamburg);
		Assert.Equal(new[] { Hamburg }, wing.Path);
		CMovement.RunHourly(state);                                     // base changed, then flown: 60 an hour over 10
		Assert.Equal(Hamburg, wing.Base);
		Assert.Equal(Hamburg, wing.Location);
		Assert.Equal(CFixedPoint.FromInt(60), wing.SubUnits[0].Organisation);
		CMovement.RunHourly(state);                                     // and the next look finds it there
		Assert.Equal(CFixedPoint.FromInt(30), wing.SubUnits[0].Organisation);
		Assert.Equal(EUnitOrder.None, wing.Order);
	}

	[Fact]
	public void AFleetSentStraightThereGoesOnlyWithinItsRangeAndAnAirUnitOnlyByAPlayer()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel, range: "160");
		CUnit wing = Wing(state, country, Kiel);
		var simulation = new CSimulation(state);

		Assert.True(new CMoveUnitCommand(Ger, fleet.Id, Baltic).MayExecute(state));
		Assert.False(new CMoveUnitCommand(Ger, fleet.Id, Danzig).MayExecute(state));
		Assert.False(new CMoveUnitCommand(Ger, fleet.Id, Hamburg).MayExecute(state));
		// An air unit a player sends straight there - from a battle - is asked nothing, beyond its range too; the AI's move
		// takes none.
		Assert.True(new CMoveUnitCommand(Ger, wing.Id, Hamburg).MayExecute(state));
		Assert.False(new CMoveUnitCommand(Ger, wing.Id, Hamburg, ai: true).MayExecute(state));

		// It takes the place of whatever order the fleet had.
		CUnitOrders.Give(state, country, fleet, EUnitOrder.Rebase, Danzig);
		simulation.Post(new CMoveUnitCommand(Ger, fleet.Id, Baltic));
		simulation.Pump();
		Assert.Equal(EUnitOrder.None, fleet.Order);
		Assert.Equal(new[] { KielBay, Baltic }, fleet.Path);
	}

	[Fact]
	public void AFleetSailsAtItsSlowestShipsSpeedTimesTheDefineForShips()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Fleet(state, country, Kiel);
		CUnit wing = Wing(state, country, Kiel);

		Assert.Equal(CFixedPoint.FromInt(10), CMovementSpeed.Speed(state, country, fleet));
		Assert.Equal(CFixedPoint.FromInt(60), CMovementSpeed.Speed(state, country, wing));
	}

	[Fact]
	public void AnAirUnitFliesOverTheSeaAndItsCountrysGroundAndNotOverANeutrals()
	{
		CGameState state = State(out CCountry country);
		CUnit wing = Wing(state, country, Danzig);

		Assert.True(CMovementRules.MayStandIn(state, country, wing, state.Province(Baltic)));
		Assert.True(CMovementRules.MayStandIn(state, country, wing, state.Province(Hamburg)));
		Assert.False(CMovementRules.MayStandIn(state, country, wing, state.Province(Gdynia)));
	}
}
