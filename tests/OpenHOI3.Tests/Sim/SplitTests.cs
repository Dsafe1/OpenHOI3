using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The new-unit button's rule, Ctrl's, and the split, against <c>docs/mechanics/unitpanel.md</c>, section 7.1. Berlin is
/// Germany's; a division holds four brigades.
/// </summary>
public class SplitTests
{
	private static readonly CCountryTag Ger = new("GER");
	private const int Berlin = 1;

	private static readonly CSubUnitType Infantry = new("infantry_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Headquarters = HeadquartersType();

	private static CSubUnitType HeadquartersType()
	{
		var type = new CSubUnitType("hq_brigade", EUnitBranch.Land);
		type.Flags[CUnitAvailability.IsBuildable] = false;
		type.Stats.Values.Set(CCommandReach.RadioStrength, CFixedPoint.FromInt(2));
		return type;
	}

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[] { germany });
		state.Defines.Set(CManpower.MilitarySection, CUnitAvailability.BrigadesInDivision, CFixedPoint.FromInt(4));
		return state;
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, params CSubUnitType[] brigades)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = Berlin, Name = $"Unit {state.LastUnitId}" };
		foreach (CSubUnitType type in brigades)
		{
			var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Name = type.Key };
			brigade.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(brigade);
		}
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void The_button_wants_two_brigades_three_with_a_leader_and_Ctrl_no_headquarters_and_no_march()
	{
		CGameState state = State(out CCountry germany);
		CUnit lone = Unit(state, germany, EUnitKind.Division, Infantry);
		CUnit pair = Unit(state, germany, EUnitKind.Division, Infantry, Infantry);
		CUnit corps = Unit(state, germany, EUnitKind.Corps, Headquarters, Infantry, Infantry);

		Assert.False(CUnitSplit.MayPress(lone));
		Assert.True(CUnitSplit.MayPress(pair));
		pair.Leader = new CLeader(1) { Skill = 1 };
		Assert.False(CUnitSplit.MayPress(pair));             // a leader asks for a third brigade
		pair.Leader = null;

		Assert.True(CUnitSplit.MaySplit(pair));
		Assert.True(CUnitSplit.MayPress(corps));
		Assert.False(CUnitSplit.MaySplit(corps));            // a headquarters brigade keeps Ctrl's split off
		pair.Path.Add(Berlin);
		Assert.False(CUnitSplit.MaySplit(pair));             // on its way
	}

	[Fact]
	public void A_split_makes_a_unit_for_every_brigade_but_the_last_and_takes_the_leaders_off_land_units()
	{
		CGameState state = State(out CCountry germany);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry, Infantry, Infantry);
		CSubUnit last = division.SubUnits[2];
		var leader = new CLeader(1) { Skill = 2 };
		CLeaders.SetLeader(state, division, leader, false, false);
		int before = state.LastUnitId;

		var command = new CUnitSplitCommand(Ger, division.Id);
		Assert.True(command.MayExecute(state));
		command.Execute(state);

		Assert.Same(last, Assert.Single(division.SubUnits));
		CUnit[] made = germany.Units.Where(unit => unit.Id > before).ToArray();
		Assert.Equal(2, made.Length);
		Assert.All(made, unit =>
		{
			Assert.Single(unit.SubUnits);
			Assert.Equal(EUnitKind.Division, unit.Kind);
			Assert.Equal(Berlin, unit.Location);
			Assert.Null(unit.Leader);
		});
		Assert.Null(division.Leader);
		Assert.Null(leader.Unit);
	}
}
