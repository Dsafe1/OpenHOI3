using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// A patrol: the fleet goes to its province and then round the sea provinces beside it,
/// shadows the first foreign fleet it sights where it is, and every twelve hours tries
/// the convoys of the water it is in. See <c>docs/mechanics/navalmissions.md</c>,
/// section 3.
/// </summary>
public static class CPatrol
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
	/// The patrol's move, and after it the look for somebody to shadow. Shadowing nobody
	/// it takes a step of its round where it is near its province and has no path, and
	/// otherwise makes for the province; shadowing a fleet under way it makes for where
	/// that fleet is going.
	/// </summary>
	public static void Move(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Mission.LiveTarget is not { } shadowed)
		{
			if (CMissions.IsNear(state, unit) && !unit.IsMoving)
			{
				CMissions.StepRound(state, country, unit, withinRange: true);
			}
			else if (!unit.IsMoving && unit.Location != unit.OrderProvince
				&& CMovementRules.MayMoveTo(state, country, unit, state.Province(unit.OrderProvince)))
			{
				CMissions.SetPath(state, unit,
					CRouteFinder.Find(state, country, unit, unit.Location, unit.OrderProvince, safe: true) ?? new List<int>());
			}
		}
		else if (shadowed.IsMoving && (!unit.IsMoving || unit.Path[0] != shadowed.Path[0]))
		{
			CMissions.SetPath(state, unit,
				CRouteFinder.Find(state, country, unit, unit.Location, shadowed.Path[0]) ?? new List<int>());
		}
		UpdateShadow(state, country, unit);
	}

	/// <summary>
	/// Lets go of the fleet it shadowed and looks for one afresh: the first fleet in the
	/// province the patrol is in, in the order they came, that is another country's and
	/// no friend's and that the patrol sights. No war is asked for. A fleet it did not shadow
	/// the hour before raises <c>PATROLTRACK</c> for the patrol's country (section 3.1).
	/// </summary>
	public static void UpdateShadow(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		CUnit? before = mission.Target;
		if (mission.LiveTarget is { } was)
		{
			was.IsShadowed = false;
			mission.Target = null;
		}
		if (unit.SubUnits.Count == 0
			|| !CUnitOrders.IsInRange(state, unit, unit.Order, state.Province(unit.OrderProvince)))
		{
			return;
		}
		foreach (CUnit other in state.UnitsIn(unit.Location))
		{
			// No friend: not of the patrol's own real faction, and no friendly agreement in its record towards the patrol's
			// country (reversing/findings/FINDINGS-navalmissions.md, shadowing; FINDINGS-liveread.md, section 41).
			if (other == unit || other.Kind != EUnitKind.Navy || other.IsRemoved || other.Owner == unit.Owner
				|| (state.Country(other.Owner) is { } theirs && theirs.Faction.Length > 0 && theirs.Faction == country.Faction)
				|| CCountryLists.HasFriendlyAgreement(state, other.Owner, country.Tag))
			{
				continue;
			}
			if (CNavalSpotting.ShouldStart(state, unit, other))
			{
				mission.Target = other;
				other.IsShadowed = true;
				// A new one, set against the one held as the hour began: told to the patrol's player alone, who the
				// interface picks out (reversing/findings/FINDINGS-navalmissions.md, section 4).
				if (other != before)
				{
					state.RaisePatrolTracked(new CPatrolTrackMessage(unit.Owner, unit.Location, other.Owner, unit.Id, unit.Name,
						other.Name));
				}
				return;
			}
		}
	}
}
