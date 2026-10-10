using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country telling one of its air units to leave the battle or the bombing it is in:
/// what the battle window's retreat button does for an air unit. The unit is sent where
/// a beaten one goes - the sea beside it with the most ports around, or with no sea its
/// base - leaves what it was in, and is left with no order. With nowhere to go, or no
/// way there, nothing happens. See <c>docs/mechanics/combatview.md</c>, section 11.
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
		return state.Country(_country) is { } country
			&& Find(country) is { Kind: EUnitKind.Air } unit && (unit.IsFighting || unit.Bombing is not null);
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnit unit = Find(country)!;
		int to = CAirRetreat.FindRetreatProvince(state, unit);
		if (to <= 0 || to == unit.Location)
		{
			return;
		}
		List<int>? route = CRouteFinder.Find(state, country, unit, unit.Location, to);
		if (route is null)
		{
			return;
		}
		// A unit ordered away leaves what it is in, and falls back if it was holding ground.
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
