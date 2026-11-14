using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The join orders the AI gives a small fleet or air group: it goes to the other's province and, arrived, is merged into
/// it - its ships or wings go over and it is taken out of the game. With no other to join, it goes back to its base. The
/// original's <c>CJoinFleetOrder</c> and <c>CJoinAirOrder</c>. See <c>docs/mechanics/agents.md</c>, section 5.4.
/// </summary>
public static class CJoinOrders
{
	public static bool IsJoin(EUnitOrder order) => order is EUnitOrder.JoinFleet or EUnitOrder.JoinAir;

	/// <summary>The order given: its target kept, and the way to it set.</summary>
	public static void Give(CGameState state, CCountry country, CUnit unit, CUnit target)
	{
		CUnitOrders.Clear(unit);
		unit.Order = unit.Kind == EUnitKind.Air ? EUnitOrder.JoinAir : EUnitOrder.JoinFleet;
		unit.OrderProvince = target.Location;
		unit.Mission.Target = target;
		unit.Mission.OrderTarget = target;
		Head(state, country, unit, target.Location);
	}

	/// <summary>
	/// The partner the AI sees a join going to: a join_air's order target, which a load never sets - the original's
	/// <c>CJoinAirOrder::LoadKey</c> takes the saved target into the join's own reference alone - so a loaded join_air
	/// is dead to the AI and called off, while it still merges; a join_fleet's target, which a load restores
	/// (<c>docs/mechanics/agents.md</c>, section 5.4; FINDINGS-liveread.md, section 51).
	/// </summary>
	public static CUnit? SeenPartner(CUnit unit)
	{
		if (unit.Order == EUnitOrder.JoinAir)
		{
			return unit.Mission.OrderTarget is { IsRemoved: false } named ? named : null;
		}
		return unit.Mission.LiveTarget;
	}

	/// <summary>
	/// Its hour: merged where it shares the other's province; otherwise the way kept to it; and with the other gone, or
	/// itself joining, home. Answers whether the order goes on.
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		bool air = unit.Kind == EUnitKind.Air;
		// An air group with no base is done where it is (CJoinAirOrder::IsActive).
		if (air && unit.Base == 0)
		{
			return false;
		}
		CUnit? target = unit.Mission.LiveTarget;
		if (target is null || target.Owner != unit.Owner || IsJoin(target.Order) || target.Kind != unit.Kind
			|| (air && target.SubUnits.Count == 0))
		{
			if (unit.Base > 0 && unit.Location != unit.Base)
			{
				Head(state, country, unit, unit.Base);
			}
			return false;
		}
		// Either merges as soon as the two share a province, the other under way or not (CJoinFleetOrder::IsActive and
		// CJoinAirOrder::IsActive; reversing/findings/FINDINGS-liveread.md, sections 65 and 67).
		if (unit.Location == target.Location)
		{
			Merge(state, unit, target);
			return false;
		}
		// An air group then asks what every air order asks, so one beyond its range from its base turns for home and
		// loses the order (CAirOrder::IsActive; FINDINGS-liveread.md, section 67).
		if (air && !Missions.CAirMissions.IsStillGoing(state, country, unit))
		{
			return false;
		}
		if (air && unit.Mission.ReturnToBase)
		{
			return true;
		}
		// A fleet's way is aimed again only in its day, at midnight (RunDaily); its hour does nothing more
		// (reversing/findings/FINDINGS-liveread.md, section 77).
		if (!air)
		{
			return true;
		}
		if (!unit.IsMoving || unit.Path[^1] != target.Location)
		{
			Head(state, country, unit, target.Location);
		}
		return true;
	}

	/// <summary>
	/// A fleet's join in its day: where its way does not end where the fleet it joins stands, a new way there - the original's
	/// <c>CJoinFleetOrder::RunDaily</c>, slot 23, which <c>CUnit::UpdateDaily</c> asks at midnight. So a fleet chasing another
	/// keeps the way it was given for a day, and two that sail past each other need not meet (Britain's 47th Navy and 1st
	/// Transport Ship Squadron; reversing/findings/FINDINGS-liveread.md, section 77).
	/// </summary>
	public static void RunDaily(CGameState state, CCountry country, CUnit unit)
	{
		CUnit? target = unit.Mission.LiveTarget;
		if (target is null || target.IsRemoved || target.Location <= 0)
		{
			return;
		}
		if (!unit.IsMoving || unit.Path[^1] != target.Location)
		{
			Head(state, country, unit, target.Location);
		}
	}

	private static void Head(CGameState state, CCountry country, CUnit unit, int province)
	{
		if (unit.Location <= 0 || province <= 0 || unit.Location == province)
		{
			return;
		}
		if (CRouteFinder.Find(state, country, unit, unit.Location, province) is { } route)
		{
			CMovement.SetPath(unit, route, clear: true);
		}
	}

	/// <summary>
	/// The mover merged into the other as the merge button merges (<c>CUnitMerge.MergeInto</c>) - its ships or wings, what
	/// it carries and the more skilled leader - and taken out of the game.
	/// </summary>
	public static void Merge(CGameState state, CUnit mover, CUnit target)
	{
		CUnitMerge.MergeInto(state, target, mover);
		CCombatRetreat.RemoveFromGame(state, mover);
	}
}
