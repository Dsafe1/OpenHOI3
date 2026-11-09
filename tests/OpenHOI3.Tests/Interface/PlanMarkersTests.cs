using System.Collections.Generic;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>A plan's objective markers and arrows, against <c>docs/mechanics/mapunits.md</c>, section 12.</summary>
public class PlanMarkersTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static (CGameState State, CCountry Germany, CUnit Theatre) World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), new CProvince(1, "Berlin", isSea: false) }, new[] { germany });
		var theatre = new CUnit(EUnitKind.Theatre, Ger) { Id = 5, Agent = new CAgent(Ger, 1) };
		germany.Units.Add(theatre);
		return (state, germany, theatre);
	}

	[Fact]
	public void AnObjectiveIsAddedHeldAtFourHundredAndTakenOffByAnotherClick()
	{
		(CGameState state, _, CUnit theatre) = World();
		new CTogglePlanObjectiveCommand(Ger, theatre.Id, 1).Execute(state);
		Assert.Equal(new CObjective(1, 400, Hold: true), Assert.Single(theatre.Plan.Objectives));
		new CTogglePlanObjectiveCommand(Ger, theatre.Id, 1).Execute(state);
		Assert.Empty(theatre.Plan.Objectives);
	}

	[Fact]
	public void TheMarkersAreOnlyForAHeadquartersUnderAnAi()
	{
		(CGameState state, CCountry germany, CUnit theatre) = World();
		theatre.Plan.Objectives.Add(new CObjective(1, 400));
		Assert.Equal(new[] { (1, true) }, CPlanMarkers.Of(state, germany, theatre).Objectives);
		Assert.Empty(CPlanMarkers.Of(state, germany, null).Objectives);
		theatre.Agent = null;
		Assert.Empty(CPlanMarkers.Of(state, germany, theatre).Objectives);
	}

	[Fact]
	public void AnArrowRunsAQuarterPastItsLastProvince()
	{
		Assert.Equal(new List<(int, int)> { (0, 0), (8, 4), (10, 5) }, CPlanMarkers.ArrowPoints(new List<(int, int)> { (0, 0), (8, 4) }));
	}
}
