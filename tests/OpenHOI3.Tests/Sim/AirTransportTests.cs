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
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The carrier and carrying by air, against <c>docs/mechanics/transport.md</c>, section 13, on a strip of ground:
///
///     1 Kiel ==port== 2 Bight --- 3 Deep
///       |
///     4 Berlin --- 5 Border --- 6 Lodz --- 7 Warsaw
///
/// Germany holds Kiel, Berlin and Border, with air bases at Kiel and Berlin; Poland holds Lodz and Warsaw. A wing covers
/// a province an hour, a ship one in two hours; a transport wing carries 15 and a paratroop brigade weighs 5.
/// </summary>
public class AirTransportTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Bight = 2;
	private const int Deep = 3;
	private const int Berlin = 4;
	private const int Border = 5;
	private const int Lodz = 6;
	private const int Warsaw = 7;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, CBombingOrders.NavalBaseLevel, CFixedPoint.One);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, CBombingOrders.AirBaseLevel, CFixedPoint.One);

	private static CProvinceEdge Edge(int to) => new(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));

	private static CProvince Land(int id, string name, CCountryTag owner, int x, int y, params CProvinceEdge[] edges) =>
		Land(id, name, owner, x, y, 0, edges);

	private static CProvince Land(int id, string name, CCountryTag owner, int x, int y, int portSea, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = y, SeaIndex = -1, Edges = edges, PortSea = portSea,
		};
	}

	private static CSubUnitType Type(string key, EUnitBranch branch, params (string Name, string Value)[] stats)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.FromInt(branch == EUnitBranch.Land ? 4 : 20));
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.FromInt(1000));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(branch == EUnitBranch.Land ? 3 : 100));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(30));
		foreach ((string name, string value) in stats)
		{
			type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		}
		return type;
	}

	private static readonly CSubUnitType TransportPlane = Type("transport_plane", EUnitBranch.Air,
		(CTransport.TransportCapability, "15"));
	private static readonly CSubUnitType Cag = CagType();
	private static readonly CSubUnitType Carrier = Type("carrier", EUnitBranch.Naval, (CShipCensus.CarrierSize, "2"));
	private static readonly CSubUnitType EscortCarrier = Type("escort_carrier", EUnitBranch.Naval, (CShipCensus.CarrierSize, "1"));
	private static readonly CSubUnitType Paratrooper = ParatrooperType();
	private static readonly CSubUnitType Infantry = Type("infantry_brigade", EUnitBranch.Land,
		(CSubUnitType.TransportWeight, "10"), (CSubUnitType.CombatWidth, "1"));

	private static CSubUnitType CagType()
	{
		CSubUnitType type = Type("cag", EUnitBranch.Air, (CAirMissions.AirAttack, "2"), (CNavalFire.SeaAttack, "2"));
		type.Flags[CAirFire.IsCag] = true;
		return type;
	}

	private static CSubUnitType ParatrooperType()
	{
		CSubUnitType type = Type("paratrooper_brigade", EUnitBranch.Land,
			(CSubUnitType.TransportWeight, "5"), (CSubUnitType.CombatWidth, "1"), ("supply_consumption", "1"));
		type.Flags[CAirTransport.CanParadrop] = true;
		return type;
	}

	private static CGameState State(out CCountry germany, out CCountry poland, bool paved = false)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CMovementSpeed.AirSpeedModifier, "0.5");
		Military(CMovementSpeed.NavalSpeedModifier, "0.5");
		Military(CMovementSpeed.LandSpeedModifier, "1");
		Military("AGGRESSIVE_STRENGTH_LIMIT", "0.25");
		Military("DEFENSIVE_STRENGTH_LIMIT", "0.5");
		Military("PASSIVE_STRENGTH_LIMIT", "0.75");
		Military("AGGRESSIVE_ORGANISATION_LIMIT", "0.2");
		Military("DEFENSIVE_ORGANISATION_LIMIT", "0.3");
		Military("PASSIVE_ORGANISATION_LIMIT", "0.5");
		Military(CAirTransport.OrganisationRequired, "0.95");
		Military(CAirTransport.DropDays, "7");
		Military(CSupplyDay.PoolDays, "15");
		Military(CAirTransport.PlaneOrganisationAfterDrop, "0.01");
		Military(CAirTransport.OrganisationAfterDrop, "0.66");
		Military(CParadrop.MinTime, "24");
		Military(CAttackDelay.ParadropDelay, "100");
		Military(CAttackDelay.Delay, "168");
		Military(CAttackDelay.DelayModify, "1");
		Military(CAirSupply.Factor, "0.5");
		Military(CCarriers.DutyOrganisationLimit, "0.2");
		Military(CCombatModifiers.ParadropPenalty, "-0.5");
		Military(CFrontLine.BaseWidth, "10");
		Military(CFrontLine.AdditionalWidth, "5");
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		CProvince kiel = Land(Kiel, "Kiel", Ger, 10, 10, Bight, Edge(Bight), Edge(Berlin));
		kiel.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(2));
		kiel.SetBuildingLevel(AirBase, CFixedPoint.FromInt(2));
		CProvince berlin = Land(Berlin, "Berlin", Ger, 10, 30, Edge(Kiel), Edge(Border));
		berlin.SetBuildingLevel(AirBase, CFixedPoint.FromInt(4));
		berlin.Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);
		berlin.Pool[EGoods.Fuel] = CFixedPoint.FromInt(100);
		var state = new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				kiel,
				new CProvince(Bight, "Bight", isSea: true) { CentreX = 30, CentreY = 10, SeaIndex = 0, Edges = new[] { Edge(Kiel), Edge(Deep) } },
				new CProvince(Deep, "Deep", isSea: true) { CentreX = 50, CentreY = 10, SeaIndex = 1, Edges = new[] { Edge(Bight) } },
				berlin,
				Land(Border, "Border", Ger, 30, 30, Edge(Berlin), Edge(Lodz)),
				Land(Lodz, "Lodz", Pol, 50, 30, Edge(Border), Edge(Warsaw)),
				Land(Warsaw, "Warsaw", Pol, 70, 30, Edge(Lodz)),
			},
			new[] { germany, poland }, buildings: paved ? CTestRoads.With(NavalBase, AirBase) : new[] { NavalBase, AirBase },
			defines: defines, wars: new List<CWar> { war })
		{
			MapWidth = 1000,
			MapHeight = 1000,
		};
		return paved ? state.Paved() : state;
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, int count, CSubUnitType type,
		int based = 0)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Base = based, Name = type.Key };
		for (int i = 0; i < count; i++)
		{
			var sub = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Strength = type.Stats.Values[CSubUnitType.MaxStrength],
				Organisation = CFixedPoint.FromInt(30), Name = type.Key + " " + i,
			};
			sub.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(sub);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	private static CUnit Planes(CGameState state, CCountry country, int wings = 1) =>
		Unit(state, country, EUnitKind.Air, Berlin, wings, TransportPlane, based: Berlin);

	private static CUnit Paratroops(CGameState state, CCountry country, int brigades = 3) =>
		Unit(state, country, EUnitKind.Division, Berlin, brigades, Paratrooper);

	private static void RunHours(CGameState state, CCountry country, CUnit unit, int hours)
	{
		for (int hour = 0; hour < hours && !unit.IsRemoved; hour++)
		{
			CMovement.RunUnitHour(state, country, unit);
			state.AdvanceOneHour();
		}
	}

	// --- a carrier as an air base ---

	[Fact]
	public void AFinishedCarrierGroupGoesOnlyToAFleetWithAnEmptyCarrierAndAfterAPostingLooksAtTheFirstFleetAlone()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit waiting = Unit(state, germany, EUnitKind.Air, 0, 1, Cag);
		// No fleet with a carrier: it waits, and never goes to an airfield.
		Assert.Null(OpenHOI3.Sim.Construction.CAiDeployment.Place(state, germany, waiting));

		CUnit full = Unit(state, germany, EUnitKind.Navy, Kiel, 1, EscortCarrier, based: Kiel);
		CCarriers.BaseOn(state, Unit(state, germany, EUnitKind.Air, Kiel, 1, Cag, based: Kiel), full.SubUnits[0]);
		Unit(state, germany, EUnitKind.Navy, Deep, 1, EscortCarrier, based: Kiel);
		Assert.Equal(Deep, OpenHOI3.Sim.Construction.CAiDeployment.Place(state, germany, waiting));
		// Once the pass has posted something, the first fleet alone is looked at.
		Assert.Null(OpenHOI3.Sim.Construction.CAiDeployment.Place(state, germany, waiting, posted: true));
	}

	[Fact]
	public void ACarrierHoldsItsCarrierSizeInWingsShipByShipAndOnlyItsOwnCountrysGroups()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, 1, EscortCarrier, based: Kiel);
		fleet.SubUnits.Add(new CSubUnit(Carrier) { Id = ++state.LastUnitId, Unit = fleet, Strength = CFixedPoint.FromInt(100) });
		fleet.SubUnits[1].Stats.CopyFrom(Carrier.Stats);
		CUnit two = Unit(state, germany, EUnitKind.Air, Berlin, 2, Cag, based: Berlin);
		CUnit one = Unit(state, germany, EUnitKind.Air, Berlin, 1, Cag, based: Berlin);

		Assert.True(CCarriers.IsCarrierAirGroup(two));
		Assert.False(CCarriers.IsCarrierAirGroup(Planes(state, germany)));
		// The escort carrier holds one wing, so the pair goes to the carrier behind it.
		Assert.Same(fleet.SubUnits[1], CCarriers.ShipWithRoom(fleet, two));
		Assert.Same(fleet.SubUnits[0], CCarriers.ShipWithRoom(fleet, one));
		Assert.Same(fleet, CCarriers.FindCarrierWithRoom(state, two, state.Province(Kiel)));
		Assert.Null(CCarriers.FindCarrierWithRoom(state, Unit(state, poland, EUnitKind.Air, Lodz, 1, Cag, based: Lodz),
			state.Province(Kiel)));

		// With the pair aboard the carrier is full; a single wing still finds the escort carrier.
		CCarriers.BaseOn(state, two, fleet.SubUnits[1]);
		Assert.False(CCarriers.HasRoom(fleet.SubUnits[1], one));
		Assert.Same(fleet.SubUnits[0], CCarriers.ShipWithRoom(fleet, one));
	}

	[Fact]
	public void RebaseToCarrierBasesTheUnitAtOnceFliesItThereAndEndsAtItsBase()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, 1, Carrier, based: Kiel);
		CUnit group = Unit(state, germany, EUnitKind.Air, Berlin, 2, Cag, based: Berlin);

		Assert.True(CUnitOrders.MayGive(state, germany, group, EUnitOrder.RebaseCarrier, state.Province(Kiel)));
		Assert.False(CUnitOrders.MayGive(state, germany, Planes(state, germany), EUnitOrder.RebaseCarrier,
			state.Province(Kiel)));
		CUnitOrders.Give(state, germany, group, EUnitOrder.RebaseCarrier, Kiel);

		Assert.Same(fleet.SubUnits[0], group.CarriedBy);
		Assert.Contains(group, fleet.SubUnits[0].Carried);
		Assert.DoesNotContain(group, germany.Units);
		// Hung under the fleet in the order of battle, so the fleet is the unit above it - and it is met once.
		Assert.Same(fleet, group.Parent);
		Assert.Equal(new[] { group }, fleet.Children);
		Assert.Single(germany.AllUnits(), unit => ReferenceEquals(unit, group));
		Assert.Equal(Kiel, group.Base);
		Assert.Equal(new[] { Kiel }, group.Path);

		RunHours(state, germany, group, 4);
		Assert.Equal(Kiel, group.Location);
		Assert.Equal(EUnitOrder.CarrierProtection, group.Order);    // done, and given the carrier's duty
	}

	[Fact]
	public void AGroupAboardIsAtItsBaseOnlyWhileTheFleetIsNotFightingAndFollowsItIdle()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, 1, Carrier, based: Kiel);
		CUnit idle = Unit(state, germany, EUnitKind.Air, Kiel, 1, Cag);
		CUnit away = Unit(state, germany, EUnitKind.Air, Berlin, 1, Cag);
		CCarriers.BaseOn(state, idle, fleet.SubUnits[0]);
		CCarriers.BaseOn(state, away, fleet.SubUnits[0]);

		Assert.True(CAirMissions.IsAtOwnBase(idle));
		fleet.Combats.Add(new CCombat(Kiel, ECombatKind.Naval));
		Assert.False(CAirMissions.IsAtOwnBase(idle));
		fleet.Combats.Clear();

		CMovement.SetPath(fleet, new[] { Bight }, clear: true);
		RunHours(state, germany, fleet, 6);
		Assert.Equal(Bight, fleet.Location);
		Assert.Equal(Bight, idle.Location);
		Assert.Equal(Bight, idle.Base);
		// One out on a mission stays where it is, based where the carrier now is.
		Assert.Equal(Berlin, away.Location);
		Assert.Equal(Bight, away.Base);
	}

	[Fact]
	public void ACarrierSunkLetsItsGroupsGoToTheNearestAirBaseOfTheirCountryWithNoOrder()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight, 1, Carrier, based: Kiel);
		CUnit group = Unit(state, germany, EUnitKind.Air, Bight, 1, Cag);
		CCarriers.BaseOn(state, group, fleet.SubUnits[0]);
		CCarriers.GiveDuty(group);

		CCombatRetreat.RemoveFromGame(state, fleet);

		Assert.Null(group.CarriedBy);
		Assert.Equal(Kiel, group.Base);          // Kiel is nearer the Bight than Berlin
		Assert.Equal(EUnitOrder.None, group.Order);
		Assert.Contains(group, germany.Units);
		Assert.Null(group.Parent);
		Assert.False(group.IsRemoved);
	}

	[Fact]
	public void AGroupStaysUnderItsFleetWhenItsCarrierSinksAndLeavesItForAnAirfield()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit theatre = Unit(state, germany, EUnitKind.Theatre, Kiel, 0, Carrier);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Kiel, 1, Carrier, based: Kiel);
		germany.Units.Remove(fleet);
		fleet.Parent = theatre;
		theatre.Children.Add(fleet);
		CUnit group = Unit(state, germany, EUnitKind.Air, Kiel, 1, Cag);
		CCarriers.BaseOn(state, group, fleet.SubUnits[0]);
		Assert.Equal(new[] { theatre, fleet }, Above(group));

		// The carrier sunk: off the ship, the fleet still above it, as the original leaves it.
		CCarriers.LetGo(state, fleet.SubUnits[0]);
		Assert.Null(group.CarriedBy);
		Assert.Same(fleet, group.Parent);
		Assert.DoesNotContain(group, germany.Units);
		Assert.Single(germany.AllUnits(), unit => ReferenceEquals(unit, group));

		// Aboard again and rebased to an airfield, it leaves the fleet for the top of the order of battle.
		CCarriers.BaseOn(state, group, fleet.SubUnits[0]);
		CUnitOrders.Give(state, germany, group, EUnitOrder.RebaseAir, Berlin);
		CUnitOrders.RunHour(state, germany, group);
		Assert.Null(group.CarriedBy);
		Assert.Null(group.Parent);
		Assert.Empty(fleet.Children);
		Assert.Contains(group, germany.Units);
	}

	[Fact]
	public void AFleetMergedAwayLeavesItsCarriersGroupsAboardWithNobodyAboveThem()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit keeper = Unit(state, germany, EUnitKind.Navy, Kiel, 2, Carrier, based: Kiel);
		CUnit giver = Unit(state, germany, EUnitKind.Navy, Kiel, 1, Carrier, based: Kiel);
		CUnit group = Unit(state, germany, EUnitKind.Air, Kiel, 1, Cag);
		CCarriers.BaseOn(state, group, giver.SubUnits[0]);

		CUnitMerge.MergeInto(state, keeper, giver);
		CCombatRetreat.RemoveFromGame(state, giver);

		// Its carrier is the keeper's now, and the original's removal leaves the group with no unit above it.
		Assert.Same(keeper, group.CarriedBy!.Unit);
		Assert.Null(group.Parent);
		Assert.Empty(keeper.Children);
		Assert.DoesNotContain(group, germany.Units);
		Assert.Single(germany.AllUnits(), unit => ReferenceEquals(unit, group));
		Assert.False(group.IsRemoved);
	}

	private static List<CUnit> Above(CUnit unit)
	{
		var above = new List<CUnit>();
		for (CUnit? at = unit.Parent; at is not null; at = at.Parent)
		{
			above.Insert(0, at);
		}
		return above;
	}

	[Fact]
	public void CarrierProtectionKeepsToTheCarrierGoesHomeWornAndFightsInTheAirOnlyBesideABattle()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Bight, 1, Carrier, based: Kiel);
		CUnit group = Unit(state, germany, EUnitKind.Air, Bight, 1, Cag);
		CCarriers.BaseOn(state, group, fleet.SubUnits[0]);

		CUnitOrders.RunHour(state, germany, group);
		Assert.Equal(EUnitOrder.CarrierProtection, group.Order);
		Assert.Equal(Bight, group.OrderProvince);

		Assert.False(CAirMissions.FightsInTheAir(state, group));
		state.Combats.Add(new CCombat(Bight, ECombatKind.Naval));
		Assert.True(CAirMissions.FightsInTheAir(state, group));
		Assert.True(CBombingOrders.ShouldBomb(state, group, state.Province(Bight)));
		Assert.False(CBombingOrders.ShouldBomb(state, group, state.Province(Deep)));

		// Under the limit - a plain average of 0.1 - it is told home, and its order stays.
		group.SubUnits[0].Organisation = CFixedPoint.Parse("0.1");
		Assert.False(CAirMissions.FightsInTheAir(state, group));
		Assert.True(CCarriers.RunProtection(state, germany, group));
		Assert.True(group.Mission.ReturnToBase);
	}

	// --- carrying by air ---

	[Fact]
	public void ALandUnitMayBoardOnlyParatroopsOrganisedForItAtTheAirUnitsWorkingBaseWithRoom()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit plane = Planes(state, germany);
		CUnit three = Paratroops(state, germany);
		CUnit four = Paratroops(state, germany, brigades: 4);

		Assert.True(CAirTransport.MayLoad(state, plane, three));
		Assert.False(CAirTransport.MayLoad(state, plane, four));                    // 20 is more than 15
		Assert.False(CAirTransport.MayLoad(state, plane, Unit(state, germany, EUnitKind.Division, Berlin, 1, Infantry)));

		three.SubUnits[0].Organisation = CFixedPoint.Parse("28.4");                 // under 95% of 30
		Assert.False(CAirTransport.MayLoad(state, plane, three));
		three.SubUnits[0].Organisation = CFixedPoint.Parse("28.5");
		Assert.True(CAirTransport.MayLoad(state, plane, three));

		three.AttackDelay = CFixedPoint.One;
		Assert.False(CAirTransport.MayLoad(state, plane, three));
		three.AttackDelay = CFixedPoint.Zero;

		// The base's air base must work at level 1: a bombed-out one does not do.
		state.Province(Berlin).SetBuildingCurrentLevel(AirBase, CFixedPoint.Parse("0.5"));
		Assert.False(CAirTransport.MayLoad(state, plane, three));
	}

	[Fact]
	public void LoadingKeepsTheUnitWhereItIsAndAPassengerGoesWithThePlaneAndOffOnlyAtTheBase()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit plane = Planes(state, germany);
		CUnit division = Paratroops(state, germany);
		division.Order = EUnitOrder.Move;

		var load = new CLoadOnPlaneCommand(Ger, division.Id, plane.Id);
		Assert.True(load.MayExecute(state));
		load.Execute(state);
		Assert.Same(plane, division.Plane);
		Assert.Equal(new[] { division }, plane.Passengers);
		Assert.Equal(EUnitOrder.None, division.Order);

		CMovement.SetPath(plane, new[] { Kiel }, clear: true);
		RunHours(state, germany, plane, 1);
		Assert.Equal(Kiel, division.Location);
		Assert.False(new CUnloadFromPlaneCommand(Ger, division.Id).MayExecute(state));   // not at its base

		// With no order, an air unit away from its base flies home.
		RunHours(state, germany, plane, 3);
		Assert.Equal(Berlin, plane.Location);
		var unload = new CUnloadFromPlaneCommand(Ger, division.Id);
		Assert.True(unload.MayExecute(state));
		unload.Execute(state);
		Assert.Null(division.Plane);
		Assert.Empty(plane.Passengers);
		Assert.Equal(Berlin, division.Location);
	}

	[Fact]
	public void APassengerAboardMayStillBeSentAndWalksStillAboard()
	{
		// The original's AI marched an airborne division aboard a plane based elsewhere, and it walked, still aboard
		// (FINDINGS-liveread.md, section 127).
		CGameState state = State(out CCountry germany, out _, paved: true);
		CUnit plane = Planes(state, germany);
		CUnit division = Paratroops(state, germany);
		CAirTransport.Board(plane, division);

		Assert.True(CMovementRules.MayMoveTo(state, germany, division, state.Province(Border)));
		CMovement.SetPath(division, new[] { Border }, clear: true);
		RunHours(state, germany, division, 48);
		Assert.Equal(Border, division.Location);
		Assert.Same(plane, division.Plane);
		Assert.Equal(new[] { division }, plane.Passengers);

		// A move command leaves it aboard too.
		CUnit second = Paratroops(state, germany);
		CAirTransport.Board(plane, second);
		new CMoveUnitCommand(Ger, second.Id, Border).Execute(state);
		Assert.True(second.IsMoving);
		Assert.Same(plane, second.Plane);
	}

	[Fact]
	public void InAnEnhancedGameAPassengerGivenAWayGetsOffFirstAndIsGivenNoneInTheAir()
	{
		CGameState state = State(out CCountry germany, out _, paved: true);
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		CUnit plane = Planes(state, germany);
		CUnit division = Paratroops(state, germany);
		CAirTransport.Board(plane, division);

		// On the ground, at the plane's base: the move takes it off the plane and it marches as a plain unit.
		var move = new CMoveUnitCommand(Ger, division.Id, Border);
		Assert.True(CMovementRules.MayMoveTo(state, germany, division, state.Province(Border)));
		Assert.True(move.MayExecute(state));
		move.Execute(state);
		Assert.Null(division.Plane);
		Assert.Empty(plane.Passengers);
		Assert.True(division.IsMoving);

		// An order that gives a way does the same.
		CUnit second = Paratroops(state, germany);
		CAirTransport.Board(plane, second);
		CUnitOrders.Give(state, germany, second, EUnitOrder.StrategicRedeployment, Border);
		Assert.Null(second.Plane);
		Assert.Empty(plane.Passengers);
		Assert.True(second.IsMoving);

		// In the air it may not get off, so it is given no way.
		CUnit third = Paratroops(state, germany);
		CAirTransport.Board(plane, third);
		CMovement.SetPath(plane, new[] { Kiel }, clear: true);
		RunHours(state, germany, plane, 1);
		Assert.Equal(Kiel, third.Location);
		Assert.False(CAirTransport.MayGetOff(state, third));
		Assert.False(CMovementRules.MayMoveTo(state, germany, third, state.Province(Berlin)));
		Assert.False(new CMoveUnitCommand(Ger, third.Id, Berlin).MayExecute(state));
		Assert.Same(plane, third.Plane);

		// A game without the fix sends it as the original does.
		state.Rules.Set(OpenHOI3.Sim.Rules.EFix.PlanePassengerMarch, false);
		Assert.True(CMovementRules.MayMoveTo(state, germany, third, state.Province(Berlin)));
	}

	// --- the invasion's carrier aircraft over the beach ---

	/// <summary>
	/// An invasion of Lodz, defended by a Polish division, with its one fleet of two carriers in the Bight under the
	/// invasion order, and four air groups aboard: two on each carrier, in the order made.
	/// </summary>
	private static OpenHOI3.Sim.Agents.CInvasion Invasion(CGameState state, CCountry germany, CCountry poland, out CUnit fleet,
		out CUnit[] groups)
	{
		fleet = Unit(state, germany, EUnitKind.Navy, Bight, 2, Carrier, based: Kiel);
		fleet.Order = EUnitOrder.Invasion;
		fleet.OrderProvince = Lodz;
		groups = new CUnit[4];
		for (int index = 0; index < groups.Length; index++)
		{
			groups[index] = Unit(state, germany, EUnitKind.Air, Bight, 1, Cag, based: Bight);
			CCarriers.BaseOn(state, groups[index], fleet.SubUnits[index / 2]);
		}
		Unit(state, poland, EUnitKind.Division, Lodz, 1, Infantry);
		var invasion = new OpenHOI3.Sim.Agents.CInvasion(Ger, Lodz);
		invasion.Add(fleet);
		state.Outbox.Clear();
		return invasion;
	}

	private static List<string> Orders(CGameState state)
	{
		var orders = new List<string>();
		foreach (CCommand command in state.Outbox)
		{
			orders.Add(command.ToString()!);
		}
		return orders;
	}

	[Fact]
	public void AnInvasionsFleetSendsItsThreeBestCarrierGroupsOverTheBeachTillSunset()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		OpenHOI3.Sim.Agents.CInvasion invasion = Invasion(state, germany, poland, out CUnit fleet, out CUnit[] groups);
		// The second group is worn: organisation and strength together under the others'.
		groups[1].SubUnits[0].Strength = CFixedPoint.FromInt(60);

		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);

		// Three of the four, the best first and the first of equals before the next: the first carrier's whole group,
		// then the second carrier's two. Each on close support over the objective.
		Assert.Equal(new[]
		{
			$"order GroundAttack to unit {groups[0].Id} at {Lodz}",
			$"order GroundAttack to unit {groups[2].Id} at {Lodz}",
			$"order GroundAttack to unit {groups[3].Id} at {Lodz}",
		}, Orders(state));

		// The order holds as the AI's every order does, and ends at the sunset over the fleet: at midnight on the equator,
		// eighteen hours on.
		CCommand first = state.Outbox[0];
		Assert.True(first.MayExecute(state));
		first.Execute(state);
		Assert.Equal(EUnitOrder.GroundAttack, groups[0].Order);
		Assert.Equal(state.TotalHours + 18, groups[0].Mission!.EndHour);
		Assert.Equal(CMission.Defensive, groups[0].Mission!.Stance);
	}

	[Fact]
	public void ACarrierGroupIsSentOnlyFromItsFleetsProvinceIdleAndFitEnough()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		OpenHOI3.Sim.Agents.CInvasion invasion = Invasion(state, germany, poland, out CUnit fleet, out CUnit[] groups);
		// Organisation under a half of its most; strength under a third; on its way somewhere; and not where the fleet is.
		groups[0].SubUnits[0].Organisation = CFixedPoint.Parse("14.9");
		groups[1].SubUnits[0].Strength = CFixedPoint.Parse("32.9");
		CMovement.SetPath(groups[2], new[] { Kiel }, clear: true);
		groups[3].Location = Kiel;
		state.ForgetPositions();
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Empty(state.Outbox);

		// Exactly a half and exactly a third are enough, whatever order the group has.
		groups[0].SubUnits[0].Organisation = CFixedPoint.FromInt(15);
		groups[0].Order = EUnitOrder.CarrierProtection;
		groups[1].SubUnits[0].Strength = CFixedPoint.FromInt(33);
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		// The stronger of the two first: 1 + 0.5 against 0.33 + 1.
		Assert.Equal(new[]
		{
			$"order GroundAttack to unit {groups[0].Id} at {Lodz}",
			$"order GroundAttack to unit {groups[1].Id} at {Lodz}",
		}, Orders(state));

		// A ship that is no carrier is not asked, whatever it holds.
		state.Outbox.Clear();
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			ship.Stats.Values.Set(CShipCensus.CarrierSize, CFixedPoint.Zero);
		}
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void NoCarrierGroupIsSentWithoutTheInvasionOrderAnEnemyOnTheBeachOrThreeHoursOfDaylight()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		OpenHOI3.Sim.Agents.CInvasion invasion = Invasion(state, germany, poland, out CUnit fleet, out CUnit[] groups);

		// Not yet under its invasion order.
		fleet.Order = EUnitOrder.Transport;
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Empty(state.Outbox);
		fleet.Order = EUnitOrder.Invasion;

		// Two fleets still to be merged.
		CUnit second = Unit(state, germany, EUnitKind.Navy, Bight, 1, EscortCarrier, based: Kiel);
		invasion.Add(second);
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Empty(state.Outbox);
		invasion.Remove(second);

		// The objective no enemy's.
		state.Province(Lodz).Controller = Ger;
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Empty(state.Outbox);
		state.Province(Lodz).Controller = Pol;

		// Nobody of the enemy's on it.
		foreach (CUnit unit in poland.Units)
		{
			unit.Location = Warsaw;
		}
		state.ForgetPositions();
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Empty(state.Outbox);
		foreach (CUnit unit in poland.Units)
		{
			unit.Location = Lodz;
		}
		state.ForgetPositions();

		// At fifteen, three hours to the sunset at eighteen: sent, for those three hours. At sixteen, no more.
		for (int hour = 0; hour < 15; hour++)
		{
			state.AdvanceOneHour();
		}
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Equal(3, state.Outbox.Count);
		state.Outbox[0].Execute(state);
		Assert.Equal(EUnitOrder.GroundAttack, groups[0].Order);
		Assert.Equal(state.TotalHours + 3, groups[0].Mission!.EndHour);
		state.Outbox.Clear();
		state.AdvanceOneHour();
		OpenHOI3.Sim.Agents.CInvasionTick.AirSupport(state, germany, invasion);
		Assert.Empty(state.Outbox);
	}

	// --- the AI's paradrop targets ---

	[Fact]
	public void AWeakPortScoresAHundredAWholeNavalLevelAndTenAWorkingAirLevelInFractions()
	{
		CGameState state = State(out _, out _, paved: true);
		CProvince warsaw = state.Province(Warsaw);
		warsaw.SetBuildingLevel(NavalBase, CFixedPoint.Parse("1.5"));
		warsaw.SetBuildingLevel(AirBase, CFixedPoint.FromInt(2));
		warsaw.SetBuildingCurrentLevel(AirBase, CFixedPoint.Parse("0.5"));
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		float depot = network.DepotOf(Warsaw) == Warsaw ? 1000f : 0f;

		Assert.Equal(100f + 5f + depot, OpenHOI3.Sim.Agents.CWeakEnemyPorts.Score(state, network, warsaw));
	}

	[Fact]
	public void AnEnemysUndefendedCapitalIsATargetAndALandBrigadeThereTakesItOff()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, paved: true);
		poland.Capital = Warsaw;
		state.Province(Warsaw).SetBuildingLevel(AirBase, CFixedPoint.FromInt(1));

		OpenHOI3.Sim.Agents.CWeakEnemyPorts.Rebuild(state, germany);
		Assert.Contains(germany.UnitAi.WeakEnemyPorts, entry => entry.Province == Warsaw);

		Unit(state, poland, EUnitKind.Division, Warsaw, 1, Infantry);
		OpenHOI3.Sim.Agents.CWeakEnemyPorts.Rebuild(state, germany);
		Assert.DoesNotContain(germany.UnitAi.WeakEnemyPorts, entry => entry.Province == Warsaw);

		// A country's own capital is never its target.
		OpenHOI3.Sim.Agents.CWeakEnemyPorts.Rebuild(state, poland);
		Assert.All(poland.UnitAi.WeakEnemyPorts, entry => Assert.NotEqual(Warsaw, entry.Province));
	}

	[Fact]
	public void APassengerAboardIsFoughtByNobodyAndAPlaneRemovedTakesItsPassengersWithIt()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit plane = Planes(state, germany);
		CUnit division = Paratroops(state, germany);
		CAirTransport.Board(plane, division);
		CUnit enemy = Unit(state, poland, EUnitKind.Division, Berlin, 1, Infantry);

		Assert.False(CCombats.MayEngage(state, enemy, division));
		Assert.Equal(plane.Base, CSupplyDraw.ProvinceOf(germany, division));

		CCombatRetreat.RemoveFromGame(state, plane);
		Assert.True(division.IsRemoved);
	}

	// --- the paradrop ---

	[Fact]
	public void AParadropWaitsForItsStartAndItsTroopsThenDropsThemTakingAnEmptyEnemyProvince()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Pol);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		CUnit plane = Planes(state, germany);
		CUnit division = Paratroops(state, germany);
		division.Previous = Border;
		state.Province(Border).Pool[EGoods.Supplies] = CFixedPoint.FromInt(1000);
		CAirTransport.Board(plane, division);
		Assert.True(CUnitOrders.MayGive(state, germany, plane, EUnitOrder.Paradrop, state.Province(Lodz)));
		Assert.False(CUnitOrders.MayGive(state, germany, plane, EUnitOrder.Paradrop, state.Province(Bight)));

		long start = CParadrop.EarliestStart(state);
		CUnitOrders.Give(state, germany, plane, EUnitOrder.Paradrop, Lodz, startHour: start);
		Assert.Equal(state.TotalHours + 24, start);
		RunHours(state, germany, plane, 24);
		Assert.Equal(Berlin, plane.Location);                    // not a step before its start

		division.SubUnits[0].Organisation = CFixedPoint.FromInt(20);
		RunHours(state, germany, plane, 3);
		Assert.Equal(Berlin, plane.Location);                    // waiting on a brigade short of organisation
		division.SubUnits[0].Organisation = CFixedPoint.FromInt(30);

		long supplies = CUnitSupply.SupplyConsumption(state, germany, division).Raw * 7;
		for (int hour = 0; hour < 12 && division.Plane is not null; hour++)
		{
			RunHours(state, germany, plane, 1);
		}

		Assert.Null(division.Plane);
		Assert.Equal(Lodz, division.Location);
		Assert.True(division.WasParadropped);
		Assert.Equal(CFixedPoint.FromInt(100), division.AttackDelay);   // the paradrop's own delay
		Assert.Equal(Ger, state.Province(Lodz).Controller);              // nobody there, taken at once
		Assert.Equal(supplies, state.Province(Lodz).Pool[EGoods.Supplies].Raw);
		Assert.Equal(1000_000 - supplies, state.Province(Border).Pool[EGoods.Supplies].Raw);
		Assert.Equal(CFixedPoint.Parse("0.3"), plane.SubUnits[0].Organisation);   // a hundredth of 30
		Assert.True(plane.Mission.ReturnToBase);
		// The player is asked after the drop: Lodz is Germany's by then, so Poland is not told - and never will be.
		Assert.DoesNotContain(told, message => message.We == CParadrop.Message);
		Assert.True(plane.Mission.Told);

		// Home with nothing aboard, the order is done.
		RunHours(state, germany, plane, 6);
		Assert.Equal(EUnitOrder.None, plane.Order);
	}

	[Fact]
	public void ADropOnADefendedProvinceStartsABattleAndTellsThePlayerHoldingItOnce()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Pol);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		CUnit plane = Planes(state, germany);
		CUnit division = Paratroops(state, germany);
		CAirTransport.Board(plane, division);
		CUnit defender = Unit(state, poland, EUnitKind.Division, Lodz, 1, Infantry);
		CUnitOrders.Give(state, germany, plane, EUnitOrder.Paradrop, Lodz, startHour: 0);

		for (int hour = 0; hour < 12 && division.Plane is not null; hour++)
		{
			RunHours(state, germany, plane, 1);
		}

		Assert.Equal(Lodz, division.Location);
		Assert.Contains(division, Assert.Single(defender.Combats).Attacker.Units);
		Assert.Equal(Pol, state.Province(Lodz).Controller);
		CPactMessage message = Assert.Single(told, each => each.We == CParadrop.Message);
		Assert.Equal(Pol, message.Told);
		Assert.Contains(("PROV", "Lodz"), message.Values);
	}

	[Fact]
	public void ADroppedUnitAttacksWithTheParadropsPenaltyAndNoLackOfSuppliesAndCountsOnceInTheWidth()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit first = Paratroops(state, germany, brigades: 1);
		CUnit second = Paratroops(state, germany, brigades: 1);
		foreach (CUnit unit in new[] { first, second })
		{
			unit.Location = Lodz;
			unit.Previous = Border;
			unit.WasParadropped = true;
			unit.SuppliesReceived = CFixedPoint.Zero;
		}
		state.ForgetPositions();
		CUnit defender = Unit(state, poland, EUnitKind.Division, Lodz, 1, Infantry);
		var combat = new CCombat(Lodz);
		combat.Attacker.Add(first);
		combat.Attacker.Add(second);
		combat.Defender.Add(defender);
		state.Combats.Add(combat);

		Assert.Equal(CFixedPoint.FromInt(10), CFrontLine.Width(state, combat));
		CCombatModifiers.Apply(state, combat, combat.Attacker);
		Assert.Contains(first.CombatModifiers, modifier => modifier.Kind == ECombatModifier.Paratroop);
		Assert.DoesNotContain(first.CombatModifiers, modifier => modifier.Kind == ECombatModifier.LackOfSupplies);

		// Out of the battle, the first hour: the mark goes and organisation falls to 0.66.
		first.Combats.Clear();
		CAirTransport.Settle(state, first);
		Assert.False(first.WasParadropped);
		Assert.Equal(CFixedPoint.Parse("19.8"), first.SubUnits[0].Organisation);
	}

	// --- the air supply ---

	[Fact]
	public void TheAirSupplyPicksTheLastShortProvinceLoadsAtItsBaseUpToItsCapAndDelivers()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit plane = Planes(state, germany, wings: 2);
		CProvince kiel = state.Province(Kiel);
		CProvince border = state.Province(Border);
		kiel.SupplyDrawn[EGoods.Supplies] = CFixedPoint.FromInt(5);
		border.SupplyDrawn[EGoods.Supplies] = CFixedPoint.FromInt(40);
		border.SupplyThroughput[EGoods.Supplies] = CFixedPoint.FromInt(10);

		Assert.True(CUnitOrders.MayGive(state, germany, plane, EUnitOrder.AirSupply, border));
		Assert.False(CAirSupply.MayList(state, plane, state.Province(Lodz)));    // an enemy's
		plane.Order = EUnitOrder.AirSupply;
		CAirMissions.SetProvinces(state, plane, new[] { Kiel, Lodz, Border });
		Assert.Equal(Border, CAirSupply.Pick(state, plane));

		CUnitOrders.Give(state, germany, plane, EUnitOrder.AirSupply, Border, provinces: new[] { Kiel, Border });
		Assert.Equal(Border, plane.Mission.Destination);
		CUnitOrders.RunHour(state, germany, plane);
		// The cap is half of 30, 15; the border went 30 short.
		Assert.Equal(CFixedPoint.FromInt(15), plane.Mission.Supplies);
		Assert.Equal(CFixedPoint.FromInt(85), state.Province(Berlin).Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(15), state.Province(Berlin).SupplyDrawn[EGoods.Supplies]);

		for (int hour = 0; hour < 6 && plane.Mission.Supplies > CFixedPoint.Zero; hour++)
		{
			RunHours(state, germany, plane, 1);
		}
		Assert.Equal(CFixedPoint.FromInt(15), border.Pool[EGoods.Supplies]);
	}
}
