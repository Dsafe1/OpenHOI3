using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
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
		// A halt empties both of the land orders window's dates (landorders.md, section 5).
		unit.NotBeforeHour = null;
		unit.ArriveNotBeforeHour = null;
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
		return LegDistance(state, unit, state.Province(unit.Location), state.Province(unit.Path[0]));
	}

	/// <summary>
	/// How far one leg is: a land unit's from the sea to land is the landing's
	/// (<c>transport.md</c>, section 8), whoever's coast it is; any other the edge's.
	/// </summary>
	public static CFixedPoint LegDistance(CGameState state, CUnit unit, CProvince here, CProvince next)
	{
		if (CMovementRules.IsLandUnit(unit) && here.IsSea && !next.IsSea)
		{
			return Transport.CLanding.Distance(state);
		}
		return here.EdgeTo(next.Id)?.Distance ?? DistanceWithoutEdge;
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
			long distance = LegDistance(state, unit, here, next).Raw;
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
			CProvince next = state.Province(id);
			whole += LegDistance(state, unit, here, next).Raw;
			here = next;
		}
		if (whole <= 0)
		{
			return 0;
		}
		return System.Math.Clamp(unit.MovementProgress.Raw * CFixedPoint.Scale / whole, 0, CFixedPoint.Scale);
	}

	/// <summary>
	/// Every unit's hour as far as going somewhere and meeting an enemy is concerned,
	/// country by country in the order of the units' numbers. A unit of a country at
	/// peace that is going nowhere has nothing to do in it - but an air unit away from its
	/// base always has, since with no order its hour is what sends it home, and so has one
	/// aboard a carrier, whose hour gives it the carrier's duty.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		state.ForgetPositions();
		foreach (CCountry country in state.Countries)
		{
			bool atWar = state.IsAtWar(country.Tag);
			List<CUnit>? busy = null;
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsMoving || unit.Order != EUnitOrder.None || unit.IsFighting || unit.Bombing is not null
					|| unit.IsRetreating
					|| unit.AttackDelay > CFixedPoint.Zero || unit.HoursAttacking > 0
					|| unit.EngageWait > 0 || unit.NoBattleHours > 0 || unit.NoBombingHours > 0
					|| (atWar && (CMovementRules.IsLandUnit(unit) || unit.Kind == EUnitKind.Navy))
					// An air unit away from its base has its hour whatever it is doing:
					// with no order, that hour is what sends it home.
					|| (unit.Kind == EUnitKind.Air && unit.Base != 0 && !Missions.CAirMissions.IsAtOwnBase(unit))
					// And one aboard a carrier with no order, whose hour gives it the carrier's duty.
					|| (unit.Kind == EUnitKind.Air && unit.CarriedBy is not null))
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
				if (!unit.IsRemoved)
				{
					RunUnitHour(state, country, unit);
				}
			}
		}
	}

	/// <summary>
	/// One unit's hour, in the original's order: the look at its order; for a land unit
	/// with somewhere to go, the look for an enemy in the province ahead; the battles
	/// it is no longer beside are left and its attack delay is seen to; it marches; and
	/// with nowhere to go and no battle it looks at the province it stands in.
	/// </summary>
	public static void RunUnitHour(CGameState state, CCountry country, CUnit unit)
	{
		// A fleet's wait after a battle runs down first of all.
		bool isFleet = unit.Kind == EUnitKind.Navy;
		if (isFleet && unit.EngageWait > 0)
		{
			unit.EngageWait--;
		}
		// A fleet in a battle or a bombing sends its carriers' aircraft in (transport.md, section 13.2).
		if (isFleet && (unit.IsFighting || unit.Bombing is not null))
		{
			Transport.CCarriers.SendAircraft(state, unit);
		}
		// An air unit found in a battle cannot be taken into another for two of its hours.
		bool isAir = unit.Kind == EUnitKind.Air;
		if (isAir && unit.IsFighting)
		{
			unit.NoBattleHours = CAirBattle.Grace;
		}
		// And one found in a bombing starts no other for as long.
		if (isAir && unit.Bombing is not null)
		{
			unit.NoBombingHours = CBombing.Grace;
		}
		CUnitOrders.RunHour(state, country, unit);
		// A unit merged into another by its join order is gone (agents.md, section 5.4).
		if (unit.IsRemoved)
		{
			return;
		}
		if (isAir)
		{
			Missions.CAirMissions.SendHomeEmpty(state, country, unit);
			if (unit.IsRemoved)
			{
				return;
			}
		}
		// A unit in a battle, or in a bombing, gains its hour's experience.
		if (unit.Combats.Count > 0 || unit.Bombing is not null)
		{
			CCombatExperience.Grant(state, unit, convoyRaid: false);
		}
		bool isLand = CMovementRules.IsLandUnit(unit);
		if (isLand && unit.IsMoving)
		{
			LookAhead(state, country, unit);
			if (unit.IsRemoved)
			{
				return;
			}
		}
		if (isLand)
		{
			CDigIn.RunHour(state, country, unit);
			CCombats.LeaveDistant(state, unit);
			CAttackDelay.RunHour(state, country, unit);
		}
		if (unit.IsMoving)
		{
			RunHour(state, country, unit);
		}
		if ((isLand || isFleet || isAir) && !unit.IsRemoved && !unit.IsMoving && !unit.IsRetreating && !unit.IsFighting)
		{
			CCombats.CheckForCombat(state, unit, unit.Location);
		}
		if (isAir && unit.NoBattleHours > 0)
		{
			unit.NoBattleHours--;
		}
		if (isAir && unit.NoBombingHours > 0)
		{
			unit.NoBombingHours--;
		}
	}

	/// <summary>
	/// A land unit looks at the province it is walking to. With an enemy standing there
	/// and no battle of its own for it yet, a unit that was given its supplies starts
	/// one or joins the one there - unless an attack delay holds it before its first
	/// step. A unit falling back with enemies where it stands as well is lost, unless
	/// the province ahead is its own country's and a friend holds it; one that has
	/// shaken them off turns and fights.
	/// </summary>
	public static void LookAhead(CGameState state, CCountry country, CUnit unit)
	{
		int next = unit.Path[0];
		if (!CCombats.HasEnemyStanding(state, unit, next) || CCombats.HasCombatIn(unit, next)
			|| unit.SuppliesReceived.Raw < 1)
		{
			return;
		}
		// Nor before the hour its order is to start at, when it does not move at all (landorders.md, section 5; ours, that
		// this holds back the look too).
		if (unit.NotBeforeHour is { } notBefore && state.TotalHours < notBefore)
		{
			return;
		}
		if (!unit.IsRetreating)
		{
			if (unit.AttackDelay > CFixedPoint.Zero && unit.MovementProgress.Raw < 1)
			{
				return;
			}
		}
		else
		{
			if (CCombats.HasEnemyStanding(state, unit, unit.Location))
			{
				if (!IsHeldByAFriend(state, country, unit, next))
				{
					StrategicWarfare.CWarfareTally.UnitLost(state, state.Province(next).Controller, unit);
					CCombatRetreat.RemoveFromGame(state, unit);
				}
				return;
			}
			unit.IsRetreating = false;
		}
		CCombats.CheckForCombat(state, unit, next);
	}

	/// <summary>Whether a province is the unit's own country's and a friend's land unit stands in it.</summary>
	private static bool IsHeldByAFriend(CGameState state, CCountry country, CUnit unit, int provinceId)
	{
		if (state.Province(provinceId).Controller != unit.Owner)
		{
			return false;
		}
		foreach (CUnit other in state.UnitsIn(provinceId))
		{
			if (CMovementRules.IsLandUnit(other) && !other.IsRetreating
				&& state.Country(other.Owner) is { } standing && CCountryLists.IsFriend(state, standing, unit.Owner, withAccess: true))
			{
				return true;
			}
		}
		return false;
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
		// A unit with an attack delay running does not take its first step.
		if (unit.MovementProgress == CFixedPoint.Zero && unit.AttackDelay > CFixedPoint.Zero
			&& unit.Order != EUnitOrder.StrategicRedeployment)
		{
			return false;
		}
		// A unit that cannot fight, about to land on ground an enemy holds, waits the hour
		// where a land unit of its country that can is bound there; otherwise it is
		// halted (transport.md, section 8).
		if (Transport.CLanding.IsLandingOnEnemy(state, unit) && !CUnitTotals.CanFight(unit))
		{
			if (!Transport.CLanding.OtherFighterBoundFor(state, country, unit, unit.Path[0]))
			{
				Halt(state, unit);
			}
			return false;
		}
		// Before the hour its order was given to start at, it does not move (landorders.md, section 5).
		if (unit.NotBeforeHour is { } notBefore && state.TotalHours < notBefore)
		{
			return false;
		}
		if (unit.Path[0] == unit.Location)
		{
			unit.Path.RemoveAt(0);
			unit.MovementProgress = CFixedPoint.Zero;
			return false;
		}

		// An air unit in a bombing stays over it, unless it is falling back.
		if (unit.Kind == EUnitKind.Air && unit.Bombing is not null && !unit.IsRetreating)
		{
			return false;
		}

		CProvince here = state.Province(unit.Location);
		CProvince next = state.Province(unit.Path[0]);
		bool isLand = CMovementRules.IsLandUnit(unit);

		// Nobody walks onto an enemy's ground with nothing to eat. Falling back, it is lost.
		if (isLand && CMovementRules.IsEnemy(state, next.Controller, unit.Owner)
			&& !CCombats.HasCombatIn(unit, next.Id) && unit.SuppliesReceived == CFixedPoint.Zero)
		{
			if (unit.IsRetreating)
			{
				StrategicWarfare.CWarfareTally.UnitLost(state, next.Controller, unit);
				CCombatRetreat.RemoveFromGame(state, unit);
			}
			else
			{
				Halt(state, unit);
			}
			return false;
		}

		CFixedPoint distance = Distance(state, unit);
		if (addSpeed && unit.Order != EUnitOrder.SupportAttack)
		{
			unit.MovementProgress += CMovementSpeed.Speed(state, country, unit);
		}
		if (unit.MovementProgress < CFixedPoint.Zero)
		{
			unit.MovementProgress = CFixedPoint.Zero;
		}

		// A unit in a battle walks up to the far end of its way and no further: it does
		// not enter while it fights.
		if (unit.IsFighting)
		{
			if (unit.MovementProgress > distance)
			{
				unit.MovementProgress = distance;
			}
			return false;
		}
		// A unit on its way and in no battle is done with waiting: the hours in which an
		// air unit starts no battle and no bombing end with its first move.
		unit.NoBombingHours = 0;
		unit.NoBattleHours = 0;

		// The way has to be there still, and open, every hour and not only on arrival.
		if (here.EdgeTo(next.Id) is not CProvinceEdge edge
			|| CMovementRules.IsCrossingBlocked(state, country, unit, edge))
		{
			Halt(state, unit);
			return false;
		}
		if (unit.MovementProgress < distance)
		{
			return false;
		}
		// Before the hour it is to arrive by, it waits at the next province with its progress full (landorders.md, section 5).
		if (unit.ArriveNotBeforeHour is { } arrival && state.TotalHours < arrival)
		{
			unit.MovementProgress = distance;
			return false;
		}

		unit.Path.RemoveAt(0);
		unit.MovementProgress -= distance;
		if (unit.AttackDelay > CFixedPoint.Zero)
		{
			unit.MovementProgress = CFixedPoint.Zero;
		}
		// Whoever leaves a province leaves the bombing of it.
		CBombing.Leave(state, unit);
		unit.IsRetreating = false;
		if (!CMovementRules.MayStandIn(state, country, unit, next))
		{
			Halt(state, unit);
			return false;
		}
		// A land unit marching into the sea boards the first fleet there that may take it,
		// and its way ends (transport.md, section 3).
		if (isLand && next.IsSea)
		{
			if (Transport.CTransport.FleetToBoard(state, unit, next) is not { } fleet)
			{
				Halt(state, unit);
				return false;
			}
			Transport.CTransport.Board(fleet, unit);
			unit.Previous = unit.Location;
			Enter(state, unit, next.Id);
			Cancel(unit);
			return false;
		}
		// A passenger stepping off - ashore from the sea, or out of its port - gets off first,
		// its days of supplies going into the province it leaves: out of the stockpile, its
		// previous province being cleared (transport.md, sections 4 and 8).
		if (isLand && unit.Transport is not null)
		{
			unit.Previous = 0;
			Transport.CTransport.GetOff(state, unit, bringSupplies: true, from: 0);
		}
		unit.Previous = unit.Location;
		// An army takes its days of supplies along, out of the province it leaves.
		if (isLand && !next.IsSea)
		{
			CSupplyCarry.Bring(state, country, unit, unit.Previous, next.Id);
		}
		Enter(state, unit, next.Id);
		if (unit.Kind == EUnitKind.Navy)
		{
			// New water ends a fleet's wait after its last battle.
			unit.EngageWait = 0;
			CCombats.CheckForCombat(state, unit, next.Id);
		}
		if (isLand || unit.Kind == EUnitKind.Air)
		{
			CCombats.CheckForCombat(state, unit, next.Id);
		}

		return !unit.IsRemoved && unit.IsMoving && !unit.IsFighting
			&& unit.MovementProgress >= Distance(state, unit);
	}

	/// <summary>
	/// Stops a unit where it stands, as its march does when the way is gone: it leaves
	/// the battles it was attacking in, and standing on an enemy's ground with nobody to
	/// fight it looks at the province it is in.
	/// </summary>
	public static void Halt(CGameState state, CUnit unit)
	{
		CCombats.LeaveAttacks(state, unit);
		Cancel(unit);
		if (CMovementRules.IsLandUnit(unit) && !unit.IsRetreating && !unit.IsFighting)
		{
			CCombats.CheckForCombat(state, unit, unit.Location);
		}
	}

	/// <summary>Puts a unit in a province, with whatever its ships carry and the land units aboard it.</summary>
	internal static void Enter(CGameState state, CUnit unit, int province)
	{
		int left = unit.Location;
		state.Positions.Move(unit, unit.Location, province);
		unit.Location = province;
		if (unit.Kind == EUnitKind.Air)
		{
			Transport.CAirTransport.Follow(state, unit, province);
			return;
		}
		Transport.CTransport.Follow(state, unit, province);
		Transport.CCarriers.Follow(state, unit, left, province);
	}
}
