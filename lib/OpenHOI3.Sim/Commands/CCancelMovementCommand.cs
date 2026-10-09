using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country halting one of its units where it stands. What the unit had walked
/// towards the next province is lost. See <c>docs/mechanics/movement.md</c>, section 1.
/// </summary>
public sealed class CCancelMovementCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;

	public CCancelMovementCommand(CCountryTag country, int unit)
	{
		_country = country;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		return state.Country(_country) is { } country && Find(country) is not null;
	}

	public override void Execute(CGameState state)
	{
		CUnit unit = Find(state.Country(_country)!)!;
		CMovement.Cancel(unit);
		CUnitOrders.Clear(unit);
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
