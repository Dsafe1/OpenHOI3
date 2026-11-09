using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>Whether a headquarters' aggression is its own: the unit panel's <c>aggression_checkbox</c>.</summary>
public sealed class CToggleUnitAggressionCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly bool _own;

	public CToggleUnitAggressionCommand(CCountryTag owner, int unit, bool own)
	{
		_owner = owner;
		_unit = unit;
		_own = own;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnitFinder.Find(state, _owner, _unit)!.HasOwnAggression = _own;
	}
}
