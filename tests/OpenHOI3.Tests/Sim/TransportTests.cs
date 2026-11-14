using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Land units carried by sea and the landing, against <c>docs/mechanics/transport.md</c>, on a small coast:
///
///     6 Rostock --- 1 Kiel ==port== 3 Bight --- 4 Deep ==port== 2 Gdynia (Poland's)
///                                       \        /
///                                       5 Hela (Poland's, no port)
///
/// A ship covers a province in two hours; an infantry brigade weighs 10 and a transport carries 40.
/// </summary>
public class TransportTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Gdynia = 2;
	private const int Bight = 3;
	private const int Deep = 4;
	private const int Hela = 5;
	private const int Rostock = 6;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));

	private static CProvince Land(int id, string name, CCountryTag owner, int x, int portSea, params CProvinceEdge[] edges)
	{
		var province = new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = 10, PortSea = portSea, SeaIndex = -1, Edges = edges,
		};
		if (portSea != 0)
		{
			province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(2));
		}
		return province;
	}

	private static CProvince Sea(int id, string name, int x, int seaIndex, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: true) { CentreX = x, CentreY = 20, SeaIndex = seaIndex, Edges = edges };
	}

	private static CSubUnitType Ship(string key, string capability = "0", string speed = "0", string defence = "0")
	{
		var type = new CSubUnitType(key, EUnitBranch.Naval);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "10");
		Set(CUnitOrders.Range, "1000");
		Set(CSubUnitType.MaxStrength, "100");
		Set(CTransport.TransportCapability, capability);
		Set(CLanding.InvasionSpeed, speed);
		Set(CLanding.InvasionDefence, defence);
		type.Flags[CShipCensus.TransportFlag] = capability != "0";
		return type;
	}

	private static readonly CSubUnitType TransportShip = Ship("transport_ship", capability: "40");
	private static readonly CSubUnitType InvasionShip = Ship("invasion_transport_ship", capability: "60", speed: "0.5", defence: "0.1");
	private static readonly CSubUnitType Destroyer = Ship("destroyer");

	private static readonly CSubUnitType Infantry = Brigade();

	private static CSubUnitType Brigade()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "4");
		Set(CSubUnitType.TransportWeight, "10");
		Set(CSubUnitType.CombatWidth, "1");
		Set(CSubUnitType.MaxStrength, "3");
		return type;
	}

	private static CGameState State(out CCountry germany, out CCountry poland)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CMovementSpeed.NavalSpeedModifier, "0.5");
		Military(CMovementSpeed.LandSpeedModifier, "1");
		Military(CLanding.SpeedBase, "0.5");
		Military(CLanding.MovementCost, "24");
		Military(CLanding.AttackLow, "0.2");
		Military(CLanding.AttackHigh, "1.0");
		Military(CLanding.DefendLow, "1.5");
		Military(CLanding.DefendHigh, "1.0");
		Military(CLanding.PenaltyDecrease, "3.5");
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Kiel, "Kiel", Ger, 10, Bight, Edge(Bight), Edge(Rostock)),
				Land(Gdynia, "Gdynia", Pol, 50, Deep, Edge(Deep), Edge(Hela)),
				Sea(Bight, "Bight", 30, 0, Edge(Kiel), Edge(Deep), Edge(Hela)),
				Sea(Deep, "Deep", 50, 1, Edge(Bight), Edge(Gdynia), Edge(Hela)),
				Land(Hela, "Hela", Pol, 40, 0, Edge(Bight), Edge(Deep), Edge(Gdynia)),
				Land(Rostock, "Rostock", Ger, 0, 0, Edge(Kiel)),
			},
			new[] { germany, poland }, buildings: CTestRoads.With(NavalBase), defines: defines, wars: new List<CWar> { war })
		{
			MapWidth = 1000,
			MapHeight = 1000,
		}.Paved();
	}

	private static CUnit Fleet(CGameState state, CCountry country, int province, params CSubUnitType[] ships)
	{
		var fleet = new CUnit(EUnitKind.Navy, country.Tag) { Id = ++state.LastUnitId, Location = province, Base = Kiel, Name = "fleet" };
		foreach (CSubUnitType type in ships)
		{
			var ship = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = fleet, Strength = CFixedPoint.FromInt(100), Organisation = CFixedPoint.FromInt(30),
				Name = type.Key,
			};
			ship.Stats.CopyFrom(type.Stats);
			fleet.SubUnits.Add(ship);
		}
		country.Units.Add(fleet);
		state.ForgetPositions();
		return fleet;
	}

	private static CUnit Division(CGameState state, CCountry country, int province, int brigades = 3)
	{
		var division = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = "division" };
		for (int i = 0; i < brigades; i++)
		{
			var brigade = new CSubUnit(Infantry)
			{
				Id = ++state.LastUnitId, Unit = division, Strength = CFixedPoint.FromInt(3), Organisation = CFixedPoint.FromInt(30),
				Name = "brigade " + i,
				// The most it has had: a brigade that never had any is under its shatter threshold, and lands nowhere an enemy holds.
				Highest = CFixedPoint.FromInt(3),
			};
			brigade.Stats.CopyFrom(Infantry.Stats);
			division.SubUnits.Add(brigade);
		}
		country.Units.Add(division);
		state.ForgetPositions();
		return division;
	}

	// --- what a fleet carries ---

	[Fact]
	public void AFleetCarriesItsShipsCapabilityAgainstEachUnitsWeightAndOnlyItsOwnersUnits()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fleet = Fleet(state, germany, Kiel, TransportShip, Destroyer);
		CUnit three = Division(state, germany, Kiel);
		CUnit five = Division(state, germany, Kiel, brigades: 5);

		Assert.Equal(40, CTransport.Capacity(fleet));
		Assert.Equal(30, CTransport.Weight(three));
		Assert.True(CTransport.CanCarry(fleet, three));
		Assert.False(CTransport.CanCarry(fleet, five));
		Assert.False(CTransport.CanCarry(fleet, Division(state, poland, Kiel)));

		CTransport.Board(fleet, three);
		Assert.Equal(30, CTransport.Load(fleet));
		Assert.False(CTransport.CanCarry(fleet, Division(state, germany, Kiel, brigades: 2)));
		Assert.True(CTransport.CanCarry(fleet, Division(state, germany, Kiel, brigades: 1)));

		// A fleet going somewhere takes nobody aboard.
		fleet.Path.Add(Bight);
		Assert.False(CTransport.CanCarry(fleet, Division(state, germany, Kiel, brigades: 1)));
	}

	// --- boarding and getting off ---

	[Fact]
	public void TheLoadCommandBoardsWhereverTheTwoAreKeepsThePathAndDropsTheOrder()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Deep, TransportShip);
		CUnit division = Division(state, germany, Rostock);
		division.Path.Add(Kiel);
		division.Order = EUnitOrder.Move;
		division.DigIn = CFixedPoint.FromInt(2);

		var load = new CLoadUnitCommand(Ger, division.Id, fleet.Id);
		Assert.True(load.MayExecute(state));
		load.Execute(state);

		Assert.Same(fleet, division.Transport);
		Assert.Equal(new[] { division }, fleet.Passengers);
		Assert.Equal(Rostock, division.Location);
		Assert.Equal(new[] { Kiel }, division.Path);
		Assert.Equal(EUnitOrder.None, division.Order);
		Assert.Equal(CFixedPoint.Zero, division.DigIn);
		// A fleet may not be ordered on while a passenger has a path.
		Assert.False(CMovementRules.MayMoveTo(state, germany, fleet, state.Province(Bight)));
	}

	[Fact]
	public void TheUnloadCommandWantsAPortAndBringsNothing()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(fleet, division);

		Assert.False(new CUnloadUnitCommand(Ger, division.Id).MayExecute(state));

		fleet.Location = Kiel;
		division.Location = Kiel;
		state.ForgetPositions();
		var unload = new CUnloadUnitCommand(Ger, division.Id);
		Assert.True(unload.MayExecute(state));
		unload.Execute(state);
		Assert.Null(division.Transport);
		Assert.Empty(fleet.Passengers);
		Assert.Equal(Kiel, division.Location);
	}

	[Fact]
	public void ALandUnitMarchingIntoTheSeaBoardsTheFirstFleetThereWithRoomAndItsWayEnds()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit full = Fleet(state, germany, Bight, Destroyer);
		CUnit fleet = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Kiel);

		Assert.True(CMovementRules.MayStandIn(state, germany, division, state.Province(Bight)));
		Assert.False(CMovementRules.MayStandIn(state, germany, division, state.Province(Deep)));
		CMovement.SetPath(division, new[] { Bight }, clear: true);
		for (int hour = 0; hour < 10 && division.Transport is null; hour++)
		{
			CMovement.RunHour(state, germany, division);
		}

		Assert.Same(fleet, division.Transport);
		Assert.Empty(full.Passengers);
		Assert.Equal(Bight, division.Location);
		Assert.Equal(Kiel, division.Previous);
		Assert.False(division.IsMoving);
		// Aboard, it may not be sent on to another sea (ours).
		Assert.False(CMovementRules.MayStandIn(state, germany, division, state.Province(Deep)));
	}

	[Fact]
	public void PassengersFollowTheFleetAndGetOffWhereItEndsItsWayInPort()
	{
		CGameState state = State(out CCountry germany, out _);
		state.Province(Gdynia).Owner = Ger;
		state.Province(Gdynia).Controller = Ger;
		CUnit fleet = Fleet(state, germany, Kiel, TransportShip);
		CUnit division = Division(state, germany, Kiel);
		CTransport.Board(fleet, division);

		CMovement.SetPath(fleet, new[] { Bight, Deep, Gdynia }, clear: true);
		var seen = new List<int>();
		for (int hour = 0; hour < 20 && fleet.IsMoving; hour++)
		{
			CMovement.RunHour(state, germany, fleet);
			if (division.Transport is not null && (seen.Count == 0 || seen[^1] != division.Location))
			{
				seen.Add(division.Location);
			}
		}

		Assert.Equal(new[] { Kiel, Bight, Deep }, seen);
		Assert.Equal(Gdynia, fleet.Location);
		Assert.Equal(Gdynia, division.Location);
		Assert.Null(division.Transport);
		Assert.Empty(fleet.Passengers);
	}

	// --- too much aboard, a sea battle, the fleet's end ---

	[Fact]
	public void TooMuchAboardTakesBrigadesAtSeaAndPutsUnitsOffInPort()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, TransportShip, TransportShip);
		CUnit first = Division(state, germany, Bight);
		CUnit second = Division(state, germany, Bight);
		CTransport.Board(fleet, first);
		CTransport.Board(fleet, second);
		fleet.SubUnits.RemoveAt(1);

		CTransport.CheckOverload(state, fleet);
		// 60 aboard and 40 of room: the first passenger loses brigades, the first one first, until it fits.
		Assert.Single(first.SubUnits);
		Assert.Equal("brigade 2", first.SubUnits[0].Name);
		Assert.Equal(3, second.SubUnits.Count);

		fleet.SubUnits.Clear();
		CTransport.CheckOverload(state, fleet);
		Assert.True(first.IsRemoved);
		Assert.True(second.IsRemoved);
		Assert.Empty(fleet.Passengers);

		CUnit inPort = Fleet(state, germany, Kiel, Destroyer);
		CUnit ashore = Division(state, germany, Kiel);
		CTransport.Board(inPort, ashore);
		CTransport.CheckOverload(state, inPort);
		Assert.Null(ashore.Transport);
		Assert.Equal(3, ashore.SubUnits.Count);
	}

	[Fact]
	public void ATransportsDamageIsSharedOverEveryBrigadeAboard()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(fleet, division);

		// 3 points of damage over 3 brigades: a point of strength and half a point of organisation each.
		CTransport.LayDamage(fleet, CFixedPoint.FromInt(3).Raw);
		Assert.All(division.SubUnits, brigade =>
		{
			Assert.Equal(CFixedPoint.FromInt(2), brigade.Strength);
			Assert.Equal(CFixedPoint.Parse("29.5"), brigade.Organisation);
		});
	}

	[Fact]
	public void AFleetLeavingTheGameTakesItsPassengers()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(fleet, division);

		CCombatRetreat.RemoveFromGame(state, fleet);
		Assert.True(division.IsRemoved);
		Assert.Empty(fleet.Passengers);
	}

	[Fact]
	public void AnIdlePassengerFightsNoOneAndIsFoughtByNoOne()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fleet = Fleet(state, germany, Kiel, TransportShip);
		CUnit passenger = Division(state, germany, Kiel);
		CTransport.Board(fleet, passenger);
		CUnit enemy = Division(state, poland, Kiel);

		Assert.False(CCombats.MayEngage(state, enemy, passenger));
		Assert.False(CCombats.HasEnemyStanding(state, enemy, Kiel));
	}

	// --- the landing ---

	[Fact]
	public void ALandingIsTwentyFourAtTheLandingsSpeedAndTakesAnEmptyCoastOnArrival()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(fleet, division);

		CMovement.SetPath(division, new[] { Hela }, clear: true);
		Assert.Equal(CFixedPoint.FromInt(24), CMovement.Distance(state, division));
		Assert.Equal(CFixedPoint.Parse("0.5"), CMovementSpeed.Speed(state, germany, division));
		Assert.Equal(48, CMovement.HoursToArrive(state, germany, division));

		int hours = 0;
		while (division.Location != Hela && hours < 100)
		{
			CMovement.RunHour(state, germany, division);
			hours++;
			if (hours == 2)
			{
				// Two hours in: 1.0 of 24, so 41 thousandths - the maintainer's screenshot's 4.10%.
				Assert.Equal(41, CLanding.Progress(state, division));
				Assert.Same(fleet, division.Transport);
			}
		}
		Assert.Equal(48, hours);
		Assert.Null(division.Transport);
		Assert.Equal(Bight, division.Previous);
		Assert.Equal(Ger, state.Province(Hela).Controller);
	}

	[Fact]
	public void AFleetsInvasionSpeedIsTheMeanOfTheShipsThatHaveOne()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, InvasionShip, InvasionShip, Destroyer);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(fleet, division);
		CMovement.SetPath(division, new[] { Hela }, clear: true);

		Assert.Equal(CFixedPoint.FromInt(1), CMovementSpeed.Speed(state, germany, division));
		Assert.Equal(CFixedPoint.Parse("0.2").Raw, CLanding.FleetDefence(fleet));
	}

	[Fact]
	public void TheLandingScalesAreTheScreenshotsFigures()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(fleet, division);
		CMovement.SetPath(division, new[] { Hela }, clear: true);

		division.MovementProgress = CFixedPoint.FromInt(1);
		CLanding.SetScales(state, division);
		Assert.Equal(232, division.LandingAttack.Raw);
		// -500 * 41 / 1000 is cut toward nought: 1480, not 1479.
		Assert.Equal(1480, division.LandingDefence.Raw);

		division.MovementProgress = CFixedPoint.Parse("9.5");
		CLanding.SetScales(state, division);
		Assert.Equal(516, division.LandingAttack.Raw);
		Assert.Equal(1303, division.LandingDefence.Raw);

		// Onto a friend's coast there are no scales.
		state.Province(Hela).Controller = Ger;
		CLanding.SetScales(state, division);
		Assert.Equal(CFixedPoint.One, division.LandingAttack);
		Assert.Equal(CFixedPoint.One, division.LandingDefence);
	}

	[Fact]
	public void TheLandingPenaltyIsAHundredthWorseWithNoInvasionDefenceAndCutByOne()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit plain = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(plain, division);
		CMovement.SetPath(division, new[] { Hela }, clear: true);
		Assert.Equal(-707, CLanding.CutPenalty(state, division, -700));

		CUnit craft = Fleet(state, germany, Bight, Ship("assault", capability: "60", defence: "3.5"));
		CUnit landed = Division(state, germany, Bight);
		CTransport.Board(craft, landed);
		CMovement.SetPath(landed, new[] { Hela }, clear: true);
		Assert.Equal(-7, CLanding.CutPenalty(state, landed, -700));
	}

	[Fact]
	public void AUnitThatCannotFightWaitsForOneThatCanOrIsHalted()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, InvasionShip);
		CUnit weak = Division(state, germany, Bight);
		CTransport.Board(fleet, weak);
		foreach (CSubUnit brigade in weak.SubUnits)
		{
			brigade.Organisation = CFixedPoint.Parse("0.5");
		}
		CUnit strong = Division(state, germany, Bight, brigades: 1);
		CTransport.Board(fleet, strong);

		CMovement.SetPath(weak, new[] { Hela }, clear: true);
		CMovement.SetPath(strong, new[] { Hela }, clear: true);
		CMovement.RunHour(state, germany, weak);
		Assert.True(weak.IsMoving);
		Assert.Equal(CFixedPoint.Zero, weak.MovementProgress);

		CMovement.Cancel(strong);
		CMovement.RunHour(state, germany, weak);
		Assert.False(weak.IsMoving);
		Assert.Same(fleet, weak.Transport);
	}

	// --- the two missions ---

	[Fact]
	public void TheTransportMissionWaitsAtBaseSailsPutsThemOffAndGoesHome()
	{
		CGameState state = State(out CCountry germany, out _);
		state.Province(Gdynia).Owner = Ger;
		state.Province(Gdynia).Controller = Ger;
		CUnit fleet = Fleet(state, germany, Kiel, TransportShip);

		Assert.True(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Transport, state.Province(Gdynia)));
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Transport, state.Province(Hela)));
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Transport, Gdynia);
		CUnitOrders.RunHour(state, germany, fleet);
		Assert.False(fleet.IsMoving);

		CUnit division = Division(state, germany, Kiel);
		CTransport.Board(fleet, division);
		CUnitOrders.RunHour(state, germany, fleet);
		Assert.Equal(Gdynia, fleet.Path[^1]);

		for (int hour = 0; hour < 20 && fleet.Location != Gdynia; hour++)
		{
			CMovement.RunUnitHour(state, germany, fleet);
		}
		Assert.Null(division.Transport);
		Assert.Equal(Gdynia, division.Location);
		CUnitOrders.RunHour(state, germany, fleet);
		Assert.True(fleet.Mission.ReturnToBase);
		Assert.Equal(Kiel, fleet.Path[^1]);
	}

	[Fact]
	public void TheInvasionWantsPassengersChoosesItsSeaAndSendsThemAshore()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Kiel, TransportShip);
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Invasion, state.Province(Hela)));

		CUnit division = Division(state, germany, Kiel);
		CTransport.Board(fleet, division);
		Assert.True(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Invasion, state.Province(Hela)));
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Invasion, state.Province(Bight)));

		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Invasion, Hela);
		// Hela names no port sea, so every candidate is at nought and the last in edge order is kept.
		Assert.Equal(Deep, fleet.Mission.LandingSea);

		for (int hour = 0; hour < 20 && fleet.Location != Deep; hour++)
		{
			CMovement.RunUnitHour(state, germany, fleet);
		}
		Assert.Equal(Deep, fleet.Location);
		CUnitOrders.RunHour(state, germany, fleet);
		Assert.Equal(new[] { Hela }, division.Path);
	}

	// --- the warning ---

	[Fact]
	public void ALandingOnThePlayersGroundIsToldTheNextHourOnce()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Pol);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		CUnit fleet = Fleet(state, germany, Bight, TransportShip);
		CUnit division = Division(state, germany, Bight);
		CTransport.Board(fleet, division);

		var move = new CMoveUnitCommand(Ger, division.Id, Hela);
		Assert.True(move.MayExecute(state));
		move.Execute(state);
		CInvasionWarnings.RunHourly(state);
		CInvasionWarnings.RunHourly(state);

		CPactMessage message = Assert.Single(told);
		Assert.Equal(CInvasionWarnings.Message, message.We);
		Assert.Equal(Pol, message.Told);
		Assert.Contains(("PROV", Hela.ToString()), message.Values);
		Assert.Single(state.InvasionWarnings);

		// Given its path again, it clears what was new.
		CInvasionWarnings.Note(state, division);
		Assert.False(state.InvasionWarnings[0].IsNew);
	}
}
