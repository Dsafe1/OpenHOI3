using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>A headquarters' aggression, -1 to 1: the unit panel's <c>aggression_slider</c>.</summary>
public sealed class CSetUnitAggressionCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly long _aggression;

	public CSetUnitAggressionCommand(CCountryTag owner, int unit, long aggression)
	{
		_owner = owner;
		_unit = unit;
		_aggression = aggression;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnitFinder.Find(state, _owner, _unit)!.Aggression = new CFixedPoint(_aggression);
	}
}
