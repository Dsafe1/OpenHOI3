using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A unit sent to another country with all under it, with nothing asked of the other: lent, or home where the other is the
/// country it came from. The AI's agents post it (<c>docs/mechanics/agents.md</c>, section 10.5). The original's
/// <c>CSendExpeditionCommand</c>.
/// </summary>
public sealed class CSendExpeditionCommand : CCommand
{
	private readonly CCountryTag _owner;
	private readonly int _unit;
	private readonly CCountryTag _target;

	public CSendExpeditionCommand(CCountryTag owner, int unit, CCountryTag target)
	{
		_owner = owner;
		_unit = unit;
		_target = target;
	}

	public override bool MayExecute(CGameState state)
	{
		return CUnitFinder.Find(state, _owner, _unit) is not null && state.Country(_owner) is not null && state.Country(_target) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = CUnitFinder.Find(state, _owner, _unit)!;
		if (unit.Lender == _target)
		{
			CExpeditions.TakeHome(state, unit);
		}
		else
		{
			CExpeditions.HandOver(state, unit, state.Country(_owner)!, state.Country(_target)!);
		}
	}
}
