using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A corps, an army or an army group handed to the AI, against <c>docs/mechanics/unitpanel.md</c>, section 9.1, on
/// <see cref="RebelWorld"/>: Germany's corps over one division.
/// </summary>
public class HeadquartersHandOverTests
{
	private static (CGameState State, CCountry Germany, CUnit Corps) World()
	{
		CGameState state = RebelWorld.State();
		CCountry germany = state.Country(RebelWorld.Ger)!;
		state.Played.Add(germany.Tag);
		germany.UnitAi.RunsUnits = false;
		var corps = new CUnit(EUnitKind.Corps, germany.Tag) { Id = ++state.LastUnitId, Name = "I Corps", Location = 2 };
		var division = new CUnit(EUnitKind.Division, germany.Tag) { Id = ++state.LastUnitId, Name = "1st", Location = 2, Parent = corps };
		corps.Children.Add(division);
		germany.Units.Add(corps);
		state.ForgetPositions();
		return (state, germany, corps);
	}

	private static void Run(CGameState state)
	{
		foreach (CCommand command in state.Outbox.ToArray())
		{
			if (command.MayExecute(state))
			{
				command.Execute(state);
			}
		}
		state.Outbox.Clear();
	}

	[Fact]
	public void A_corps_handed_over_has_an_agent_of_no_theatre_that_goes_with_its_unit()
	{
		(CGameState state, CCountry germany, CUnit corps) = World();
		Assert.True(CAutomateUnitCommand.MayHandOver(corps));
		new CAutomateUnitCommand(germany.Tag, corps.Id).Execute(state);
		Assert.True(corps.Plan.Active);
		// Every stance starts defensive, by decision (compatibility.md, entry 32).
		corps.Plan.Stance = CUnitPlan.Blitzing;
		corps.Plan.AirStance = CUnitPlan.Offensive;
		Run(state);
		Assert.Equal((CUnitPlan.Defensive, CUnitPlan.DefensiveAirOrNaval, CUnitPlan.DefensiveAirOrNaval),
			(corps.Plan.Stance, corps.Plan.AirStance, corps.Plan.NavalStance));

		CAgents.Adopt(state, germany);
		CAgent agent = Assert.Single(germany.UnitAi.Agents);
		Assert.Equal(CAgents.BelowTheatre, agent.Theatre);
		Assert.Same(agent, corps.Agent);
		Assert.Null(CAgentUnits.AgentFor(germany, CAgents.BelowTheatre));

		// No theatre to lose, so the prune keeps it - until its unit goes.
		CAgents.Prune(state, germany);
		Assert.Single(germany.UnitAi.Agents);
		corps.IsRemoved = true;
		CAgents.Prune(state, germany);
		Assert.Empty(germany.UnitAi.Agents);
	}

	[Fact]
	public void The_box_pressed_again_lets_the_corps_go_and_a_division_may_not_be_handed_over()
	{
		(CGameState state, CCountry germany, CUnit corps) = World();
		new CAutomateUnitCommand(germany.Tag, corps.Id).Execute(state);
		CAgents.Adopt(state, germany);
		Run(state);
		Assert.True(corps.Plan.Active);
		new CAutomateUnitCommand(germany.Tag, corps.Id).Execute(state);
		// The plan is switched off by the command the agent's end posts, as the original's.
		Assert.IsType<CActivatePlanCommand>(Assert.Single(state.Outbox));
		Run(state);
		Assert.False(corps.Plan.Active);
		Assert.Null(corps.Agent);
		Assert.Empty(germany.UnitAi.Agents);
		Assert.False(CAutomateUnitCommand.MayHandOver(corps.Children[0]));
	}
}
