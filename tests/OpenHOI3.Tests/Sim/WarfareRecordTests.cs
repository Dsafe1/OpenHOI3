using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.StrategicWarfare;

namespace OpenHOI3.Tests.Sim;

/// <summary>The Strategic Warfare Manager's rings and charts, against <c>docs/mechanics/strategicwarfare.md</c>.</summary>
public class WarfareRecordTests
{
	private static readonly CFixed15 One = CFixed15.One;

	[Fact]
	public void TheDayRingStartsOneOn()
	{
		var category = new CWarfareCategory(13);
		Assert.Equal(1, category.DayRing.Offset);
		Assert.Equal(0, category.MonthRing.Offset);
		Assert.Equal(0, category.DayRing.Current);
	}

	[Fact]
	public void ATallyGoesInTheSlotBeforeTheOffset()
	{
		var category = new CWarfareCategory(13);
		category.Add(2, One);
		Assert.Equal(One.Raw, category.DayRing[0, 2]);
		Assert.Equal(0, category.DayRing[1, 2]);
	}

	[Fact]
	public void TheMonthsElementIsTheDaysSoFar()
	{
		var category = new CWarfareCategory(13);
		category.Add(0, One);
		category.AdvanceDay();
		category.Add(0, One);
		category.AdvanceDay();
		Assert.Equal(2 * One.Raw, category.MonthRing[0, 0]);
		Assert.Equal(2 * One.Raw, category.YearRing[0, 0]);
	}

	[Fact]
	public void TheFirstMonthClosesAfter29DaysOfTallies()
	{
		var category = new CWarfareCategory(13);
		for (int day = 0; day < 29; day++)
		{
			category.Add(0, One);
			category.AdvanceDay();
		}
		Assert.Equal(0, category.DayRing.Offset);
		Assert.True(category.DayRing.IsFull);
		Assert.Equal(1, category.MonthRing.Offset);
		Assert.Equal(29 * One.Raw, category.MonthRing[0, 0]);
		Assert.Equal(0, category.MonthRing[1, 0]);
	}

	[Fact]
	public void AWrappedDayRingKeepsRollingThirtyDaysInTheMonth()
	{
		var category = new CWarfareCategory(13);
		for (int day = 0; day < 40; day++)
		{
			category.Add(0, One);
			category.AdvanceDay();
		}
		Assert.Equal(11, category.DayRing.Offset);
		Assert.Equal(28 * One.Raw, category.DayRing.Sum(0));
		Assert.Equal(1, category.MonthRing.Offset);
	}

	[Fact]
	public void AnUnwrappedSeriesIsNewestFirstAndPadded()
	{
		var ring = new CWarfareRing(4) { Offset = 2 };
		ring[0, 0] = 5;
		ring[1, 0] = 6;
		ring[2, 0] = 7;
		Assert.Equal(new long[] { 7, 6, 5, 0 }, ring.Series(0));
	}

	[Fact]
	public void AWrappedSeriesStartsBeforeTheOffset()
	{
		var ring = new CWarfareRing(3) { Offset = 1, IsFull = true };
		ring[0, 0] = 5;
		ring[1, 0] = 6;
		ring[2, 0] = 7;
		Assert.Equal(new long[] { 5, 7, 6 }, ring.Series(0));
	}

	[Fact]
	public void AHeightIsAWholePercentOfTheScale()
	{
		long scale = 10 * CFixed15.Scale;
		Assert.Equal(50, CWarfareText.Height(5 * CFixed15.Scale, scale));
		Assert.Equal(100, CWarfareText.Height(scale, scale));
		Assert.Equal(0, CWarfareText.Height(0, scale));
	}

	[Fact]
	public void TheScaleIsNeverLessThanAHundredth()
	{
		Assert.Equal(CWarfareText.LeastScale, CWarfareText.Scale(new[] { new CWarfareSeries(new long[] { 0 }, CColor.Gray) }));
	}

	[Theory]
	[InlineData(1, 1, 3)]
	[InlineData(0, 10, 2)]
	[InlineData(10, 0, 1)]
	public void TheNavalComparatorIsEqualWithinEightIc(int first, int second, int frame)
	{
		Assert.Equal(frame, CWarfareText.NavalFrame(first * CFixed15.Scale, second * CFixed15.Scale));
	}

	[Theory]
	[InlineData(0, 10, 1)]
	[InlineData(10, 0, 2)]
	public void TheLandComparatorIsTheOtherWayRound(int first, int second, int frame)
	{
		Assert.Equal(frame, CWarfareText.LandIcFrame(first * CFixed15.Scale, second * CFixed15.Scale));
	}

	[Fact]
	public void RunningWarsAreListedFirst()
	{
		var ledger = new CWarfareLedger();
		ledger.Records.Add(new CWarfareRecord(new CCountryTag("AAA"), 0, 13) { Ended = true });
		ledger.Records.Add(new CWarfareRecord(new CCountryTag("BBB"), 0, 13));
		Assert.Equal(new[] { "BBB", "AAA" }, CWarfareText.Rows(ledger).Select(record => record.Enemy.Value));
	}

