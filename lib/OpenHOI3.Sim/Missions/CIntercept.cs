using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// An intercept: the fleet waits in its base's province, and when it sights an enemy
/// fleet no stronger than itself in the order's province or one beside it, goes for it -
/// to where it is, or to where it is going. Once it has been told to return to base the
/// order does nothing more, which is the original's. See
/// <c>docs/mechanics/navalmissions.md</c>, section 5.
/// </summary>
public static class CIntercept
{
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base != 0 && unit.Location != unit.Base)
		{
			CMissions.ReturnToBase(state, country, unit);
		}
	}

	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (unit.Base == 0)
		{
			return false;
		}
		if (unit.IsFighting || mission.ReturnToBase)
		{
			return true;
		}
		if (CMissions.IsTooWorn(state, country, unit))
		{
			CMissions.HeadForNearestBase(state, country, unit);
			return true;
		}

		if (mission.LiveTarget is null && unit.Location != unit.Base)
		{
			CMissions.ReturnToBase(state, country, unit);
		}
		if (unit.IsRetreating)
		{
			mission.Target = null;
			CMissions.ReturnToBase(state, country, unit);
		}
		if (mission.LiveTarget is null && unit.Location == unit.Base)
		{
			if (Look(state, unit) is { } found)
			{
				mission.Target = found;
				CMissions.GoTo(state, country, unit, found.Location);
			}
		}
		if (unit.IsFighting || mission.LiveTarget is not { } target)
		{
			return true;
		}

		if (!CUnitOrders.IsInRange(state, unit, unit.Order, state.Province(target.Location)))
		{
			CMissions.ReturnToBase(state, country, unit);
		}
		int goal;
		if (target.IsMoving && !unit.IsMoving)
		{
			goal = target.Path[0];
		}
		else
		{
			if (target.Location == 0 || unit.IsMoving)
			{
				return true;
			}
			if (!state.Province(target.Location).IsSea)
			{
				CMissions.ReturnToBase(state, country, unit);
				return true;
			}
			goal = target.Location;
		}
		CMissions.GoTo(state, country, unit, goal);
		return true;
	}

	/// <summary>
	/// The fleet to go for: one in the order's province, and with none there the first
	/// that one of the provinces beside it gives, in the order of its edges.
	/// </summary>
	private static CUnit? Look(CGameState state, CUnit unit)
	{
		CProvince centre = state.Province(unit.OrderProvince);
		if (CEnemyFleets.FindIn(state, centre, unit) is { } there)
		{
			return there;
		}
		foreach (CProvinceEdge edge in centre.Edges)
		{
			if (edge.To > 0 && edge.To < state.Provinces.Count
				&& CEnemyFleets.FindIn(state, state.Province(edge.To), unit) is { } beside)
			{
				return beside;
			}
		}
		return null;
	}
}
