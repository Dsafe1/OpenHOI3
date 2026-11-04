using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// When units may be merged, which of them the rest go into and what the merge does, against
/// <c>docs/mechanics/selection.md</c>, section 5.4. Berlin and Potsdam are Germany's; a division holds four brigades.
/// </summary>
public class MergeTests
{
	private static readonly CCountryTag Ger = new("GER");
	private const int Berlin = 1;
	private const int Potsdam = 2;

	private static readonly CSubUnitType Infantry = TypeWithStrength("infantry_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Destroyer = TypeWithStrength("destroyer", EUnitBranch.Naval);

	private static CSubUnitType TypeWithStrength(string key, EUnitBranch branch)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(10));
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
				new CProvince(Potsdam, "Potsdam", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[] { germany });
		state.Defines.Set(CManpower.MilitarySection, CUnitAvailability.BrigadesInDivision, CFixedPoint.FromInt(4));
		return state;
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, int province, params CSubUnitType[] brigades)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = province, Name = $"Unit {state.LastUnitId}" };
		foreach (CSubUnitType type in brigades)
		{
			var brigade = new CSubUnit(type) { Id = ++state.LastUnitId, Unit = unit, Name = type.Key, Strength = CFixedPoint.FromInt(10) };
			brigade.Stats.CopyFrom(type.Stats);
			unit.SubUnits.Add(brigade);
		}
		country.Units.Add(unit);
		return unit;
	}

	[Fact]
	public void Units_may_be_merged_idle_together_at_one_level_of_a_division_or_below_and_within_a_division()
	{
		CGameState state = State(out CCountry germany);
		CUnit first = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit second = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Infantry);
		CUnit third = Unit(state, germany, EUnitKind.Division, Berlin, Infantry);
		CUnit away = Unit(state, germany, EUnitKind.Division, Potsdam, Infantry);
		CUnit corps = Unit(state, germany, EUnitKind.Corps, Berlin, Infantry);
		CUnit otherCorps = Unit(state, germany, EUnitKind.Corps, Berlin, Infantry);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Berlin, Destroyer);

		Assert.True(CUnitMerge.MayMerge(state, germany, new[] { first, second }));           // four, a division's most
		Assert.False(CUnitMerge.MayMerge(state, germany, new[] { first, second, third }));   // five
		Assert.False(CUnitMerge.MayMerge(state, germany, new[] { first }));                  // one alone
		Assert.False(CUnitMerge.MayMerge(state, germany, new[] { first, away }));            // apart
		Assert.False(CUnitMerge.MayMerge(state, germany, new[] { corps, otherCorps }));      // above a division
		Assert.False(CUnitMerge.MayMerge(state, germany, new[] { first, fleet }));           // another level
		second.Path.Add(Potsdam);
		Assert.False(CUnitMerge.MayMerge(state, germany, new[] { first, second }));          // on its way
	}

	[Fact]
	public void The_unit_merged_into_has_the_most_brigades_then_strength_then_leader_and_the_first_of_equals()
	{
		CGameState state = State(out CCountry germany);
		CUnit small = Unit(state, germany, EUnitKind.Division, Berlin, Infantry);
		CUnit large = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Infantry);
		Assert.Same(large, CUnitMerge.Into(new[] { small, large }));

		// Two alike: the first. A twentieth short of full strength costs a point; a leader of skill 1 brings two.
		CUnit twin = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Infantry);
		Assert.Same(large, CUnitMerge.Into(new[] { large, twin }));
		large.SubUnits[0].Strength = CFixedPoint.FromInt(9);
		Assert.Equal(950, CUnitMerge.StrengthShare(large));
		Assert.Same(twin, CUnitMerge.Into(new[] { large, twin }));
		large.Leader = new CLeader(1) { Skill = 1 };
		Assert.Same(large, CUnitMerge.Into(new[] { large, twin }));
	}

	[Fact]
	public void A_merge_hands_every_brigade_to_the_one_chosen_keeps_the_more_skilled_leader_and_takes_the_rest_out()
	{
		CGameState state = State(out CCountry germany);
		CUnit first = Unit(state, germany, EUnitKind.Division, Berlin, Infantry);
		CUnit second = Unit(state, germany, EUnitKind.Division, Berlin, Infantry, Infantry);
		var weak = new CLeader(1) { Skill = 1 };
		var strong = new CLeader(2) { Skill = 3 };
		CLeaders.SetLeader(state, first, strong, false, false);
		CLeaders.SetLeader(state, second, weak, false, false);

		var command = new CMergeCommand(Ger, new[] { first.Id, second.Id });
		Assert.True(command.MayExecute(state));
		command.Execute(state);

		// The second scores 20 + 10 + 2 against the first's 10 + 10 + 6, takes the first's brigade and its better leader.
		Assert.Equal(3, second.SubUnits.Count);
		Assert.All(second.SubUnits, brigade => Assert.Same(second, brigade.Unit));
		Assert.Same(strong, second.Leader);
		Assert.Same(second, strong.Unit);
		Assert.Null(weak.Unit);
		Assert.True(first.IsRemoved);
		Assert.Empty(first.SubUnits);
	}
}
