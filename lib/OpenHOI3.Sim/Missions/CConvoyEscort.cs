using System.Collections.Generic;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// An escort: the fleet sails up and down the first stretch of a convoy's route that
/// lies within its range of its base. It does nothing to the convoy; what it is worth is
/// the battle it brings a raider to. See <c>docs/mechanics/navalmissions.md</c>,
/// section 7.
/// </summary>
public static class CConvoyEscort
{
	/// <summary>The convoy of a number, or null.</summary>
	public static CConvoy? Find(CGameState state, int id)
	{
		if (id == 0)
		{
			return null;
		}
		foreach (CCountry country in state.Countries)
		{
			foreach (CConvoy convoy in country.Convoys)
			{
				if (convoy.Id == id)
				{
					return convoy;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// The two ends of the fleet's beat: over the provinces of the convoy's route from
	/// its start, the first unbroken run of sea provinces nearer its base by sea than its
	/// range. Nothing where there is none.
	/// </summary>
	public static (int First, int Last) FindBeat(CGameState state, CUnit unit, CConvoy convoy)
	{
		int first = 0;
		int last = 0;
		if (unit.Base <= 0 || unit.Base >= state.Provinces.Count)
		{
			return (first, last);
		}
		CProvince home = state.Province(unit.Base);
		CFixedPoint range = CUnitOrders.RangeOf(unit);
		foreach (int id in convoy.Path)
		{
			CProvince province = state.Province(id);
			if (province.IsSea && CFixedPoint.FromInt(state.NavalDistances.Between(home, province)) < range)
			{
				last = id;
				if (first == 0)
				{
					first = id;
				}
			}
			else if (first != 0)
			{
				break;
			}
		}
		return (first, last);
	}

	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (Find(state, mission.Convoy) is not { } convoy || convoy.Path.Count <= 1)
		{
			return;
		}
		(mission.BeatFirst, mission.BeatLast) = FindBeat(state, unit, convoy);
		if (mission.BeatFirst == 0)
		{
			return;
		}
		Send(state, country, unit, mission.BeatFirst);
		mission.Heading = mission.BeatFirst;
	}

	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (unit.Base == 0)
		{
			return false;
		}
		if (unit.IsFighting)
		{
			return true;
		}
		if (Find(state, mission.Convoy) is not { } convoy)
		{
			mission.Convoy = 0;
			return false;
		}
		if (CMissions.IsTooWorn(state, country, unit))
		{
			CMissions.HeadForNearestBase(state, country, unit);
			return true;
		}
		if (mission.BeatFirst == 0 || mission.BeatLast == 0)
		{
			(mission.BeatFirst, mission.BeatLast) = FindBeat(state, unit, convoy);
			if (mission.BeatFirst == 0 || mission.BeatLast == 0)
			{
				return false;
			}
		}
		if (unit.Location == mission.Heading)
		{
			TurnAbout(state, country, unit);
		}
		return true;
	}

	/// <summary>
	/// At the end it was making for, the fleet turns for the other. The first time it
	/// comes to the first end this "turns" it to the end it is in, and it sets off for
	/// the last an hour later; that is the original's.
	/// </summary>
	private static void TurnAbout(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		mission.Turned = !mission.Turned;
		int goal = mission.Turned ? mission.BeatFirst : mission.BeatLast;
		Send(state, country, unit, goal);
		mission.Heading = goal;
	}

	private static void Send(CGameState state, CCountry country, CUnit unit, int goal)
	{
		if (unit.Location != goal && CMovementRules.MayMoveTo(state, country, unit, state.Province(goal)))
		{
			CMissions.SetPath(state, unit,
				CRouteFinder.Find(state, country, unit, unit.Location, goal, safe: true) ?? new List<int>());
		}
	}
}
