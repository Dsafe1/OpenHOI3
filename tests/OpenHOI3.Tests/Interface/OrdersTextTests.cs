using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

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
			new[] { germany }, buildings: new[] { NavalBase, AirBase });
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
		Assert.Equal(new[] { true, false, false, false, false, false, false, false, false }, toSea.Select(row => row.MayGive));
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
	public void TheChangeOfBaseIsTheOnlyOrderOfAnAirUnitThatCanBeGivenAndIsTicked()
	{
		CGameState state = State(out CCountry country);
		CUnit fighters = Unit(state, country, EUnitKind.Air, Kiel, ("air_attack", "5"));

		List<COrderRow> rows = COrdersText.Rows(state, country, fighters, state.Province(Hamburg));
		Assert.Equal(new[] { false, false, false, true }, rows.Select(row => row.MayGive));
		Assert.Equal(3, COrdersText.Suggested(rows, state, country, fighters, state.Province(Hamburg)));
		Assert.Equal(EUnitOrder.RebaseAir, rows[3].Order);

		// Rostock has no air base: the row is there and cannot be ticked, and nothing is.
		rows = COrdersText.Rows(state, country, fighters, state.Province(Rostock));
		Assert.False(rows[3].MayGive);
		Assert.Equal(-1, COrdersText.Suggested(rows, state, country, fighters, state.Province(Rostock)));
		Assert.Equal(COrdersText.AirWindow, COrdersText.WindowFor(fighters));
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
}
