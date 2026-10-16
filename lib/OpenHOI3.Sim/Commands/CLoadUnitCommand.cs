using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country putting one of its land units aboard one of its fleets, as the original's load command does: it names
/// both, is refused where either is gone or the fleet may not take the unit, and otherwise puts it aboard at once and
/// for nothing - wherever the two are - and drops its order. See <c>docs/mechanics/transport.md</c>, section 3.
/// </summary>
public sealed class CLoadUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly int _fleet;

	public CLoadUnitCommand(CCountryTag country, int unit, int fleet)
	{
		_country = country;
		_unit = unit;
		_fleet = fleet;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country, _unit) is { } unit
			&& Find(country, _fleet) is { } fleet && CTransport.CanCarry(fleet, unit);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnit unit = Find(country, _unit)!;
		if (!CMovementRules.IsLandUnit(unit) || unit.Transport is not null)
		{
			return;
		}
		CTransport.Board(Find(country, _fleet)!, unit);
		CUnitOrders.Clear(unit);
	}

	private static CUnit? Find(CCountry country, int id)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == id)
			{
				return unit;
			}
		}
		return null;
	}
}
