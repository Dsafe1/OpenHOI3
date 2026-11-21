using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The unit panel's <c>automate_checkbox</c>: the highest AI agent over a headquarters taken away, or, with none, one made for it.
/// The original's runs straight off the interface; here it is a command. A theatre's headquarters, a corps, an army or an army
/// group. See <c>docs/mechanics/unitpanel.md</c>, section 9.
/// </summary>
public sealed class CAutomateUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;

	public CAutomateUnitCommand(CCountryTag owner, int unit)
	{
		_owner = owner;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_owner)!;
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		// By what the game holds - a plan that is on - and never by the AI's agents: a command runs on every machine, and a
		// machine that does not run the AI has no agent to ask, or a stale one (docs/multiplayer.md, section 3).
		if (CAgents.HighestAutomated(unit) is { } automated)
		{
			CAgent? agent = automated.Agent;
			CAgents.LetGo(state, automated);
			if (agent is not null)
			{
				country.UnitAi.Agents.Remove(agent);
			}
			return;
		}
		if (CAgents.TheatreHeadedBy(state, country, unit) is not null)
		{
			// Only the plan is switched on here. The agent is the AI's to make, in its next pass (CAgents.Adopt), with the
			// area, the powers and the sorting of what is under the headquarters, which reach every machine as its commands.
			unit.Plan.Active = true;
			// A theatre's plan begins at Prepare, its air and naval stances as low as they go.
			state.Outbox.Add(new CSetPlanCommand(country.Tag, unit.Id, CUnitPlan.Prepare, 0, 0));
		}
		else if (MayHandOver(unit))
		{
			// Below a theatre the plan keeps the stances it held; the agent and its area are the AI's to make (CAgents.Adopt).
			unit.Plan.Active = true;
		}
	}

	/// <summary>A corps, an army or an army group may be handed to the AI as well as a theatre's headquarters.</summary>
	public static bool MayHandOver(CUnit unit)
	{
		return unit.Kind is EUnitKind.ArmyGroup or EUnitKind.Army or EUnitKind.Corps;
	}
}
