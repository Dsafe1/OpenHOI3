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
		if (order is EUnitOrder.Rebase or EUnitOrder.RebaseAir or EUnitOrder.RebaseCarrier || unit.Base == 0)
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
				EUnitOrder.Sortie, EUnitOrder.ConvoyRaid, EUnitOrder.Transport, EUnitOrder.Invasion,
			},
			EUnitKind.Air => new[]
			{
				EUnitOrder.CarrierProtection, EUnitOrder.AirSuperiority, EUnitOrder.AirIntercept, EUnitOrder.Paradrop,
				EUnitOrder.RebaseCarrier, EUnitOrder.Nuke, EUnitOrder.AirSupply, EUnitOrder.StrategicBombing,
				EUnitOrder.LogisticalStrike, EUnitOrder.RunwayCratering, EUnitOrder.InstallationStrike,
				EUnitOrder.GroundAttack, EUnitOrder.Interdiction, EUnitOrder.PortStrike, EUnitOrder.NavalStrike,
				EUnitOrder.AirConvoyRaid, EUnitOrder.AirReserve, EUnitOrder.RebaseAir,
			},
			_ => System.Array.Empty<EUnitOrder>(),
		};
	}

	/// <summary>
	/// Whether an order may be given to a unit for a province: the order's own test, and
	/// the province within reach.
	/// </summary>
	/// <param name="convoy">The number of the convoy an escort is to sail with.</param>
	/// <param name="anyRange">The AI's own order: a fleet's mission is not held to its range.</param>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, EUnitOrder order, CProvince province,
		int convoy = 0, bool anyRange = false)
	{
		if (unit.SubUnits.Count == 0 || unit.Location <= 0 || province.Id == 0)
		{
			return false;
		}
		if (CMissions.IsMission(order))
		{
			return CMissions.MayGive(state, country, unit, order, province, convoy, anyRange);
		}
		if (CAirMissions.IsAirMission(order))
		{
			return CAirMissions.MayGive(state, country, unit, order, province);
		}
		if (CBombingOrders.IsBombingMission(order))
		{
			return CBombingOrders.MayGive(state, country, unit, order, province);
		}
		// The window's rows ask the original's whole test of a move; the AI's own order, which no window gives, is held to
		// what a move command asks (movement.md, section 1).
		bool MayMove() => anyRange
			? CMovementRules.MayCommandMoveTo(state, country, unit, province)
			: CMovementRules.MayMoveTo(state, country, unit, province);
		switch (order)
		{
			case EUnitOrder.Move:
				if (CMovementRules.IsLandUnit(unit))
				{
					// The land orders window's: a neighbour only (landorders.md, section 6).
					return !unit.IsFighting && IsBeside(state, unit, province) && MayMove();
				}
				return unit.Kind == EUnitKind.Navy
					&& IsInRange(state, unit, order, province)
					&& MayMove();
			case EUnitOrder.Rebase:
				return unit.Kind == EUnitKind.Navy
					&& CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero
					&& MayMove()
					&& state.Country(province.Controller) is { } controller && CCountryLists.IsFriend(state, controller, country.Tag, withAccess: true);
			case EUnitOrder.RebaseAir:
				return unit.Kind == EUnitKind.Air && !province.IsSea
					&& !CMovementRules.IsEnemy(state, province.Controller, country.Tag)
					&& state.Country(province.Controller) is { } holder && CCountryLists.IsFriend(state, holder, country.Tag, withAccess: true)
					&& CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero;
			// Carrying by air and the carrier (transport.md, section 13): none of the four asks the range.
			case EUnitOrder.RebaseCarrier:
				return Transport.CCarriers.MayRebase(state, unit, province);
			case EUnitOrder.CarrierProtection:
				return unit.Kind == EUnitKind.Air;                // the window offers it only aboard a carrier there
			// The original's tests, which ask no distance and no way (landorders.md, section 6).
			case EUnitOrder.SupportAttack:
				return CMovementRules.IsLandUnit(unit)
					&& CMovementRules.IsEnemy(state, province.Controller, unit.Owner)
					&& !unit.IsFighting && IsBeside(state, unit, province);
			case EUnitOrder.StrategicRedeployment:
				return CMovementRules.IsLandUnit(unit)
					&& !CMovementRules.IsEnemy(state, province.Controller, unit.Owner)
					&& !PathCrossesEnemy(state, unit) && !unit.IsFighting
					&& IsOwnedLand(state.Province(unit.Location)) && IsOwnedLand(province);
			case EUnitOrder.Paradrop:
				return CParadrop.MayGive(unit, province);
			case EUnitOrder.Nuke:
				return !province.IsSea && CNukeMission.MayGive(state, unit, province) && IsInRange(state, unit, order, province);
			case EUnitOrder.AirSupply:
				return CAirSupply.MayGive(unit, province);
			case EUnitOrder.AirReserve:
				return CAirReserve.MayGive(state, country, unit, province) && IsInRange(state, unit, order, province);
			default:
				return false;
		}
	}

	/// <summary>Whether the province is next to the one the unit stands in: an edge leads to it.</summary>
	private static bool IsBeside(CGameState state, CUnit unit, CProvince province)
	{
		return state.Province(unit.Location).EdgeTo(province.Id) is not null;
	}

	/// <summary>Whether a province an enemy of the unit's country holds lies on the way it already has.</summary>
	private static bool PathCrossesEnemy(CGameState state, CUnit unit)
	{
		foreach (int step in unit.Path)
		{
			if (CMovementRules.IsEnemy(state, state.Province(step).Controller, unit.Owner))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Land a country owns, which stands for the original's owner area of a province with provinces in it
	/// (<c>landorders.md</c>, section 11).
	/// </summary>
	private static bool IsOwnedLand(CProvince province)
	{
		return !province.IsSea && province.Owner is not null;
	}

	/// <summary>
	/// The order the original has picked when its window comes up: a change of base
	/// where the province has the base for one and is at peace with the unit's country -
	/// the transport mission instead for a fleet carrying anything, whether or not that
	/// may be given (<c>transport.md</c>, section 12) - and for a fleet otherwise a plain
	/// move. None where nothing may be given.
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
			return unit.Kind == EUnitKind.Navy && unit.Passengers.Count > 0 ? EUnitOrder.Transport : rebase;
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
	/// <param name="provinces">An air mission: the provinces it covers. With none said, the order's own.</param>
	/// <param name="time">An air mission: by day, by night, or both.</param>
	/// <param name="endHour">An air mission: the hour of the game after which it is over.</param>
	/// <param name="startHour">
	/// An air mission: the hour before which it is not fit to fly; with none, now. A land unit's order: the hour before
	/// which the unit does not move, or none (<c>landorders.md</c>, section 3).
	/// </param>
	/// <param name="arriveHour">A land unit's order: the hour before which it does not arrive, or none.</param>
	/// <param name="target">The unit the order names as its target, which nothing here reads (savegame.md, section 9).</param>
	public static void Give(CGameState state, CCountry country, CUnit unit, EUnitOrder order, int province,
		int stance = CMission.Defensive, int priority = 0, int convoy = 0,
		IReadOnlyList<int>? provinces = null, int time = CMission.DayAndNight, long endHour = CMission.NoEnd,
		long? startHour = null, long? arriveHour = null, CUnit? target = null)
	{
		Clear(unit);
		unit.Order = order;
		unit.OrderProvince = province;
		// A given order's halt figure is its stance's limit, the command naming none (savegame.md, section 9).
		unit.Mission = new CMission
		{
			Stance = stance, Priority = priority, Convoy = convoy, Time = time, EndHour = endHour,
			StartHour = startHour ?? state.TotalHours, Stop = CAirMissions.OrganisationLimit(state, stance),
			OrderTarget = target,
		};
		if (CMovementRules.IsLandUnit(unit))
		{
			// The order's two dates are the unit's, written before the order starts (landorders.md, section 5).
			unit.NotBeforeHour = startHour;
			unit.ArriveNotBeforeHour = arriveHour;
			// A support attack stops a unit on its way where it stands, and routes nothing (section 7).
			if (order == EUnitOrder.SupportAttack)
			{
				if (unit.IsMoving)
				{
					CMovement.Halt(state, unit);
				}
				return;
			}
		}
		if (CAirMissions.IsAirMission(order))
		{
			CAirMissions.SetProvinces(state, unit, provinces ?? new[] { province });
			CAirMissions.Start(state, country, unit);
			return;
		}
		if (CBombingOrders.IsBombingMission(order))
		{
			CAirMissions.SetProvinces(state, unit, provinces ?? new[] { province });
			CBombingMissions.Start(state, country, unit);
			return;
		}
		switch (order)
		{
			case EUnitOrder.RebaseCarrier:
				Transport.CCarriers.StartRebase(state, country, unit);
				return;
			case EUnitOrder.CarrierProtection:
				return;
			case EUnitOrder.Paradrop:
				CParadrop.TakeOff(state, country, unit);
				return;
			case EUnitOrder.Nuke:
				CNukeMission.Start(state, country, unit);
				return;
			case EUnitOrder.AirReserve:
				CAirReserve.Start(state, country, unit);
				return;
			case EUnitOrder.AirSupply:
				// Its start is the bombing frame's hour.
				CAirMissions.SetProvinces(state, unit, provinces ?? new[] { province });
				CBombingMissions.RunBombing(state, country, unit);
				return;
		}
		if (CMissions.IsMission(order))
		{
			if (order == EUnitOrder.Sortie)
			{
				unit.Mission.Waypoints.Add(province);
			}
			CMissions.Start(state, country, unit);
			return;
		}
		List<int>? route = CRouteFinder.Find(state, country, unit, unit.Location, province,
			landSafety: order == EUnitOrder.StrategicRedeployment ? ERouteSafety.VerySafe : ERouteSafety.Plain);
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
		CNukeMission.GiveBack(unit.Mission);
		unit.Order = EUnitOrder.None;
		unit.OrderProvince = 0;
		unit.Mission = new CMission();
	}

	/// <summary>
	/// A strategic redeployment's hour (<c>landorders.md</c>, section 8): done in the province; otherwise the attack
	/// delay started again, whatever happens next; a next province an enemy holds or with a battle in it ends the order
	/// where it is the target and is gone round otherwise; a battle ends it; and with no way the very safe way is found
	/// again. Where none is, the order ends. An enhanced game's fix <c>redeploy_delay_once</c> draws the delay once, as the
	/// unit arrives; on the way it holds the delay at the one with no roll in it, drawing nothing. So the unit arrives
	/// with a whole delay as the original's does, and one whose redeployment is cut short - halted, given another order,
	/// stopped by an enemy ahead - still has one, for one draw of the generator where the original makes one an hour.
	/// </summary>
	private static void RunRedeployment(CGameState state, CCountry country, CUnit unit, int target)
	{
		bool once = state.Rules.Has(Rules.EFix.RedeployDelayOnce);
		if (unit.Location == target)
		{
			if (once)
			{
				Combat.CAttackDelay.Start(state, country, unit);
			}
			Clear(unit);
			return;
		}
		if (once)
		{
			Combat.CAttackDelay.Hold(state, country, unit);
		}
		else
		{
			Combat.CAttackDelay.Start(state, country, unit);
		}
		if (unit.IsMoving)
		{
			CProvince next = state.Province(unit.Path[0]);
			if (CMovementRules.IsEnemy(state, next.Controller, unit.Owner) || Combat.CCombats.In(state, next.Id) is not null)
			{
				CMovement.Halt(state, unit);
				// The new way is given as any way is, so a unit in a battle leaves it, falling back where it was holding
				// ground; one still in a battle after that ends the order (FINDINGS-liveread.md, section 87).
				if (next.Id == target || !RouteVerySafe(state, country, unit, target) || unit.IsFighting)
				{
					Clear(unit);
				}
				return;
			}
		}
		if (unit.IsFighting)
		{
			Clear(unit);
			return;
		}
		if (!unit.IsMoving && !RouteVerySafe(state, country, unit, target))
		{
			Clear(unit);
		}
	}

	/// <summary>Gives the unit the very safe way to the province, answering whether there is one.</summary>
	private static bool RouteVerySafe(CGameState state, CCountry country, CUnit unit, int target)
	{
		if (CRouteFinder.Find(state, country, unit, unit.Location, target, landSafety: ERouteSafety.VerySafe) is not { } route)
		{
			return false;
		}
		Combat.CCombats.LeaveForMarch(state, unit);
		CMovement.SetPath(unit, route, clear: true);
		return true;
	}

	/// <summary>
	/// A support attack's hour (<c>landorders.md</c>, section 7): nothing before its hour; in a battle, the first hour
	/// starts the attack delay and every hour counts as one of attacking; out of the battle it joined, the order ends with
	/// a halt; while the delay runs it waits; with a land battle in the province a unit with no way is given the one step
	/// in, which has it join; and with none a unit with a way loses it and the order.
	/// </summary>
	private static void RunSupportAttack(CGameState state, CCountry country, CUnit unit, int target)
	{
		if (unit.NotBeforeHour is { } notBefore && state.TotalHours < notBefore)
		{
			return;
		}
		if (unit.IsFighting)
		{
			if (!unit.Mission.Joined)
			{
				Combat.CAttackDelay.Start(state, country, unit);
				unit.Mission.Joined = true;
			}
			unit.HoursAttacking++;
			return;
		}
		if (unit.Mission.Joined)
		{
			CMovement.Halt(state, unit);
			Clear(unit);
			return;
		}
		if (unit.AttackDelay > CFixedPoint.Zero)
		{
			return;
		}
		if (Combat.CCombats.In(state, target) is not null)
		{
			if (!unit.IsMoving && CRouteFinder.Find(state, country, unit, unit.Location, target) is { } route)
			{
				CMovement.SetPath(unit, route, clear: true);
			}
			return;
		}
		if (unit.IsMoving)
		{
			CMovement.Halt(state, unit);
			Clear(unit);
		}
	}

	/// <summary>
	/// What having no order does each hour: an air unit that is away from its base, has
	/// no way to go, is in no battle and no bombing, and has a base is given the way home
	/// - where there is one. So a mission taken back over enemy ground flies home as soon
	/// as its bombing or its battle is over. Nothing else is moved.
	/// </summary>
	private static void HeadHome(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Kind != EUnitKind.Air || unit.IsMoving || unit.Base == 0 || CAirMissions.IsAtOwnBase(unit)
			|| unit.IsFighting || unit.Bombing is not null)
		{
			return;
		}
		if (CRouteFinder.Find(state, country, unit, unit.Location, unit.Base) is { } route)
		{
			CMovement.SetPath(unit, route, clear: true);
		}
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
			HeadHome(state, country, unit);
			// An air group aboard a carrier with nothing to do keeps over it (transport.md, section 13.1).
			Transport.CCarriers.GiveDuty(unit);
			return;
		}
		if (Agents.CJoinOrders.IsJoin(unit.Order))
		{
			if (!Agents.CJoinOrders.RunHour(state, country, unit))
			{
				Clear(unit);
			}
			return;
		}
		if (unit.Order is EUnitOrder.RebaseCarrier or EUnitOrder.CarrierProtection or EUnitOrder.Paradrop
			or EUnitOrder.AirSupply or EUnitOrder.Nuke or EUnitOrder.AirReserve)
		{
			bool going = unit.Order switch
			{
				EUnitOrder.RebaseCarrier => Transport.CCarriers.RunRebase(state, country, unit),
				EUnitOrder.CarrierProtection => Transport.CCarriers.RunProtection(state, country, unit),
				EUnitOrder.Paradrop => CParadrop.RunHour(state, country, unit),
				EUnitOrder.Nuke => CNukeMission.RunHour(state, country, unit),
				EUnitOrder.AirReserve => CAirReserve.RunHour(state, country, unit),
				_ => CAirSupply.RunHour(state, country, unit),
			};
			if (!going)
			{
				Clear(unit);
			}
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
		if (CAirMissions.IsAirMission(unit.Order))
		{
			if (!CAirMissions.RunHour(state, country, unit))
			{
				Clear(unit);
			}
			return;
		}
		if (CBombingOrders.IsBombingMission(unit.Order))
		{
			if (!CBombingMissions.RunHour(state, country, unit))
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

			case EUnitOrder.StrategicRedeployment:
				RunRedeployment(state, country, unit, target);
				break;

			case EUnitOrder.SupportAttack:
				RunSupportAttack(state, country, unit, target);
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
				// A carrier is a base of its own, so a group aboard one leaves it even for its port's airfield.
				if (unit.Base != target || unit.CarriedBy is not null)
				{
					unit.Base = target;
					Transport.CCarriers.LeaveCarrier(state, unit);
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
