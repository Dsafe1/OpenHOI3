using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// A unit on the march. It holds the provinces still ahead of it and how far it has
/// got towards the first; every hour its speed is added to that, and when it reaches
/// the distance to the next province the unit is in it, with whatever was left over
/// counted towards the one after. See <c>docs/mechanics/movement.md</c>, sections 4
/// to 6.
/// </summary>
public static class CMovement
{
	/// <summary>How far it is to a province the one a unit stands in has no edge to.</summary>
	public static readonly CFixedPoint DistanceWithoutEdge = CFixedPoint.One;

	/// <summary>
	/// Gives a unit a path. With <paramref name="clear"/> it replaces the one it has,
	/// and what the unit has already walked is kept only where the new path starts with
	/// the province the old one started with; without, it is added to the end.
	/// </summary>
	public static void SetPath(CUnit unit, IReadOnlyList<int> path, bool clear)
	{
		if (clear)
		{
			if (unit.Path.Count == 0 || path.Count == 0 || path[0] != unit.Path[0])
			{
				unit.MovementProgress = CFixedPoint.Zero;
			}
			unit.Path.Clear();
		}
		unit.Path.AddRange(path);
	}

	/// <summary>Stops a unit where it stands. What it had walked is lost.</summary>
	public static void Cancel(CUnit unit)
	{
		unit.Path.Clear();
		unit.MovementProgress = CFixedPoint.Zero;
	}

	/// <summary>
	/// How far the unit has to go to be in the next province of its path: the distance
	/// of the edge from the province it is in, which is not what the way back measures.
	/// </summary>
	public static CFixedPoint Distance(CGameState state, CUnit unit)
	{
		if (!unit.IsMoving)
		{
			return CFixedPoint.Zero;
		}
		return state.Province(unit.Location).EdgeTo(unit.Path[0])?.Distance ?? DistanceWithoutEdge;
	}

	/// <summary>
	/// The hours until the unit is in the next province of its path, at the speed it has
	/// now: what is left of the distance over the speed, rounded up. -1 for a unit that
	/// is going nowhere or cannot move.
	/// </summary>
	public static long HoursToNext(CGameState state, CCountry country, CUnit unit)
	{
		long speed = CMovementSpeed.Speed(state, country, unit).Raw;
		if (!unit.IsMoving || speed <= 0)
		{
			return -1;
		}
		long left = Distance(state, unit).Raw - unit.MovementProgress.Raw;
		return left <= 0 ? 0 : (left + speed - 1) / speed;
	}

	/// <summary>
	/// The hours until the unit is at the end of its path, were nothing to change on the
	/// way: each leg at the speed the unit would have on it, with what is left over of
	/// one counted towards the next as the march itself counts it. -1 for a unit that is
	/// going nowhere or cannot move.
	/// </summary>
	public static long HoursToArrive(CGameState state, CCountry country, CUnit unit)
	{
		if (!unit.IsMoving)
		{
			return -1;
		}
		long hours = 0;
		long progress = unit.MovementProgress.Raw;
		CProvince here = state.Province(unit.Location);
		foreach (int id in unit.Path)
		{
			CProvince next = state.Province(id);
			long speed = CMovementSpeed.Speed(state, country, unit, here, next).Raw;
			long distance = (here.EdgeTo(id)?.Distance ?? DistanceWithoutEdge).Raw;
			if (progress < distance)
			{
				if (speed <= 0)
				{
					return -1;
				}
				long leg = (distance - progress + speed - 1) / speed;
				hours += leg;
				progress += leg * speed;
			}
			progress -= distance;
			here = next;
		}
		return hours;
	}

	/// <summary>
	/// How much of the way ahead of it the unit has walked, in thousandths: its progress
	/// over the distances of every leg of its path added up. It is what the unit's arrow
	/// is filled by, and it starts again from nothing in each province the unit enters,
	/// the path being that much shorter.
	/// </summary>
	public static long WalkedShare(CGameState state, CUnit unit)
	{
		if (!unit.IsMoving)
		{
			return 0;
		}
		long whole = 0;
		CProvince here = state.Province(unit.Location);
		foreach (int id in unit.Path)
		{
			whole += (here.EdgeTo(id)?.Distance ?? DistanceWithoutEdge).Raw;
			here = state.Province(id);
		}
		if (whole <= 0)
		{
			return 0;
		}
		return System.Math.Clamp(unit.MovementProgress.Raw * CFixedPoint.Scale / whole, 0, CFixedPoint.Scale);
	}

	/// <summary>
	/// Every unit's hour as far as going somewhere is concerned, country by country in
	/// the order of the units' numbers: first the look at its order, then its march.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			List<CUnit>? busy = null;
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsMoving || unit.Order != EUnitOrder.None)
				{
					(busy ??= new List<CUnit>()).Add(unit);
				}
			}
			if (busy is null)
			{
				continue;
			}
			busy.Sort((a, b) => a.Id.CompareTo(b.Id));
			foreach (CUnit unit in busy)
			{
				CUnitOrders.RunHour(state, country, unit);
				if (unit.IsMoving)
				{
					RunHour(state, country, unit);
				}
			}
		}
	}

	/// <summary>
	/// One unit's hour: its speed is added once, and it then enters province after
	/// province for as long as what it has covers the next distance too.
	/// </summary>
	public static void RunHour(CGameState state, CCountry country, CUnit unit)
	{
		bool again = Advance(state, country, unit, addSpeed: true);
		while (again)
		{
			again = Advance(state, country, unit, addSpeed: false);
		}
	}

	/// <summary>
	/// One pass of the hour. Answers whether another is due: the unit entered a
	/// province, still has a path, and its progress already covers the next distance.
	/// </summary>
	public static bool Advance(CGameState state, CCountry country, CUnit unit, bool addSpeed)
	{
		if (unit.SubUnits.Count == 0 || !unit.IsMoving)
		{
			return false;
		}
		if (unit.Path[0] == unit.Location)
		{
			unit.Path.RemoveAt(0);
			unit.MovementProgress = CFixedPoint.Zero;
			return false;
		}

		CProvince here = state.Province(unit.Location);
		CProvince next = state.Province(unit.Path[0]);
		CFixedPoint distance = Distance(state, unit);
		if (addSpeed)
		{
			unit.MovementProgress += CMovementSpeed.Speed(state, country, unit);
		}
		if (unit.MovementProgress < CFixedPoint.Zero)
		{
			unit.MovementProgress = CFixedPoint.Zero;
		}

		// The way has to be there still, and open, every hour and not only on arrival.
		if (here.EdgeTo(next.Id) is not CProvinceEdge edge
			|| CMovementRules.IsCrossingBlocked(state, country, unit, edge))
		{
			Cancel(unit);
			return false;
		}
		if (unit.MovementProgress < distance)
		{
			return false;
		}

		unit.Path.RemoveAt(0);
		unit.MovementProgress -= distance;
		if (!CMovementRules.MayStandIn(state, country, unit, next))
		{
			Cancel(unit);
			return false;
		}
		unit.Previous = unit.Location;
		Enter(unit, next.Id);

		return unit.IsMoving && unit.MovementProgress >= Distance(state, unit);
	}

	/// <summary>Puts a unit in a province, with whatever its ships carry.</summary>
	private static void Enter(CUnit unit, int province)
	{
		unit.Location = province;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			foreach (CUnit carried in subUnit.Carried)
			{
				if (!carried.IsMoving)
				{
					carried.Location = province;
				}
			}
		}
	}
}
