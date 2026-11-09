using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Alerts;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Settings;
using OpenHOI3.Sim.Units;
using Xunit;

namespace OpenHOI3.Tests.Interface;

/// <summary>What the alerts test and say, against <c>docs/mechanics/alerts.md</c>.</summary>
public class AlertPassTests
{
	private static readonly CCountryTag Eng = new("ENG");
	private static readonly CCountryTag Fra = new("FRA");

	private static string Word(string key) => key;

	private static (CGameState State, CCountry England, CCountry France) World()
	{
		var england = new CCountry { Tag = Eng, Name = "England", DefinitionFile = "" };
		var france = new CCountry { Tag = Fra, Name = "France", DefinitionFile = "" };
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "London", isSea: false) { Owner = Eng, Controller = Eng },
			new CProvince(2, "Paris", isSea: false) { Owner = Fra, Controller = Fra },
		};
		england.Capital = 1;
		england.ActingCapital = 1;
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { england, france });
		state.Defines.Set("diplomacy", "NAP_EXPIRY_ALERT_MONTHS_AHEAD", CFixedPoint.Zero);
		return (state, england, france);
	}

	private static CUnit Unit(CCountry country, int province, int id = 1)
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		var unit = new CUnit(EUnitKind.Army, country.Tag) { Id = id, Location = province, Name = "Unit " + id };
		unit.SubUnits.Add(new CSubUnit(type));
		country.Units.Add(unit);
		return unit;
	}

	private static List<EAlert> Up(CGameState state, CCountry player) => CAlertPass.Run(Word, state, player).Select(alert => alert.Alert).ToList();

	[Fact]
	public void Mobilised_at_peace_is_up_and_at_war_it_is_not_asked()
	{
		(CGameState state, CCountry england, _) = World();
		england.Mobilised = true;
		Assert.Contains(EAlert.MobilizedInPeace, Up(state, england));
	}

	[Fact]
	public void A_unit_short_of_supplies_is_named_with_its_province()
	{
		(CGameState state, CCountry england, _) = World();
		CUnit unit = Unit(england, 1);
		unit.SuppliesReceived = new CFixedPoint(999);
		CAlertShown alert = CAlertPass.Run(Word, state, england).Single(shown => shown.Alert == EAlert.BadSupply);
		Assert.Equal("alert_bad_supply_instant", alert.Tooltip.Text);
		Assert.Same(unit, CAlertPass.NextUnit(state, england, EAlert.BadSupply, null));
	}

	[Fact]
	public void A_full_unit_raises_no_supply_alert()
	{
		(CGameState state, CCountry england, _) = World();
		Unit(england, 1);
		Assert.DoesNotContain(EAlert.BadSupply, Up(state, england));
	}

	[Fact]
	public void The_click_goes_round_the_units_that_are_short()
	{
		(CGameState state, CCountry england, _) = World();
		CUnit first = Unit(england, 1, 1);
		CUnit second = Unit(england, 1, 2);
		first.SuppliesReceived = CFixedPoint.Zero;
		second.SuppliesReceived = CFixedPoint.Zero;
		Assert.Same(second, CAlertPass.NextUnit(state, england, EAlert.BadSupply, first));
		Assert.Same(first, CAlertPass.NextUnit(state, england, EAlert.BadSupply, second));
	}

	[Fact]
	public void Troops_in_a_neutral_country_are_foreign_and_with_access_are_not()
	{
		(CGameState state, CCountry england, CCountry france) = World();
		Unit(england, 2);
		Assert.Contains(EAlert.TroopsInForeignTerritory, Up(state, england));
		CGameState.Status(england, france).HasMilitaryAccess = true;
		Assert.DoesNotContain(EAlert.TroopsInForeignTerritory, Up(state, england));
	}

	[Fact]
	public void A_law_earlier_in_the_file_is_offered_and_one_later_is_not()
	{
		(CGameState state, CCountry england, _) = World();
		var group = new CLawGroup("civil_law", 1);
		var early = new CLaw("civil_law", new OpenHOI3.Sim.Modifiers.CModifier("early"));
		var current = new CLaw("civil_law", new OpenHOI3.Sim.Modifiers.CModifier("current"));
		var late = new CLaw("civil_law", new OpenHOI3.Sim.Modifiers.CModifier("late"));
		group.Laws.AddRange(new[] { early, current, late });
		var withGroups = new CGameState(new CDate(1936, 1, 1), state.Provinces.ToArray(), new[] { england }) { LawGroups = new[] { group } };
		withGroups.Defines.Set("diplomacy", "NAP_EXPIRY_ALERT_MONTHS_AHEAD", CFixedPoint.Zero);
		england.Laws["civil_law"] = current;
		CAlertShown alert = CAlertPass.Run(Word, withGroups, england).Single(shown => shown.Alert == EAlert.PossibleLaw);
		Assert.Equal("alert_possible_law_instant", alert.Tooltip.Text);
		england.Laws["civil_law"] = early;
		Assert.DoesNotContain(EAlert.PossibleLaw, Up(withGroups, england));
	}

	[Fact]
	public void Handing_politics_to_the_ai_stops_the_law_alert()
	{
		(CGameState state, CCountry england, _) = World();
		state.Automation.Flags[(int)EAutomation.Politics] = 1;
		Assert.DoesNotContain(EAlert.PossibleLaw, Up(state, england));
	}

	[Fact]
	public void Production_spare_is_what_it_was_given_less_what_it_used()
	{
		(CGameState state, CCountry england, _) = World();
		england.TotalIc = 100;
		england.ProductionShares[(int)EProductionCategory.Production] = CFixed15.FromInt(1);
		england.ProductionSpent = CFixedPoint.FromInt(40);
		Assert.Equal(CFixedPoint.FromInt(60), CProduction.SpareIn(state, england, EProductionCategory.Production));
		Assert.Equal(60, CProduction.AvailableIc(state, england));
		Assert.Contains(EAlert.WastedIc, Up(state, england));
	}

	[Fact]
	public void Supplies_are_never_spare()
	{
		(CGameState state, CCountry england, _) = World();
		england.TotalIc = 100;
		england.ProductionShares[(int)EProductionCategory.Supplies] = CFixed15.FromInt(1);
		Assert.Equal(CFixedPoint.Zero, CProduction.SpareIn(state, england, EProductionCategory.Supplies));
	}
}
