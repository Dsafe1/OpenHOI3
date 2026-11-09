using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// The unit panel's <c>automate_checkbox</c>: the highest AI agent over a headquarters taken away, or, with none, one made for it.
/// The original's runs straight off the interface; here it is a command. Only a theatre's headquarters is handed over, the agents
/// here being a theatre's. See <c>docs/mechanics/unitpanel.md</c>, section 9.
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
		if (CAgents.HighestOver(unit) is { } agent)
		{
			CAgents.Remove(state, country, agent);
			return;
		}
		if (CAgents.TheatreHeadedBy(state, country, unit) is { } theatre)
		{
			var made = new CAgent(country.Tag, theatre.Id);
			country.UnitAi.Agents.Add(made);
			CAgents.Bind(state, country, made, unit);
			// A theatre's plan begins at Prepare, its air and naval stances as low as they go.
			state.Outbox.Add(new CSetPlanCommand(country.Tag, unit.Id, CUnitPlan.Prepare, 0, 0));
		}
	}
}
