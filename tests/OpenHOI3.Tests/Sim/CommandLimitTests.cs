using System;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Rules;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>How many units a command holds, against <c>docs/mechanics/attaching.md</c>, section 8.</summary>
public class CommandLimitTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static readonly CSubUnitType Infantry = new("infantry_brigade", EUnitBranch.Land);

	private int _nextId;

	private static (CGameState State, CCountry Germany) World(bool enhanced)
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var provinces = new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) };
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany });
		if (enhanced)
		{
			state.Rules.CopyFrom(new CRules(Array.Empty<EFix>()));
		}
		return (state, germany);
	}

	private CUnit Unit(CCountry country, EUnitKind kind, CUnit? parent = null, int brigades = 2)
	{
		var unit = new CUnit(kind, Ger) { Id = ++_nextId, Location = 1, Parent = parent };
		for (int i = 0; i < (kind == EUnitKind.Division ? brigades : 1); i++)
		{
			unit.AddSubUnit(new CSubUnit(Infantry) { Id = ++_nextId, Unit = unit });
		}
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

	private static void Define(CGameState state, string name, string value)
	{
		state.Defines.Set(CManpower.MilitarySection, name, CFixedPoint.Parse(value));
	}

	[Fact]
	public void A_faithful_game_keeps_the_originals_five_whatever_the_files_say()
	{
		(CGameState state, CCountry germany) = World(enhanced: false);
		Define(state, CCommandLimit.CorpsDefine, "3");
		Define(state, CCommandLimit.LoneBrigadeDefine, "1");
		germany.Modifier.Set(CCommandLimit.CorpsKind, CFixedPoint.FromInt(4));
		CUnit corps = Unit(germany, EUnitKind.Corps);
		CUnit division = Unit(germany, EUnitKind.Division);
		CUnit lone = Unit(germany, EUnitKind.Division, brigades: 1);

		Assert.Equal(5, CCommandLimit.Of(state, corps));
		for (int i = 0; i < 4; i++)
		{
			Unit(germany, EUnitKind.Division, corps);
		}
		Unit(germany, EUnitKind.Division, corps, brigades: 1);
		Assert.False(CDeployment.MayTakeSubordinate(state, corps, division));
		Assert.False(CDeployment.MayTakeSubordinate(state, corps, lone));
	}

	[Fact]
	public void An_enhanced_game_takes_each_levels_base_from_the_defines_and_five_with_none()
	{
		(CGameState state, CCountry germany) = World(enhanced: true);
		CUnit army = Unit(germany, EUnitKind.Army);
		CUnit corps = Unit(germany, EUnitKind.Corps);
		Assert.Equal(5, CCommandLimit.Of(state, corps));

		Define(state, CCommandLimit.CorpsDefine, "4");
		Define(state, CCommandLimit.ArmyGroupDefine, "7");
		Assert.Equal(4, CCommandLimit.Of(state, corps));
		Assert.Equal(5, CCommandLimit.Of(state, army));
		Assert.Equal(7, CCommandLimit.Of(state, Unit(germany, EUnitKind.ArmyGroup)));

		CUnit division = Unit(germany, EUnitKind.Division);
		for (int i = 0; i < 4; i++)
		{
			Assert.True(CDeployment.MayTakeSubordinate(state, corps, division));
			Unit(germany, EUnitKind.Division, corps);
		}
		Assert.False(CDeployment.MayTakeSubordinate(state, corps, division));
		// A theatre has no limit, and the ranks are asked as ever.
		CUnit theatre = Unit(germany, EUnitKind.Theatre);
		for (int i = 0; i < 9; i++)
		{
			Unit(germany, EUnitKind.Corps, theatre);
		}
		Assert.True(CDeployment.MayTakeSubordinate(state, theatre, division));
		Assert.False(CDeployment.MayTakeSubordinate(state, division, corps));
	}

	[Fact]
	public void The_countrys_modifier_and_technology_add_their_whole_part_and_never_leave_less_than_one()
	{
		(CGameState state, CCountry germany) = World(enhanced: true);
		Define(state, CCommandLimit.CorpsDefine, "4");
		Define(state, CCommandLimit.ArmyDefine, "4");
		CUnit corps = Unit(germany, EUnitKind.Corps);
		CUnit army = Unit(germany, EUnitKind.Army);

		// A triggered modifier's one, and a technology's half a level: the sum's whole part.
		germany.Modifier.Set(CCommandLimit.CorpsKind, CFixedPoint.FromInt(1));
		germany.Technology.Add(CCommandLimit.CorpsKind, CFixedPoint.Parse("0.5"));
		Assert.Equal(5, CCommandLimit.Of(state, corps));
		germany.Technology.Add(CCommandLimit.CorpsKind, CFixedPoint.Parse("0.5"));
		Assert.Equal(6, CCommandLimit.Of(state, corps));
		// A corps' kind is no army's.
		Assert.Equal(4, CCommandLimit.Of(state, army));

		germany.Modifier.Set(CCommandLimit.ArmyKind, CFixedPoint.FromInt(-9));
		Assert.Equal(1, CCommandLimit.Of(state, army));
		Assert.True(CDeployment.MayTakeSubordinate(state, army, corps));
	}

	[Fact]
	public void A_division_of_one_brigade_takes_no_place_where_the_defines_say_so()
	{
		(CGameState state, CCountry germany) = World(enhanced: true);
		Define(state, CCommandLimit.CorpsDefine, "2");
		CUnit corps = Unit(germany, EUnitKind.Corps);
		CUnit division = Unit(germany, EUnitKind.Division);
		CUnit lone = Unit(germany, EUnitKind.Division, brigades: 1);
		Unit(germany, EUnitKind.Division, corps);
		Unit(germany, EUnitKind.Division, corps, brigades: 1);

		// Without the define it is a unit like any other.
		Assert.False(CCommandLimit.SparesLoneBrigades(state));
		Assert.Equal(2, CCommandLimit.Counted(state, corps));
		Assert.False(CDeployment.MayTakeSubordinate(state, corps, division));
		Assert.False(CDeployment.MayTakeSubordinate(state, corps, lone));

		Define(state, CCommandLimit.LoneBrigadeDefine, "1");
		Assert.Equal(1, CCommandLimit.Counted(state, corps));
		Assert.True(CDeployment.MayTakeSubordinate(state, corps, division));
		Unit(germany, EUnitKind.Division, corps);
		Assert.False(CDeployment.MayTakeSubordinate(state, corps, division));
		// One more of one brigade goes under a full commander.
		Assert.True(CDeployment.MayTakeSubordinate(state, corps, lone));
		// A headquarters' one brigade is no division's.
		Assert.False(CCommandLimit.IsLoneBrigade(corps));
	}
}
