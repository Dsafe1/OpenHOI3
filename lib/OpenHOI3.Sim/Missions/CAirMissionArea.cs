using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The provinces an air mission covers when its area is a circle: from the mission's
/// province outwards through neighbours the unit may fly to, every province within the
/// unit's range of its base and within the area's range of the mission's province, the
/// walk going on only from provinces that are. It is the one of the original window's
/// shapes that was read. See <c>docs/mechanics/aircombat.md</c>, section 2.
/// </summary>
public static class CAirMissionArea
{
	/// <param name="range">How far from the mission's province the area reaches, in the units of a unit's range.</param>
	public static List<int> Circle(CGameState state, CCountry country, CUnit unit, EUnitOrder order, int centre,
		CFixedPoint range)
	{
		var found = new List<int>();
		if (centre <= 0 || centre >= state.Provinces.Count)
		{
			return found;
		}
		var seen = new HashSet<int> { centre };
		Walk(state, country, unit, order, state.Province(centre), centre, range, seen, found);
		return found;
	}

	private static void Walk(CGameState state, CCountry country, CUnit unit, EUnitOrder order, CProvince from,
		int centre, CFixedPoint range, HashSet<int> seen, List<int> found)
	{
		if (CUnitOrders.IsInRange(state, unit, order, from))
		{
			found.Add(from.Id);
		}
		CProvince middle = state.Province(centre);
		foreach (CProvinceEdge edge in from.Edges)
		{
			if (!seen.Add(edge.To))
			{
				continue;
			}
			CProvince next = state.Province(edge.To);
			if (CMovementRules.MayMoveTo(state, country, unit, next)
				&& CUnitOrders.IsInRange(state, unit, order, next)
				&& CFixedPoint.FromInt(CMapDistance.Between(middle, next, state.MapWidth)) <= range)
			{
				Walk(state, country, unit, order, next, centre, range, seen, found);
			}
		}
	}
}
