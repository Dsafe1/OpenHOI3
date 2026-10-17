using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit's plan given its objectives, replacing what it had. The AI's agents post it with a theatre's objectives
/// (<c>docs/mechanics/agents.md</c>, section 7.4).
/// </summary>
public sealed class CSetPlanObjectivesCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly CObjective[] _objectives;

	public CSetPlanObjectivesCommand(CCountryTag owner, int unit, IEnumerable<CObjective> objectives)
	{
		_owner = owner;
		_unit = unit;
		_objectives = new List<CObjective>(objectives).ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnitPlan plan = CUnitFinder.Find(state, _owner, _unit)!.Plan;
		plan.Objectives.Clear();
		plan.Objectives.AddRange(_objectives);
	}
}
