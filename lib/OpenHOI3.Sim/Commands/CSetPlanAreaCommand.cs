using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit's area of operations set - an empty one too, which clears it - and its plan's wants padded with noughts to one a
/// unit type. The original's <c>CSetPlanOpsAreaCommand</c> (<c>docs/mechanics/agents.md</c>, section 8.2).
/// </summary>
public sealed class CSetPlanAreaCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int[] _area;

	public CSetPlanAreaCommand(CCountryTag owner, int unit, IEnumerable<int> area)
	{
		_owner = owner;
		_unit = unit;
		_area = new List<int>(area).ToArray();
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		CUnitPlan plan = unit.Plan;
		plan.Area.Clear();
		plan.Area.AddRange(_area);
		Agents.CAircraftWants.Pad(state, plan);
		Agents.CPlanWants.Refresh(state, unit);
	}
}
