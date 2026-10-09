using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country giving one of its fleets or air units an order for a province: a move, or
/// a change of base. The way there is worked out when the command runs. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 5.
/// </summary>
public sealed class CSetUnitOrderCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly EUnitOrder _order;
	private readonly int _province;

	public CSetUnitOrderCommand(CCountryTag country, int unit, EUnitOrder order, int province)
	{
		_country = country;
		_unit = unit;
		_order = order;
		_province = province;
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country || Find(country) is not { } unit
			|| _province <= 0 || _province >= state.Provinces.Count)
		{
			return false;
		}
		return CUnitOrders.MayGive(state, country, unit, _order, state.Province(_province));
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnitOrders.Give(state, country, Find(country)!, _order, _province);
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
