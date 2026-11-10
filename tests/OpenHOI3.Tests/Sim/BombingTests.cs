using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The bombing missions, the bombing itself and the daily repair against
/// <c>docs/mechanics/bombing.md</c>, on a strip of ground:
///
///     1 Berlin --- 2 Border --- 3 Field --- 4 Warsaw --- 5 East
///     (Germany's, an airfield)     |        (Poland's: factories, guns, a port, an airfield)
///                               6 Bay
///
/// Germany holds the first two and Poland the rest; the bay is sea, and Warsaw's port
/// lies on it. Each is forty from the next as a range measures it, and a wing covers one
/// province an hour.
/// </summary>
public class BombingTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Border = 2;
	private const int Field = 3;
	private const int Warsaw = 4;
	private const int East = 5;
	private const int Bay = 6;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, CBombingOrders.NavalBaseLevel, CFixedPoint.One);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, CBombingOrders.AirBaseLevel, CFixedPoint.One);
	private static readonly CBuilding Guns = new(2, "anti_air", 10, CBombingOrders.AntiAir, CFixedPoint.One)
	{
		DamageFactor = CFixedPoint.Parse("0.25"),
	};
	private static readonly CBuilding Factory = new(3, "industry", 10, CBombingOrders.Industry, CFixedPoint.One);
	private static readonly CBuilding Roads = new(4, "infra", 10, CBombingOrders.Infrastructure, CFixedPoint.Parse("0.1"))
	{
		DamageFactor = CFixedPoint.Parse("0.25"),
	};
	private static readonly CBuilding Radar = new(5, "radar_station", 10, CBombingOrders.RadarLevel, CFixedPoint.One);
	private static readonly CBuilding Bunker = new(6, "underground", 1, "", CFixedPoint.Zero) { IsRepaired = false };

	private static CProvinceEdge Edge(int to)
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));
	}

	private static CProvince Land(int id, string name, CCountryTag owner, int x, params CProvinceEdge[] edges)
	{
		return new CProvince(id, name, isSea: false)
		{
			Owner = owner, Controller = owner, CentreX = x, CentreY = 10, Edges = edges,
			PortSea = id == Warsaw ? Bay : 0, SeaIndex = id == Warsaw ? 1 : -1,
		};
	}

	private static CSubUnitType Wing(string key, string strategic = "0", string soft = "0", string hard = "0",
		string sea = "0", string air = "0", bool bomber = true)
	{
		var type = new CSubUnitType(key, EUnitBranch.Air);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "20");
		Set(CUnitOrders.Range, "1000");
		Set(CSubUnitType.MaxStrength, "100");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CBombingOrders.StrategicAttack, strategic);
		Set(CUnitTotals.SoftAttack, soft);
		Set(CUnitTotals.HardAttack, hard);
		Set(CNavalFire.SeaAttack, sea);
		Set(CAirMissions.AirAttack, air);
		Set(CAirFire.Priority, "3");
		type.Flags[CAirModifiers.IsBomber] = bomber;
		return type;
	}

	private static readonly CSubUnitType Heavy = Wing("strategic_bomber", strategic: "7", soft: "1", air: "2");
	private static readonly CSubUnitType Tactical = Wing("tactical_bomber", strategic: "1.5", soft: "6", hard: "2", sea: "3", air: "1");
	private static readonly CSubUnitType Torpedo = Wing("naval_bomber", sea: "7", air: "1");
	private static readonly CSubUnitType Fighter = Wing("interceptor", air: "4", bomber: false);

	private static readonly CSubUnitType Infantry = Brigade("infantry_brigade", softness: "1", airAttack: "2");
	private static readonly CSubUnitType Armour = Brigade("armor_brigade", softness: "0.25", airAttack: "0");
	private static readonly CSubUnitType Cruiser = Ship("light_cruiser", airAttack: "3", hull: "1");

	private static CSubUnitType Brigade(string key, string softness, string airAttack)
	{
		var type = new CSubUnitType(key, EUnitBranch.Land);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "4");
		Set(CSubUnitType.MaxStrength, "30");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CUnitTotals.Softness, softness);
		Set(CAirMissions.AirAttack, airAttack);
		return type;
	}

	private static CSubUnitType Ship(string key, string airAttack, string hull)
	{
		var type = new CSubUnitType(key, EUnitBranch.Naval);
		void Set(string name, string value) => type.Stats.Values.Set(name, CFixedPoint.Parse(value));
		Set(CMovementSpeed.MaximumSpeed, "20");
		Set(CUnitOrders.Range, "2000");
		Set(CSubUnitType.MaxStrength, "100");
		Set(CSubUnitType.DefaultOrganisation, "30");
		Set(CAirMissions.AirAttack, airAttack);
		Set(CNavalBattle.Hull, hull);
		return type;
	}

	private static CGameState State(out CCountry germany, out CCountry poland, bool atWar = true)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var defines = new CDefines();
		void Military(string name, string value) => defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
		Military(CMovementSpeed.AirSpeedModifier, "0.5");
		Military("AGGRESSIVE_STRENGTH_LIMIT", "0.25");
		Military("DEFENSIVE_STRENGTH_LIMIT", "0.5");
		Military("PASSIVE_STRENGTH_LIMIT", "0.75");
		Military("AGGRESSIVE_ORGANISATION_LIMIT", "0.2");
		Military("DEFENSIVE_ORGANISATION_LIMIT", "0.3");
		Military("PASSIVE_ORGANISATION_LIMIT", "0.5");
		Military(CAirFire.AvoidChance, "48");
		Military(CAirModifiers.StackingPenalty, "-0.1");
		Military(CAirModifiers.MostStackingPenalty, "-0.8");
		Military(CBomberModifiers.CagStackingPenalty, "0.5");
		Military(CBomberModifiers.SurpriseChance, "25");
		Military(CBomberModifiers.SurpriseBonus, "0.75");
		Military(CBomberModifiers.SurpriseRounds, "3");
		Military(CBomberModifiers.AirCombatOnBombing, "-0.1");
		Military(CCombatModifiers.NightPenalty, "-0.5");
		Military(CBomberFire.RegimentDamage, "1.85");
		Military(CBomberFire.ShipDamage, "1.05");
		Military(CBomberFire.WingDamage, "1.05");
		Military(CBomberFire.CagStrengthBonus, "2");
		Military(CBomberFire.CagOrganisationBonus, "2");
		Military(CProvinceBombing.ResourcesInfraFraction, "0.075");
		Military(CProvinceBombing.Resources, "0.9");
		Military(CProvinceBombing.Production, "0.05");
		Military(CProvinceBombing.Buildings, "0.05");
		Military(CAntiAircraft.StaticScale, "5");
		defines.Set(CProvinceRepair.EconomySection, CProvinceRepair.RepairSpeed, CFixedPoint.Parse("0.1"));
		var wars = new List<CWar>();
		if (atWar)
		{
			var war = new CWar("war");
			war.Attackers.Add(Ger);
			war.Defenders.Add(Pol);
			wars.Add(war);
		}
		CProvince berlin = Land(Berlin, "Berlin", Ger, 10, Edge(Border));
		berlin.SetBuildingLevel(AirBase, CFixedPoint.FromInt(4));
		CProvince warsaw = Land(Warsaw, "Warsaw", Pol, 40, Edge(Field), Edge(East));
		warsaw.SetBuildingLevel(AirBase, CFixedPoint.FromInt(4));
		warsaw.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(4));
		warsaw.SetBuildingLevel(Guns, CFixedPoint.FromInt(2));
		warsaw.SetBuildingLevel(Factory, CFixedPoint.FromInt(8));
		warsaw.SetBuildingLevel(Roads, CFixedPoint.FromInt(10));
		warsaw.SetBuildingLevel(Bunker, CFixedPoint.One);
		warsaw.MaxProducing[EGoods.Metal] = CFixedPoint.FromInt(9);
		warsaw.CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(9);
		warsaw.Pool[EGoods.Supplies] = CFixedPoint.FromInt(100);
		warsaw.Pool[EGoods.Fuel] = CFixedPoint.FromInt(50);
		CProvince east = Land(East, "East", Pol, 50, Edge(Warsaw));
		east.SetBuildingLevel(Factory, CFixedPoint.FromInt(2));
		east.SetBuildingLevel(Radar, CFixedPoint.FromInt(3));
		var bay = new CProvince(Bay, "Bay", isSea: true) { CentreX = 30, CentreY = 20, SeaIndex = 0, Edges = new[] { Edge(Field) } };
		var state = new CGameState(new CDate(1936, 6, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				berlin,
				Land(Border, "Border", Ger, 20, Edge(Berlin), Edge(Field)),
				Land(Field, "Field", Pol, 30, Edge(Border), Edge(Warsaw), Edge(Bay)),
				warsaw,
				east,
				bay,
			},
			new[] { germany, poland },
			buildings: new[] { NavalBase, AirBase, Guns, Factory, Roads, Radar, Bunker }, defines: defines, wars: wars)
		{
			MapWidth = 1000,
			MapHeight = 1000,
		};
		CProvinceModifiers.Rebuild(state);
		return state;
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, params CSubUnitType[] types)
	{
		var unit = new CUnit(kind, country.Tag)
		{
			Id = ++state.LastUnitId, Location = province, Name = country.Tag + " unit " + state.LastUnitId,
			Base = kind == EUnitKind.Air ? (country.Tag == Ger ? Berlin : Warsaw) : kind == EUnitKind.Navy ? Warsaw : 0,
		};
		foreach (CSubUnitType type in types)
		{
			var subUnit = new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Strength = type.Stats.Values[CSubUnitType.MaxStrength],
				Organisation = CFixedPoint.FromInt(30), Experience = CFixedPoint.Zero, Name = type.Key,
			};
			subUnit.Stats.CopyFrom(type.Stats);
			subUnit.Highest = subUnit.Strength;
			unit.SubUnits.Add(subUnit);
		}
		country.Units.Add(unit);
		state.ForgetPositions();
		return unit;
	}

	// ---- the nuclear bomb (nukes.md)

	private static CGameState NukeWorld(out CCountry germany, out CCountry poland, bool atWar = true)
	{
		CGameState state = State(out germany, out poland, atWar);
		state.Defines.Set(OpenHOI3.Sim.Research.CResearch.CountrySection, "NUKE_UNITY_IMPACT", CFixedPoint.One);
		return state;
	}

	[Fact]
	public void A_bomb_comes_out_of_the_stock_and_goes_back_with_the_order()
	{
		CGameState state = NukeWorld(out CCountry germany, out _);
		germany.Nukes = CFixedPoint.FromInt(2);
		CUnit bomber = Air(state, germany, Warsaw, Heavy);
		CUnitOrders.Give(state, germany, bomber, EUnitOrder.Nuke, Warsaw);
		Assert.Equal(1000, germany.Nukes.Raw);
		Assert.Equal(1, bomber.Mission.Nukes);
		CUnitOrders.Clear(bomber);
		Assert.Equal(2000, germany.Nukes.Raw);
	}

	[Fact]
	public void With_no_bomb_in_the_stock_the_order_ends_with_nothing_lost()
	{
		CGameState state = NukeWorld(out CCountry germany, out _);
		CUnit bomber = Air(state, germany, Warsaw, Heavy);
		CUnitOrders.Give(state, germany, bomber, EUnitOrder.Nuke, Warsaw);
		Assert.False(CNukeMission.RunHour(state, germany, bomber));
		Assert.Equal(0, germany.Nukes.Raw);
	}

	[Fact]
	public void The_bomb_lays_the_buildings_flat_and_empties_the_goods_and_is_spent()
	{
		CGameState state = NukeWorld(out CCountry germany, out CCountry poland);
		germany.Nukes = CFixedPoint.One;
		poland.NationalUnity = CFixedPoint.FromInt(50);
		CUnit bomber = Air(state, germany, Warsaw, Heavy);
		CUnitOrders.Give(state, germany, bomber, EUnitOrder.Nuke, Warsaw);
		bomber.Path.Clear();
		Assert.False(CNukeMission.RunHour(state, germany, bomber));
		CProvince warsaw = state.Province(Warsaw);
		Assert.Equal(CFixedPoint.Zero, warsaw.BuildingLevel(Factory));
		Assert.Equal(CFixedPoint.FromInt(8), warsaw.BuildingMaxLevel(Factory));
		Assert.Equal(CFixedPoint.Zero, warsaw.Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Zero, warsaw.CurrentProducing[EGoods.Metal]);
		Assert.Equal(49000, poland.NationalUnity.Raw);
		CUnitOrders.Clear(bomber);
		Assert.Equal(0, germany.Nukes.Raw);
	}

	[Fact]
	public void A_core_adds_its_industry_and_the_acting_capital_doubles_it()
	{
		CGameState state = NukeWorld(out _, out CCountry poland);
		CProvince warsaw = state.Province(Warsaw);
		long industry = warsaw.Modifier[CIndustrialCapacity.ProvinceIc].Raw;
		Assert.Equal(1000, CNukeMission.UnityLost(state, poland, warsaw));
		warsaw.Cores.Add(Pol);
		Assert.Equal(1000 + industry, CNukeMission.UnityLost(state, poland, warsaw));
		poland.ActingCapital = Warsaw;
		Assert.Equal((1000 + industry) * 2, CNukeMission.UnityLost(state, poland, warsaw));
	}

	[Fact]
	public void A_brigade_loses_about_a_hundredth_of_its_strength_and_all_its_organisation()
	{
		CGameState state = NukeWorld(out CCountry germany, out CCountry poland);
		germany.Nukes = CFixedPoint.One;
		CUnit division = Unit(state, poland, EUnitKind.Division, Warsaw, Infantry);
		long before = division.SubUnits[0].Strength.Raw;
		CUnit bomber = Air(state, germany, Warsaw, Heavy);
		CUnitOrders.Give(state, germany, bomber, EUnitOrder.Nuke, Warsaw);
		bomber.Path.Clear();
		CNukeMission.RunHour(state, germany, bomber);
		long lost = before - division.SubUnits[0].Strength.Raw;
		Assert.InRange(lost, before * 6 / 1000, before * 11 / 1000);
		Assert.Equal(CFixedPoint.Zero, division.SubUnits[0].Organisation);
	}

	[Fact]
	public void The_order_ends_where_the_target_is_no_enemys_and_the_bomb_comes_back()
	{
		CGameState state = NukeWorld(out CCountry germany, out _, atWar: false);
		CUnit bomber = Air(state, germany, Berlin, Heavy);
		bomber.Order = EUnitOrder.Nuke;
		bomber.OrderProvince = Warsaw;
		bomber.Mission.Nukes = 1;
		bomber.Mission.BombsFrom = germany;
		Assert.False(CNukeMission.RunHour(state, germany, bomber));
		CUnitOrders.Clear(bomber);
		Assert.Equal(1000, germany.Nukes.Raw);
	}

	[Fact]
	public void Technology_adds_to_the_stock_by_the_month()
	{
		CGameState state = NukeWorld(out CCountry germany, out _);
		germany.Technology.Set(CNukeMission.Production, CFixedPoint.Parse("0.1"));
		CNukeMission.RunMonthly(state);
		Assert.Equal(100, germany.Nukes.Raw);
	}

	[Fact]
	public void The_row_wants_a_bomb_for_each_unit_selected_and_is_ticked_even_where_it_cannot_be_given()
	{
		CGameState state = NukeWorld(out CCountry germany, out _);
		CUnit bomber = Air(state, germany, Berlin, Heavy);
		CProvince warsaw = state.Province(Warsaw);
		Assert.DoesNotContain(OpenHOI3.Interface.Screens.COrdersText.Rows(state, germany, bomber, warsaw), row => row.Order == EUnitOrder.Nuke);
		germany.Nukes = CFixedPoint.One;
		var rows = OpenHOI3.Interface.Screens.COrdersText.Rows(state, germany, bomber, warsaw);
		Assert.Contains(rows, row => row.Order == EUnitOrder.Nuke);
		Assert.DoesNotContain(OpenHOI3.Interface.Screens.COrdersText.Rows(state, germany, bomber, warsaw, selected: 2), row => row.Order == EUnitOrder.Nuke);
		int ticked = OpenHOI3.Interface.Screens.CAirOrdersText.Suggested(rows, state, germany, bomber, warsaw, EUnitOrder.None);
		Assert.Equal(EUnitOrder.Nuke, rows[ticked].Order);
	}

	private static CUnit Air(CGameState state, CCountry country, int province, params CSubUnitType[] wings)
	{
		return Unit(state, country, EUnitKind.Air, province, wings);
	}

	private static void Give(CGameState state, CCountry country, CUnit unit, EUnitOrder order, int province,
		params int[] provinces)
	{
		CUnitOrders.Give(state, country, unit, order, province, CMission.Aggressive,
			provinces: provinces.Length == 0 ? null : provinces);
	}

	private static void RunHours(CGameState state, int hours)
	{
		for (int hour = 0; hour < hours; hour++)
		{
			CTick.RunHour(state);
		}
	}

	// --- which mission may be given, and what tells them apart ---

	[Fact]
	public void EachMissionMayBeGivenOnlyForWhatItBombs()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit heavies = Air(state, germany, Berlin, Heavy);
		CUnit torpedoes = Air(state, germany, Berlin, Torpedo);

		Assert.True(CUnitOrders.MayGive(state, germany, heavies, EUnitOrder.GroundAttack, state.Province(Warsaw)));
		Assert.False(CUnitOrders.MayGive(state, germany, heavies, EUnitOrder.GroundAttack, state.Province(Border)));   // its own
		Assert.False(CUnitOrders.MayGive(state, germany, heavies, EUnitOrder.Interdiction, state.Province(Bay)));
		Assert.True(CUnitOrders.MayGive(state, germany, heavies, EUnitOrder.StrategicBombing, state.Province(Warsaw)));
		Assert.False(CUnitOrders.MayGive(state, germany, torpedoes, EUnitOrder.StrategicBombing, state.Province(Warsaw)));  // no strategic attack
		Assert.False(CUnitOrders.MayGive(state, germany, torpedoes, EUnitOrder.RunwayCratering, state.Province(Warsaw)));
		Assert.True(CUnitOrders.MayGive(state, germany, torpedoes, EUnitOrder.NavalStrike, state.Province(Bay)));
		Assert.False(CUnitOrders.MayGive(state, germany, torpedoes, EUnitOrder.NavalStrike, state.Province(Warsaw)));
		Assert.True(CUnitOrders.MayGive(state, germany, torpedoes, EUnitOrder.PortStrike, state.Province(Warsaw)));
		Assert.False(CUnitOrders.MayGive(state, germany, torpedoes, EUnitOrder.PortStrike, state.Province(Bay)));
		Assert.True(new CSetUnitOrderCommand(Ger, heavies.Id, EUnitOrder.LogisticalStrike, Warsaw).MayExecute(state));
	}

	[Fact]
	public void EveryMissionThatBombsBuildingsBombsTheGunsToo()
	{
		Assert.True(CBombingOrders.BombsBuilding(EUnitOrder.StrategicBombing, Factory));
		Assert.True(CBombingOrders.BombsBuilding(EUnitOrder.StrategicBombing, Guns));
		Assert.False(CBombingOrders.BombsBuilding(EUnitOrder.StrategicBombing, Roads));
		Assert.True(CBombingOrders.BombsBuilding(EUnitOrder.LogisticalStrike, Roads));
		Assert.True(CBombingOrders.BombsBuilding(EUnitOrder.RunwayCratering, AirBase));
		Assert.True(CBombingOrders.BombsBuilding(EUnitOrder.InstallationStrike, Radar));
		Assert.False(CBombingOrders.BombsBuilding(EUnitOrder.InstallationStrike, Factory));
		Assert.True(CBombingOrders.BombsBuilding(EUnitOrder.PortStrike, NavalBase));
		Assert.True(CBombingOrders.BombsBuilding(EUnitOrder.PortStrike, Guns));
		Assert.False(CBombingOrders.BombsBuilding(EUnitOrder.GroundAttack, Guns));
		Assert.False(CBombingOrders.BombsBuilding(EUnitOrder.NavalStrike, NavalBase));

		Assert.True(CBombingOrders.BombsStockpile(EUnitOrder.LogisticalStrike, EGoods.Supplies));
		Assert.True(CBombingOrders.BombsStockpile(EUnitOrder.LogisticalStrike, EGoods.Fuel));
		Assert.False(CBombingOrders.BombsStockpile(EUnitOrder.LogisticalStrike, EGoods.Metal));
		Assert.False(CBombingOrders.BombsStockpile(EUnitOrder.StrategicBombing, EGoods.Supplies));
		Assert.True(CBombingOrders.BombsProduction(EUnitOrder.StrategicBombing, EGoods.Metal));
		Assert.True(CBombingOrders.BombsProduction(EUnitOrder.StrategicBombing, EGoods.CrudeOil));
		Assert.False(CBombingOrders.BombsProduction(EUnitOrder.StrategicBombing, EGoods.Money));
		Assert.False(CBombingOrders.BombsProduction(EUnitOrder.LogisticalStrike, EGoods.Metal));
	}

	[Fact]
	public void ABombingMissionsListIsNotCutToItsRange()
	{
		CGameState state = State(out CCountry germany, out _);
		var shortLegged = Wing("short", strategic: "5");
		shortLegged.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.FromInt(50));
		CUnit heavies = Air(state, germany, Berlin, shortLegged);

		Give(state, germany, heavies, EUnitOrder.StrategicBombing, Warsaw, Warsaw, East, Border, Bay);

		// Warsaw is a hundred and twenty away. Its own ground is not listed; water is.
		Assert.Equal(new[] { Warsaw, East, Bay }, heavies.Mission.Provinces);
	}

	// --- the sortie ---

	[Fact]
	public void AStrategicBombingFliesOutBombsForFourHoursAndComesHome()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit heavies = Air(state, germany, Berlin, Heavy);
		CProvince warsaw = state.Province(Warsaw);
		Give(state, germany, heavies, EUnitOrder.StrategicBombing, Warsaw);
		Assert.Equal(Warsaw, heavies.Mission.Destination);

		// Three provinces at one an hour, the first set off on the hour after the order.
		RunHours(state, 4);
		Assert.Equal(Warsaw, heavies.Location);
		CCombat bombing = Assert.IsType<CCombat>(heavies.Bombing);
		Assert.Equal(ECombatKind.GroundBombing, bombing.Kind);
		Assert.Empty(bombing.Defender.Units);
		Assert.Contains(Pol, bombing.Defender.Countries);
		Assert.False(heavies.IsFighting);

		RunHours(state, 3);
		Assert.Null(heavies.Bombing);
		Assert.Null(CBombing.In(state, Warsaw));
		Assert.True(warsaw.BuildingLevel(Factory) < CFixedPoint.FromInt(8));
		Assert.True(warsaw.BuildingLevel(Guns) < CFixedPoint.FromInt(2));
		Assert.Equal(CFixedPoint.FromInt(8), warsaw.BuildingMaxLevel(Factory));
		Assert.Equal(CFixedPoint.FromInt(10), warsaw.BuildingLevel(Roads));
		Assert.True(warsaw.CurrentProducing[EGoods.Metal] < CFixedPoint.FromInt(9));
		Assert.Equal(CFixedPoint.FromInt(100), warsaw.Pool[EGoods.Supplies]);

		// The hour after, it is told to return, and its province is forgotten.
		RunHours(state, 1);
		Assert.True(heavies.Mission.ReturnToBase);
		Assert.Equal(0, heavies.Mission.Destination);
		RunHours(state, 3);
		Assert.Equal(Berlin, heavies.Location);
		// Home, it is no longer returning, picks its province afresh and flies again.
		RunHours(state, 2);
		Assert.Equal(EUnitOrder.StrategicBombing, heavies.Order);
		Assert.Equal(Warsaw, heavies.Mission.Destination);
		Assert.True(heavies.IsMoving);
	}

	[Fact]
	public void AnAirUnitInABombingStaysOverItAndItsWaitEndsWithItsFirstMove()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit heavies = Air(state, germany, Warsaw, Heavy);
		heavies.Order = EUnitOrder.StrategicBombing;
		heavies.Mission.Destination = Warsaw;
		CCombat? bombing = CBombing.Start(state, state.Province(Warsaw), heavies);
		Assert.NotNull(bombing);

		CMovement.SetPath(heavies, new[] { Field }, clear: true);
		Assert.False(CMovement.Advance(state, germany, heavies, addSpeed: true));
		Assert.Equal(Warsaw, heavies.Location);
		Assert.Equal(CFixedPoint.Zero, heavies.MovementProgress);

		// Out of it, with hours still to wait: its first move ends the wait.
		CBombing.Leave(state, heavies);
		heavies.NoBombingHours = 1;
		heavies.NoBattleHours = 1;
		CMovement.Advance(state, germany, heavies, addSpeed: true);
		Assert.Equal(0, heavies.NoBombingHours);
		Assert.Equal(0, heavies.NoBattleHours);
	}

	[Fact]
	public void AStrikeKeepsItsProvinceFromSortieToSortie()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Air(state, germany, Berlin, Tactical);
		CUnit division = Unit(state, poland, EUnitKind.Division, Warsaw, Infantry, Infantry);
		Give(state, germany, bombers, EUnitOrder.GroundAttack, Warsaw);

		RunHours(state, 4);
		CCombat bombing = Assert.IsType<CCombat>(bombers.Bombing);
		Assert.Equal(ECombatKind.LandBombing, bombing.Kind);
		Assert.Contains(division, bombing.Defender.Units);
		Assert.Same(bombing, division.Bombing);
		// A unit under bombs is in no battle: it may march and attack as before.
		Assert.False(division.IsFighting);

		RunHours(state, 4);
		Assert.Null(bombers.Bombing);
		Assert.Null(division.Bombing);
		Assert.True(bombers.Mission.ReturnToBase);
		Assert.Equal(Warsaw, bombers.Mission.Destination);
	}

	[Fact]
	public void WhoeverWalksOutOfAProvinceLeavesTheBombingOfIt()
	{
		CGameState state = State(out CCountry germany, out CCountry poland).Paved(Roads);
		CUnit bombers = Air(state, germany, Warsaw, Tactical);
		bombers.Order = EUnitOrder.GroundAttack;
		bombers.Mission.Destination = Warsaw;
		CUnit division = Unit(state, poland, EUnitKind.Division, Warsaw, Infantry);
		CCombat bombing = Assert.IsType<CCombat>(CBombing.Start(state, state.Province(Warsaw), bombers));
		Assert.Same(bombing, division.Bombing);

		CMovement.SetPath(division, new[] { East }, clear: true);
		division.MovementProgress = CFixedPoint.FromInt(100);
		CMovement.Advance(state, poland, division, addSpeed: false);

		Assert.Equal(East, division.Location);
		Assert.Null(division.Bombing);
		Assert.DoesNotContain(division, bombing.Defender.Units);
	}

	[Fact]
	public void AnAirUnitToldToFallBackGoesWhereABeatenOneGoesAndIsLeftWithNoOrder()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Air(state, germany, Warsaw, Tactical);
		bombers.Order = EUnitOrder.GroundAttack;
		bombers.Mission.Destination = Warsaw;
		CUnit division = Unit(state, poland, EUnitKind.Division, Warsaw, Infantry);
		CCombat bombing = Assert.IsType<CCombat>(CBombing.Start(state, state.Province(Warsaw), bombers));
		Assert.False(new CFallBackCommand(Pol, division.Id).MayExecute(state));     // an air unit's alone

		var command = new CFallBackCommand(Ger, bombers.Id);
		Assert.True(command.MayExecute(state));
		command.Execute(state);

		// No sea beside Warsaw: its base. It was holding ground, so it is falling back.
		Assert.Null(bombers.Bombing);
		Assert.DoesNotContain(bombers, bombing.Attacker.Units);
		Assert.Equal(EUnitOrder.None, bombers.Order);
		Assert.Equal(Berlin, bombers.Path[^1]);
		Assert.True(bombers.IsRetreating);
		Assert.False(command.MayExecute(state));                                    // it is in nothing now

		// Beside the sea it flies out to sea, as a beaten unit does.
		CUnit others = Air(state, germany, Field, Tactical);
		others.Order = EUnitOrder.GroundAttack;
		others.Mission.Destination = Field;
		Unit(state, poland, EUnitKind.Division, Field, Infantry);
		Assert.NotNull(CBombing.Start(state, state.Province(Field), others));
		new CFallBackCommand(Ger, others.Id).Execute(state);
		Assert.Equal(new[] { Bay }, others.Path);
		Assert.Null(others.Bombing);
	}

	[Fact]
	public void AMissionTakenBackStaysOverItsBombingAndThenFliesHome()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Air(state, germany, Warsaw, Tactical);
		bombers.Order = EUnitOrder.GroundAttack;
		bombers.Mission.Destination = Warsaw;
		Unit(state, poland, EUnitKind.Division, Warsaw, Infantry);
		Assert.NotNull(CBombing.Start(state, state.Province(Warsaw), bombers));

		var drop = new CDropOrderCommand(Ger, bombers.Id);
		Assert.True(drop.MayExecute(state));
		drop.Execute(state);
		Assert.Equal(EUnitOrder.None, bombers.Order);

		// While its bombing lasts it stays over it; with that over, it is given the way home.
		CUnitOrders.RunHour(state, germany, bombers);
		Assert.False(bombers.IsMoving);
		CCombats.LeaveAll(state, bombers);
		CUnitOrders.RunHour(state, germany, bombers);
		Assert.Equal(Berlin, bombers.Path[^1]);

		// At its own base, and with no base, nothing moves it; a change of base is not taken back.
		CUnit home = Air(state, germany, Berlin, Fighter);
		CUnitOrders.RunHour(state, germany, home);
		Assert.False(home.IsMoving);
		home.Order = EUnitOrder.RebaseAir;
		Assert.False(new CDropOrderCommand(Ger, home.Id).MayExecute(state));
	}

	[Fact]
	public void TheHourlyPassSendsAnAirUnitWithNoOrderHomeFromWhereverItIs()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit bombers = Air(state, germany, Warsaw, Tactical);
		bombers.Order = EUnitOrder.GroundAttack;
		new CDropOrderCommand(Ger, bombers.Id).Execute(state);

		CMovement.RunHourly(state);

		Assert.True(bombers.IsMoving);
		Assert.Equal(Berlin, bombers.Path[^1]);
	}

	// --- which province ---

	[Fact]
	public void AStrategicBombingPicksTheMostIndustryForTheFewestGuns()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit heavies = Air(state, germany, Berlin, Heavy);
		heavies.Order = EUnitOrder.StrategicBombing;
		heavies.Mission.Provinces.AddRange(new[] { Warsaw, East });

		// Warsaw: eight of industry and nine of metal over two of guns; East: two, and no guns.
		Assert.Equal(Warsaw, CBombingProvinces.Pick(state, heavies));
		state.Province(Warsaw).Modifier.Set(CBombingOrders.AntiAir, CFixedPoint.FromInt(10));
		Assert.Equal(East, CBombingProvinces.Pick(state, heavies));
	}

	[Fact]
	public void NoMoreThanThreeWingsAreSentAtOneProvince()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit first = Air(state, germany, Berlin, Heavy, Heavy, Heavy);
		first.Order = EUnitOrder.StrategicBombing;
		first.Mission.Destination = Warsaw;
		CUnit second = Air(state, germany, Berlin, Heavy);
		second.Order = EUnitOrder.StrategicBombing;
		second.Mission.Provinces.AddRange(new[] { Warsaw, East });

		Assert.Equal(East, CBombingProvinces.Pick(state, second));
	}

	[Fact]
	public void GroundAttackGoesRoundItsListAndInterdictionTakesTheLastItCanSee()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit bombers = Air(state, germany, Berlin, Tactical);
		bombers.Order = EUnitOrder.GroundAttack;
		bombers.Mission.Provinces.AddRange(new[] { Field, Warsaw, East });
		Assert.Equal(Field, CBombingProvinces.Pick(state, bombers));
		bombers.Mission.Destination = Field;
		Assert.Equal(Warsaw, CBombingProvinces.Pick(state, bombers));
		bombers.Mission.Destination = East;
		Assert.Equal(Field, CBombingProvinces.Pick(state, bombers));

		// Interdiction flies only at ground its country sees into: nothing of Poland's here.
		bombers.Order = EUnitOrder.Interdiction;
		bombers.Mission.Destination = 0;
		Assert.Equal(0, CBombingProvinces.Pick(state, bombers));
		state.Province(Field).Intel[germany.Index] = 9;
		state.Province(Warsaw).Intel[germany.Index] = 9;
		Assert.Equal(Warsaw, CBombingProvinces.Pick(state, bombers));
	}

	// --- who is in it ---

	[Fact]
	public void ASecondUnitJoinsABombingOfLandUnitsAndNotOneOfTheGroundAlone()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CProvince warsaw = state.Province(Warsaw);
		CUnit first = Air(state, germany, Warsaw, Heavy);
		first.Order = EUnitOrder.StrategicBombing;
		first.Mission.Destination = Warsaw;
		CUnit second = Air(state, germany, Warsaw, Heavy);
		second.Order = EUnitOrder.StrategicBombing;
		second.Mission.Destination = Warsaw;
		Unit(state, poland, EUnitKind.Division, Warsaw, Infantry);

		CCombat ground = Assert.IsType<CCombat>(CBombing.Start(state, warsaw, first));
		Assert.Equal(ECombatKind.GroundBombing, ground.Kind);
		Assert.Empty(ground.Defender.Units);             // a land unit is in no bombing of the ground
		CBombing.Look(state, second, warsaw);
		Assert.Null(second.Bombing);                     // nobody on the other side to be against it

		// Its end has everybody there look again, and neither starts another in its hours of grace.
		first.NoBombingHours = second.NoBombingHours = CBombing.Grace;
		CCombats.Finish(state, ground);
		Assert.Null(CBombing.In(state, Warsaw));
		first.NoBombingHours = second.NoBombingHours = 0;
		first.Order = EUnitOrder.GroundAttack;
		second.Order = EUnitOrder.GroundAttack;
		CCombat land = Assert.IsType<CCombat>(CBombing.Start(state, warsaw, first));
		Assert.Equal(ECombatKind.LandBombing, land.Kind);
		CBombing.Look(state, second, warsaw);
		Assert.Same(land, second.Bombing);
		Assert.Equal(new[] { first, second }, land.Attacker.Units);
	}

	[Fact]
	public void APortStrikeTakesInTheFleetsInThePortAndARunwayStrikeTheAircraftAtTheirBase()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CProvince warsaw = state.Province(Warsaw);
		CUnit fleet = Unit(state, poland, EUnitKind.Navy, Warsaw, Cruiser);
		CUnit atBase = Air(state, poland, Warsaw, Fighter);
		CUnit division = Unit(state, poland, EUnitKind.Division, Warsaw, Infantry);
		CUnit torpedoes = Air(state, germany, Warsaw, Torpedo);
		torpedoes.Order = EUnitOrder.PortStrike;
		torpedoes.Mission.Destination = Warsaw;

		CCombat strike = Assert.IsType<CCombat>(CBombing.Start(state, warsaw, torpedoes));
		Assert.Equal(ECombatKind.GroundBombing, strike.Kind);
		Assert.Contains(fleet, strike.Defender.Units);
		Assert.DoesNotContain(division, strike.Defender.Units);
		// The aircraft there are nothing to a port strike: it does not go for them.
		Assert.DoesNotContain(atBase, strike.Defender.Units);

		torpedoes.NoBombingHours = CBombing.Grace;
		CCombats.Finish(state, strike);
		CUnit heavies = Air(state, germany, Warsaw, Heavy);
		heavies.Order = EUnitOrder.RunwayCratering;
		heavies.Mission.Destination = Warsaw;
		CCombat runway = Assert.IsType<CCombat>(CBombing.Start(state, warsaw, heavies));
		Assert.Contains(atBase, runway.Defender.Units);
		Assert.DoesNotContain(fleet, runway.Defender.Units);
		CBomberFire.CollectTargets(state, runway.Defender, runway.Attacker, heavies, germany);
		Assert.Equal(new[] { atBase.SubUnits[0] }, runway.Attacker.Targets);
	}

	// --- the bombers' fire ---

	[Fact]
	public void TheAttackFiredIsTheMissionsOwn()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Air(state, germany, Berlin, Tactical);
		CSubUnit wing = bombers.SubUnits[0];
		CSubUnit soft = Unit(state, poland, EUnitKind.Division, Warsaw, Infantry).SubUnits[0];
		CSubUnit hard = Unit(state, poland, EUnitKind.Division, Warsaw, Armour).SubUnits[0];
		long AttackOf(EUnitOrder order, CSubUnit target)
		{
			bombers.Order = order;
			return CBomberFire.AttackOf(state, bombers, wing, target);
		}

		// Soft six and hard two: all soft against infantry, a quarter soft against armour.
		Assert.Equal(6000, AttackOf(EUnitOrder.GroundAttack, soft));
		Assert.Equal(3000, AttackOf(EUnitOrder.Interdiction, hard));
		Assert.Equal(3000, AttackOf(EUnitOrder.NavalStrike, soft));
		Assert.Equal(3000, AttackOf(EUnitOrder.PortStrike, soft));
		Assert.Equal(2000, AttackOf(EUnitOrder.RunwayCratering, soft));
		Assert.Equal(0, AttackOf(EUnitOrder.StrategicBombing, soft));
		Assert.Equal(0, AttackOf(EUnitOrder.LogisticalStrike, soft));
	}

	[Fact]
	public void TheBombersLeadersSofteningTheirTacticalAndTheirNavalAttackComeIntoWhatIsFired()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Air(state, germany, Berlin, Tactical);
		var trait = new CTrait("a_trait");
		trait.Effects.Add(new CTraitEffect(ETraitEffect.DefenderSoftness, CFixedPoint.One, ""));
		trait.Effects.Add(new CTraitEffect(ETraitEffect.TacticalAttack, CFixedPoint.Parse("0.5"), ""));
		trait.Effects.Add(new CTraitEffect(ETraitEffect.NavalAttack, CFixedPoint.Parse("0.2"), ""));
		bombers.Leader = new CLeader(1) { Skill = 1, Rank = 1, Name = "marshal" };
		bombers.Leader.Traits.Add(trait);
		CSubUnit hard = Unit(state, poland, EUnitKind.Division, Warsaw, Armour).SubUnits[0];

		// Armour's quarter of softness doubled to a half: 6 x 0.5 + 2 x 0.5, and a half more.
		bombers.Order = EUnitOrder.GroundAttack;
		Assert.Equal(6000, CBomberFire.AttackOf(state, bombers, bombers.SubUnits[0], hard));
		bombers.Order = EUnitOrder.NavalStrike;
		Assert.Equal(3600, CBomberFire.AttackOf(state, bombers, bombers.SubUnits[0], hard));
	}

	[Fact]
	public void ABomberLedByASkillOfTenHasEveryShipOnItsList()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit fleet = Unit(state, poland, EUnitKind.Navy, Bay, Cruiser, Cruiser, Cruiser);
		CUnit torpedoes = Air(state, germany, Bay, Torpedo);
		torpedoes.Order = EUnitOrder.NavalStrike;
		torpedoes.Leader = new CLeader(1) { Skill = 10, Rank = 0, Name = "marshal" };
		var bombing = new CCombat(Bay, ECombatKind.NavalBombing);
		CBombing.Join(bombing.Attacker, torpedoes);
		CBombing.Join(bombing.Defender, fleet);
		state.SeedHour();

		// A hundred in a hundred, whatever his rank.
		CBomberFire.CollectTargets(state, bombing.Defender, bombing.Attacker, torpedoes, germany);
		Assert.Equal(fleet.SubUnits, bombing.Attacker.Targets);
	}

	[Fact]
	public void AShipGoesOnALeaderlessBombersListOneTimeInAHundred()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		var ships = new CSubUnitType[400];
		System.Array.Fill(ships, Cruiser);
		CUnit fleet = Unit(state, poland, EUnitKind.Navy, Bay, ships);
		CUnit torpedoes = Air(state, germany, Bay, Torpedo);
		torpedoes.Order = EUnitOrder.NavalStrike;
		var bombing = new CCombat(Bay, ECombatKind.NavalBombing);
		CBombing.Join(bombing.Attacker, torpedoes);
		CBombing.Join(bombing.Defender, fleet);
		state.SeedHour();

		CBomberFire.CollectTargets(state, bombing.Defender, bombing.Attacker, torpedoes, germany);

		Assert.InRange(bombing.Attacker.Targets.Count, 0, 16);
		Assert.Equal(0, CCombatLeaders.UnitSkill(torpedoes));
	}

	[Fact]
	public void GroundAttackFiresAtEverybodyUnlessARollSinglesOutThoseWhoAreFighting()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit bombers = Air(state, germany, Warsaw, Tactical);
		bombers.Order = EUnitOrder.GroundAttack;
		CUnit resting = Unit(state, poland, EUnitKind.Division, Warsaw, Infantry, Infantry);
		var bombing = new CCombat(Warsaw, ECombatKind.LandBombing);
		CBombing.Join(bombing.Attacker, bombers);
		CBombing.Join(bombing.Defender, resting);
		state.SeedHour();

		// Nobody is fighting, so nobody is singled out and nothing is rolled.
		long before = state.Random.Draws;
		CBomberFire.CollectFighting(state, bombing.Defender, bombing.Attacker, germany);
		Assert.Empty(bombing.Attacker.Targets);
		Assert.Equal(before, state.Random.Draws);
		CBomberFire.CollectTargets(state, bombing.Defender, bombing.Attacker, bombers, germany);
		Assert.Equal(resting.SubUnits, bombing.Attacker.Targets);
	}

	// --- what the bombs do ---

	[Fact]
	public void AWingWithNoStrategicAttackStillMakesOneRunAnHour()
	{
		CGameState state = State(out CCountry germany, out _);
		CProvince warsaw = state.Province(Warsaw);
		CUnit torpedoes = Air(state, germany, Warsaw, Torpedo);
		torpedoes.Order = EUnitOrder.PortStrike;
		var bombing = new CCombat(Warsaw, ECombatKind.GroundBombing);
		CBombing.Join(bombing.Attacker, torpedoes);
		state.SeedHour();

		long before = state.Random.Draws;
		CProvinceBombing.Bomb(state, bombing, bombing.Attacker, torpedoes, torpedoes.SubUnits[0],
			new CProvinceBombing.CNeed(state, Warsaw));

		Assert.Equal(before + 1, state.Random.Draws);
		Assert.True(warsaw.BuildingLevel(NavalBase) < CFixedPoint.FromInt(4));
		// A die of one to three, times a twentieth of a level, off a port in full repair.
		long lost = 4000 - warsaw.BuildingLevel(NavalBase).Raw;
		Assert.Contains(lost, new long[] { 50, 100, 150 });
		// The guns lose a quarter of that, by their damage factor.
		Assert.Equal(lost / 4, 2000 - warsaw.BuildingLevel(Guns).Raw);
		// The bombers keep what they took off each building.
		Assert.Equal(new CFixedPoint(lost), bombing.Attacker.BuildingsDestroyed[NavalBase.Key]);
		Assert.Equal(new CFixedPoint(lost / 4), bombing.Attacker.BuildingsDestroyed[Guns.Key]);
	}

	[Fact]
	public void ABuildingLosesLessTheMoreOfItIsGone()
	{
		CGameState state = State(out CCountry germany, out _);
		CProvince warsaw = state.Province(Warsaw);
		warsaw.SetBuildingCurrentLevel(Factory, CFixedPoint.FromInt(2));     // a quarter of its eight
		CUnit heavies = Air(state, germany, Warsaw, Heavy);
		heavies.Order = EUnitOrder.StrategicBombing;
		var bombing = new CCombat(Warsaw, ECombatKind.GroundBombing);
		CBombing.Join(bombing.Attacker, heavies);
		state.SeedHour();

		// Seven of strategic attack: seven runs, each a die of one to three.
		long before = state.Random.Draws;
		CProvinceBombing.Bomb(state, bombing, bombing.Attacker, heavies, heavies.SubUnits[0],
			new CProvinceBombing.CNeed(state, Warsaw));

		Assert.Equal(before + 7, state.Random.Draws);
		long lost = 2000 - warsaw.BuildingLevel(Factory).Raw;
		// At full repair seven runs take 0.35 to 1.05; at a quarter, under a quarter of that.
		Assert.InRange(lost, 70, 262);
		Assert.True(warsaw.CurrentProducing[EGoods.Metal] < CFixedPoint.FromInt(9));
		Assert.Equal(CFixedPoint.FromInt(100), warsaw.Pool[EGoods.Supplies]);
	}

	[Fact]
	public void ALogisticalStrikeDestroysMoreOfAStockpileThatIsFullAgainstItsNeed()
	{
		CGameState state = State(out CCountry germany, out _);
		CProvince warsaw = state.Province(Warsaw);
		warsaw.SetBuildingCurrentLevel(Roads, CFixedPoint.Zero);             // all ten of its roads gone
		CUnit heavies = Air(state, germany, Warsaw, Wing("one", strategic: "1"));
		heavies.Order = EUnitOrder.LogisticalStrike;
		var bombing = new CCombat(Warsaw, ECombatKind.GroundBombing);
		CBombing.Join(bombing.Attacker, heavies);
		state.SeedHour();

		CProvinceBombing.Bomb(state, bombing, bombing.Attacker, heavies, heavies.SubUnits[0],
			new CProvinceBombing.CNeed(state, Warsaw));

		// Ten levels missing: 10 x 0.075 x 0.9 = 0.675 a point of the die, and nobody draws on it.
		long supplies = 100000 - warsaw.Pool[EGoods.Supplies].Raw;
		Assert.Contains(supplies, new long[] { 675, 1350, 2025 });
		Assert.Equal(supplies, 50000 - warsaw.Pool[EGoods.Fuel].Raw);
		Assert.Equal(new CFixedPoint(supplies), bombing.Attacker.StockpileDestroyed[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(9), warsaw.CurrentProducing[EGoods.Metal]);
	}

	// --- the fire that comes back ---

	[Fact]
	public void TheProvinceFiresItsGunsAndOneAndAHalfTimesItsUnitsAirAttack()
	{
		CGameState state = State(out _, out CCountry poland);
		CProvince warsaw = state.Province(Warsaw);
		// Two levels of guns, times five.
		Assert.Equal(10000, CAntiAircraft.ProvinceFire(state, warsaw));

		Unit(state, poland, EUnitKind.Division, Warsaw, Infantry, Infantry);   // two each
		Assert.Equal(10000 + 6000, CAntiAircraft.ProvinceFire(state, warsaw));

		// A fleet in the port is on both of the province's lists, and counts twice.
		Unit(state, poland, EUnitKind.Navy, Warsaw, Cruiser);                   // three
		Assert.Equal(10000 + 6000 + 9000, CAntiAircraft.ProvinceFire(state, warsaw));

		poland.Technology.Set(CAntiAircraft.ProvincialEfficiency, CFixedPoint.Parse("0.2"));
		Assert.Equal(12000 + 6000 + 9000, CAntiAircraft.ProvinceFire(state, warsaw));
	}

	[Fact]
	public void AtSeaEveryUnitOfACountryThatIsNobodysPuppetFiresThroughTheSeaZoneTheBombersToo()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CProvince bay = state.Province(Bay);
		Assert.Equal(0, CAntiAircraft.ProvinceFire(state, bay));

		Unit(state, poland, EUnitKind.Navy, Bay, Cruiser);       // three
		Unit(state, germany, EUnitKind.Air, Bay, Tactical);      // one, though it is the bomber
		Assert.Equal(1500 * 4, CAntiAircraft.ProvinceFire(state, bay));

		// A puppet's units are nobody's friend at sea: its overlord is somebody.
		poland.Overlord = Ger;
		Assert.Equal(1500, CAntiAircraft.ProvinceFire(state, bay));
	}

	[Fact]
	public void TheFireWearsTheBombersAndTheOtherSideNeverLoses()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit heavies = Air(state, germany, Warsaw, Heavy);
		heavies.Order = EUnitOrder.StrategicBombing;
		heavies.Mission.Destination = Warsaw;
		Unit(state, poland, EUnitKind.Division, Warsaw, Infantry, Infantry, Infantry, Infantry);
		CCombat bombing = Assert.IsType<CCombat>(CBombing.Start(state, state.Province(Warsaw), heavies));
		state.SeedHour();

		Assert.False(CBombing.RunHour(state, bombing));
		Assert.True(heavies.SubUnits[0].Organisation < CFixedPoint.FromInt(30));
		Assert.True(heavies.SubUnits[0].Strength < CFixedPoint.FromInt(100));
		Assert.False(CBombing.RunHour(state, bombing));
		Assert.False(CBombing.RunHour(state, bombing));
		Assert.True(CBombing.RunHour(state, bombing));
		Assert.Equal(4, bombing.Duration);
		Assert.False(bombing.IsConcluded);
	}

	[Fact]
	public void BombersWornToNothingFallBackAtTheFourthHour()
	{
		CGameState state = State(out CCountry germany, out _);
		CUnit heavies = Air(state, germany, Warsaw, Heavy);
		heavies.Order = EUnitOrder.StrategicBombing;
		heavies.Mission.Destination = Warsaw;
		heavies.SubUnits[0].Organisation = new CFixedPoint(100);
		CCombat bombing = Assert.IsType<CCombat>(CBombing.Start(state, state.Province(Warsaw), heavies));
		state.SeedHour();

		for (int hour = 0; hour < 3; hour++)
		{
			Assert.False(CBombing.RunHour(state, bombing));
			heavies.SubUnits[0].Organisation = new CFixedPoint(100);
		}
		Assert.True(CBombing.RunHour(state, bombing));

		Assert.True(bombing.IsConcluded);
		Assert.True(bombing.AttackerLost);
		Assert.Null(heavies.Bombing);
		Assert.Empty(bombing.Attacker.Units);
	}

	// --- what the hour makes of the bombers ---

	[Fact]
	public void EveryWingOfTheBombersCrowdsTheOthersAndABattleOfItsOwnCostsABomber()
	{
		CGameState state = State(out CCountry germany, out CCountry poland);
		CUnit heavies = Air(state, germany, Warsaw, Heavy, Heavy);
		heavies.Order = EUnitOrder.StrategicBombing;
		var bombing = new CCombat(Warsaw, ECombatKind.GroundBombing);
		CBombing.Join(bombing.Attacker, heavies);

		CBomberModifiers.Apply(state, bombing);
		CSubUnit wing = heavies.SubUnits[0];
		// Two wings at a tenth each - where an air battle would count one.
		Assert.Contains(wing.CombatModifiers, modifier => modifier.Kind == ECombatModifier.Stacking && modifier.Attack.Raw == -200);
		Assert.DoesNotContain(wing.CombatModifiers, modifier => modifier.Kind == ECombatModifier.AirCombat);
		Assert.DoesNotContain(wing.CombatModifiers, modifier => modifier.Kind == ECombatModifier.BaseProximity);

		// In an air battle against twice its strength: a tenth, twice over.
		CUnit fighters = Air(state, poland, Warsaw, Fighter, Fighter, Fighter, Fighter);
		var battle = new CCombat(Warsaw, ECombatKind.Air);
		battle.Attacker.Add(fighters);
		battle.Defender.Add(heavies);
		Assert.Equal(-200, CBomberModifiers.AirCombat(state, heavies));
		CBomberModifiers.Apply(state, bombing);
		Assert.Contains(wing.CombatModifiers, modifier => modifier.Kind == ECombatModifier.AirCombat && modifier.Attack.Raw == -200);
	}

	[Fact]
	public void TheRadioAndAFriendsRadarBesideTheProvinceCountForTheBombers()
	{
		CGameState state = State(out CCountry germany, out _);
		state.Defines.Set(CManpower.MilitarySection, CRadarCover.CombatImpact, CFixedPoint.Parse("0.05"));
		germany.Technology.Set(CBomberModifiers.CombatEfficiency, CFixedPoint.Parse("0.1"));
		state.Province(Border).Modifier.Set(CRadarCover.RadarLevel, CFixedPoint.FromInt(4));
		CUnit heavies = Air(state, germany, Field, Heavy);
		heavies.Order = EUnitOrder.StrategicBombing;
		var bombing = new CCombat(Field, ECombatKind.GroundBombing);
		CBombing.Join(bombing.Attacker, heavies);

		CBomberModifiers.Apply(state, bombing);

		CSubUnit wing = heavies.SubUnits[0];
		Assert.Contains(wing.CombatModifiers, modifier => modifier.Kind == ECombatModifier.Radio && modifier.Attack.Raw == 100);
		Assert.Contains(wing.CombatModifiers, modifier => modifier.Kind == ECombatModifier.Radar && modifier.Attack.Raw == 200);
		// Poland's own radar, in East, is no friend's.
		Assert.Equal(0, CRadarCover.Level(state, state.Province(Warsaw), bombing.Attacker));
	}

	// --- what comes back ---

	[Fact]
	public void ADayBringsBackProductionAndATenthAsMuchOfEveryBuildingThatIsRepaired()
	{
		CGameState state = State(out _, out CCountry poland);
		CProvince warsaw = state.Province(Warsaw);
		warsaw.SetBuildingCurrentLevel(Factory, CFixedPoint.FromInt(3));
		warsaw.SetBuildingCurrentLevel(Guns, CFixedPoint.Parse("1.95"));
		warsaw.SetBuildingCurrentLevel(Bunker, CFixedPoint.Zero);
		warsaw.CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(4);
		CProvinceModifiers.Rebuild(state);

		// Ten levels of roads are one whole: a tenth and one, 1.1 a day.
		Assert.Equal(1100, CProvinceRepair.Rate(state, warsaw));
		CProvinceRepair.RunDaily(state);
		Assert.Equal(CFixedPoint.Parse("5.1"), warsaw.CurrentProducing[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Parse("3.11"), warsaw.BuildingLevel(Factory));
		Assert.Equal(CFixedPoint.FromInt(2), warsaw.BuildingLevel(Guns));         // not past what was built
		Assert.Equal(CFixedPoint.Zero, warsaw.BuildingLevel(Bunker));             // repair = no

		// A province with no roads gets back a tenth and a hundredth.
		Assert.Equal(110, CProvinceRepair.Rate(state, state.Province(East)));
		poland.Technology.Set(CProvinceRepair.RepairRate, CFixedPoint.Parse("0.5"));
		Assert.Equal(1650, CProvinceRepair.Rate(state, warsaw));
	}

	[Fact]
	public void WhatAProvinceIsWorthIsWhatWorksOfItsBuildings()
	{
		CGameState state = State(out _, out _);
		CProvince warsaw = state.Province(Warsaw);
		Assert.Equal(CFixedPoint.FromInt(8), warsaw.Modifier[CBombingOrders.Industry]);

		warsaw.SetBuildingCurrentLevel(Factory, CFixedPoint.Parse("2.5"));
		CProvinceModifiers.Rebuild(state);

		Assert.Equal(CFixedPoint.Parse("2.5"), warsaw.Modifier[CBombingOrders.Industry]);
		// A port bombed flat is a port still.
		warsaw.SetBuildingCurrentLevel(NavalBase, CFixedPoint.Zero);
		Assert.Equal(CFixedPoint.FromInt(4), CMovementRules.BuildingLevel(state, warsaw, CMovementRules.NavalBase));
		Assert.True(CBombing.HasPort(state, warsaw));
	}
}
