using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The four commands the window a leader is chosen in posts, against
/// <c>docs/mechanics/leaders.md</c>, section 12.
/// </summary>
public class LeaderCommandTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static readonly CSubUnitType Infantry = MakeType("infantry_brigade", EUnitBranch.Land);
	private static readonly CSubUnitType Destroyer = MakeType("destroyer", EUnitBranch.Naval);

	private static CSubUnitType MakeType(string key, EUnitBranch branch)
	{
		var type = new CSubUnitType(key, branch);
		type.Stats.Values.Set(CMovementSpeed.MaximumSpeed, CFixedPoint.FromInt(4));
		type.Stats.Values.Set(CSubUnitType.MaxStrength, CFixedPoint.FromInt(100));
		return type;
	}

	private static CGameState State(out CCountry germany)
	{
		germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var defines = new CDefines();
		defines.Set(CManpower.MilitarySection, CLeaders.NewLeaderOrgHit, CFixedPoint.Parse("0.5"));
		return new CGameState(new CDate(1936, 1, 1),
			new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger } },
			new[] { germany }, defines: defines);
	}

	private static CUnit Unit(CGameState state, CCountry country, EUnitKind kind, CSubUnitType type, int brigades)
	{
		var unit = new CUnit(kind, country.Tag) { Id = ++state.LastUnitId, Location = 1, Name = $"unit {state.LastUnitId}" };
		for (int i = 0; i < brigades; i++)
		{
			unit.SubUnits.Add(new CSubUnit(type)
			{
				Id = ++state.LastUnitId, Unit = unit, Strength = CFixedPoint.FromInt(100), Organisation = CFixedPoint.FromInt(30),
			});
		}
		country.Units.Add(unit);
		return unit;
	}

	private static CLeader Leader(CGameState state, CCountry country, int id, int rank, ELeaderType type = ELeaderType.Land)
	{
		var leader = new CLeader(id) { Country = country.Tag, Rank = rank, Type = type, Name = $"leader {id}" };
		state.Leaders[id] = leader;
		country.Leaders.Add(leader);
		country.ActiveLeaders.Add(leader);
		return leader;
	}

	private static void Run(CGameState state, CCommand command)
	{
		Assert.True(command.MayExecute(state));
		command.Execute(state);
	}

	[Fact]
	public void ALeaderChosenForAUnitLeavesHisOldOneAndCostsTheUnitOrganisationAndHisHours()
	{
		CGameState state = State(out CCountry germany);
		CUnit first = Unit(state, germany, EUnitKind.Division, Infantry, 2);
		CUnit second = Unit(state, germany, EUnitKind.Division, Infantry, 2);
		CLeader leader = Leader(state, germany, 7, 1);
		CLeaders.SetLeader(state, first, leader, false, false);
		leader.TraitProgress.Duration = 12;

		// The window asks that he commands nothing else, so he cannot be moved from one unit to another.
		Assert.False(new CAssignLeaderCommand(Ger, second.Id, 7).MayExecute(state));

		Run(state, new CAssignLeaderCommand(Ger, second.Id, 7, onlyFree: false));

		Assert.Null(first.Leader);
		Assert.Same(leader, second.Leader);
		Assert.Equal(CFixedPoint.FromInt(15), second.SubUnits[0].Organisation);
		Assert.Equal(CFixedPoint.FromInt(30), first.SubUnits[0].Organisation);
		Assert.Equal(0, leader.TraitProgress.Duration);
	}

	[Fact]
	public void ALeaderTheUnitMayNotTakeIsRefusedAndNoLeaderIsAlwaysAllowed()
	{
		CGameState state = State(out CCountry germany);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry, 2);
		CUnit lone = Unit(state, germany, EUnitKind.Division, Infantry, 1);
		Leader(state, germany, 1, 2);                                   // a corps' rank
		Leader(state, germany, 2, 1);

		Assert.False(new CAssignLeaderCommand(Ger, division.Id, 1).MayExecute(state));
		Assert.False(new CAssignLeaderCommand(Ger, lone.Id, 2).MayExecute(state));   // one brigade
		Assert.False(new CAssignLeaderCommand(Ger, 999, null).MayExecute(state));     // no such unit

		Run(state, new CAssignLeaderCommand(Ger, division.Id, 2));
		Run(state, new CAssignLeaderCommand(Ger, division.Id, null));

		Assert.Null(division.Leader);
		Assert.Null(state.Leaders[2].Unit);
		Assert.Equal(CFixedPoint.FromInt(15), division.SubUnits[0].Organisation);    // only the leader cost any
	}

	[Fact]
	public void PromotionMovesOneRankWithinOneToFourAndTakesAGeneralOffHisUnit()
	{
		CGameState state = State(out CCountry germany);
		CUnit division = Unit(state, germany, EUnitKind.Division, Infantry, 2);
		CUnit fleet = Unit(state, germany, EUnitKind.Navy, Destroyer, 1);
		CLeader general = Leader(state, germany, 1, 1);
		CLeader admiral = Leader(state, germany, 2, 3, ELeaderType.Sea);
		CLeaders.SetLeader(state, division, general, false, false);
		CLeaders.SetLeader(state, fleet, admiral, false, false);

		Assert.False(new CPromoteLeaderCommand(1, down: true).MayExecute(state));     // not below 1
		Assert.False(new CPromoteLeaderCommand(99, down: false).MayExecute(state));

		Run(state, new CPromoteLeaderCommand(1, down: false));
		Run(state, new CPromoteLeaderCommand(2, down: false));

		Assert.Equal(2, general.Rank);
		Assert.Null(division.Leader);
		Assert.Null(general.Unit);
		Assert.Equal(4, admiral.Rank);
		Assert.Same(admiral, fleet.Leader);                                           // an admiral keeps his
		Assert.False(new CPromoteLeaderCommand(2, down: false).MayExecute(state));    // not above 4
		Assert.Equal(CFixedPoint.FromInt(30), division.SubUnits[0].Organisation);
	}

	[Fact]
	public void RemovingAllLeavesEveryUnitOfTheCountryWithNoneAndCostsNoOrganisation()
	{
		CGameState state = State(out CCountry germany);
		CUnit first = Unit(state, germany, EUnitKind.Division, Infantry, 2);
		CUnit second = Unit(state, germany, EUnitKind.Division, Infantry, 2);
		CLeaders.SetLeader(state, first, Leader(state, germany, 1, 1), false, false);
		CLeaders.SetLeader(state, second, Leader(state, germany, 2, 1), false, false);

		Run(state, new CRemoveAllLeadersCommand(Ger));

		Assert.Null(first.Leader);
		Assert.Null(second.Leader);
		Assert.Null(state.Leaders[1].Unit);
		Assert.Equal(CFixedPoint.FromInt(30), first.SubUnits[0].Organisation);
	}

	[Fact]
	public void TheCheckboxTurnsTheCountrysChoosingOffAndOnAgain()
	{
		CGameState state = State(out CCountry germany);

		Run(state, new CToggleAutoAssignLeadersCommand(Ger));
		Assert.False(germany.AutoAssignLeaders);
		Run(state, new CToggleAutoAssignLeadersCommand(Ger));
		Assert.True(germany.AutoAssignLeaders);
	}
}
