using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The provinces an air mission covers when its area is a circle of a given range about
/// the mission's province - the window's circle, for the developer switch and the
/// trials, which have no window. See <c>docs/mechanics/aircombat.md</c>, section 2.
/// </summary>
public static class CAirMissionArea
{
	/// <param name="range">How far from the mission's province the area reaches, in the units of a unit's range.</param>
	public static List<int> Circle(CGameState state, CCountry country, CUnit unit, EUnitOrder order, int centre,
		CFixedPoint range)
	{
		if (centre <= 0 || centre >= state.Provinces.Count)
		{
			return new List<int>();
		}
		var area = new CAirArea(state, country, unit, order, centre, EAirAreaShape.Circle);
		area.SetRange(range);
		return new List<int>(area.Provinces);
	}
}
