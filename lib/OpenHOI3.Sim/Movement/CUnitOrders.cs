using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// The orders that send a fleet or an air unit somewhere: which may be given, how far a
/// unit reaches from its base, what giving one does, and the hourly look that drops an
/// order once it is done. See <c>docs/mechanics/fleetsandair.md</c>, sections 3 to 5.
/// </summary>
public static class CUnitOrders
{
	public const string Range = "range";

	/// <summary>What a wing keeps of its organisation on landing at a new base, in thousandths.</summary>
	private const long OrganisationAfterRebase = 500;

	/// <summary>
	/// How far the unit reaches: the shortest <c>range</c> among its ships or wings, in
	/// the units distances between provinces are measured in.
	/// </summary>
	public static CFixedPoint RangeOf(CUnit unit)
	{
		bool any = false;
		CFixedPoint shortest = CFixedPoint.Zero;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			CFixedPoint range = subUnit.Stats[Range];
			if (!any || range < shortest)
			{
				shortest = range;
				any = true;
			}
		}
		return shortest;
	}

	/// <summary>
	/// How far a province is from the unit's base, as its range is set against it: by
	/// sea for a fleet, in a straight line for an air unit. Nothing for a unit with no
	/// base, and for a land unit.
	/// </summary>
	public static CFixedPoint DistanceFromBase(CGameState state, CUnit unit, CProvince province)
	{
		if (unit.Base <= 0 || unit.Base >= state.Provinces.Count)
		{
			return CFixedPoint.Zero;
		}
		CProvince home = state.Province(unit.Base);
		return unit.Kind switch
		{
			EUnitKind.Navy => CFixedPoint.FromInt(state.NavalDistances.Between(home, province)),
			EUnitKind.Air => CFixedPoint.FromInt(CMapDistance.Between(home, province, state.MapWidth)),
			_ => CFixedPoint.Zero,
		};
	}

	/// <summary>
	/// Whether a province is within the unit's reach for an order. A change of base is
	/// never out of reach, nor is anything for a unit that has no base.
	/// </summary>
	public static bool IsInRange(CGameState state, CUnit unit, EUnitOrder order, CProvince province)
	{
		if (order == EUnitOrder.Rebase || order == EUnitOrder.RebaseAir || unit.Base == 0)
		{
			return true;
		}
		return RangeOf(unit) >= DistanceFromBase(state, unit, province);
	}

	/// <summary>The orders that send a unit of this kind somewhere, in the order the original lists them.</summary>
	public static IReadOnlyList<EUnitOrder> For(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Navy => new[]
			{
				EUnitOrder.Move, EUnitOrder.Rebase, EUnitOrder.Reserve, EUnitOrder.Patrol, EUnitOrder.Intercept,
				EUnitOrder.Sortie, EUnitOrder.ConvoyRaid,
			},
			EUnitKind.Air => new[] { EUnitOrder.RebaseAir },
			_ => System.Array.Empty<EUnitOrder>(),
		};
	}

	/// <summary>
	/// Whether an order may be given to a unit for a province: the order's own test, and
	/// the province within reach.
	/// </summary>
	/// <param name="convoy">The number of the convoy an escort is to sail with.</param>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, EUnitOrder order, CProvince province,
		int convoy = 0)
	{
		if (unit.SubUnits.Count == 0 || unit.Location <= 0 || province.Id == 0)
		{
			return false;
		}
		if (CMissions.IsMission(order))
		{
			return CMissions.MayGive(state, country, unit, order, province, convoy);
		}
		switch (order)
		{
			case EUnitOrder.Move:
				return unit.Kind == EUnitKind.Navy
					&& IsInRange(state, unit, order, province)
					&& CMovementRules.MayMoveTo(state, country, unit, province);
			case EUnitOrder.Rebase:
				return unit.Kind == EUnitKind.Navy
					&& CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero
					&& CMovementRules.MayMoveTo(state, country, unit, province)
					&& CMovementRules.IsFriendly(state, country, province.Controller);
			case EUnitOrder.RebaseAir:
				return unit.Kind == EUnitKind.Air && !province.IsSea
					&& !CMovementRules.IsEnemy(state, province.Controller, country.Tag)
					&& CMovementRules.IsFriendly(state, country, province.Controller)
					&& CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero;
			default:
				return false;
		}
	}

	/// <summary>
	/// The order the original has picked when its window comes up: a change of base
	/// where the province has the base for one and is at peace with the unit's country,
	/// and for a fleet otherwise a plain move. None where nothing may be given.
	/// </summary>
	public static EUnitOrder Suggested(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		EUnitOrder rebase = unit.Kind == EUnitKind.Air ? EUnitOrder.RebaseAir : EUnitOrder.Rebase;
		string building = unit.Kind == EUnitKind.Air ? CMovementRules.AirBase : CMovementRules.NavalBase;
		if (CMovementRules.MayStandIn(state, country, unit, province)
			&& !CMovementRules.IsEnemy(state, province.Controller, country.Tag)
			&& CMovementRules.BuildingLevel(state, province, building) > CFixedPoint.Zero
			&& MayGive(state, country, unit, rebase, province))
		{
			return rebase;
		}
		return MayGive(state, country, unit, EUnitOrder.Move, province) ? EUnitOrder.Move : EUnitOrder.None;
	}

	/// <summary>
	/// Gives a unit an order, which replaces the one it had, and sets it on its way. A
	/// plain move with no way there leaves the unit where it is with whatever path it
	/// had; a change of base empties the path where there is no way.
	/// </summary>
	/// <param name="stance">How bold the order is: 0 aggressive, 1 defensive, 2 passive.</param>
	/// <param name="priority">From 0 to 4.</param>
	/// <param name="convoy">The number of the convoy an escort is to sail with.</param>
	public static void Give(CGameState state, CCountry country, CUnit unit, EUnitOrder order, int province,
		int stance = CMission.Defensive, int priority = 0, int convoy = 0)
	{
		Clear(unit);
		unit.Order = order;
		unit.OrderProvince = province;
		unit.Mission = new CMission { Stance = stance, Priority = priority, Convoy = convoy };
		if (CMissions.IsMission(order))
		{
			if (order == EUnitOrder.Sortie)
			{
				unit.Mission.Waypoints.Add(province);
			}
			CMissions.Start(state, country, unit);
			return;
		}
		List<int>? route = CRouteFinder.Find(state, country, unit, unit.Location, province);
		// A unit in a battle is given a path only where the battle lets it go - one at
		// sea not in its first hours - and then leaves it.
		if (unit.IsFighting && (route is not null || order == EUnitOrder.Rebase))
		{
			if (!Combat.CNavalDisengage.MayLeave(unit))
			{
				return;
			}
			Combat.CCombats.LeaveForMarch(state, unit);
		}
		if (route is not null)
		{
			CMovement.SetPath(unit, route, clear: true);
		}
		else if (order == EUnitOrder.Rebase)
		{
			CMovement.SetPath(unit, System.Array.Empty<int>(), clear: true);
		}
	}

	/// <summary>
	/// Takes a unit's order away. Its path is not touched. A fleet a patrol was
	/// shadowing is let go.
	/// </summary>
	public static void Clear(CUnit unit)
	{
		if (unit.Order == EUnitOrder.Patrol && unit.Mission.LiveTarget is { } shadowed)
		{
			shadowed.IsShadowed = false;
		}
		unit.Order = EUnitOrder.None;
		unit.OrderProvince = 0;
		unit.Mission = new CMission();
	}

	/// <summary>
	/// The hourly look at a unit's order, before the unit moves: a change of base makes
	/// the province the unit's base the first time it is looked at; an order is done
	/// when the unit is in its province, or has no way left to it; and a wing lands at
	/// its new base with half its organisation.
	/// </summary>
	public static void RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Order == EUnitOrder.None)
		{
			return;
		}
		if (CMissions.IsMission(unit.Order))
		{
			if (!CMissions.RunHour(state, country, unit))
			{
				Clear(unit);
			}
			return;
		}
		int target = unit.OrderProvince;
		switch (unit.Order)
		{
			case EUnitOrder.Move:
				if (unit.Location == target || !unit.IsMoving)
				{
					Clear(unit);
				}
				break;

			case EUnitOrder.Rebase:
				if (unit.Base == 0)
				{
					Clear(unit);
					break;
				}
				unit.Base = target;
				if (unit.Location == target || !unit.IsMoving)
				{
					Clear(unit);
				}
				break;

			case EUnitOrder.RebaseAir:
				if (unit.Base == 0)
				{
					Clear(unit);
					break;
				}
				if (unit.Base != target)
				{
					unit.Base = target;
					unit.CarriedBy?.Carried.Remove(unit);
					unit.CarriedBy = null;
				}
				if (unit.Location == target)
				{
					foreach (CSubUnit wing in unit.SubUnits)
					{
						long kept = wing.Organisation.Raw * OrganisationAfterRebase / CFixedPoint.Scale;
						wing.Organisation = new CFixedPoint(kept < 0 ? 0 : kept);
					}
					Clear(unit);
					break;
				}
				// A wing with nowhere left to go looks for a way again before it gives up.
				if (!unit.IsMoving && CRouteFinder.Find(state, country, unit, unit.Location, target) is { } route)
				{
					CMovement.SetPath(unit, route, clear: true);
				}
				if (!unit.IsMoving)
				{
					Clear(unit);
				}
				break;
		}
	}
}
