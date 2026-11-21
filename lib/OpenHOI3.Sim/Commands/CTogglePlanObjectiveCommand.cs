using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A province made an objective of a headquarters' plan, or no longer one: one the player sets is always to be held, at priority 400.
/// The agent's area is then worked out again. See <c>docs/mechanics/mapunits.md</c>, section 12.
/// </summary>
public sealed class CTogglePlanObjectiveCommand : CCommand
{
	private const int HandPlacedPriority = 400;

	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int _province;

	public CTogglePlanObjectiveCommand(CCountryTag owner, int unit, int province)
	{
		_owner = owner;
		_unit = unit;
		_province = province;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		if (unit.Plan.Objectives.RemoveAll(objective => objective.Province == _province) == 0)
		{
			unit.Plan.Objectives.Add(new CObjective(_province, HandPlacedPriority, Hold: true));
		}
		if (unit.Agent is { } agent && state.Country(_owner) is { } country)
		{
			CAgentArea.Set(state, country, agent);
		}
		// Below a theatre the area is the plan builder's, for the unit's topmost agent (unitpanel.md, section 9.1).
		if (CAgents.HighestOver(unit) is { Theatre: CAgents.BelowTheatre } below)
		{
			below.PlanBuildPending = true;
		}
	}
}
