using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit's plan switched on, or off and reset to a new plan's, and whether the AI may reorganise under it. The
/// original's <c>CActivateUnitPlanCommand</c>; the agents post it as they are bound and let go
/// (<c>docs/mechanics/agents.md</c>, section 7.1).
/// </summary>
public sealed class CActivatePlanCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly bool _active;
	private readonly bool _allowHqReorg;

	public CActivatePlanCommand(CCountryTag owner, int unit, bool active, bool allowHqReorg)
	{
		_owner = owner;
		_unit = unit;
		_active = active;
		_allowHqReorg = allowHqReorg;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnitPlan plan = CUnitFinder.Find(state, _owner, _unit)!.Plan;
		if (_active)
		{
			plan.Active = true;
		}
		else
		{
			plan.Reset();
		}
		plan.AllowHqReorg = _allowHqReorg;
	}
}
