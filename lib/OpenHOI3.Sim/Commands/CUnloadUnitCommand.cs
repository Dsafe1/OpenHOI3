using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country putting one of its land units off the fleet it is aboard, as the original's unload command does: refused
/// unless the unit is aboard and stands in a land province - the fleet in port - and otherwise it gets off where it is,
/// bringing nothing. See <c>docs/mechanics/transport.md</c>, section 4.
/// </summary>
public sealed class CUnloadUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;

	public CUnloadUnitCommand(CCountryTag country, int unit)
	{
		_country = country;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country) is { Transport: not null } unit
			&& unit.Location > 0 && !state.Province(unit.Location).IsSea;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = Find(state.Country(_country)!)!;
		CTransport.GetOff(state, unit, bringSupplies: false, unit.Previous);
	}

	private CUnit? Find(CCountry country)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == _unit)
			{
				return unit;
			}
		}
		return null;
	}
}
