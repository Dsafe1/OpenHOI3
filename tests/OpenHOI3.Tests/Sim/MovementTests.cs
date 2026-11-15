using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Sim.Weather;

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
			new[] { germany, poland }, CTestRoads.With(), defines: defines, wars: wars).Paved();
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

	[Fact]
	public void AProvinceKeepsItsUnitsInTheOrderTheyCame()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit first = Division(state, country, Berlin, Infantry());
		CUnit second = Division(state, country, Berlin, Infantry());
		Assert.Equal(new[] { first, second }, state.UnitsIn(Berlin));

		// One made next, which the order of battle would put anywhere, goes on the end of a table made afresh...
		var made = new CUnit(EUnitKind.Division, country.Tag) { Id = ++state.LastUnitId, Location = Berlin, Name = "New" };
		country.Units.Insert(0, made);
		state.ForgetPositions();
		Assert.Equal(new[] { first, second, made }, state.UnitsIn(Berlin));

		// ...and one that leaves and comes back stands behind those who stayed.
		first.Location = Potsdam;
		state.ForgetPositions();
		Assert.Equal(new[] { second, made }, state.UnitsIn(Berlin));
		first.Location = Berlin;
		state.ForgetPositions();
		Assert.Equal(new[] { second, made, first }, state.UnitsIn(Berlin));
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

	// --- the weather ---

	/// <summary>The base game's three figures: mud and cold each take three quarters, below one degree of frost.</summary>
	private static void SetWeatherDefines(CGameState state)
	{
		state.Defines.Set(CWeatherEffects.WeatherSection, CWeatherEffects.MuddynessMovementModifier, CFixedPoint.Parse("-0.75"));
		state.Defines.Set(CWeatherEffects.WeatherSection, CWeatherEffects.ColdMovementModifier, CFixedPoint.Parse("-0.75"));
		state.Defines.Set(CWeatherEffects.WeatherSection, CWeatherEffects.LandLowTemperatureThreshold, CFixedPoint.FromInt(-1));
	}

	[Fact]
	public void MudSlowsByHowMuddyTheGroundIsAndNotAtAllOnceAnyOfItIsFrozen()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		SetWeatherDefines(state);
		CWeather weather = state.Province(Berlin).Weather;

		Assert.Equal(0, CWeatherEffects.Movement(state, weather, country));
		weather.Muddyness = CFixedPoint.Parse("0.06");
		Assert.Equal(-45, CWeatherEffects.Movement(state, weather, country));
		weather.Muddyness = CFixedPoint.One;
		Assert.Equal(-750, CWeatherEffects.Movement(state, weather, country));
		weather.Frozen = CFixedPoint.Parse("0.001");
		Assert.Equal(0, CWeatherEffects.Movement(state, weather, country));
	}

	[Fact]
	public void ColdSlowsBelowTheThresholdAndACountrysWinterEffectsTakeTheirShareOffIt()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		SetWeatherDefines(state);
		CWeather weather = state.Province(Berlin).Weather;

		weather.Temperature = CFixedPoint.FromInt(-1);                 // not below it
		Assert.Equal(0, CWeatherEffects.Movement(state, weather, country));
		weather.Temperature = CFixedPoint.Parse("-1.001");
		Assert.Equal(-750, CWeatherEffects.Movement(state, weather, country));

		// The base game's strategic resource for it gives 0.15.
		country.Modifier.Set(CWeatherEffects.WinterEffects, CFixedPoint.Parse("0.15"));
		Assert.Equal(-637, CWeatherEffects.Movement(state, weather, country));   // -750 x 0.85, cut
	}

	[Fact]
	public void MudAndColdTogetherStopShortOfTakingAllOfAUnitsSpeed()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		SetWeatherDefines(state);
		CWeather weather = state.Province(Berlin).Weather;
		weather.Muddyness = CFixedPoint.One;
		weather.Temperature = CFixedPoint.FromInt(-10);

		Assert.Equal(CWeatherEffects.MovementFloor, CWeatherEffects.Movement(state, weather, country));
	}

	[Fact]
	public void TheWeatherAndTheProvincesOwnFigureAreAddedIntoOneTermOfTheSpeed()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		SetWeatherDefines(state);
		CUnit unit = Division(state, country, Berlin, Infantry());
		unit.Path.Add(Potsdam);

		state.Province(Potsdam).Weather.Muddyness = CFixedPoint.One;   // where it is going: nothing
		Assert.Equal(1000, CMovementSpeed.Modifier(state, country, unit));

		state.Province(Berlin).Weather.Muddyness = CFixedPoint.One;
		Assert.Equal(250, CMovementSpeed.Modifier(state, country, unit));
		Assert.Equal(CFixedPoint.Parse("0.05"), CMovementSpeed.Speed(state, country, unit));   // 4 x 0.25 x 0.05

		// Added, 1000 - 750 - 200, where multiplied would have given 200.
		state.Province(Berlin).Modifier.Set(CMovementSpeed.LocalUnitSpeed, CFixedPoint.Parse("-0.2"));
		Assert.Equal(50, CMovementSpeed.Modifier(state, country, unit));
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
	public void WhatTheHourRunsForACountryAfterItsUnitsComesOnceForEachCountryWithItsUnitsMoved()
	{
		// The original's country hour: its units, then its theatres, before the next country's units (theatres.md, section 1).
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Type("rocket", "50"));
		unit.Path.AddRange(new[] { Potsdam, Dessau });
		var seen = new List<(string Tag, int Where)>();

		CMovement.RunHourly(state, after => seen.Add((after.Tag.Value, unit.Location)));

		Assert.Equal(state.Countries.Select(c => c.Tag.Value), seen.Select(s => s.Tag));
		Assert.Equal(Dessau, seen.Single(s => s.Tag == "GER").Where);
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
	public void ALandUnitMayNotStandInOrPassThroughLandWithOneLevelOfInfrastructure()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		CUnit unit = Division(state, country, Berlin, Infantry());
		Assert.NotNull(Route(state, country, unit, Leipzig));

		// Dessau, the only way to Leipzig, down to one level: in no area, so nobody's land unit stands there.
		state.Province(Dessau).SetBuildingLevel(CTestRoads.Infrastructure, CFixedPoint.One);
		OpenHOI3.Sim.Supply.CSupplyNetworks.Refresh(state, force: true);
		Assert.False(CMovementRules.MayStandIn(state, country, unit, state.Province(Dessau)));
		Assert.Null(Route(state, country, unit, Leipzig));
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
			new[] { country }, CTestRoads.With()).Paved();
		CUnit unit = Division(state, country, start, Infantry());

		Assert.Equal(new[] { a, goal }, Route(state, country, unit, goal));
	}

	[Fact]
	public void ASearchFindsTheSameWayWhateverSearchesTheStateMadeBeforeIt()
	{
		// The working arrays are kept from one search to the next; nothing a search leaves in them may reach the next.
		CCountry country = Germany();
		CGameState state = State(country, potsdamToDessauKind: EProvinceEdgeKind.Impassable);
		CUnit unit = Division(state, country, Berlin, Infantry());

		List<int>? Fresh(int from, int to)
		{
			CGameState alone = State(country, potsdamToDessauKind: EProvinceEdgeKind.Impassable);
			return Route(alone, country, Division(alone, country, from, Infantry()), to);
		}

		Assert.Null(Route(state, country, unit, Leipzig));                     // settles all it can reach and fails
		for (int i = 0; i < 3; i++)
		{
			unit.Location = Leipzig;
			Assert.Equal(Fresh(Leipzig, Dessau), Route(state, country, unit, Dessau));
			unit.Location = Berlin;
			Assert.Equal(Fresh(Berlin, Potsdam), Route(state, country, unit, Potsdam));
			Assert.Null(Route(state, country, unit, Leipzig));
		}
		Assert.Equal(new[] { Dessau }, Fresh(Leipzig, Dessau));
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

	// --- too weak, and out of a neutral ---

	private static CWar GermanyAgainstPoland()
	{
		var war = new CWar("a war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		return war;
	}

	/// <summary>A division at its full strength, which it has had: its strength over its most is the whole.</summary>
	private static CUnit FullDivision(CGameState state, CCountry country, int province)
	{
		CUnit unit = Division(state, country, province, Infantry());
		unit.SubUnits[0].Highest = unit.SubUnits[0].Strength;
		return unit;
	}

	[Fact]
	public void ALandUnitUnderItsShatterThresholdIsNeitherRoutedNorSentOntoGroundAnEnemyHolds()
	{
		CCountry country = Germany();
		CGameState state = State(country, new[] { GermanyAgainstPoland() });
		CUnit unit = FullDivision(state, country, Berlin);
		Assert.Equal(new[] { Potsdam, Posen }, Route(state, country, unit, Posen));

		// A half is the threshold for a country with all its officers, no leader and no experience; under it, not onto
		// Poland's ground - whether the goal or a step - while its own ground is open to it as before.
		unit.SubUnits[0].Strength = CFixedPoint.Parse("49.9");
		Assert.True(CMovementRules.IsTooWeak(state, unit));
		Assert.Null(Route(state, country, unit, Posen));
		Assert.False(CMovementRules.MayMoveTo(state, country, unit, state.Province(Posen)));
		Assert.Equal(new[] { Potsdam, Dessau, Leipzig }, Route(state, country, unit, Leipzig));

		unit.SubUnits[0].Strength = CFixedPoint.Parse("50");
		Assert.False(CMovementRules.IsTooWeak(state, unit));
		Assert.NotNull(Route(state, country, unit, Posen));

		// A unit that has never had any strength is too weak.
		unit.SubUnits[0].Highest = CFixedPoint.Zero;
		unit.SubUnits[0].Strength = CFixedPoint.Zero;
		Assert.True(CMovementRules.IsTooWeak(state, unit));
	}

	[Fact]
	public void AnAirUnitOrAFleetIsNotHeldBackByTheLandUnitsTestsOfAStep()
	{
		CCountry country = Germany();
		CGameState state = State(country, new[] { GermanyAgainstPoland() });
		var air = new CUnit(EUnitKind.Air, Ger) { Id = ++state.LastUnitId, Location = Potsdam };
		air.SubUnits.Add(new CSubUnit(Type("interceptor", "300", fuel: "1")) { Id = ++state.LastUnitId, Unit = air });
		country.Units.Add(air);
		air.FuelReceived = CFixedPoint.Zero;
		CProvince posen = state.Province(Posen);
		CProvinceEdge edge = state.Province(Potsdam).EdgeTo(Posen)!.Value;

		Assert.True(CMovementRules.IsTooWeak(state, air));                     // it has never had any strength
		Assert.True(CMovementRules.MayStep(state, country, air, state.Province(Potsdam), posen, edge));
	}

	/// <summary>
	/// The strip with Potsdam held by a third country, the Netherlands, which Germany may have military access through:
	/// Berlin - Potsdam (Dutch) - Posen (Polish), and Dessau and Leipzig beyond Potsdam.
	/// </summary>
	private static (CGameState State, CCountry Germany, CCountry Holland) ThroughANeutral(bool hollandAtWar)
	{
		CCountry germany = Germany();
		var holland = new CCountry { Tag = new CCountryTag("HOL"), Name = "Holland", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		CWar war = GermanyAgainstPoland();
		if (hollandAtWar)
		{
			war.Attackers.Add(holland.Tag);
		}
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CMovementSpeed.LandSpeedModifier, CFixedPoint.Parse("0.05"));
		var state = new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				Land(Berlin, "Berlin", Ger, Plains, Edge(Potsdam, "1")),
				Land(Potsdam, "Potsdam", holland.Tag, Plains, Edge(Berlin, "2"), Edge(Dessau, "1"), Edge(Posen, "1")),
				Land(Dessau, "Dessau", Ger, Forest, Edge(Potsdam, "1"), Edge(Leipzig, "1")),
				Land(Leipzig, "Leipzig", Ger, Plains, Edge(Dessau, "1")),
				Land(Posen, "Posen", Pol, Plains, Edge(Potsdam, "1")),
			},
			new[] { germany, holland, poland }, CTestRoads.With(), defines: defines, wars: new[] { war }).Paved();
		CGameState.Status(germany, holland).HasMilitaryAccess = true;
		return (state, germany, holland);
	}

	[Fact]
	public void ACountryDoesNotAttackOutOfANeutralThatOnlyLetsItThrough()
	{
		(CGameState state, CCountry germany, CCountry holland) = ThroughANeutral(hollandAtWar: false);
		CUnit unit = FullDivision(state, germany, Berlin);

		// Through the Dutch province to German ground, and onto it, as before; into Poland out of it, not.
		Assert.Equal(new[] { Potsdam, Dessau }, Route(state, germany, unit, Dessau));
		Assert.Equal(new[] { Potsdam }, Route(state, germany, unit, Potsdam));
		Assert.Null(Route(state, germany, unit, Posen));
		Assert.False(CMovementRules.MayLandUnitStep(state, unit, state.Province(Potsdam), state.Province(Posen)));

		// A unit already standing there is refused the step as well.
		unit.Location = Potsdam;
		Assert.Null(Route(state, germany, unit, Posen));
	}

	[Fact]
	public void OutOfAProvinceHeldByAnAllyAtWarWithTheEnemyOrWithoutAccessTheStepIsOpen()
	{
		(CGameState atWar, CCountry germany, _) = ThroughANeutral(hollandAtWar: true);
		CUnit unit = FullDivision(atWar, germany, Berlin);
		Assert.Equal(new[] { Potsdam, Posen }, Route(atWar, germany, unit, Posen));

		// Without access the rule does not apply - here it is a faction that lets Germany in.
		(CGameState faction, CCountry germany2, CCountry holland) = ThroughANeutral(hollandAtWar: false);
		CGameState.Status(germany2, holland).HasMilitaryAccess = false;
		germany2.Faction = "axis";
		holland.Faction = "axis";
		CUnit other = FullDivision(faction, germany2, Berlin);
		Assert.Equal(new[] { Potsdam, Posen }, Route(faction, germany2, other, Posen));
	}

	// --- the message that a unit has arrived ---

	[Fact]
	public void APlayedCountrysUnitIsToldItHasArrivedAtTheEndOfItsWayAndNotBefore()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		state.Played.Add(Ger);
		var told = new List<(int Unit, int Where)>();
		state.UnitArrived += arrived => told.Add((arrived.Id, arrived.Location));
		CUnit unit = Division(state, country, Berlin, Type("fast", "6"));
		unit.Path.AddRange(new[] { Potsdam, Dessau });
		unit.MovementProgress = CFixedPoint.Parse("0.9");

		CMovement.RunHourly(state);                                            // into Potsdam, Dessau still ahead
		Assert.Equal(Potsdam, unit.Location);
		Assert.Empty(told);

		for (int hour = 0; hour < 3 && unit.IsMoving; hour++)
		{
			CMovement.RunHourly(state);
		}
		Assert.Equal(Dessau, unit.Location);
		Assert.Equal(new[] { (unit.Id, Dessau) }, told);
	}

	[Fact]
	public void AnUnplayedCountrysUnitIsToldNothing()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		int told = 0;
		state.UnitArrived += _ => told++;
		CUnit unit = Division(state, country, Berlin, Type("fast", "6"));
		unit.Path.Add(Potsdam);
		unit.MovementProgress = CFixedPoint.Parse("0.9");

		CMovement.RunHourly(state);
		Assert.Equal(Potsdam, unit.Location);
		Assert.Equal(0, told);
	}

	[Fact]
	public void AnAirUnitIsToldOnlyAtItsBaseAndAFleetOnPatrolOrAtConvoysNever()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		state.Played.Add(Ger);
		var air = new CUnit(EUnitKind.Air, Ger) { Location = Potsdam, Base = Berlin };
		var fleet = new CUnit(EUnitKind.Navy, Ger) { Location = Potsdam };
		var division = new CUnit(EUnitKind.Division, Ger) { Location = Potsdam };

		Assert.False(CArrival.IsTold(state, air));
		air.Location = Berlin;
		Assert.True(CArrival.IsTold(state, air));
		Assert.True(CArrival.IsTold(state, fleet));
		foreach (EUnitOrder order in new[] { EUnitOrder.Patrol, EUnitOrder.ConvoyEscort, EUnitOrder.ConvoyRaid })
		{
			fleet.Order = order;
			Assert.False(CArrival.IsTold(state, fleet));
		}
		fleet.Order = EUnitOrder.Sortie;
		Assert.True(CArrival.IsTold(state, fleet));
		division.Order = EUnitOrder.StrategicRedeployment;
		Assert.True(CArrival.IsTold(state, division));

		Assert.Equal(CArrival.Land, CArrival.TypeOf(division));
		Assert.Equal(CArrival.Naval, CArrival.TypeOf(fleet));
		Assert.Equal(CArrival.Air, CArrival.TypeOf(air));
	}

	[Fact]
	public void AUnitHaltedOnArrivalWhereItMayNoLongerStandIsToldItHasArrivedWhereItWas()
	{
		CCountry country = Germany();
		CGameState state = State(country);
		state.Played.Add(Ger);
		var told = new List<int>();
		state.UnitArrived += arrived => told.Add(arrived.Location);
		CUnit unit = Division(state, country, Potsdam, Type("fast", "6"));
		unit.Path.Add(Dessau);
		unit.MovementProgress = CFixedPoint.Parse("0.9");
		state.Province(Dessau).Owner = Pol;                                     // a neutral's now: it may not stand there
		state.Province(Dessau).Controller = Pol;

		CMovement.RunHourly(state);
		Assert.Equal(Potsdam, unit.Location);
		Assert.False(unit.IsMoving);
		Assert.Equal(new[] { Potsdam }, told);
	}

	// --- the click's test of a unit in a battle (section 1) ---

	/// <summary>A division of three points of strength, which it has had, and some organisation.</summary>
	private static CUnit Fighter(CGameState state, CCountry country, int province, string organisation = "30")
	{
		CUnit unit = Division(state, country, province, Infantry());
		unit.SubUnits[0].Strength = CFixedPoint.FromInt(3);
		unit.SubUnits[0].Highest = CFixedPoint.FromInt(3);
		unit.SubUnits[0].Organisation = CFixedPoint.Parse(organisation);
		state.ForgetPositions();
		return unit;
	}

	[Fact]
	public void AUnitInABattleIsSentOnlyWhereItMayFallBack()
	{
		CCountry germany = Germany();
		CGameState state = State(germany, new[] { GermanyAgainstPoland() });
		CCountry poland = state.Country(Pol)!;
		CUnit unit = Fighter(state, germany, Potsdam);
		unit.Combats.Add(new CCombat(Potsdam));

		// Only where it stands or next door: Leipzig is two provinces off. The command asks none of it.
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Dessau)));
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Berlin)));
		Assert.False(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Leipzig)));
		Assert.True(CMovementRules.MayCommandMoveTo(state, germany, unit, state.Province(Leipzig)));

		// Into the enemy's ground beside it while it has an organisation of one and no enemy stands there.
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Posen)));
		unit.SubUnits[0].Organisation = CFixedPoint.Parse("0.999");
		Assert.False(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Posen)));
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Dessau)));
		unit.SubUnits[0].Organisation = CFixedPoint.One;

		// An enemy standing there with a whole point of strength between its units shuts it; under one point does not.
		CUnit polish = Fighter(state, poland, Posen);
		polish.SubUnits[0].Strength = CFixedPoint.Parse("0.999");
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Posen)));
		Assert.Equal(0, CCombatRetreat.EnemyStrengthIn(state, Ger, Posen, air: false));
		polish.SubUnits[0].Strength = CFixedPoint.One;
		Assert.Equal(1, CCombatRetreat.EnemyStrengthIn(state, Ger, Posen, air: false));
		Assert.False(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Posen)));

		// Out of the battle, none of it is asked.
		unit.Combats.Clear();
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Leipzig)));
	}

	[Fact]
	public void NoUnitIsSentWhileItHasAWayAndABattleInTheNextProvinceOfIt()
	{
		CCountry germany = Germany();
		CGameState state = State(germany, new[] { GermanyAgainstPoland() });
		CUnit unit = Fighter(state, germany, Potsdam);
		unit.Path.Add(Posen);
		unit.Combats.Add(new CCombat(Posen));

		Assert.False(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Dessau)));
		Assert.False(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Potsdam)));
		Assert.True(CMovementRules.MayCommandMoveTo(state, germany, unit, state.Province(Dessau)));

		// A battle where it stands, its way going elsewhere, does not: it is sent where it may fall back.
		unit.Combats.Clear();
		unit.Combats.Add(new CCombat(Potsdam));
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Dessau)));
		// Nor does the battle ahead with no way: it is fought from where it stands.
		unit.Combats.Clear();
		unit.Combats.Add(new CCombat(Posen));
		unit.Path.Clear();
		Assert.True(CMovementRules.MayMoveTo(state, germany, unit, state.Province(Dessau)));
	}

	[Fact]
	public void AnAirUnitInABattleIsSentAnywhereButWhereEnemyAircraftAre()
	{
		CCountry germany = Germany();
		CGameState state = State(germany, new[] { GermanyAgainstPoland() });
		CCountry poland = state.Country(Pol)!;
		var air = new CUnit(EUnitKind.Air, Ger) { Id = ++state.LastUnitId, Location = Potsdam };
		air.SubUnits.Add(new CSubUnit(Type("interceptor", "300")) { Id = ++state.LastUnitId, Unit = air });
		germany.Units.Add(air);
		air.Combats.Add(new CCombat(Potsdam, ECombatKind.Air));

		// No edge is asked of an air unit: Leipzig is two provinces off.
		Assert.True(CMovementRules.MayMoveTo(state, germany, air, state.Province(Leipzig)));
		Assert.True(CMovementRules.MayMoveTo(state, germany, air, state.Province(Posen)));

		var polish = new CUnit(EUnitKind.Air, Pol) { Id = ++state.LastUnitId, Location = Posen };
		polish.SubUnits.Add(new CSubUnit(Type("interceptor", "300")) { Id = ++state.LastUnitId, Unit = polish, Strength = CFixedPoint.FromInt(100) });
		poland.Units.Add(polish);
		state.ForgetPositions();
		Assert.False(CMovementRules.MayMoveTo(state, germany, air, state.Province(Posen)));
		Assert.True(CMovementRules.MayMoveTo(state, germany, air, state.Province(Leipzig)));
	}
}