	private static OpenHOI3.Sim.Units.CSubUnitType Infantry()
	{
		var type = new OpenHOI3.Sim.Units.CSubUnitType("infantry_brigade", OpenHOI3.Sim.Units.EUnitBranch.Land);
		type.Stats.Values.Set(OpenHOI3.Sim.Units.CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(OpenHOI3.Sim.Units.CSubUnitStats.BuildCostIc, CFixedPoint.FromInt(4));
		type.Stats.Values.Set(OpenHOI3.Sim.Units.CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(3));
		type.Stats.Values.Set("repair_cost_multiplier", CFixedPoint.One);
		return type;
	}

	private static (CGameState State, OpenHOI3.Sim.Combat.CCombat Combat, CWarfareRecord First, CWarfareRecord Second) LandBattle()
	{
		var ger = new CCountry { Tag = new CCountryTag("GER"), Name = "GER", DefinitionFile = "" };
		var fra = new CCountry { Tag = new CCountryTag("FRA"), Name = "FRA", DefinitionFile = "" };
		var eng = new CCountry { Tag = new CCountryTag("ENG"), Name = "ENG", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new OpenHOI3.Sim.Provinces.CProvince(0, "", isSea: false) }, new[] { ger, fra, eng });
		state.Warfare.Player = ger.Tag;
		CWarfareRecord first = state.Warfare.Add(state, fra.Tag);
		CWarfareRecord second = state.Warfare.Add(state, eng.Tag);
		var combat = new OpenHOI3.Sim.Combat.CCombat(0);
		combat.Attacker.AddCountry(ger.Tag);
		combat.Defender.AddCountry(fra.Tag);
		combat.Defender.AddCountry(eng.Tag);
		combat.Attacker.Units.Add(Brigade(ger.Tag));
		combat.Defender.Units.Add(Brigade(fra.Tag));
		return (state, combat, first, second);
	}

	private static OpenHOI3.Sim.Units.CUnit Brigade(CCountryTag owner)
	{
		var unit = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Division, owner);
		var brigade = new OpenHOI3.Sim.Units.CSubUnit(Infantry()) { Unit = unit, Strength = CFixedPoint.FromInt(30), PendingStrengthDamage = 300000 };
		OpenHOI3.Sim.Units.CUnits.RebuildStats(brigade);
		unit.SubUnits.Add(brigade);
		return unit;
	}

	[Fact]
	public void WhatThePlayerLostInALandHourGoesWholeOnEveryEnemysRecord()
	{
		(CGameState state, OpenHOI3.Sim.Combat.CCombat combat, CWarfareRecord france, CWarfareRecord britain) = LandBattle();
		CWarfareTally.LandHour(state, combat);
		long ours = france.Categories[CWarfareRecord.Land].DayRing[0, 0];
		Assert.True(ours > 0);
		Assert.Equal(ours, britain.Categories[CWarfareRecord.Land].DayRing[0, 0]);
		// France's own loss goes on France's record alone; Britain has no unit in it.
		Assert.True(france.Categories[CWarfareRecord.Land].DayRing[0, 1] > 0);
		Assert.Equal(0, britain.Categories[CWarfareRecord.Land].DayRing[0, 1]);
	}

	[Fact]
	public void ABattleThePlayerIsNotInCountsForNothing()
	{
		(CGameState state, OpenHOI3.Sim.Combat.CCombat combat, CWarfareRecord france, _) = LandBattle();
		state.Warfare.Player = new CCountryTag("ITA");
		CWarfareTally.LandHour(state, combat);
		Assert.Equal(0, france.Categories[CWarfareRecord.Land].DayRing[0, 0]);
	}

	[Fact]
	public void ALandHoursLossesAreNotedForTheSideAHundredTimesOver()
	{
		(_, OpenHOI3.Sim.Combat.CCombat combat, _, _) = LandBattle();
		combat.Attacker.NoteLosses();
		// 300 points of notes is three of strength; a brigade is written as its men.
		Assert.Equal(300000, combat.Attacker.Destroyed["infantry_brigade"]);
	}

	[Fact]
	public void ABrigadesLossIsPricedByTheRateOfItsStrength()
	{
		var type = new OpenHOI3.Sim.Units.CSubUnitType("infantry_brigade", OpenHOI3.Sim.Units.EUnitBranch.Land);
		type.Stats.Values.Set(OpenHOI3.Sim.Units.CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(OpenHOI3.Sim.Units.CSubUnitStats.BuildCostIc, CFixedPoint.FromInt(4));
		type.Stats.Values.Set(OpenHOI3.Sim.Units.CSubUnitStats.BuildCostManpower, CFixedPoint.FromInt(3));
		type.Stats.Values.Set("repair_cost_multiplier", CFixedPoint.One);
		var country = new CCountry { Tag = new CCountryTag("GER"), Name = "GER", DefinitionFile = "" };
		// A rate of half a point, four IC at one point of multiplier: three points of strength come to 24.
		(long ic, long men) = CWarfareCosts.Partial(country, type, 3 * CFixed15.Scale);
		Assert.Equal(24 * CFixed15.Scale, ic);
		Assert.InRange(men, CFixed15.Scale * 3 / 10 - 16, CFixed15.Scale * 3 / 10 + 16);
	}
}
