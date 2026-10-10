using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The raid on convoys as a mission: the fleet goes to its province and round the sea
/// provinces beside it as a patrol does, shadowing nobody, and every twelve hours tries
/// the convoys of the water it is in. What a try does is <see cref="Combat.CConvoyRaid"/>.
/// See <c>docs/mechanics/navalmissions.md</c>, section 4.
/// </summary>
public static class CConvoyRaiding
{
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		if (unit.IsFighting)
		{
			return true;
		}
		if (CMissions.IsTooWorn(state, country, unit))
		{
			CMissions.HeadForNearestBase(state, country, unit);
			return true;
		}
		Move(state, country, unit);
		CMissions.TryConvoys(state, country, unit);
		return true;
	}

	/// <summary>
	/// Near its province - and not lying at its own base - with no path, the raider takes
	/// a step of its round, with no thought for its range; otherwise, with no path, it
	/// makes for the province.
	/// </summary>
	public static void Move(CGameState state, CCountry country, CUnit unit)
	{
		bool atOwnBase = unit.Base != 0 && unit.Location == unit.Base;
		if (!atOwnBase && CMissions.IsNear(state, unit) && !unit.IsMoving)
		{
			CMissions.StepRound(state, country, unit, withinRange: false);
		}
		else if (!unit.IsMoving && unit.Location != unit.OrderProvince
			&& CMovementRules.MayMoveTo(state, country, unit, state.Province(unit.OrderProvince)))
		{
			CMissions.SetPath(state, unit,
				CRouteFinder.Find(state, country, unit, unit.Location, unit.OrderProvince, safe: true) ?? new List<int>());
		}
	}
}
