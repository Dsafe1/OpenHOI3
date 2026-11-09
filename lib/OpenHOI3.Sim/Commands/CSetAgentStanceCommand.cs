using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The player picking the ground stance of the AI that commands a headquarters: nothing where no agent holds it; the stance posted,
/// and the agent's reserve let go. See <c>docs/mechanics/unitpanel.md</c>, section 9.
/// </summary>
public sealed class CSetAgentStanceCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int _stance;

	public CSetAgentStanceCommand(CCountryTag owner, int unit, int stance)
	{
		_owner = owner;
		_unit = unit;
		_stance = stance;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		if (CUnitFinder.Find(state, _owner, _unit)?.Agent is not { } agent)
		{
			return;
		}
		CPlanStances.SetStance(state, state.Country(_owner)!, agent, _stance);
		foreach (CUnit division in agent.HeldBack)
		{
			division.IsHeldBack = false;
		}
		agent.HeldBack.Clear();
	}
}
