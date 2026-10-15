using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The provinces countries ask their allies to take, against <c>docs/mechanics/provinceview.md</c>, section 6.
/// </summary>
public class AlliedObjectiveTests
{
	[Fact]
	public void An_objective_is_kept_by_both_in_the_order_asked_and_taken_back_from_both()
	{
		var objectives = new CAlliedObjectives();
		objectives.Set(RebelWorld.Pol, RebelWorld.Ger, 4, active: true);
		objectives.Set(RebelWorld.Slo, RebelWorld.Ger, 4, active: true);
		Assert.Equal(new[] { RebelWorld.Pol, RebelWorld.Slo }, objectives.AskedBy(RebelWorld.Ger, 4));
		Assert.Single(objectives.AskedOf(RebelWorld.Pol));
		objectives.Set(RebelWorld.Pol, RebelWorld.Ger, 4, active: false);
		Assert.Equal(new[] { RebelWorld.Slo }, objectives.AskedBy(RebelWorld.Ger, 4));
		Assert.Empty(objectives.AskedOf(RebelWorld.Pol));
	}

	[Fact]
	public void Leaving_a_faction_clears_every_objective_either_way()
	{
		CGameState state = RebelWorld.State();
		state.AlliedObjectives.Set(RebelWorld.Pol, RebelWorld.Ger, 4, active: true);
		state.AlliedObjectives.Set(RebelWorld.Ger, RebelWorld.Slo, 5, active: true);
		CFactionMembership.Leave(state, state.Country(RebelWorld.Ger)!);
		Assert.Empty(state.AlliedObjectives.AskedBy(RebelWorld.Ger, 4));
		Assert.Empty(state.AlliedObjectives.AskedOf(RebelWorld.Pol));
		Assert.Empty(state.AlliedObjectives.AskedBy(RebelWorld.Slo, 5));
	}

	[Fact]
	public void The_command_asks_where_both_are_countries_and_the_province_not_below_nought()
	{
		CGameState state = RebelWorld.State();
		Assert.False(new CSetAlliedObjectiveCommand(RebelWorld.Pol, RebelWorld.Ger, -1, true).MayExecute(state));
		var command = new CSetAlliedObjectiveCommand(RebelWorld.Pol, RebelWorld.Ger, 0, true);
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.Equal(new[] { RebelWorld.Pol }, state.AlliedObjectives.AskedBy(RebelWorld.Ger, 0));
	}
}
