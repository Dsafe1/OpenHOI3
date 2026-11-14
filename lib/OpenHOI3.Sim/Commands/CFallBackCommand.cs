using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country telling one of its land or air units in a battle to go where a beaten one goes: what the battle window's
/// retreat button does for each of the player's units in it, the original's move order to its retreat province. A land
/// unit goes to the province beside it that is worth most as somewhere to fall back to (<c>landcombat.md</c>, section 9);
/// an air unit to the sea beside it with the most ports around, or with no sea its base. It leaves every battle it is in -
/// and, an air unit, the bombing - is marked as falling back where it was holding ground, and is left with no order. With
/// nowhere to go, or no way there, nothing happens: the button removes nobody, where a beaten unit with nowhere is removed.
/// See <c>docs/mechanics/combatview.md</c>, sections 2 and 11.
/// </summary>
public sealed class CFallBackCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;

	public CFallBackCommand(CCountryTag country, int unit)
	{
		_country = country;
		_unit = unit;
	}

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country || Find(country) is not { } unit)
		{
			return false;
		}
		return unit.Kind switch
		{
			EUnitKind.Air => unit.IsFighting || unit.Bombing is not null,
			EUnitKind.Navy => false,
			_ => unit.IsFighting,
		};
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnit unit = Find(country)!;
		int to = unit.Kind == EUnitKind.Air ? CAirRetreat.FindRetreatProvince(state, unit) : CCombatRetreat.FindRetreatProvince(state, unit);
		if (to <= 0 || to == unit.Location)
		{
			return;
		}
		List<int>? route = CRouteFinder.Find(state, country, unit, unit.Location, to);
		if (route is null)
		{
			return;
		}
		// A unit ordered away leaves what it is in, and falls back if it was holding ground (landcombat.md, section 9).
		bool holding = CBombing.IsHoldingGround(unit);
		CCombats.LeaveAll(state, unit);
		if (holding)
		{
			unit.IsRetreating = true;
		}
		CMovement.SetPath(unit, route, clear: true);
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
