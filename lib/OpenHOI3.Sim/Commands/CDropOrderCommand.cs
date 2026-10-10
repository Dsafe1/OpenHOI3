using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country taking back the order one of its units is under: what the cross beside a
/// unit's order does for every unit but a land unit on the march. The unit is left with
/// no order and keeps any way it was on. Every order may be taken back but an air
/// unit's change of base. See <c>docs/mechanics/unitpanel.md</c>, section 5.
/// </summary>
public sealed class CDropOrderCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;

	public CDropOrderCommand(CCountryTag country, int unit)
	{
		_country = country;
		_unit = unit;
	}

	/// <summary>Whether an order may be taken back: any but an air unit's change of base.</summary>
	public static bool MayDrop(EUnitOrder order)
	{
		return order != EUnitOrder.RebaseAir;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country) is { } unit && MayDrop(unit.Order);
	}

	public override void Execute(CGameState state)
	{
		CUnitOrders.Clear(Find(state.Country(_country)!)!);
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
