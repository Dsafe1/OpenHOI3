using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit's plan given how many of each unit type it wants. The AI's agents post it with a theatre's aircraft
/// (<c>docs/mechanics/agents.md</c>, section 9).
/// </summary>
public sealed class CSetPlanForcesCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int[] _min;

	public CSetPlanForcesCommand(CCountryTag owner, int unit, IEnumerable<int> min)
	{
		_owner = owner;
		_unit = unit;
		_min = new List<int>(min).ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		CUnitPlan plan = unit.Plan;
		plan.Min.Clear();
		plan.Min.AddRange(_min);
		Agents.CPlanWants.Refresh(state, unit);
	}
}
