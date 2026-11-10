using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The window of orders against <c>docs/mechanics/fleetsandair.md</c>, section 4. Kiel
/// has a port onto the bay, a naval base and an air base; Hamburg has an air base.
/// </summary>
public class OrdersTextTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Kiel = 1;
	private const int Hamburg = 2;
	private const int Bay = 3;
	private const int Rostock = 4;

	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "", CFixedPoint.Zero);
	private static readonly CBuilding AirBase = new(1, "air_base", 10, "", CFixedPoint.Zero);

	private static CProvinceEdge Edge(int to)
	{
		return new CProvinceEdge(to, EProvinceEdgeKind.Plain, 0, CFixedPoint.FromInt(10));
	}

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var kiel = new CProvince(Kiel, "Kiel", isSea: false)
		{
			Owner = Ger, Controller = Ger, PortSea = Bay, SeaIndex = 0, Edges = new[] { Edge(Bay), Edge(Hamburg) },
		};
		kiel.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));
		kiel.SetBuildingLevel(AirBase, CFixedPoint.FromInt(1));
		var hamburg = new CProvince(Hamburg, "Hamburg", isSea: false) { Owner = Ger, Controller = Ger, Edges = new[] { Edge(Kiel) } };
		hamburg.SetBuildingLevel(AirBase, CFixedPoint.FromInt(2));
		var rostock = new CProvince(Rostock, "Rostock", isSea: false)
		{
			Owner = Ger, Controller = Ger, PortSea = Bay, SeaIndex = 2, Edges = new[] { Edge(Bay) },
		};
		rostock.SetBuildingLevel(NavalBase, CFixedPoint.FromInt(1));
		var bay = new CProvince(Bay, "Bay", isSea: true) { SeaIndex = 1, Edges = new[] { Edge(Kiel), Edge(Rostock) } };
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), kiel, hamburg, bay, rostock },
			new[] { germany }, buildings: OpenHOI3.Tests.Sim.CTestRoads.With(NavalBase, AirBase)).Paved();
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province,
		params (string Statistic, string Value)[] statistics)
	{
		var type = new CSubUnitType(kind == EUnitKind.Navy ? "destroyer" : "wing",
			kind == EUnitKind.Navy ? EUnitBranch.Naval : EUnitBranch.Air);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.FromInt(20));
		type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.FromInt(1000));
		foreach ((string statistic, string value) in statistics)
		{
			type.Stats.Values.Set(statistic, CFixedPoint.Parse(value));
		}
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Base = province, Name = "Unit" };
		var subUnit = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit };
		subUnit.Stats.CopyFrom(type.Stats);
		unit.SubUnits.Add(subUnit);
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void WithSeveralFleetsARowIsGreyedWhereAnyOfThemMayNotTakeIt()
	{
		CGameState state = State(out CCountry country);
		// The first carries a division, the second nothing: only the first may be given the transport.
		CUnit fleet = Unit(state, country, EUnitKind.Navy, Kiel);
		CUnit empty = Unit(state, country, EUnitKind.Navy, Kiel);
		var division = new CUnit(EUnitKind.Division, Ger) { Id = ++state.LastUnitId, Location = Kiel, Name = "Division" };
		country.Units.Add(division);
		CTransport.Board(fleet, division);
		CProvince sea = state.Province(Rostock);

		List<COrderRow> first = COrdersText.Rows(state, country, fleet, sea);
		List<COrderRow> second = COrdersText.Rows(state, country, empty, sea);
		List<COrderRow> both = COrdersText.Rows(state, country, new[] { fleet, empty }, sea);

		// The first fleet's rows, each open only where both may take it (selection.md, section 6).
		Assert.Equal(first.Select(row => row.NameKey), both.Select(row => row.NameKey));
		Assert.Equal(first.Zip(second, (a, b) => a.MayGive && b.MayGive), both.Select(row => row.MayGive));
		Assert.NotEqual(first.Select(row => row.MayGive), both.Select(row => row.MayGive));
		// One fleet alone: its own rows.
		Assert.Equal(first, COrdersText.Rows(state, country, new[] { fleet }, sea));
	}

	[Fact]
	public void AFleetIsShownEveryNavalOrderAndOnlyTheOnesItCanBeGivenAreOpen()
	{
		CGameState state = State(out CCountry country);
		CUnit fleet = Unit(state, country, EUnitKind.Navy, Kiel);

		List<COrderRow> toSea = COrdersText.Rows(state, country, fleet, state.Province(Bay));
		Assert.Equal(new[]
		{
			"MOVE_ORDER", "ORDER_REBASE", "ORDER_RESERVE", "ORDER_PATROL", "ORDER_INTERCEPT", "ORDER_SORTIE",
			"ORDER_CONVOY_RAID", "ORDER_TRANSPORT", "ORDER_INVADE",
		}, toSea.Select(row => row.NameKey));
		// Out at sea: the move and the four missions that are for water; no base, no port, nothing to carry.
		Assert.Equal(new[] { true, false, false, true, true, true, true, false, false }, toSea.Select(row => row.MayGive));
		Assert.Equal(0, COrdersText.Suggested(toSea, state, country, fleet, state.Province(Bay)));

		// To a port with a naval base both can be given, and the change of base is the one ticked.
		List<COrderRow> toPort = COrdersText.Rows(state, country, fleet, state.Province(Rostock));
		Assert.Equal(new[] { true, true }, toPort.Take(2).Select(row => row.MayGive));
		Assert.Equal(EUnitOrder.Rebase, toPort[1].Order);
		Assert.Equal(1, COrdersText.Suggested(toPort, state, country, fleet, state.Province(Rostock)));
		Assert.Equal(COrdersText.NavalWindow, COrdersText.WindowFor(fleet));
	}

	[Fact]
	public void AnAirUnitIsShownOnlyTheOrdersItsWingsCouldFly()
	{
		CGameState state = State(out CCountry country);
		CUnit fighters = Unit(state, country, EUnitKind.Air, Kiel, ("air_attack", "5"));
		CUnit bombers = Unit(state, country, EUnitKind.Air, Kiel, ("strategic_attack", "3"), ("soft_attack", "2"), ("sea_attack", "1"));

		Assert.Equal(new[] { "AIRSUP", "INTERCEPT", "ORDER_AIR_RESERVE", "ORDER_AIR_REBASE" },
			COrdersText.Rows(state, country, fighters, state.Province(Hamburg)).Select(row => row.NameKey));
		Assert.Equal(new[]
		{
			"STRAT_BOMB", "LOG_BOMB", "AIRFIELD_BOMB", "INSTALLATION_BOMB", "GROUNDATTACK_BOMB", "INTERDICT_BOMB",
			"PORTSTRIKE_BOMB", "ORDER_AIR_RESERVE", "ORDER_AIR_REBASE",
		}, COrdersText.Rows(state, country, bombers, state.Province(Hamburg)).Select(row => row.NameKey));
		// Over the sea there is nowhere to land and nothing on the ground to bomb.
		Assert.Equal(new[] { "NAVALSTRIKE_BOMB", "ORDER_AIR_CONVOY_RAID" },
			COrdersText.Rows(state, country, bombers, state.Province(Bay)).Select(row => row.NameKey));
		Assert.Equal(new[] { "AIRSUP", "INTERCEPT" },
			COrdersText.Rows(state, country, fighters, state.Province(Bay)).Select(row => row.NameKey));
	}

	[Fact]
	public void AFightersTwoMissionsAndItsChangeOfBaseCanBeGivenAndTheChangeOfBaseIsTicked()
	{
		CGameState state = State(out CCountry country);
		CUnit fighters = Unit(state, country, EUnitKind.Air, Kiel, ("air_attack", "5"));

		// Air superiority and the intercept, the reserve that is not built, the change of base.
		List<COrderRow> rows = COrdersText.Rows(state, country, fighters, state.Province(Hamburg));
		Assert.Equal(new[] { true, true, false, true }, rows.Select(row => row.MayGive));
		Assert.Equal(new[] { EUnitOrder.AirSuperiority, EUnitOrder.AirIntercept }, rows.Take(2).Select(row => row.Order));
		Assert.Equal(3, COrdersText.Suggested(rows, state, country, fighters, state.Province(Hamburg)));
		Assert.Equal(EUnitOrder.RebaseAir, rows[3].Order);

		// Rostock has no air base: the row is there and cannot be ticked, and nothing is.
		rows = COrdersText.Rows(state, country, fighters, state.Province(Rostock));
		Assert.False(rows[3].MayGive);
		Assert.Equal(-1, COrdersText.Suggested(rows, state, country, fighters, state.Province(Rostock)));
		Assert.Equal(COrdersText.AirWindow, COrdersText.WindowFor(fighters));
	}

	[Fact]
	public void ABombingMissionHasItsOwnNameAndSaysWhatItIsDoing()
	{
		CGameState state = State(out CCountry country);
		CUnit bombers = Unit(state, country, EUnitKind.Air, Kiel, ("strategic_attack", "5"));
		Assert.Equal("STRAT_BOMB", COrdersText.NameKey(EUnitOrder.StrategicBombing));
		Assert.Equal("LOG_BOMB", COrdersText.NameKey(EUnitOrder.LogisticalStrike));
		Assert.Equal("AIRFIELD_BOMB", COrdersText.NameKey(EUnitOrder.RunwayCratering));
		Assert.Equal("INSTALLATION_BOMB", COrdersText.NameKey(EUnitOrder.InstallationStrike));
		Assert.Equal("GROUNDATTACK_BOMB", COrdersText.NameKey(EUnitOrder.GroundAttack));
		Assert.Equal("INTERDICT_BOMB", COrdersText.NameKey(EUnitOrder.Interdiction));
		Assert.Equal("PORTSTRIKE_BOMB", COrdersText.NameKey(EUnitOrder.PortStrike));
		Assert.Equal("NAVALSTRIKE_BOMB", COrdersText.NameKey(EUnitOrder.NavalStrike));

		// Over its own country's ground the four strategic rows are there and cannot be ticked.
		List<COrderRow> rows = COrdersText.Rows(state, country, bombers, state.Province(Hamburg));
		Assert.Equal(new[] { "STRAT_BOMB", "LOG_BOMB", "AIRFIELD_BOMB", "INSTALLATION_BOMB" }, rows.Take(4).Select(row => row.NameKey));
		Assert.All(rows.Take(4), row => Assert.False(row.MayGive));
		Assert.Equal(EUnitOrder.StrategicBombing, rows[0].Order);

		static string Localise(string key) => key switch
		{
			"STRAT_BOMB_SHORT_DESC" => "Strategic Bombing($TYPE$)",
			"STRAT_BOMB_WAITING" => "Resting",
			"STRAT_BOMB_ATTACKING" => "Attacking $PROV$",
			"STRAT_BOMB_RETURNING" => "Returning",
			_ => key,
		};
		bombers.Order = EUnitOrder.StrategicBombing;
		bombers.OrderProvince = Hamburg;
		Assert.Equal("Strategic Bombing(Resting)", CUnitPanelText.Order(Localise, state, bombers));
		bombers.Mission.Destination = Hamburg;
		Assert.Equal("Strategic Bombing(Attacking Hamburg)", CUnitPanelText.Order(Localise, state, bombers));
		// Over the province, the mission has forgotten where it was going and the bombing says.
		bombers.Mission.Destination = 0;
		bombers.Bombing = new CCombat(Hamburg, ECombatKind.GroundBombing);
		Assert.Equal("Strategic Bombing(Attacking Hamburg)", CUnitPanelText.Order(Localise, state, bombers));
		bombers.Bombing = null;
		Assert.Equal("Strategic Bombing(Resting)", CUnitPanelText.Order(Localise, state, bombers));
		bombers.Mission.ReturnToBase = true;
		Assert.Equal("Strategic Bombing(Returning)", CUnitPanelText.Order(Localise, state, bombers));
	}

	[Fact]
	public void TheStanceAndThePriorityGoRoundAtEitherEnd()
	{
		Assert.Equal("STAN_AGRESSIVE", COrdersText.StanceKey(0));
		Assert.Equal("STAN_DEFENSIVE", COrdersText.StanceKey(1));
		Assert.Equal("STAN_PASSIVE", COrdersText.StanceKey(2));
		Assert.Equal(2, COrdersText.StepStance(0, -1));
		Assert.Equal(0, COrdersText.StepStance(2, 1));
		Assert.Equal(1, COrdersText.StepStance(0, 1));
		Assert.Equal(4, COrdersText.StepPriority(0, -1));
		Assert.Equal(0, COrdersText.StepPriority(4, 1));
		Assert.Equal(3, COrdersText.StepPriority(2, 1));
	}

	[Fact]
	public void A_date_widget_writes_the_year_the_month_cut_to_three_letters_the_day_and_the_hour()
	{
		// As the maintainer's screenshots of the land window have it (landorders.md, section 2).
		long hour = (long)new CDate(1936, 1, 4).DayNumber * CGameState.HoursPerDay + 11;
		Assert.Equal("1936 Jan 4 11:00", CAirOrdersText.DateText(key => key, hour));
		Assert.Equal("1936 May 1 0:00", CAirOrdersText.DateText(key => key, (long)new CDate(1936, 5, 1).DayNumber * 24));
	}

	[Fact]
	public void A_land_unit_is_shown_the_same_three_rows_and_the_first_it_may_be_given_of_support_move_and_redeployment_is_ticked()
	{
		CGameState state = State(out CCountry country);
		var division = new CUnit(EUnitKind.Division, Ger) { Id = ++state.LastUnitId, Location = Kiel, Name = "Division" };
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.FromInt(4));
		var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = division };
		brigade.Stats.CopyFrom(type.Stats);
		division.SubUnits.Add(brigade);
		country.Units.Add(division);
		Assert.Equal(COrdersText.LandWindow, COrdersText.WindowFor(division));

		// Beside, at home: a move and a redeployment, no support attack - and the move is ticked (landorders.md, section 2).
		List<COrderRow> beside = COrdersText.Rows(state, country, new[] { division }, state.Province(Hamburg));
		Assert.Equal(new[] { "MOVE_ORDER", "SUPPORT_ATTACK", "ORDER_STRATEGIC_REDEPLOYMENT" }, beside.Select(row => row.NameKey));
		Assert.Equal(new[] { true, false, true }, beside.Select(row => row.MayGive));
		Assert.Equal(0, COrdersText.Suggested(beside, state, country, division, state.Province(Hamburg)));

		// Not beside: only the redeployment, which is ticked.
		List<COrderRow> away = COrdersText.Rows(state, country, new[] { division }, state.Province(Rostock));
		Assert.Equal(new[] { false, false, true }, away.Select(row => row.MayGive));
		Assert.Equal(2, COrdersText.Suggested(away, state, country, division, state.Province(Rostock)));

		// One of two that may not take a row greys it: the second stands in Rostock, not beside Hamburg.
		var other = new CUnit(EUnitKind.Division, Ger) { Id = ++state.LastUnitId, Location = Rostock, Name = "Other" };
		var second = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = other };
		second.Stats.CopyFrom(type.Stats);
		other.SubUnits.Add(second);
		country.Units.Add(other);
		List<COrderRow> both = COrdersText.Rows(state, country, new[] { division, other }, state.Province(Hamburg));
		Assert.Equal(new[] { false, false, true }, both.Select(row => row.MayGive));
	}
}
