using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A fleet or an air group ordered to join another of its country's of the same kind: it goes there and is merged into
/// it. The AI's agents post it as they merge small fleets and air groups (<c>docs/mechanics/agents.md</c>, section 5.4).
/// </summary>
public sealed class CJoinUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int _target;

	public CJoinUnitCommand(CCountryTag owner, int unit, int target)
	{
		_owner = owner;
		_unit = unit;
		_target = target;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is { Kind: EUnitKind.Navy or EUnitKind.Air } unit
			&& CUnitFinder.Find(state, _owner, _target) is { } target && target.Kind == unit.Kind
			&& !ReferenceEquals(unit, target);
	}

	public override void Execute(CGameState state)
	{
		CJoinOrders.Give(state, state.Country(_owner)!, CUnitFinder.Find(state, _owner, _unit)!,
			CUnitFinder.Find(state, _owner, _target)!);
	}
}
