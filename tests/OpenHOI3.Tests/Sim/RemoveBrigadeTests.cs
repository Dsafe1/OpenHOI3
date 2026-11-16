using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// <c>remove_brigade</c> and the end of the hour's queue of units to be removed, against
/// <c>docs/mechanics/effects.md</c>, section 9 (<c>reversing/findings/FINDINGS-effectrun.md</c>, section 3.6).
/// </summary>
public class RemoveBrigadeTests
{
	private static readonly CCountryTag Ger = new("GER");

	private const int Berlin = 1;
	private const int Baltic = 2;

	private static readonly CBuilding Infra = new(0, "infra", 10, "infrastructure", CFixedPoint.Parse("0.1"));
	private static readonly CBuilding NavalBase = new(1, "naval_base", 10, "", CFixedPoint.Zero);
	private static readonly CBuilding AirBase = new(2, "air_base", 10, "", CFixedPoint.Zero);

	private static CSubUnitType Type(string key, EUnitBranch branch)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(30));
		type.Stats.Values.Set(CSubUnitType.DefaultOrganisation, CFixedPoint.FromInt(40));
		type.Stats.Values.Set(CSubUnitType.Officers, CFixedPoint.FromInt(100));
		return type;
	}

	private static readonly CSubUnitType Infantry = Type("infantry_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Destroyer = Type("destroyer", EUnitBranch.Naval);

	private static (CGameState State, CCountry Germany) World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin };
		germany.OfficerRatio = CFixedPoint.Parse("1.4");
		germany.Officers = CFixedPoint.FromInt(300);
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, COfficers.MostRatio, CFixedPoint.Parse("1.4"));
		var berlin = new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger };
		berlin.SetBuildingLevel(Infra, CFixedPoint.FromInt(10));
		var state = new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), berlin, new CProvince(Baltic, "Baltic", isSea: true) },
			new[] { germany }, buildings: new[] { Infra, NavalBase, AirBase }, defines: defines);
		CProvinceModifiers.Rebuild(state);
		return (state, germany);
	}

	private static int _ids = 100;

	private static CUnit Unit(CCountry country, EUnitKind kind = EUnitKind.Division, CUnit? parent = null, int location = Berlin)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++_ids, Location = location, Parent = parent };
		if (parent is null)
		{
			country.Units.Add(unit);
		}
		else
		{
			parent.Children.Add(unit);
		}
		return unit;
	}

	private static CSubUnit Brigade(CUnit unit, string name, CSubUnitType? type = null, string strength = "30")
	{
		var brigade = new CSubUnit(type ?? Infantry) { Id = ++_ids, Name = name, Unit = unit, Strength = CFixedPoint.Parse(strength) };
		unit.SubUnits.Add(brigade);
		return brigade;
	}

	private static void Run(CGameState state, string text)
	{
		CEffects.Run(state, ScriptWorld.Effects(text), CEventScope.Start(Ger));
	}

	[Fact]
	public void A_brigade_among_others_goes_at_once_and_its_name_is_matched_with_case()
	{
		(CGameState state, CCountry germany) = World();
		CUnit unit = Unit(germany);
		CSubUnit first = Brigade(unit, "1. Infanterie");
		CSubUnit second = Brigade(unit, "2. Infanterie");

		Run(state, "remove_brigade = \"1. infanterie\"");
		Assert.Equal(2, unit.SubUnits.Count);

		Run(state, "remove_brigade = \"1. Infanterie\"");

		Assert.Equal(new[] { second }, unit.SubUnits);
		Assert.Null(first.Unit);
		Assert.Empty(state.PendingUnitRemovals);
		Assert.Equal(CFixedPoint.FromInt(300), germany.Officers);     // nothing given back or taken
	}

	[Fact]
	public void Only_the_first_brigade_of_the_name_goes_its_units_in_the_order_they_were_made()
	{
		(CGameState state, CCountry germany) = World();
		CUnit later = Unit(germany);
		CUnit earlier = Unit(germany);
		later.Id = 900;
		earlier.Id = 800;
		Brigade(later, "Twin");
		Brigade(later, "Other");
		Brigade(earlier, "Twin");
		Brigade(earlier, "Other");

		Run(state, "remove_brigade = Twin");

		Assert.Equal(2, later.SubUnits.Count);
		Assert.Single(earlier.SubUnits);
	}

	[Fact]
	public void A_ship_taken_from_its_fleet_goes_on_the_list_of_the_sunk()
	{
		(CGameState state, CCountry germany) = World();
		CUnit fleet = Unit(germany, EUnitKind.Navy, location: Baltic);
		CSubUnit leaving = Brigade(fleet, "Z1", Destroyer);
		Brigade(fleet, "Z2", Destroyer);

		Run(state, "remove_brigade = Z1");

		Assert.Equal(new[] { new CSunkShip(Ger, leaving) }, state.SunkShips);
		Assert.Equal(1, CSunkShips.OursSunk(state, Ger));
	}

	[Fact]
	public void A_units_only_brigade_stays_until_the_hour_ends_and_then_the_unit_goes_with_its_officers()
	{
		(CGameState state, CCountry germany) = World();
		CUnit unit = Unit(germany);
		CSubUnit only = Brigade(unit, "Lonely", strength: "15");

		Run(state, "remove_brigade = Lonely remove_brigade = Lonely");

		// Still there for the rest of the hour, and queued twice by the two lines.
		Assert.Same(unit, only.Unit);
		Assert.True(CConditions.Holds(state, ScriptWorld.Condition("brigade_exist = Lonely"), CEventScope.Start(Ger)));
		Assert.Equal(new[] { unit.Id, unit.Id }, state.PendingUnitRemovals);

		CTick.RunHour(state);

		Assert.True(unit.IsRemoved);
		Assert.Empty(germany.Units);
		Assert.Empty(state.PendingUnitRemovals);
		// 100 officers at half strength, times the ratio of 1.4: 70 off the 300.
		Assert.Equal(CFixedPoint.FromInt(230), germany.Officers);
	}

	[Fact]
	public void What_a_removed_unit_commanded_is_left_at_the_top_and_not_handed_up()
	{
		(CGameState state, CCountry germany) = World();
		CUnit army = Unit(germany, EUnitKind.Army);
		CUnit corps = Unit(germany, EUnitKind.Corps, army);
		Brigade(corps, "Korps-HQ");
		CUnit division = Unit(germany, EUnitKind.Division, corps);
		Brigade(division, "Div");

		Run(state, "remove_brigade = Korps-HQ");
		CPendingUnitRemovals.Drain(state);

		Assert.True(corps.IsRemoved);
		Assert.Null(division.Parent);
		Assert.Contains(division, germany.Units);
		Assert.Empty(army.Children);
		Assert.False(division.IsOrphaned);
	}

	[Fact]
	public void A_theatres_units_are_marked_orphans_and_a_unit_gone_by_the_hours_end_is_passed_over()
	{
		(CGameState state, CCountry germany) = World();
		CUnit theatre = Unit(germany, EUnitKind.Theatre);
		Brigade(theatre, "HQ");
		CUnit division = Unit(germany, EUnitKind.Division, theatre);
		Brigade(division, "Div");
		state.PendingUnitRemovals.Add(12345);

		Run(state, "remove_brigade = HQ");
		CPendingUnitRemovals.Drain(state);

		Assert.True(theatre.IsRemoved);
		Assert.True(division.IsOrphaned);
		Assert.Equal(new[] { division }, germany.Units);
	}
}
