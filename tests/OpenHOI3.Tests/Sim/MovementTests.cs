using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Movement against <c>docs/mechanics/movement.md</c>, on a country of four provinces in
/// a row with a neighbour's province beside the second:
///
///     1 Berlin - 2 Potsdam - 3 Dessau - 4 Leipzig
///                    |
///                 5 Posen (Poland's)
///
/// The base game's infantry walks at 4 and <c>LAND_SPEED_MODIFIER</c> is 0.05, so an
/// hour is worth 0.2.
/// </summary>
public class MovementTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Potsdam = 2;
	private const int Dessau = 3;
	private const int Leipzig = 4;
	private const int Posen = 5;

	private static readonly CTerrain Plains = new("plains", isWater: false, default);
	private static readonly CTerrain Forest = new("forest", isWater: false, default);

	private static CSubUnitType Type(string key, string speed, bool mobile = false, string fuel = "0")
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.Parse(speed));
		type.Stats.Values.Set(CUnitSupply.FuelStatistic, CFixedPoint.Parse(fuel));
		type.Flags[CMovementSpeed.IsMobile] = mobile;
		return type;
	}

	private static CSubUnitType Infantry() => Type("infantry_brigade", "4");

	private static CProvinceEdge Edge(int to, string distance, EProvinceEdgeKind kind = EProvinceEdgeKind.Plain, int through = 0)
	{
		return new CProvinceEdge(to, kind, through, CFixedPoint.Parse(distance));
	}

	private static CProvince Land(int id, string name, CCountryTag owner, CTerrain terrain, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: false) { Owner = owner, Controller = owner, Terrain = terrain, Edges = edges };
	}

	private static CGameState State(CCountry germany, IEnumerable<CWar>? wars = null, string potsdamToDessau = "1",
		EProvinceEdgeKind potsdamToDessauKind = EProvinceEdgeKind.Plain, int through = 0)
	{
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CMovementSpeed.LandSpeedModifier, CFixedPoint.Parse("0.05"));
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		return new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Berlin, "Berlin", Ger, Plains, Edge(Potsdam, "1")),
				Land(Potsdam, "Potsdam", Ger, Plains, Edge(Berlin, "2"),
					Edge(Dessau, potsdamToDessau, potsdamToDessauKind, through), Edge(Posen, "1")),
				Land(Dessau, "Dessau", Ger, Forest, Edge(Potsdam, "1"), Edge(Leipzig, "1")),
				Land(Leipzig, "Leipzig", Ger, Plains, Edge(Dessau, "1")),
				Land(Posen, "Posen", Pol, Plains, Edge(Potsdam, "1")),
			},
			new[] { germany, poland }, defines: defines, wars: wars);
	}

	private static CCountry Germany() => new() { Tag = Ger, Name = "Germany", DefinitionFile = "" };

	private static CUnit Division(CGameState state, CCountry country, int province, params CSubUnitType[] types)
	{
		var unit = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = "Division" };
		foreach (CSubUnitType type in types)
		{
			var subUnit = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit };
			subUnit.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		return unit;
	}

	private static void SetAdjuster(CSubUnit subUnit, string name, string movement)
	{
		var adjuster = new CModifier(name);
		adjuster.Set(CMovementSpeed.MovementAdjuster, CFixedPoint.Parse(movement));
		subUnit.Stats.Adjusters[name] = adjuster;
	}

	// --- speed ---

	[Fact]
	public void AUnitIsAsFastAsItsSlowestBrigadeTimesTheDefineForItsKind()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry(), Type("artillery_brigade", "3"));

		Assert.Equal(CFixedPoint.FromInt(3), CMovementSpeed.SlowestBrigade(unit));
		Assert.Equal(CFixedPoint.Parse("0.15"), CMovementSpeed.Speed(state, country, unit));
	}

	[Fact]
	public void TheNextProvincesTerrainCountsByTheAverageOfTheBrigadesAdjusters()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Potsdam, Infantry(), Infantry());
		SetAdjuster(unit.SubUnits[0], "forest", "-0.2");               // the other has none: -0.1 between them

		unit.Path.Add(Dessau);                                         // forest
		Assert.Equal(900, CMovementSpeed.Modifier(state, country, unit));
		Assert.Equal(CFixedPoint.Parse("0.18"), CMovementSpeed.Speed(state, country, unit));

		unit.Path[0] = Berlin;                                         // plains, which nothing adjusts
		Assert.Equal(1000, CMovementSpeed.Modifier(state, country, unit));
	}

	[Fact]
	public void ARiverAndACrossingBothCountByTheAdjusterForRivers()
	{
		foreach (EProvinceEdgeKind kind in new[] { EProvinceEdgeKind.River, EProvinceEdgeKind.Crossing })
		{
			CCountry country = Germany();
			CGameState state = State(country, potsdamToDessauKind: kind);
			CUnit unit = Division(state, country, Potsdam, Infantry());
			SetAdjuster(unit.SubUnits[0], CMovementSpeed.RiverAdjuster, "-0.5");

			unit.Path.Add(Dessau);
			Assert.Equal(500, CMovementSpeed.Modifier(state, country, unit));
			unit.Path[0] = Berlin;
			Assert.Equal(1000, CMovementSpeed.Modifier(state, country, unit));
		}
	}

	[Fact]
	public void AUnitThatBurnsFuelKeepsATenthOfItsSpeedWithNone()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit tanks = Division(state, country, Berlin, Type("armor_brigade", "8", fuel: "1.2"));
		CUnit infantry = Division(state, country, Berlin, Infantry());

		tanks.FuelReceived = CFixedPoint.Zero;
		infantry.FuelReceived = CFixedPoint.Zero;
		Assert.Equal(100, CMovementSpeed.Modifier(state, country, tanks));
		Assert.Equal(1000, CMovementSpeed.Modifier(state, country, infantry));   // burns none

		tanks.FuelReceived = CFixedPoint.Parse("0.5");
		Assert.Equal(550, CMovementSpeed.Modifier(state, country, tanks));
		tanks.FuelReceived = CFixedPoint.One;
		Assert.Equal(1000, CMovementSpeed.Modifier(state, country, tanks));
	}

	[Fact]
	public void TheProvinceAUnitIsInCountsAndNotTheOneItIsGoingTo()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		unit.Path.Add(Potsdam);

		state.Province(Potsdam).Modifier.Set(CMovementSpeed.LocalUnitSpeed, CFixedPoint.Parse("-0.2"));
		Assert.Equal(1000, CMovementSpeed.Modifier(state, country, unit));
		state.Province(Berlin).Modifier.Set(CMovementSpeed.LocalUnitSpeed, CFixedPoint.Parse("-0.2"));
		Assert.Equal(800, CMovementSpeed.Modifier(state, country, unit));
	}

	[Fact]
	public void TheCountrysBonusForMobileBrigadesCountsByTheShareOfThemThatAre()
	{
		// The base game's rubber gives 0.15.
		CCountry country = Germany();
		country.Modifier.Set(CMovementSpeed.MobileSpeed, CFixedPoint.Parse("0.15"));
		CGameState state = State(country);
		CUnit half = Division(state, country, Berlin, Type("motorized_brigade", "8", mobile: true), Infantry());
		CUnit none = Division(state, country, Berlin, Infantry());
		CUnit third = Division(state, country, Berlin, Type("motorized_brigade", "8", mobile: true), Infantry(), Infantry());

		Assert.Equal(500, CMovementSpeed.MobileShare(half));
		Assert.Equal(1075, CMovementSpeed.Modifier(state, country, half));
		Assert.Equal(1000, CMovementSpeed.Modifier(state, country, none));
		Assert.Equal(333, CMovementSpeed.MobileShare(third));
		Assert.Equal(1049, CMovementSpeed.Modifier(state, country, third));       // 0.15 x 0.333, cut
	}

	// --- the march ---

	[Fact]
	public void AUnitEntersTheNextProvinceWhenItsProgressReachesTheDistanceAndKeepsWhatIsLeft()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Type("fast", "6"));   // 0.3 an hour
		unit.Path.AddRange(new[] { Potsdam, Dessau });

		for (int hour = 0; hour < 3; hour++)
		{
			CMovement.RunHourly(state);
		}
		Assert.Equal(Berlin, unit.Location);
		Assert.Equal(CFixedPoint.Parse("0.9"), unit.MovementProgress);

		CMovement.RunHourly(state);
		Assert.Equal(Potsdam, unit.Location);
		Assert.Equal(Berlin, unit.Previous);
		Assert.Equal(CFixedPoint.Parse("0.2"), unit.MovementProgress);
		Assert.Equal(new[] { Dessau }, unit.Path);
	}

	[Fact]
	public void OneHourCarriesAUnitThroughEveryProvinceItsProgressCovers()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Type("rocket", "50"));   // 2.5 an hour
		unit.Path.AddRange(new[] { Potsdam, Dessau, Leipzig });

		CMovement.RunHourly(state);

		// 2.5 covers Berlin to Potsdam and Potsdam to Dessau, one each, and half of the next.
		Assert.Equal(Dessau, unit.Location);
		Assert.Equal(Potsdam, unit.Previous);
		Assert.Equal(CFixedPoint.Parse("0.5"), unit.MovementProgress);
		Assert.Equal(new[] { Leipzig }, unit.Path);
	}

	[Fact]
	public void TheDistanceIsThatOfTheEdgeOutOfTheProvinceTheUnitIsIn()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());

		unit.Path.Add(Potsdam);
		Assert.Equal(CFixedPoint.One, CMovement.Distance(state, unit));        // Berlin to Potsdam
		Assert.Equal(5, CMovement.HoursToNext(state, country, unit));          // 1 at 0.2 an hour

		unit.Location = Potsdam;
		unit.Path[0] = Berlin;
		Assert.Equal(CFixedPoint.FromInt(2), CMovement.Distance(state, unit)); // and the way back is twice as far
		unit.MovementProgress = CFixedPoint.Parse("0.5");
		Assert.Equal(8, CMovement.HoursToNext(state, country, unit));          // 1.5 left: 7.5 hours, rounded up
	}

	[Fact]
	public void WhatAUnitHasWalkedIsCountedAgainstTheWholeOfTheWayAhead()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		Assert.Equal(0, CMovement.WalkedShare(state, unit));               // going nowhere

		unit.Path.AddRange(new[] { Potsdam, Dessau, Leipzig });             // three legs of 1
		unit.MovementProgress = CFixedPoint.Parse("0.6");
		Assert.Equal(200, CMovement.WalkedShare(state, unit));

		unit.Location = Potsdam;                                            // one province on: two legs left
		unit.Path.RemoveAt(0);
		Assert.Equal(300, CMovement.WalkedShare(state, unit));
	}

	[Fact]
	public void AUnitWhoseWayIsGoneOrClosedStopsWhereItStands()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		unit.Path.AddRange(new[] { Leipzig });                                  // Berlin has no edge to Leipzig
		unit.MovementProgress = CFixedPoint.Parse("0.4");

		CMovement.RunHourly(state);

		Assert.Equal(Berlin, unit.Location);
		Assert.False(unit.IsMoving);
		Assert.Equal(CFixedPoint.Zero, unit.MovementProgress);
	}

	[Fact]
	public void APathThatBeginsWhereTheUnitStandsLosesThatProvinceAndItsProgress()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		unit.Path.AddRange(new[] { Berlin, Potsdam });
		unit.MovementProgress = CFixedPoint.Parse("0.4");

		CMovement.RunHourly(state);

		Assert.Equal(new[] { Potsdam }, unit.Path);
		Assert.Equal(CFixedPoint.Zero, unit.MovementProgress);
	}

	// --- where a unit may go ---

	[Fact]
	public void ACountryWalksOnItsOwnGroundAFriendsAnEnemysAndNobodys()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CProvince posen = state.Province(Posen);

		Assert.True(CMovementRules.MayEnter(state, country, state.Province(Potsdam)));
		Assert.False(CMovementRules.MayEnter(state, country, posen));          // a neutral's

		country.Faction = "axis";
		state.Country(Pol)!.Faction = "axis";
		Assert.True(CMovementRules.MayEnter(state, country, posen));           // a friend's
		state.Country(Pol)!.Faction = "";

		posen.Owner = null;
		posen.Controller = null;
		Assert.True(CMovementRules.MayEnter(state, country, posen));           // nobody's

		var war = new CWar("a war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		CGameState atWar = State(country, new[] { war });
		Assert.True(CMovementRules.MayEnter(atWar, country, atWar.Province(Posen)));
	}

	[Fact]
	public void AUnitMayAlwaysStandOnGroundHeldByWhoeverHoldsTheGroundUnderIt()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Posen, Infantry());              // on a neutral's ground somehow
		state.Province(Dessau).Controller = Pol;

		Assert.True(CMovementRules.MayStandIn(state, country, unit, state.Province(Dessau)));
		unit.Location = Berlin;
		Assert.False(CMovementRules.MayStandIn(state, country, unit, state.Province(Posen)));
	}

	// --- the route ---

	private static List<int>? Route(CGameState state, CCountry country, CUnit unit, int to)
	{
		return CRouteFinder.Find(state, country, unit, unit.Location, to);
	}

	[Fact]
	public void ARouteLeavesOutWhereItStartsAndEndsWithWhereItGoes()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());

		Assert.Equal(new[] { Potsdam, Dessau, Leipzig }, Route(state, country, unit, Leipzig));
		Assert.Empty(Route(state, country, unit, Berlin)!);
		Assert.Null(Route(state, country, unit, Posen));                       // it may not stand there
	}

	[Fact]
	public void ARouteDoesNotGoOverAnImpassableEdgeOrACrossingAnEnemyHolds()
	{
		CCountry country = Germany();
		CGameState walled = State(country, potsdamToDessauKind: EProvinceEdgeKind.Impassable);
		CUnit unit = Division(walled, country, Berlin, Infantry());
		Assert.Null(Route(walled, country, unit, Leipzig));

		var war = new CWar("a war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		CGameState crossing = State(country, new[] { war }, potsdamToDessauKind: EProvinceEdgeKind.Crossing, through: Posen);
		Assert.Null(Route(crossing, country, unit, Leipzig));                  // Poland holds the province crossed

		CGameState open = State(country, potsdamToDessauKind: EProvinceEdgeKind.Crossing, through: Posen);
		Assert.NotNull(Route(open, country, unit, Leipzig));                   // and at peace it is open
	}

	[Fact]
	public void AProvinceIsSettledTheFirstTimeItIsReachedSoTheRouteIsNotAlwaysTheCheapest()
	{
		// Start touches A, far, and B, near, in that order; B touches A; A touches the
		// goal. Through B it would be 3. A is reached first from the start, at 10, and
		// that stands.
		const int start = 1;
		const int a = 2;
		const int b = 3;
		const int goal = 4;
		CCountry country = Germany();
		var state = new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(start, "Start", Ger, Plains, Edge(a, "10"), Edge(b, "1")),
				Land(a, "A", Ger, Plains, Edge(start, "10"), Edge(b, "1"), Edge(goal, "1")),
				Land(b, "B", Ger, Plains, Edge(start, "1"), Edge(a, "1")),
				Land(goal, "Goal", Ger, Plains, Edge(a, "1")),
			},
			new[] { country });
		CUnit unit = Division(state, country, start, Infantry());

		Assert.Equal(new[] { a, goal }, Route(state, country, unit, goal));
	}

	[Fact]
	public void TheStraightLineIsMeasuredTheShortWayRoundTheMapAndRoundedDown()
	{
		CCountry country = Germany();
		var west = new CProvince(1, "West", isSea: false) { CentreX = 10, CentreY = 0 };
		var east = new CProvince(2, "East", isSea: false) { CentreX = 90, CentreY = 10 };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), west, east },
			new[] { country })
		{
			MapWidth = 100,
		};

		Assert.Equal(22, CRouteFinder.StraightLine(state, west, east));        // 20 across and 10 up: 22.36
		Assert.Equal(0, CRouteFinder.WholeRoot(0));
		Assert.Equal(1, CRouteFinder.WholeRoot(3));
		Assert.Equal(12, CRouteFinder.WholeRoot(144));
		Assert.Equal(12, CRouteFinder.WholeRoot(168));
	}

	// --- the order ---

	[Fact]
	public void AnOrderWorksTheWayOutWhenItRunsAndReplacesThePathTheUnitHas()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		var simulation = new CSimulation(state);

		simulation.Post(new CMoveUnitCommand(Ger, unit.Id, Leipzig));
		simulation.Pump();
		Assert.Equal(new[] { Potsdam, Dessau, Leipzig }, unit.Path);

		simulation.Post(new CMoveUnitCommand(Ger, unit.Id, Potsdam));
		simulation.Pump();
		Assert.Equal(new[] { Potsdam }, unit.Path);
	}

	[Fact]
	public void WhatAUnitHasWalkedIsKeptOnlyWhereTheNewPathBeginsAsTheOldOneDid()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Potsdam, Infantry());
		unit.Path.AddRange(new[] { Dessau, Leipzig });
		unit.MovementProgress = CFixedPoint.Parse("0.6");

		CMovement.SetPath(unit, new[] { Dessau }, clear: true);
		Assert.Equal(CFixedPoint.Parse("0.6"), unit.MovementProgress);

		CMovement.SetPath(unit, new[] { Berlin }, clear: true);
		Assert.Equal(CFixedPoint.Zero, unit.MovementProgress);
		Assert.Equal(new[] { Berlin }, unit.Path);
	}

	[Fact]
	public void AnOrderAddedToAPathStartsWhereThatPathEnds()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		var simulation = new CSimulation(state);
		simulation.Post(new CMoveUnitCommand(Ger, unit.Id, Dessau));
		simulation.Post(new CMoveUnitCommand(Ger, unit.Id, Berlin, append: true));
		simulation.Pump();

		Assert.Equal(new[] { Potsdam, Dessau, Potsdam, Berlin }, unit.Path);
	}

	[Fact]
	public void AnOrderToWhereTheUnitStandsOrACancelStopsIt()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		var simulation = new CSimulation(state);

		unit.Path.Add(Potsdam);
		unit.MovementProgress = CFixedPoint.Parse("0.4");
		simulation.Post(new CMoveUnitCommand(Ger, unit.Id, Berlin));
		simulation.Pump();
		Assert.False(unit.IsMoving);
		Assert.Equal(CFixedPoint.Zero, unit.MovementProgress);

		unit.Path.Add(Potsdam);
		unit.MovementProgress = CFixedPoint.Parse("0.4");
		simulation.Post(new CCancelMovementCommand(Ger, unit.Id));
		simulation.Pump();
		Assert.False(unit.IsMoving);
		Assert.Equal(CFixedPoint.Zero, unit.MovementProgress);
	}

	[Fact]
	public void AnOrderIsRefusedForGroundTheUnitMayNotStandOnAndForAFleet()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Potsdam, Infantry());
		var fleet = new CUnit(EUnitKind.Navy, Ger) { Id = ++state.LastUnitId, Location = Berlin };
		fleet.SubUnits.Add(new CSubUnit(Type("destroyer", "30")) { Id = ++state.LastUnitId, Unit = fleet });
		country.Units.Add(fleet);

		Assert.False(new CMoveUnitCommand(Ger, unit.Id, Posen).MayExecute(state));
		Assert.False(new CMoveUnitCommand(Ger, fleet.Id, Potsdam).MayExecute(state));
		Assert.False(new CMoveUnitCommand(Ger, 9999, Berlin).MayExecute(state));
		Assert.True(new CMoveUnitCommand(Ger, unit.Id, Leipzig).MayExecute(state));
	}
}
