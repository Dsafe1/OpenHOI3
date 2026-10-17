using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit put under another of the same owner ranking above it, last among its subordinates, with no cap on how many
/// it holds. The AI's agents post it as they fill their commands (<c>docs/mechanics/agents.md</c>, section 4.3).
/// </summary>
public sealed class CAttachUnitCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int _parent;

	public CAttachUnitCommand(CCountryTag owner, int unit, int parent)
	{
		_owner = owner;
		_unit = unit;
		_parent = parent;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is { } unit && CUnitFinder.Find(state, _owner, _parent) is { } parent
			&& parent.Kind < unit.Kind;
	}

	public override void Execute(CGameState state)
	{
		CAgentUnits.Attach(state, CUnitFinder.Find(state, _owner, _unit)!, CUnitFinder.Find(state, _owner, _parent)!);
	}
}
