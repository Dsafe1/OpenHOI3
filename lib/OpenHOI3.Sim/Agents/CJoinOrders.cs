using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
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
		Head(state, country, unit, target.Location);
	}

	/// <summary>
	/// Its hour: merged where it shares the other's province; otherwise the way kept to it; and with the other gone, or
	/// itself joining, home. Answers whether the order goes on.
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CUnit? target = unit.Mission.LiveTarget;
		if (target is null || target.Owner != unit.Owner || IsJoin(target.Order) || target.Kind != unit.Kind)
		{
			if (unit.Base > 0 && unit.Location != unit.Base)
			{
				Head(state, country, unit, unit.Base);
			}
			return false;
		}
		if (unit.Location == target.Location && !target.IsMoving)
		{
			Merge(state, unit, target);
			return false;
		}
		if (!unit.IsMoving || unit.Path[^1] != target.Location)
		{
			Head(state, country, unit, target.Location);
		}
		return true;
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

	/// <summary>The mover's ships or wings handed to the other, its leader freed and the mover taken out of the game.</summary>
	public static void Merge(CGameState state, CUnit mover, CUnit target)
	{
		foreach (CSubUnit subUnit in mover.SubUnits.ToArray())
		{
			mover.SubUnits.Remove(subUnit);
			target.SubUnits.Add(subUnit);
			subUnit.Unit = target;
		}
		CLeaders.SetLeader(state, mover, null, false, false);
		CCombatRetreat.RemoveFromGame(state, mover);
		state.ForgetPositions();
	}
}
