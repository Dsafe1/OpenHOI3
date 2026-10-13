using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the air orders window decides beyond its list, against
/// <c>docs/mechanics/fleetsandair.md</c>, section 4: the row ticked first, the sliders,
/// the boxes of the hours, the dates and the map's colours. Germany's Berlin has an
/// airfield; Warsaw is Poland's, at war with it, and makes IC; Baltic is sea.
/// </summary>
public class AirOrdersTextTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private const int Berlin = 1;
	private const int Warsaw = 2;
	private const int Baltic = 3;

	private static readonly CBuilding AirBase = new(0, "air_base", 10, "", CFixedPoint.Zero);

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" };
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		var berlin = new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger, CentreX = 10, CentreY = 10 };
		berlin.SetBuildingLevel(AirBase, CFixedPoint.FromInt(4));
		var warsaw = new CProvince(Warsaw, "Warsaw", isSea: false) { Owner = Pol, Controller = Pol, CentreX = 20, CentreY = 10 };
		warsaw.Modifier.Set(CIndustrialCapacity.ProvinceIc, CFixedPoint.FromInt(3));
		var baltic = new CProvince(Baltic, "Baltic", isSea: true) { CentreX = 10, CentreY = 20 };
		return new CGameState(new CDate(1936, 6, 1),
			new[] { new CProvince(0, "", isSea: false), berlin, warsaw, baltic },
			new[] { germany, poland }, buildings: new[] { AirBase }, wars: new[] { war })
		{
			MapWidth = 1000,
			MapHeight = 1000,
			StartDate = new CDate(1936, 1, 1),
			EndDate = new CDate(1948, 1, 1),
		};
	}

	private static CUnit Unit(CGameState state, CCountry germany, params (bool Bomber, string Soft, string Strategic, string Sea)[] wings)
	{
		var unit = new CUnit(EUnitKind.Air, Ger) { Id = ++state.LastUnitId, Location = Berlin, Base = Berlin, Name = "air" };
		foreach ((bool bomber, string soft, string strategic, string sea) in wings)
		{
			var type = new CSubUnitType(bomber ? "bomber" : "fighter", EUnitBranch.Air);
			type.Flags[CAirModifiers.IsBomber] = bomber;
			type.Stats.Values.Set(CUnitOrders.Range, CFixedPoint.FromInt(800));
			type.Stats.Values.Set("soft_attack", CFixedPoint.Parse(soft));
			type.Stats.Values.Set("strategic_attack", CFixedPoint.Parse(strategic));
			type.Stats.Values.Set("sea_attack", CFixedPoint.Parse(sea));
			var wing = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Name = "w" };
			wing.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(wing);
		}
		germany.Units.Add(unit);
		return unit;
	}

	private static readonly (bool, string, string, string) Fighter = (false, "1", "0", "0");
	private static readonly (bool, string, string, string) Strategic = (true, "2", "8", "0");
	private static readonly (bool, string, string, string) Tactical = (true, "6", "2", "0");
	private static readonly (bool, string, string, string) SeaFighter = (false, "1", "0", "3");

	private static List<COrderRow> Rows(params EUnitOrder[] orders)
	{
		var rows = new List<COrderRow>();
		foreach (EUnitOrder order in orders)
		{
			rows.Add(new COrderRow(COrdersText.NameKey(order), order, true));
		}
		return rows;
	}

	private static EUnitOrder Ticked(CGameState state, CCountry germany, CUnit unit, int province,
		List<COrderRow> rows, EUnitOrder keep = EUnitOrder.None)
	{
		int row = CAirOrdersText.Suggested(rows, state, germany, unit, state.Province(province), keep);
		return row < 0 ? EUnitOrder.None : rows[row].Order;
	}

	[Fact]
	public void FightersAreSentForSuperiorityOverAnEnemyAndToInterceptElsewhere()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany, Fighter, Fighter, Tactical);
		List<COrderRow> rows = Rows(EUnitOrder.AirSuperiority, EUnitOrder.AirIntercept);

		Assert.Equal(EUnitOrder.AirSuperiority, Ticked(state, germany, unit, Warsaw, rows));
		Assert.Equal(EUnitOrder.AirIntercept, Ticked(state, germany, unit, Baltic, rows));
	}

	[Fact]
	public void AChangeOfBaseComesFirstAndTheOrderKeptBeforeTheLadder()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany, Fighter);
		List<COrderRow> rows = Rows(EUnitOrder.AirSuperiority, EUnitOrder.AirIntercept, EUnitOrder.RebaseAir);

		Assert.Equal(EUnitOrder.RebaseAir, Ticked(state, germany, unit, Berlin, rows, EUnitOrder.AirIntercept));
		Assert.Equal(EUnitOrder.AirIntercept, Ticked(state, germany, unit, Warsaw, rows, EUnitOrder.AirIntercept));
	}

	[Fact]
	public void ASeaAttackTakesTheRowFromTheFighters()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany, SeaFighter);
		List<COrderRow> rows = Rows(EUnitOrder.AirSuperiority, EUnitOrder.AirIntercept, EUnitOrder.NavalStrike);

		Assert.Equal(EUnitOrder.NavalStrike, Ticked(state, germany, unit, Baltic, rows));
	}

	[Fact]
	public void BombersGoForTheRunwayTheIndustryOrTheGround()
	{
		CGameState state = State(out CCountry germany);
		List<COrderRow> rows = Rows(EUnitOrder.StrategicBombing, EUnitOrder.RunwayCratering, EUnitOrder.GroundAttack,
			EUnitOrder.Interdiction);

		CUnit heavy = Unit(state, germany, Strategic, Strategic, Tactical);
		Assert.Equal(EUnitOrder.StrategicBombing, Ticked(state, germany, heavy, Warsaw, rows));
		state.Province(Warsaw).SetBuildingLevel(AirBase, CFixedPoint.FromInt(1));
		Assert.Equal(EUnitOrder.RunwayCratering, Ticked(state, germany, heavy, Warsaw, rows));

		CUnit light = Unit(state, germany, Tactical, Tactical, Strategic);
		Assert.Equal(EUnitOrder.GroundAttack, Ticked(state, germany, light, Warsaw, rows));
	}

	[Fact]
	public void ASliderIsTakenOnlyOnceItHasMovedATwentiethOfWhereItIs()
	{
		Assert.False(CAirOrdersText.Takes(500, 520));
		Assert.True(CAirOrdersText.Takes(500, 530));
		Assert.False(CAirOrdersText.Takes(0, 0));
		Assert.True(CAirOrdersText.Takes(16, 0));
		Assert.True(CAirOrdersText.Takes(0, 1));
	}

	[Fact]
	public void TheSlidersGiveAShareOfTheUnitsRangeAndOfATurn()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany, Fighter);
		Func<string, string> localise = key => key switch
		{
			"ORDER_RANGE_VAL" => "$VAL$ kms",
			_ => "$VAL$ Deg",
		};

		Assert.Equal(CFixedPoint.FromInt(200), CAirOrdersText.RangeAt(unit, 250));
		Assert.Equal("200 kms", CAirOrdersText.RangeFigure(localise, CAirOrdersText.RangeAt(unit, 250)));
		Assert.Equal("90.00 Deg", CAirOrdersText.DegreesFigure(localise, "ORDER_WIDTH_VAL", CAirOrdersText.TurnAt(250)));
		Assert.Equal("5.76 Deg", CAirOrdersText.DegreesFigure(localise, "ORDER_ANGLE_VAL", CAirOrdersText.TurnAt(16)));
	}

	[Fact]
	public void TheBoxesOfTheHoursAreOneChoiceAndClearingTheTickedOneChangesNothing()
	{
		int time = CAirOrdersText.ChooseTime(CMission.DayAndNight, new CTimeBoxes(false, true, true), out CTimeBoxes shown);
		Assert.Equal(CMission.ByNight, time);
		Assert.Equal(new CTimeBoxes(false, true, false), shown);

		time = CAirOrdersText.ChooseTime(time, new CTimeBoxes(true, true, false), out shown);
		Assert.Equal(CMission.ByDay, time);
		Assert.Equal(new CTimeBoxes(true, false, false), shown);

		time = CAirOrdersText.ChooseTime(time, new CTimeBoxes(false, false, false), out shown);
		Assert.Equal(CMission.ByDay, time);
		Assert.Equal(new CTimeBoxes(false, false, false), shown);
	}

	[Fact]
	public void ADateIsHeldBetweenTheDefinesStartAndEnd()
	{
		CGameState state = State(out _);
		long now = state.TotalHours;

		Assert.Equal(now + 24, CAirOrdersText.Step(state, now, EDateStep.Day, 1));
		Assert.Equal(now + 8760, CAirOrdersText.Step(state, now, EDateStep.Year, 1));
		Assert.Equal((long)new CDate(1936, 1, 1).DayNumber * 24, CAirOrdersText.Step(state, now, EDateStep.Year, -1));
		Assert.Equal((long)new CDate(1948, 1, 1).DayNumber * 24,
			CAirOrdersText.Step(state, (long)new CDate(1947, 12, 31).DayNumber * 24, EDateStep.Month, 1));
		Assert.Equal((long)new CDate(1936, 7, 1).DayNumber * 24 + 5, CAirOrdersText.AddMonths(now + 5, 1));
	}

	[Fact]
	public void TheMapShowsTheUnitsReachAndTheMissionsList()
	{
		CGameState state = State(out CCountry germany);
		CUnit unit = Unit(state, germany, Fighter);
		var listed = new HashSet<int> { Warsaw };
		CColor? Of(int id) => CAirAreaColours.Of(state, unit, EUnitOrder.AirSuperiority, listed, state.Province(id));

		Assert.Equal(new CColor(0.5f, 0.8f, 0.8f), Of(Warsaw));
		Assert.Equal(0.9f, Of(Berlin)!.Value.G, 3);
		Assert.Equal(0.9f, Of(Baltic)!.Value.G, 3);
		listed.Clear();
		Assert.Equal(0.9f, Of(Warsaw)!.Value.R, 3);
	}
}
