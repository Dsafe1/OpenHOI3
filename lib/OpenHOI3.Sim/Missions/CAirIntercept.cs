using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The intercept: a unit waits at its base, watches the provinces of its mission and
/// everything within two steps of them, and flies to meet the strongest enemy air unit
/// that is in its provinces or about to enter one. Having caught it - the two in one
/// province - it is told to return. See <c>docs/mechanics/aircombat.md</c>, section 4.
/// </summary>
public static class CAirIntercept
{
	/// <summary>How many provinces of a unit's path are looked at for one of the mission's.</summary>
	private const int StepsAhead = 3;

	/// <summary>How far around the mission's provinces the watch reaches.</summary>
	private const int StepsAround = 2;

	/// <summary>Makes the provinces the mission watches: its own, and every province within two steps of one.</summary>
	public static void BuildWatch(CGameState state, CUnit unit)
	{
		CMission mission = unit.Mission;
		mission.Watched.Clear();
		mission.WatchedSet.Clear();
		foreach (int id in mission.Provinces)
		{
			if (mission.WatchedSet.Add(id))
			{
				mission.Watched.Add(id);
			}
			foreach (CProvinceEdge edge in state.Province(id).Edges)
			{
				if (!mission.WatchedSet.Contains(edge.To))
				{
					AddWithNeighbours(state, mission, edge.To, StepsAround - 1);
				}
			}
		}
	}

	private static void AddWithNeighbours(CGameState state, CMission mission, int id, int steps)
	{
		mission.WatchedSet.Add(id);
		mission.Watched.Add(id);
		if (steps < 1)
		{
			return;
		}
		foreach (CProvinceEdge edge in state.Province(id).Edges)
		{
			if (!mission.WatchedSet.Contains(edge.To))
			{
				AddWithNeighbours(state, mission, edge.To, steps - 1);
			}
		}
	}

	/// <summary>The mission's own hour, before the frame every air order shares.</summary>
	public static void RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (unit.IsMoving || unit.IsFighting)
		{
			return;
		}
		CAirMissions.DayNightGate(state, country, unit);
		if (mission.ReturnToBase)
		{
			return;
		}
		if (unit.FuelReceived != CFixedPoint.Zero && state.TotalHours <= mission.EndHour
			&& CAirMissions.IsFitToFly(state, unit))
		{
			if (mission.Meeting != 0 && mission.Meeting == unit.Location)
			{
				if (PickTarget(state, country, unit) is { } again)
				{
					mission.Target = again;
				}
				GoForTarget(state, country, unit);
			}
			if (mission.LiveTarget is not { } target)
			{
				mission.Target = null;
				if (unit.Location == unit.Base)
				{
					if (PickTarget(state, country, unit) is { } found)
					{
						mission.Target = found;
					}
					GoForTarget(state, country, unit);
					return;
				}
			}
			else
			{
				if (!mission.WatchedSet.Contains(target.Location))
				{
					mission.Target = null;
					CAirMissions.ReturnToBase(state, country, unit);
					return;
				}
				if (target.Location != unit.Location || unit.Base == 0)
				{
					GoForTarget(state, country, unit);
					return;
				}
			}
		}
		CAirMissions.ReturnToBase(state, country, unit);
	}

	/// <summary>
	/// The enemy air unit to go for, and where to meet it: over the provinces the mission
	/// watches that the unit may fly to and its country knows, every air unit of another
	/// country that is in a province of the mission - no meeting province - or has one
	/// among the first three of its path - which becomes the meeting province.
	/// </summary>
	public static CUnit? PickTarget(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		CUnit? best = null;
		long most = CAirTargets.Nothing;
		foreach (int id in mission.Watched)
		{
			CProvince province = state.Province(id);
			if (!CMovementRules.MayStandIn(state, country, unit, province)
				|| CIntelligence.Level(state, province, unit.Owner) < CIntelligence.LeastToPick)
			{
				continue;
			}
			foreach (CUnit other in state.UnitsIn(id))
			{
				if (other.Kind != EUnitKind.Air || other.IsRemoved || other.Owner == unit.Owner)
				{
					continue;
				}
				int meeting = 0;
				if (!mission.Provinces.Contains(other.Location))
				{
					for (int step = 0; step < other.Path.Count && step < StepsAhead && meeting == 0; step++)
					{
						if (mission.Provinces.Contains(other.Path[step]))
						{
							meeting = other.Path[step];
						}
					}
					if (meeting == 0)
					{
						continue;
					}
				}
				if (CAirTargets.Score(state, unit, other, needsWings: true) is { } score && score > most)
				{
					most = score;
					mission.Meeting = meeting;
					best = other;
				}
			}
		}
		return best;
	}

	/// <summary>
	/// Gives the unit its way: to the meeting province where it has one and is not in
	/// it; otherwise to the target's next province where the target is moving - nothing
	/// where that is where it is going already - and else to the province the target is in.
	/// </summary>
	public static void GoForTarget(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (mission.Meeting != 0 && mission.Meeting != unit.Location)
		{
			if (CAirMissions.GoTo(state, country, unit, mission.Meeting))
			{
				mission.Destination = mission.Meeting;
			}
			return;
		}
		if (mission.LiveTarget is not { } target)
		{
			return;
		}
		int ahead = CAirTargets.NextProvince(target);
		if (mission.Destination != 0 && ahead == mission.Destination)
		{
			return;
		}
		int goal = ahead != 0 && ahead != target.Location ? ahead : target.Location;
		if (CAirMissions.GoTo(state, country, unit, goal))
		{
			mission.Destination = goal;
		}
	}
}
