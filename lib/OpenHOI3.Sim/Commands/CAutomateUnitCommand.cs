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
		if (CAgents.TheatreHeadedBy(state, country, unit) is not null || MayHandOver(unit))
		{
			// Only the plan is switched on here. The agent is the AI's to make, in its next pass (CAgents.Adopt), with the
			// area, the powers and the sorting of what is under the headquarters, which reach every machine as its commands.
			unit.Plan.Active = true;
			// Ours, by decision: every stance begins defensive, ground, air and naval, where the original's theatre begins
			// at Prepare and its air and naval stances at nought, and a lower headquarters keeps what it held
			// (docs/compatibility.md, entry 32).
			state.Outbox.Add(new CSetPlanCommand(country.Tag, unit.Id, CUnitPlan.Defensive, CUnitPlan.DefensiveAirOrNaval,
				CUnitPlan.DefensiveAirOrNaval));
		}
	}

	/// <summary>A corps, an army or an army group may be handed to the AI as well as a theatre's headquarters.</summary>
	public static bool MayHandOver(CUnit unit)
	{
		return unit.Kind is EUnitKind.ArmyGroup or EUnitKind.Army or EUnitKind.Corps;
	}
}
