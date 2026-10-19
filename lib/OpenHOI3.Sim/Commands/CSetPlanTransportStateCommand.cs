using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit's plan given the port its troops embark at and the one they debark at - nought for none - and nothing else. The
/// AI's agents post it with their sealift (<c>docs/mechanics/moving.md</c>, the sealift).
/// </summary>
public sealed class CSetPlanTransportStateCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly int _embark;
	private readonly int _debark;

	public CSetPlanTransportStateCommand(CCountryTag owner, int unit, int embark, int debark)
	{
		_owner = owner;
		_unit = unit;
		_embark = embark;
		_debark = debark;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnitPlan plan = CUnitFinder.Find(state, _owner, _unit)!.Plan;
		plan.Embark = _embark;
		plan.Debark = _debark;
	}
}
