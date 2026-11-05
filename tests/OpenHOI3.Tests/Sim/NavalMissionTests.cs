using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A fleet's missions against <c>docs/mechanics/navalmissions.md</c>, on a small sea:
///
///     1 Kiel --- 3 Bight --- 4 Deep --- 5 Far --- 6 Outer
///                               |
///                            2 Gdynia (Poland's)
///
/// Kiel's port opens onto the Bight and Gdynia's onto the Deep. A ship covers one
/// province an hour.
/// </summary>
public class NavalMissionTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Kiel = 1;
	private const int Gdynia = 2;
	private const int Bight = 3;
	private const int Deep = 4;
	private const int Far = 5;
	private const int Outer = 6;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, "", CFixedPoint.Zero);

	private static CProvinceEdge Edge(int to)
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));
	}

	private static CProvince Port(int id, string name, CCountryTag owner, int x, int portSea, int seaIndex, params CProvinceEdge[] edges)
	{
		var province = new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = 10, PortSea = portSea, SeaIndex = seaIndex, Edges = edges,
		};
		province.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(2));
		return province;
	}

	private static CProvince Sea(int id, string name, int x, int seaIndex, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: true) { CentreX = x, CentreY = 20, SeaIndex = seaIndex, Edges = edges };
	}

	private static CSubUnitType Type(string key, string attack = "10", string convoyAttack = "0", string range = "1000",
		bool capital = false, bool submarine = false, bool transport = false)
	{
		var type = new CSubUnitType(key, EUnitBranch.Naval);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "20");
		Set(CUnitOrders.Range, range);
		Set(CSubUnitType.MaxStrength, "100");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CNavalFire.SeaAttack, attack);
		Set(CConvoyRaid.ConvoyAttack, convoyAttack);
		Set(CEnemyFleets.SeaDefence, "5");
		Set(CNavalTargets.SubAttack, "2");
		Set(CNavalBattle.Hull, "1");
		Set(CNavalBattle.FiringDistance, "0.2");
		Set(CNavalTargets.Priority, "4");
		Set(CNavalSpotting.SurfaceDetection, "100");
		Set(CNavalSpotting.SubDetection, "100");
		Set(CNavalSpotting.Visibility, "1000");
		type.Flags[CShipCensus.CapitalFlag] = capital;
		type.Flags[CShipCensus.SubmarineFlag] = submarine;
		type.Flags[CShipCensus.TransportFlag] = transport;
		return type;
	}

	private static readonly CSubUnitType Destroyer = Type("destroyer");
	private static readonly CSubUnitType Battleship = Type("battleship", attack: "15", capital: true);
	private static readonly CSubUnitType Submarine = Type("submarine", attack: "3", convoyAttack: "10", submarine: true);
	private static readonly CSubUnitType TransportShip = Type("transport_ship", attack: "0", transport: true);
	private static readonly CSubUnitType ShortLegged = Type("coaster", range: "200");

	private static CGameState State(out CCountry germany, out CCountry poland, bool atWar = true)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CMovementSpeed.NavalSpeedModifier, "0.5");
		Military(CNavalBattle.HullLimit, "16");
		Military(CNavalBattle.HullPenalty, "0.04");
		Military(CNavalBattle.MostPenalty, "0.8");
		Military(CNavalFire.OrganisationDice, "1");
		Military(CNavalFire.StrengthDice, "10");
		Military(CNavalFire.CriticalChance, "10");
		Military(CNavalFire.CriticalDamage, "10");
		Military(CNavalFire.StrengthDamage, "0.12");
		Military(CNavalFire.OrganisationDamage, "1");
		Military(CNavalFire.AvoidChance, "48");
		Military(CNavalModifiers.SurpriseChance, "30");
		Military(CNavalModifiers.SurpriseBonus, "5");
		Military(CNavalModifiers.SurpriseRounds, "3");
		Military(CNavalModifiers.PrideBonus, "10");
		Military(CCombatModifiers.NightPenalty, "-0.5");
		Military(CReserve.DefensiveLimit, "0.5");
		Military(CReserve.PassiveLimit, "0.75");
		Military(CNavalSpotting.AfterAttackFactor, "4");
		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		return new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Port(Kiel, "Kiel", Ger, 10, Bight, 0, Edge(Bight)),
				Port(Gdynia, "Gdynia", Pol, 50, Deep, 1, Edge(Deep)),
				Sea(Bight, "Bight", 30, 2, Edge(Kiel), Edge(Deep)),
				Sea(Deep, "Deep", 50, 3, Edge(Bight), Edge(Gdynia), Edge(Far)),
				Sea(Far, "Far", 70, 4, Edge(Deep), Edge(Outer)),
				Sea(Outer, "Outer", 90, 5, Edge(Far)),
			},
			new[] { germany, poland }, buildings: new[] { NavalBase, AirBase }, defines: defines, wars: wars)
		{
			MapWidth = 1000,
			MapHeight = 1000,
			SubUnitTypes = new Dictionary<string, CSubUnitType>
			{
				[Destroyer.Key] = Destroyer, [TransportShip.Key] = TransportShip,
			},
		};
	}

	private static CUnit Fleet(CGameState state, CCountry country, int province, params CSubUnitType[] ships)
	{
		var fleet = new CUnit(EUnitKind.Navy, country.Tag)
		{
			Id = ++state.LastUnitId, Location = province, Base = country.Tag == Ger ? Kiel : Gdynia, Name = country.Tag + " fleet",
		};
		foreach (CSubUnitType type in ships)
		{
			var ship = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = fleet, Strength = CFixedPoint.FromInt(100),
				Organisation = CFixedPoint.FromInt(30), Experience = CFixedPoint.Zero, Name = type.Key,
			};
			ship.Stats.CopyFrom(type.Stats);
			fleet.SubUnits.Add(ship);
		}
		country.Units.Add(fleet);
		state.ForgetPositions();
		return fleet;
	}

	private static CConvoy Convoy(CGameState state, CCountry owner, int transports, int escorts, params int[] path)
	{
		var convoy = new CConvoy
		{
			Id = ++state.LastConvoyId, Owner = owner.Tag, Start = path[0], End = path[^1], Transports = transports, Escorts = escorts,
		};
		convoy.Path.AddRange(path);
		owner.Convoys.Add(convoy);
		return convoy;
	}

	/// <summary>Runs hours and answers the provinces the fleet was in, one for each run of hours it stayed.</summary>
	private static List<int> Track(CGameState state, CUnit fleet, int hours)
	{
		var seen = new List<int> { fleet.Location };
		for (int hour = 0; hour < hours; hour++)
		{
			CTick.RunHour(state);
			if (seen[^1] != fleet.Location)
			{
				seen.Add(fleet.Location);
			}
		}
		return seen;
	}

	// --- giving one ---

	[Fact]
	public void AMissionIsForWaterInRangeAndAReserveForOnesOwnPort()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Kiel, Destroyer);
		CUnit coaster = Fleet(state, germany, Kiel, ShortLegged);

		foreach (EUnitOrder order in new[] { EUnitOrder.Patrol, EUnitOrder.Intercept, EUnitOrder.Sortie, EUnitOrder.ConvoyRaid })
		{
			Assert.True(CUnitOrders.MayGive(state, germany, fleet, order, state.Province(Deep)));
			Assert.False(CUnitOrders.MayGive(state, germany, fleet, order, state.Province(Kiel)));
			// The Bight is 89 by sea from Kiel and the Deep 169: a range of 150 reaches the one.
			Assert.True(CUnitOrders.MayGive(state, germany, Fleet(state, germany, Kiel, Type("near", range: "150")), order, state.Province(Bight)));
			Assert.False(CUnitOrders.MayGive(state, germany, Fleet(state, germany, Kiel, Type("near", range: "150")), order, state.Province(Deep)));
		}
		Assert.True(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Reserve, state.Province(Kiel)));
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Reserve, state.Province(Gdynia)));
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Reserve, state.Province(Bight)));
		Assert.True(coaster.SubUnits.Count > 0);

		// Nothing is given to a fleet in a battle.
		fleet.Combats.Add(new CCombat(Bight, ECombatKind.Naval));
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.Patrol, state.Province(Deep)));
	}

	[Fact]
	public void AnOrderFromTheWindowIsAggressiveAndOneMadeWithNothingSaidIsDefensive()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Kiel, Destroyer);

		Assert.Equal(CMission.Defensive, fleet.Mission.Stance);
		var command = new CSetUnitOrderCommand(Ger, fleet.Id, EUnitOrder.Patrol, Deep);
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.Equal(EUnitOrder.Patrol, fleet.Order);
		Assert.Equal(CMission.Aggressive, fleet.Mission.Stance);

		new CSetUnitOrderCommand(Ger, fleet.Id, EUnitOrder.Patrol, Deep, CMission.Passive, priority: 3).Execute(state);
		Assert.Equal(CMission.Passive, fleet.Mission.Stance);
		Assert.Equal(3, fleet.Mission.Priority);
		Assert.False(new CSetUnitOrderCommand(Ger, fleet.Id, EUnitOrder.Patrol, Deep, stance: 7).MayExecute(state));
	}

	// --- the frame they share ---

	[Fact]
	public void APlayersWornFleetBreaksOffByItsStanceAndNobodyElsesDoes()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Deep, Destroyer, Destroyer);
		fleet.SubUnits[0].Strength = CFixedPoint.FromInt(70);              // 30% lost

		// One ship worn of two is enough: as many worn as not.
		fleet.Mission.Stance = CMission.Defensive;                          // a quarter
		Assert.False(CMissions.IsTooWorn(state, germany, fleet));          // nobody plays Germany
		state.Played.Add(Ger);
		Assert.True(CMissions.IsTooWorn(state, germany, fleet));
		fleet.Mission.Stance = CMission.Aggressive;                         // a half
		Assert.False(CMissions.IsTooWorn(state, germany, fleet));
		fleet.Mission.Stance = CMission.Passive;                            // a tenth
		Assert.True(CMissions.IsTooWorn(state, germany, fleet));
		fleet.SubUnits[0].Strength = CFixedPoint.FromInt(95);
		Assert.False(CMissions.IsTooWorn(state, germany, fleet));
	}

	[Fact]
	public void AWornFleetMakesForTheNearestHarbourAndLiesThereUnderItsOrder()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		state.Played.Add(Ger);
		CUnit fleet = Fleet(state, germany, Far, Destroyer);
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Patrol, Far);
		CMovement.Cancel(fleet);
		fleet.SubUnits[0].Strength = CFixedPoint.FromInt(40);

		List<int> seen = Track(state, fleet, 6);
		Assert.Equal(new[] { Far, Deep, Bight, Kiel }, seen);
		Assert.Equal(EUnitOrder.Patrol, fleet.Order);
		Assert.False(fleet.IsMoving);

		// Mended, it takes its mission up again from where it lies.
		fleet.SubUnits[0].Strength = CFixedPoint.FromInt(100);
		CTick.RunHour(state);
		Assert.True(fleet.IsMoving);
		Assert.Equal(Far, fleet.Path[^1]);
	}

	[Fact]
	public void SafeWaterCostsThreeTimesAsMuchBesideAnEnemysBase()
	{
		CGameState state = State(out CCountry germany, out _);
		CProvince deep = state.Province(Deep);
		// The Deep lies off Gdynia, which is Poland's and has a naval base.
		Assert.Equal(30000, CRouteFinder.SafeStepCost(state, germany, deep, 10000));
		state.Province(Gdynia).SetBuildingLevel(AirBase, CFixedPoint.One);
		Assert.Equal(90000, CRouteFinder.SafeStepCost(state, germany, deep, 10000));
		// The Bight lies off Kiel alone, and a step onto land costs what it costs.
		Assert.Equal(10000, CRouteFinder.SafeStepCost(state, germany, state.Province(Bight), 10000));
		Assert.Equal(10000, CRouteFinder.SafeStepCost(state, germany, state.Province(Gdynia), 10000));

		CGameState peace = State(out germany, out _, atWar: false);
		Assert.Equal(10000, CRouteFinder.SafeStepCost(peace, germany, peace.Province(Deep), 10000));
	}

	// --- patrol ---

	[Fact]
	public void APatrolGoesRoundTheSeaProvincesBesideItsOwn()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		CUnit fleet = Fleet(state, germany, Kiel, Destroyer);
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Patrol, Deep);

		// Out to the Deep; then its neighbours in the order of its edges - the Bight, Gdynia
		// which is land and is passed over, the Far - and back to the middle after the last.
		List<int> seen = Track(state, fleet, 14);
		Assert.Equal(new[] { Kiel, Bight, Deep, Bight, Deep, Far, Deep, Bight }, seen.Take(8));
		Assert.Equal(EUnitOrder.Patrol, fleet.Order);
	}

	[Fact]
	public void APatrolKeepsToItsRangeOnItsRoundAndARaiderDoesNot()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		// 200 reaches the Deep from Kiel and not the Far.
		CUnit patrol = Fleet(state, germany, Deep, ShortLegged);
		CUnitOrders.Give(state, germany, patrol, EUnitOrder.Patrol, Deep);
		Assert.DoesNotContain(Far, Track(state, patrol, 12));

		CGameState other = State(out germany, out _, atWar: false);
		CUnit raider = Fleet(other, germany, Deep, ShortLegged);
		CUnitOrders.Give(other, germany, raider, EUnitOrder.ConvoyRaid, Deep);
		Assert.Contains(Far, Track(other, raider, 12));
	}

	[Fact]
	public void APatrolShadowsAForeignFleetWithNoWarAndLetsItGoWithItsOrder()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, atWar: false);
		CUnit patrol = Fleet(state, germany, Bight, Destroyer);
		CUnit stranger = Fleet(state, poland, Bight, Destroyer);
		CUnitOrders.Give(state, germany, patrol, EUnitOrder.Patrol, Bight);

		Assert.Same(stranger, patrol.Mission.Target);
		Assert.True(stranger.IsShadowed);
		Assert.Empty(state.Combats);

		// The water a patrol shadows somebody in is known to its country at 9 from the day's turn.
		Assert.Equal(0, CIntelligence.Level(state, state.Province(Bight), Ger));
		CProvinceIntel.Rebuild(state);
		Assert.Equal(CProvinceIntel.Most, CIntelligence.Level(state, state.Province(Bight), Ger));

		CUnitOrders.Clear(patrol);
		Assert.False(stranger.IsShadowed);
		CProvinceIntel.Rebuild(state);
		Assert.Equal(CProvinceIntel.AtSea, CIntelligence.Level(state, state.Province(Bight), Ger));
		// A fleet that sails on raises what it has entered at the hour's end (intelligence.md, section 5).
		patrol.Location = Deep;
		state.ForgetPositions();
		Assert.Equal(0, CIntelligence.Level(state, state.Province(Deep), Ger));
		CProvinceIntel.RaiseHourly(state);
		Assert.Equal(CProvinceIntel.AtSea, CIntelligence.Level(state, state.Province(Deep), Ger));
	}

	[Fact]
	public void APatrolFollowsTheFleetItShadows()
	{
		CGameState state = State(out CCountry germany, out CCountry poland, atWar: false);
		CUnit patrol = Fleet(state, germany, Bight, Destroyer);
		CUnit stranger = Fleet(state, poland, Bight, Destroyer);
		CUnitOrders.Give(state, germany, patrol, EUnitOrder.Patrol, Bight);
		CMovement.SetPath(stranger, new[] { Deep, Far }, clear: true);

		CPatrol.Move(state, germany, patrol);
		Assert.Equal(new[] { Deep }, patrol.Path);
	}

	[Fact]
	public void APatrolsStanceDecidesWhatItWillFightAndWhoMayBringItToBattle()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Ger);
		CUnit patrol = Fleet(state, germany, Bight, Battleship);
		CUnit heavy = Fleet(state, poland, Bight, Battleship, Battleship);
		CUnit light = Fleet(state, poland, Bight, Destroyer);
		patrol.Order = EUnitOrder.Patrol;

		// Not aggressive, it leaves alone a fleet with more than five thirds of its heavy ships.
		patrol.Mission.Stance = CMission.Defensive;
		Assert.False(CNavalSpotting.ShouldStart(state, patrol, heavy));
		Assert.True(CNavalSpotting.ShouldStart(state, patrol, light));
		patrol.Mission.Stance = CMission.Aggressive;
		Assert.True(CNavalSpotting.ShouldStart(state, patrol, heavy));

		// Found on patrol: aggressive, anybody's defender; passive, nobody's; else a weaker fleet's alone.
		Assert.True(CNavalSpotting.DefenderAccepts(patrol, heavy));
		patrol.Mission.Stance = CMission.Passive;
		Assert.False(CNavalSpotting.DefenderAccepts(patrol, light));
		patrol.Mission.Stance = CMission.Defensive;
		Assert.True(CNavalSpotting.DefenderAccepts(patrol, light));
		Assert.False(CNavalSpotting.DefenderAccepts(patrol, heavy));
	}

	// --- the raid ---

	[Fact]
	public void ARaiderStartsNoBattleBetweenFleetsAndIsNobodysDefender()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Ger);
		state.Played.Add(Pol);
		CUnit raider = Fleet(state, germany, Bight, Destroyer);
		CUnit other = Fleet(state, poland, Bight, Destroyer);
		raider.Order = EUnitOrder.ConvoyRaid;

		Assert.False(CNavalSpotting.ShouldStart(state, raider, other));
		Assert.False(CNavalSpotting.DefenderAccepts(raider, other));
		Assert.Null(CNavalBattle.Start(state, state.Province(Bight), other));
		Assert.Empty(state.Combats);
	}

	[Fact]
	public void AConvoyBecomesAFleetOfDestroyersAndTransportShips()
	{
		CGameState state = State(out _, out CCountry poland);
		// A route of ten provinces shows a raid half its ships: a fifth of its length, two.
		CConvoy convoy = Convoy(state, poland, 5, 4, Gdynia, Deep, Bight, Deep, Far, Outer, Far, Deep, Bight, Deep);
		CUnit fleet = CConvoyRaid.MakeFleet(state, convoy, Bight)!;

		// Two escorts, and two and a half transports: four and a half ships, which is four.
		Assert.Equal(new[] { "destroyer", "destroyer", "transport_ship", "transport_ship" },
			fleet.SubUnits.Select(ship => ship.Type.Key));
		Assert.Equal(Pol, fleet.Owner);

		// A short route shows them all, and one ship at least of whatever there is any of.
		CConvoy near = Convoy(state, poland, 3, 0, Gdynia, Deep);
		Assert.Equal(3, CConvoyRaid.MakeFleet(state, near, Deep)!.SubUnits.Count);
		CConvoy far = Convoy(state, poland, 1, 1, Enumerable.Repeat(Deep, 30).ToArray());
		Assert.Equal(new[] { "destroyer", "transport_ship" }, CConvoyRaid.MakeFleet(state, far, Deep)!.SubUnits.Select(ship => ship.Type.Key));
		Assert.Null(CConvoyRaid.MakeFleet(state, Convoy(state, poland, 0, 0, Gdynia, Deep), Deep));

		Assert.Equal(4, CConvoyRaid.RoundToNearest(4500));
		Assert.Equal(6, CConvoyRaid.RoundToNearest(5500));
		Assert.Equal(5, CConvoyRaid.RoundToNearest(4501));
	}

	[Fact]
	public void WhatTheConvoysFleetLostIsCountedAtTwoShipsToTheHundred()
	{
		CGameState state = State(out _, out CCountry poland);
		CConvoy convoy = Convoy(state, poland, 6, 3, Gdynia, Deep);
		CUnit fleet = CConvoyRaid.MakeFleet(state, convoy, Deep)!;

		// Fifteen points off the transports is 0.3 of a ship, which is not yet one.
		fleet.SubUnits[3].Strength = CFixedPoint.FromInt(85);
		CConvoyRaid.Count(state, convoy, fleet);
		Assert.Equal((6, 3), (convoy.Transports, convoy.Escorts));
		Assert.Equal(CConvoy.NeverAttacked, convoy.LastAttackHour);

		// Sixteen is over it and counts as a whole transport.
		fleet.SubUnits[3].Strength = CFixedPoint.FromInt(84);
		CConvoyRaid.Count(state, convoy, fleet);
		Assert.Equal((5, 3), (convoy.Transports, convoy.Escorts));
		Assert.Equal(state.TotalHours, convoy.LastAttackHour);

		// A destroyer with half its strength gone is one escort; under that, none.
		fleet.SubUnits[3].Strength = CFixedPoint.FromInt(100);
		fleet.SubUnits[0].Strength = CFixedPoint.FromInt(51);
		CConvoyRaid.Count(state, convoy, fleet);
		Assert.Equal((5, 3), (convoy.Transports, convoy.Escorts));
		fleet.SubUnits[0].Strength = CFixedPoint.FromInt(50);
		CConvoyRaid.Count(state, convoy, fleet);
		Assert.Equal((5, 2), (convoy.Transports, convoy.Escorts));

		// And a convoy loses no more than it has.
		CConvoyLosses.Lose(state, convoy, 99, 99);
		Assert.Equal((0, 0), (convoy.Transports, convoy.Escorts));
	}

	[Fact]
	public void ARaidIsFiveRoundsAgainstTheConvoysFleetAndIsThenGone()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit raider = Fleet(state, germany, Bight, Submarine, Submarine, Submarine);
		CConvoy convoy = Convoy(state, poland, 8, 0, Gdynia, Deep, Bight);

		// Three submarines at 30 organisation: 3 x 10 x 1 x 0.3.
		Assert.Equal(9000, CConvoyRaid.Attack(raider));
		Assert.True(CConvoyRaid.Resolve(state, germany, raider));

		Assert.True(convoy.Transports < 8);
		Assert.Equal(state.TotalHours, state.Province(Bight).LastConvoyAttackHour);
		Assert.Equal(state.TotalHours, convoy.LastAttackHour);
		Assert.False(raider.IsFighting);
		Assert.Empty(state.Combats);
		// Its ships were hardly touched: a convoy's hit does an eightieth of its strength damage.
		Assert.All(raider.SubUnits, ship => Assert.True(ship.Strength > CFixedPoint.FromInt(99)));
		// A raided convoy wants five escorts more for three days.
		Assert.Equal(convoy.Transports / 2 + 1 + 5, CConvoyRules.DesiredEscorts(state, convoy));
		state.Province(Bight).LastConvoyAttackHour = CProvince.NoConvoyAttack;
		for (int day = 0; day < 3; day++)
		{
			for (int hour = 0; hour < CGameState.HoursPerDay; hour++)
			{
				state.AdvanceOneHour();
			}
		}
		Assert.Equal(convoy.Transports / 2 + 1 + 3, CConvoyRules.DesiredEscorts(state, convoy));
	}

	[Fact]
	public void ARaiderNeedsToFindAConvoyOfAnEnemysAndToBeMoreThanItsEscorts()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit raider = Fleet(state, germany, Bight, Submarine);
		Assert.False(CConvoyRaid.Resolve(state, germany, raider));                 // no convoy here

		Convoy(state, germany, 5, 0, Kiel, Bight, Deep);                           // its own
		Assert.False(CConvoyRaid.Resolve(state, germany, raider));
		CConvoy empty = Convoy(state, poland, 0, 2, Gdynia, Deep, Bight);          // no transport
		Assert.False(CConvoyRaid.Resolve(state, germany, raider));

		// One submarine is worth 3; two escorts over three provinces defend at 3.33.
		empty.Transports = 4;
		Assert.Equal(3333, CConvoyRules.Defence(empty));
		Assert.False(CConvoyRaid.Resolve(state, germany, raider));
		Assert.Equal(4, empty.Transports);
		Assert.Equal(CProvince.NoConvoyAttack, state.Province(Bight).LastConvoyAttackHour);

		CGameState peace = State(out germany, out poland, atWar: false);
		CUnit atPeace = Fleet(peace, germany, Bight, Submarine, Submarine);
		Convoy(peace, poland, 5, 0, Gdynia, Deep, Bight);
		Assert.False(CConvoyRaid.Resolve(peace, germany, atPeace));
	}

	[Fact]
	public void APatrolAndARaiderTryTheConvoysEveryTwelveHours()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit raider = Fleet(state, germany, Bight, Submarine, Submarine, Submarine);
		Convoy(state, poland, 40, 0, Gdynia, Deep, Bight);
		CUnitOrders.Give(state, germany, raider, EUnitOrder.ConvoyRaid, Bight);
		CMovement.Cancel(raider);
		raider.Mission.Round = 99;                                                 // it stays where it is

		CTick.RunHour(state);
		long first = state.Province(Bight).LastConvoyAttackHour;
		Assert.Equal(state.TotalHours, first);
		Assert.Equal(CMissions.HoursBetweenRaids, raider.Mission.RaidCountdown);
		for (int hour = 0; hour < 11; hour++)
		{
			CTick.RunHour(state);
		}
		Assert.Equal(first, state.Province(Bight).LastConvoyAttackHour);
		CTick.RunHour(state);
		Assert.Equal(first + 12, state.Province(Bight).LastConvoyAttackHour);
	}

	[Fact]
	public void WaterAConvoyWasAttackedInIsWatchedTheHarder()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Pol);
		// A ship that sees next to nothing: one chance in ten thousand for each pair.
		CSubUnitType blind = Type("blind");
		blind.Stats.Values.Set(CNavalSpotting.SurfaceDetection, CFixedPoint.Parse("0.001"));
		CSubUnitType dim = Type("dim");
		dim.Stats.Values.Set(CNavalSpotting.Visibility, CFixedPoint.One);
		CUnit hunter = Fleet(state, poland, Bight, blind);
		CUnit raider = Fleet(state, germany, Bight, dim);

		int Sightings()
		{
			int seen = 0;
			for (int roll = 0; roll < 4000; roll++)
			{
				seen += CNavalSpotting.ShouldStart(state, hunter, raider) ? 1 : 0;
			}
			return seen;
		}

		int plain = Sightings();
		raider.IsShadowed = true;
		Assert.InRange(Sightings(), plain, plain + 4);                             // still under the least there is
		raider.IsShadowed = false;
		Assert.True(plain < 8);

		// A figure a thousand times the least: a fleet that sees well finds it far more often.
		blind.Stats.Values.Set(CNavalSpotting.SurfaceDetection, CFixedPoint.FromInt(200));
		hunter.SubUnits[0].Stats.CopyFrom(blind.Stats);
		int clear = Sightings();
		state.Province(Bight).LastConvoyAttackHour = state.TotalHours;
		int alerted = Sightings();
		Assert.True(alerted > clear * 3, $"{alerted} against {clear}");
		raider.IsShadowed = true;
		Assert.True(Sightings() > alerted, "a shadowed fleet is seen twice as easily");
	}

	// --- intercept ---

	[Fact]
	public void AnInterceptorWaitsInPortAndGoesForAnEnemyBesideItsProvince()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit interceptor = Fleet(state, germany, Kiel, Destroyer, Destroyer);
		CUnitOrders.Give(state, germany, interceptor, EUnitOrder.Intercept, Bight);
		CTick.RunHour(state);
		Assert.False(interceptor.IsMoving);

		// An enemy in the Deep, which is beside the Bight: seen from port with no roll.
		CUnit enemy = Fleet(state, poland, Deep, Destroyer);
		CTick.RunHour(state);
		Assert.Same(enemy, interceptor.Mission.Target);
		Assert.Equal(Bight, interceptor.Location);
		CTick.RunHour(state);
		Assert.Equal(Deep, interceptor.Location);
		CCombat combat = Assert.Single(state.Combats);
		Assert.Contains(interceptor, combat.Attacker.Units);
	}

	[Fact]
	public void AnInterceptGivenOnAFleetNamesItAndIsStillAfterNobody()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit interceptor = Fleet(state, germany, Kiel, Destroyer, Destroyer);
		CUnit enemy = Fleet(state, poland, Gdynia, Destroyer);
		var command = new CSetUnitOrderCommand(germany.Tag, interceptor.Id, EUnitOrder.Intercept, Bight, target: enemy.Id);
		Assert.True(command.MayExecute(state));
		command.Execute(state);

		// The order names the enemy, as the original's AI gives it; the fleet it chases is the one its search finds.
		Assert.Same(enemy, interceptor.Mission.OrderTarget);
		Assert.Null(interceptor.Mission.Target);
	}

	[Fact]
	public void AnInterceptorLeavesAStrongerFleetAlone()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit interceptor = Fleet(state, germany, Kiel, Destroyer);
		Fleet(state, poland, Bight, Battleship, Battleship);
		CUnitOrders.Give(state, germany, interceptor, EUnitOrder.Intercept, Bight);
		CTick.RunHour(state);
		Assert.Null(interceptor.Mission.Target);
		Assert.False(interceptor.IsMoving);
	}

	[Fact]
	public void AnInterceptorToldToReturnToBaseDoesNothingMoreUnderItsOrder()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		// Given the order at sea, it is sent home.
		CUnit interceptor = Fleet(state, germany, Bight, Destroyer, Destroyer);
		CUnitOrders.Give(state, germany, interceptor, EUnitOrder.Intercept, Bight);
		Assert.True(interceptor.Mission.ReturnToBase);
		Assert.Equal(new[] { Kiel }, interceptor.Path);
		CTick.RunHour(state);
		Assert.Equal(Kiel, interceptor.Location);

		// And lies there whatever comes: nothing takes the return to base back.
		Fleet(state, poland, Bight, Destroyer);
		for (int hour = 0; hour < 6; hour++)
		{
			CTick.RunHour(state);
		}
		Assert.Equal(Kiel, interceptor.Location);
		Assert.False(interceptor.IsMoving);
		Assert.Null(interceptor.Mission.Target);
		Assert.Equal(EUnitOrder.Intercept, interceptor.Order);

		// Given anew from port, it goes.
		CUnitOrders.Give(state, germany, interceptor, EUnitOrder.Intercept, Bight);
		Assert.False(interceptor.Mission.ReturnToBase);
		CTick.RunHour(state);
		Assert.NotNull(interceptor.Mission.Target);
	}

	// --- sortie ---

	[Fact]
	public void ASortieSailsToItsProvinceAndTurnsForHomeWhenItArrives()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Kiel, Destroyer);
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Sortie, Far);
		Assert.Equal(new[] { Bight, Deep, Far }, fleet.Path);

		Assert.Equal(new[] { Kiel, Bight, Deep, Far, Deep, Bight, Kiel }, Track(state, fleet, 10));
		Assert.True(fleet.Mission.ReturnToBase);
		Assert.Equal(EUnitOrder.Sortie, fleet.Order);
	}

	[Fact]
	public void ASortieGoesForAnEnemyBesideItsWayAndDoesNotTakeUpItsWayAgain()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		state.Played.Add(Ger);
		CUnit fleet = Fleet(state, germany, Kiel, Destroyer, Destroyer);
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Sortie, Outer);
		CUnit enemy = Fleet(state, poland, Far, Destroyer);
		CTick.RunHour(state);                                                      // to the Bight
		CTick.RunHour(state);                                                      // to the Deep, beside the Far
		CTick.RunHour(state);                                                      // and at the enemy
		Assert.Equal(Far, fleet.Location);
		Assert.Contains(fleet, Assert.Single(state.Combats).Attacker.Units);

		// The enemy gone and the battle over, it lies where it fought: its path was the way to him.
		CCombats.Finish(state, state.Combats[0]);
		CCombatRetreat.RemoveFromGame(state, enemy);
		state.ForgetPositions();
		for (int hour = 0; hour < 4; hour++)
		{
			CTick.RunHour(state);
		}
		Assert.Equal(Far, fleet.Location);
		Assert.False(fleet.IsMoving);
		Assert.Equal(EUnitOrder.Sortie, fleet.Order);
	}

	[Fact]
	public void ASortieOrderedAwayFromItsBaseIsLeftWithTheLegHome()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit fleet = Fleet(state, germany, Bight, Destroyer);
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Sortie, Far);

		// The way out was plotted and then replaced by the way from the Far to Kiel, which does
		// not begin where the fleet is. That is the original's.
		Assert.Equal(new[] { Deep, Bight, Kiel }, fleet.Path);
	}

	// --- escort ---

	[Fact]
	public void AnEscortSailsUpAndDownTheStretchOfTheRouteWithinItsRange()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		CConvoy convoy = Convoy(state, germany, 5, 0, Kiel, Bight, Deep, Far, Outer);
		// 200 from Kiel reaches the Bight and the Deep.
		CUnit fleet = Fleet(state, germany, Kiel, ShortLegged);
		Assert.Equal((Bight, Deep), CConvoyEscort.FindBeat(state, fleet, convoy));

		Assert.True(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.ConvoyEscort, state.Province(Bight), convoy.Id));
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.ConvoyEscort, state.Province(Bight), convoy: 999));
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.ConvoyEscort, Bight, convoy: convoy.Id);
		Assert.Equal(new[] { Kiel, Bight, Deep, Bight, Deep, Bight }, Track(state, fleet, 12).Take(6));

		// With its convoy gone the order ends.
		germany.Convoys.Clear();
		CTick.RunHour(state);
		Assert.Equal(EUnitOrder.None, fleet.Order);
	}

	[Fact]
	public void AnEscortHasNoBeatWhereTheRouteIsOutOfItsRange()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		CConvoy convoy = Convoy(state, germany, 5, 0, Outer, Far);
		CUnit fleet = Fleet(state, germany, Kiel, ShortLegged);
		Assert.Equal((0, 0), CConvoyEscort.FindBeat(state, fleet, convoy));
		Assert.False(CUnitOrders.MayGive(state, germany, fleet, EUnitOrder.ConvoyEscort, state.Province(Far), convoy.Id));
	}

	// --- reserve ---

	[Fact]
	public void AReservesSoundShipsChangePlacesWithTheWornOnesOfTheFleetsBesideIt()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		CUnit reserve = Fleet(state, germany, Kiel, Destroyer, Destroyer, Battleship);
		CUnit worn = Fleet(state, germany, Kiel, Destroyer, Destroyer, Destroyer);
		worn.SubUnits[0].Strength = CFixedPoint.FromInt(30);                       // under half
		worn.SubUnits[1].Strength = CFixedPoint.FromInt(49);
		worn.SubUnits[2].Strength = CFixedPoint.FromInt(80);                       // not worn enough
		CUnit atSea = Fleet(state, germany, Bight, Destroyer);
		atSea.SubUnits[0].Strength = CFixedPoint.FromInt(10);
		reserve.SubUnits[1].Strength = CFixedPoint.FromInt(60);

		CUnitOrders.Give(state, germany, reserve, EUnitOrder.Reserve, Kiel);
		CMissions.RunDaily(state);

		// The most worn gets the soundest, the next the next - 60, which is sounder than 49.
		Assert.Equal(new[] { 80, 100, 60 }, worn.SubUnits.Select(ship => (int)ship.Strength.ToInt()));
		Assert.Equal(new[] { "battleship", "destroyer", "destroyer" }, reserve.SubUnits.Select(ship => ship.Type.Key));
		Assert.Equal(new[] { 100, 30, 49 }, reserve.SubUnits.Select(ship => (int)ship.Strength.ToInt()));
		Assert.All(worn.SubUnits, ship => Assert.Same(worn, ship.Unit));
		// A fleet that is not in the port gets nothing.
		Assert.Equal(10, atSea.SubUnits[0].Strength.ToInt());
	}

	[Fact]
	public void AReserveSeesToTheHighestPriorityFirstAndAPassiveFleetSooner()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		CUnit reserve = Fleet(state, germany, Kiel, Destroyer);
		CUnit first = Fleet(state, germany, Kiel, Destroyer);
		CUnit second = Fleet(state, germany, Kiel, Destroyer);
		first.SubUnits[0].Strength = CFixedPoint.FromInt(40);
		second.SubUnits[0].Strength = CFixedPoint.FromInt(20);
		second.Mission.Priority = 3;
		CUnitOrders.Give(state, germany, reserve, EUnitOrder.Reserve, Kiel);
		CMissions.RunDaily(state);
		Assert.Equal(100, second.SubUnits[0].Strength.ToInt());
		Assert.Equal(40, first.SubUnits[0].Strength.ToInt());

		// At 70 a ship is worn for a passive fleet, whose limit is three quarters, and for no other.
		CGameState other = State(out germany, out _, atWar: false);
		reserve = Fleet(other, germany, Kiel, Destroyer);
		CUnit careful = Fleet(other, germany, Kiel, Destroyer);
		careful.SubUnits[0].Strength = CFixedPoint.FromInt(70);
		CReserve.Exchange(other, reserve, careful);
		Assert.Equal(70, careful.SubUnits[0].Strength.ToInt());
		careful.Mission.Stance = CMission.Passive;
		CReserve.Exchange(other, reserve, careful);
		Assert.Equal(100, careful.SubUnits[0].Strength.ToInt());
	}

	[Fact]
	public void AReserveSentForFromElsewhereMakesThePortItsBaseAndStays()
	{
		CGameState state = State(out CCountry germany, out _, atWar: false);
		CUnit fleet = Fleet(state, germany, Deep, Destroyer);
		fleet.Base = 0;
		// With no base nothing is out of range.
		CUnitOrders.Give(state, germany, fleet, EUnitOrder.Reserve, Kiel);
		Assert.Equal(Kiel, fleet.Base);
		Assert.True(fleet.Mission.ReturnToBase);
		Assert.Equal(new[] { Deep, Bight, Kiel }, Track(state, fleet, 5));
		Assert.Equal(EUnitOrder.Reserve, fleet.Order);

		// One that leaves its port without having been sent for loses the order.
		CUnit other = Fleet(state, germany, Kiel, Destroyer);
		CUnitOrders.Give(state, germany, other, EUnitOrder.Reserve, Kiel);
		CTick.RunHour(state);
		Assert.Equal(EUnitOrder.Reserve, other.Order);
		CMovement.SetPath(other, new[] { Bight }, clear: true);
		CTick.RunHour(state);
		Assert.Equal(EUnitOrder.None, other.Order);
	}

	// --- how good a country is at a mission ---

	[Fact]
	public void MissionEfficiencyIsWhatTechnologiesGiveAndGoesOnAShipsAttack()
	{
		var doctrine = new CTechnology("trade_interdiction", 12, new OpenHOI3.Sim.Modifiers.CModifier("trade_interdiction"));
		doctrine.MissionEfficiency["convoy_raid"] = CFixedPoint.Parse("0.06");
		doctrine.MissionReduction["convoy_raid"] = CFixedPoint.Parse("-0.5");
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit raider = Fleet(state, germany, Bight, Submarine);
		raider.Order = EUnitOrder.ConvoyRaid;

		Assert.Equal(0, CMissionEfficiency.Of(state, germany, EUnitOrder.ConvoyRaid));
		Assert.Equal("convoy_raid", CMissionEfficiency.NameOf(EUnitOrder.ConvoyRaid));
		Assert.Equal(32, CMissionEfficiency.Names.Count);
		Assert.Equal(0, CMissionEfficiency.Of(state, germany, EUnitOrder.None));

		state = StateWith(doctrine, out germany, out poland);
		germany.TechnologyLevels[doctrine.Name] = 3;
		raider = Fleet(state, germany, Bight, Submarine);
		raider.Order = EUnitOrder.ConvoyRaid;
		Assert.Equal(180, CMissionEfficiency.Of(state, germany, EUnitOrder.ConvoyRaid));
		Assert.Equal(0, CMissionEfficiency.Of(state, germany, EUnitOrder.Patrol));

		CUnit stand = Fleet(state, poland, Bight, Destroyer);
		var combat = new CCombat(Bight, ECombatKind.Naval) { IsConvoyBattle = true };
		CNavalBattle.Join(state, combat, combat.Attacker, raider);
		CNavalBattle.Join(state, combat, combat.Defender, stand);
		combat.Hour = 5;                                                           // past any surprise
		CNavalModifiers.Apply(state, combat, combat.Attacker);
		Assert.Contains(raider.SubUnits[0].CombatModifiers, modifier => modifier.Kind == ECombatModifier.MissionEfficiency);
		Assert.Equal(1180, raider.SubUnits[0].CombatAttack.Raw);
	}

	private static CGameState StateWith(CTechnology technology, out CCountry germany, out CCountry poland)
	{
		CGameState plain = State(out germany, out poland);
		return new CGameState(plain.Date, plain.Provinces.ToArray(), new[] { germany, poland },
			buildings: plain.Buildings.ToArray(), defines: plain.Defines, wars: plain.Wars, technologies: new[] { technology })
		{
			MapWidth = 1000,
			MapHeight = 1000,
			SubUnitTypes = plain.SubUnitTypes,
		};
	}
}
