using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Commands;

/// <summary>
/// A country ordering one of its units to a province. The way there is worked out when
/// the command runs, so every machine works out the same one. It replaces the path the
/// unit has, or with <c>append</c> starts where that path ends and is added to it. A
/// unit with no way there keeps what it had. See <c>docs/mechanics/movement.md</c>,
/// section 1.
///
/// A land unit and a fleet take it; a fleet only within its range. It leaves the unit
/// with no order. An air unit is sent by an order of its own: see
/// <c>CSetUnitOrderCommand</c>.
/// </summary>
public sealed class CMoveUnitCommand : CCommand
{
	private readonly CCountryTag _country;
	private readonly int _unit;
	private readonly int _province;
	private readonly bool _append;
	private readonly ERouteSafety _landSafety;

	/// <param name="landSafety">How a land unit's way keeps clear of the enemy: the original command's two flags, which the
	/// AI sets and a player's click does not.</param>
	public CMoveUnitCommand(CCountryTag country, int unit, int province, bool append = false,
		ERouteSafety landSafety = ERouteSafety.Plain)
	{
		_country = country;
		_unit = unit;
		_province = province;
		_append = append;
		_landSafety = landSafety;
	}

	public override string ToString() => $"move unit {_unit} to {_province}";

	public override bool MayExecute(CGameState state)
	{
		if (state.Country(_country) is not { } country || Find(country) is not { } unit)
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Air || unit.Location <= 0 || unit.SubUnits.Count == 0
			|| _province <= 0 || _province >= state.Provinces.Count)
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Navy && !CUnitOrders.IsInRange(state, unit, unit.Order, state.Province(_province)))
		{
			return false;
		}
		return CMovementRules.MayMoveTo(state, country, unit, state.Province(_province));
	}

	public override void Execute(CGameState state)
	{
		CCountry country = state.Country(_country)!;
		CUnit unit = Find(country)!;
		// A fleet in a battle is not given its way at once: the province is kept and it
		// starts counting the hours until it may break away.
		if (CNavalDisengage.Defer(state, unit, _province))
		{
			return;
		}
		bool append = _append && unit.IsMoving;
		int from = append ? unit.Path[^1] : unit.Location;
		List<int>? route = CRouteFinder.Find(state, country, unit, from, _province, landSafety: _landSafety);
		if (route is not null)
		{
			// A unit ordered away from a battle leaves it, and falls back if it was holding ground.
			CCombats.LeaveForMarch(state, unit);
			CMovement.SetPath(unit, route, clear: !append);
			// A landing on a player's ground is noted for the warning (transport.md, section 11).
			Transport.CInvasionWarnings.Note(state, unit);
			CUnitOrders.Clear(unit);
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
