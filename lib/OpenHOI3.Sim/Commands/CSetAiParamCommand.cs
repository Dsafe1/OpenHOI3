using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A land unit given where the AI's sealift is sending it and where an airborne army waits to be flown from - the saved
/// <c>target</c> and <c>staging_province</c> - nought for none. The AI's agents post it (<c>docs/mechanics/moving.md</c>,
/// the sealift).
/// </summary>
public sealed class CSetAiParamCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int _target;
	private readonly int _staging;

	public CSetAiParamCommand(CCountryTag owner, int unit, int target, int staging)
	{
		_owner = owner;
		_unit = unit;
		_target = target;
		_staging = staging;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		unit.SealiftTarget = _target;
		unit.Staging = _staging;
	}
}
