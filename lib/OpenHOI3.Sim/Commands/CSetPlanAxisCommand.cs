using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit's plan given its axis - the way its attack runs, and a second the AI leaves empty - replacing what it had. The
/// AI's agents post it at an attacking stance (<c>docs/mechanics/moving.md</c>, section 11.6).
/// </summary>
public sealed class CSetPlanAxisCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int[] _path;
	private readonly int[] _path2;

	public CSetPlanAxisCommand(CCountryTag owner, int unit, IEnumerable<int> path, IEnumerable<int> path2)
	{
		_owner = owner;
		_unit = unit;
		_path = new List<int>(path).ToArray();
		_path2 = new List<int>(path2).ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		CUnitPlan plan = unit.Plan;
		plan.Path.Clear();
		plan.Path.AddRange(_path);
		plan.Path2.Clear();
		plan.Path2.AddRange(_path2);
		// Below a theatre the area is the plan builder's, for the unit's topmost agent (unitpanel.md, section 9.1).
		if (Agents.CAgents.HighestOver(unit) is { Theatre: Agents.CAgents.BelowTheatre } below)
		{
			below.PlanBuildPending = true;
		}
	}
}
