using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// How strongly an enemy holds an area, as the AI weighs it: its enemies' brigades there in thirds, less a quarter of the
/// brigades its friends bring against the area's holder there and in the areas beside it held by the holder's enemies -
/// doubled with no such cut in a small area of one port, at least 5 in a capital's area held by five or more, and at most
/// 10. The original's <c>CountEnemyUnitsInArea</c>, which the invasion planner and an area's need ask. See
/// <c>docs/mechanics/moving.md</c>, section 16.
/// </summary>
public static class CAreaEnemies
{
	private const int Cap = 10;

	private const int CapitalFloor = 5;

	public static int Count(CGameState state, CCountry country, CSupplyArea area, bool uncapped = false)
	{
		// The enemies' brigades that can fight, in thirds - at least one where there are any.
		int theirs = 0;
		foreach (int id in area.Provinces)
		{
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (CTheatres.IsLand(unit) && unit.Owner != country.Tag && CCountryLists.IsEnemy(state, country, unit.Owner)
					&& CUnitTotals.CanFight(unit))
				{
					theirs += unit.SubUnits.Count;
				}
			}
		}
		int count = theirs > 0 ? System.Math.Max(theirs / 3, 1) : 0;
		int floor = 1;
		CCountry? holder = state.Country(area.Controller);
		if (holder is not null && CSupplyNetworks.Refresh(state).AreaOf(holder.ActingCapital) == area && count >= CapitalFloor)
		{
			floor = CapitalFloor;
		}
		// The brigades of the country's friends fighting the holder, there and in the areas beside held by its enemies.
		int ours = 0;
		if (holder is not null)
		{
			ours += Friends(state, country, holder, area);
			foreach (CSupplyArea beside in area.Neighbours)
			{
				if (beside.Controller != holder.Tag && CCountryLists.IsEnemy(state, holder, beside.Controller))
				{
					ours += Friends(state, country, holder, beside);
				}
			}
		}
		// A quarter of them, rounded half to even as the original's float is.
		int result = count - (int)System.Math.Round(ours / 4.0, System.MidpointRounding.ToEven);
		if (area.Provinces.Count < 4 && area.Ports.Count == 1)
		{
			result = 2 * count;
		}
		if (result > 0 && result < floor)
		{
			result = floor;
		}
		if (result < 0)
		{
			return 0;
		}
		return result > Cap && !uncapped ? Cap : result;
	}

	private static int Friends(CGameState state, CCountry country, CCountry holder, CSupplyArea area)
	{
		int brigades = 0;
		foreach (int id in area.Provinces)
		{
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (CTheatres.IsLand(unit) && unit.Owner != holder.Tag && CCountryLists.IsEnemy(state, holder, unit.Owner)
					&& CUnitTotals.CanFight(unit) && CCountryLists.IsFriend(state, country, unit.Owner, false))
				{
					brigades += unit.SubUnits.Count;
				}
			}
		}
		return brigades;
	}
}
