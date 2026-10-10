using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country giving one of its fleets or air units an order for a province: a move, a
/// change of base, or for a fleet a mission, with how bold it is to be about it. The way
/// there is worked out when the command runs. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 5, and
/// <c>docs/mechanics/navalmissions.md</c>.
/// </summary>
public sealed class CSetUnitOrderCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly EUnitOrder _order;
	private readonly int _province;
	private readonly int _stance;
	private readonly int _priority;
	private readonly int _convoy;

	/// <param name="stance">0 aggressive, which is what the orders window starts at; 1 defensive; 2 passive.</param>
	/// <param name="priority">From 0 to 4.</param>
	/// <param name="convoy">The number of the convoy an escort is to sail with.</param>
	public CSetUnitOrderCommand(CCountryTag country, int unit, EUnitOrder order, int province,
		int stance = CMission.Aggressive, int priority = 0, int convoy = 0)
	{
		_country = country;
		_unit = unit;
		_order = order;
		_province = province;
		_stance = stance;
		_priority = priority;
		_convoy = convoy;
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country || Find(country) is not { } unit
			|| _province <= 0 || _province >= state.Provinces.Count
			|| _stance < CMission.Aggressive || _stance > CMission.Passive)
		{
			return false;
		}
		return CUnitOrders.MayGive(state, country, unit, _order, state.Province(_province), _convoy);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnitOrders.Give(state, country, Find(country)!, _order, _province, _stance, _priority, _convoy);
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
