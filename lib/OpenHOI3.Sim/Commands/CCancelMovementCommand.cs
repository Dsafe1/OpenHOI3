using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country halting one of its units where it stands, as the original's stop does: its
/// order goes, it breaks off every attack it is in, its way and what it had walked
/// towards the next province are lost, and on ground an enemy holds it looks for a
/// battle there. See <c>docs/mechanics/movement.md</c>, section 1.
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
		// Its order goes, every attack it is in is broken off - not a battle it defends -
		// and its way and what it had walked of it.
		CUnitOrders.Clear(unit);
		CCombats.LeaveAttacks(state, unit);
		CMovement.Cancel(unit);
		// Halted on ground an enemy holds, it looks for a battle there at once.
		if (!unit.IsRetreating && !unit.IsFighting && unit.Location > 0
			&& CMovementRules.IsEnemy(state, unit.Owner, state.Province(unit.Location).Controller))
		{
			CCombats.CheckForCombat(state, unit, unit.Location);
		}
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
