using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// A sortie: the fleet sails to its province and turns for home the hour it arrives,
/// and on the way - and lying in port afterwards - goes for the strongest enemy fleet it
/// sights in a sea province beside the one it is in that is no stronger than itself. It
/// is written to do what the original does, which does not take up its way again after
/// such a fight. See <c>docs/mechanics/navalmissions.md</c>, section 6.
/// </summary>
public static class CSortie
{
	/// <summary>
	/// Plots the way: to each waypoint from the one it is making for, each leg replacing
	/// the last, and where the last waypoint is no harbour and the fleet is not in its
	/// base's province, the leg from there home - which replaces them all.
	/// </summary>
	public static void Plot(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (mission.ReturnToBase || mission.Waypoints.Count == 0)
		{
			return;
		}
		if (mission.Current == 0)
		{
			mission.Current = mission.Waypoints[0];
		}
		int from = unit.Location;
		int index = mission.Waypoints.IndexOf(mission.Current);
		for (int at = index; at >= 0 && at < mission.Waypoints.Count; at++)
		{
			int waypoint = mission.Waypoints[at];
			CMissions.SetPathFrom(state, country, unit, from, waypoint, safe: waypoint == mission.Current);
			from = waypoint;
		}
		int last = mission.Waypoints[^1];
		if (!IsHarbour(state, last) && unit.Base != 0 && unit.Location != unit.Base
			&& CMovementRules.MayMoveTo(state, country, unit, state.Province(unit.Base)))
		{
			CMissions.SetPath(state, unit,
				CRouteFinder.Find(state, country, unit, from, unit.Base) ?? new List<int>());
		}
	}

	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (unit.Base == 0 || mission.Waypoints.Count == 0)
		{
			return false;
		}
		if (unit.IsFighting)
		{
			return true;
		}
		if (unit.Location == mission.Waypoints[^1])
		{
			if (!IsHarbour(state, mission.Waypoints[0]))
			{
				CMissions.ReturnToBase(state, country, unit);
			}
			return true;
		}
		if (CMissions.IsTooWorn(state, country, unit))
		{
			CMissions.HeadForNearestBase(state, country, unit);
			return true;
		}

		if (unit.Location == mission.Current)
		{
			int index = mission.Waypoints.IndexOf(mission.Current);
			if (index >= 0 && index + 1 < mission.Waypoints.Count)
			{
				mission.Current = mission.Waypoints[index + 1];
				if (mission.Current == mission.Waypoints[^1] && IsHarbour(state, mission.Current))
				{
					unit.Base = mission.Current;
				}
			}
		}

		CUnit? best = null;
		foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
		{
			if (edge.To <= 0 || edge.To >= state.Provinces.Count || !state.Province(edge.To).IsSea)
			{
				continue;
			}
			CUnit? found = CEnemyFleets.FindIn(state, state.Province(edge.To), unit);
			best ??= found;
			if (found is not null && CEnemyFleets.Strength(best!) < CEnemyFleets.Strength(found))
			{
				best = found;
			}
		}
		if (best is not null)
		{
			CMissions.GoTo(state, country, unit, best.Location);
		}
		return true;
	}

	private static bool IsHarbour(CGameState state, int province)
	{
		return CMovementRules.BuildingLevel(state, state.Province(province), CMovementRules.NavalBase) > CFixedPoint.Zero;
	}
}
