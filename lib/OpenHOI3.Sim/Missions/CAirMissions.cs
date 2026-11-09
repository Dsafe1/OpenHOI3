using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// An air unit's fighting missions - air superiority and the intercept - and the frame
/// every air mission shares: the gate that keeps a mission by day out of the night, the
/// test of whether a unit is fit to fly, and the hourly asking that sends a unit home
/// and, home again, out once more until its end hour. See
/// <c>docs/mechanics/aircombat.md</c>, sections 1 to 4.
/// </summary>
public static class CAirMissions
{
	public const string AirAttack = "air_attack";

	private static readonly string[] StrengthLimits =
	{
		"AGGRESSIVE_STRENGTH_LIMIT", "DEFENSIVE_STRENGTH_LIMIT", "PASSIVE_STRENGTH_LIMIT",
	};

	private static readonly string[] OrganisationLimits =
	{
		"AGGRESSIVE_ORGANISATION_LIMIT", "DEFENSIVE_ORGANISATION_LIMIT", "PASSIVE_ORGANISATION_LIMIT",
	};

	/// <summary>The define's organisation limit of a stance: aggressive, passive, and defensive for any other.</summary>
	public static CFixedPoint OrganisationLimit(CGameState state, int stance)
	{
		string key = stance switch
		{
			CMission.Aggressive => OrganisationLimits[0],
			CMission.Passive => OrganisationLimits[2],
			_ => OrganisationLimits[1],
		};
		return state.Defines[CManpower.MilitarySection, key];
	}

	/// <summary>The orders under which an air unit starts and joins battles in the air.</summary>
	public static bool IsAirMission(EUnitOrder order)
	{
		return order is EUnitOrder.AirSuperiority or EUnitOrder.AirIntercept;
	}

	/// <summary>
	/// Whether an order is one an air unit flies under at all: a change of base, the bombing missions, and the
	/// carrier's, the paradrop and the air supply among them.
	/// </summary>
	public static bool IsAirOrder(EUnitOrder order)
	{
		return IsAirMission(order) || order == EUnitOrder.RebaseAir || CBombingOrders.IsBombingMission(order)
			|| order is EUnitOrder.RebaseCarrier or EUnitOrder.CarrierProtection or EUnitOrder.Paradrop
				or EUnitOrder.AirSupply or EUnitOrder.Nuke;
	}

	/// <summary>
	/// Whether a unit fights in the air - starts and joins battles there - under its order: under a fighting mission,
	/// and under carrier protection while its carrier's province has a battle (<c>transport.md</c>, section 13.2).
	/// </summary>
	public static bool FightsInTheAir(CGameState state, CUnit unit)
	{
		return IsAirMission(unit.Order) || Transport.CCarriers.FightsInTheAir(state, unit);
	}

	/// <summary>A mission may be given for any province the unit may stand in.</summary>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, EUnitOrder order, CProvince province)
	{
		return unit.Kind == EUnitKind.Air && IsAirMission(order)
			&& CMovementRules.MayStandIn(state, country, unit, province);
	}

	/// <summary>
	/// Whether a unit is in the province of its base - for an air group aboard a carrier, where the carrier's fleet is,
	/// and only while that fleet is in no battle and no bombing (<c>transport.md</c>, section 13.1).
	/// </summary>
	public static bool IsAtOwnBase(CUnit unit)
	{
		if (unit.Base == 0 || unit.Location != unit.Base)
		{
			return false;
		}
		return unit.CarriedBy?.Unit is not { } fleet || (!fleet.IsFighting && fleet.Bombing is null);
	}

	/// <summary>
	/// Keeps of a list the provinces a mission takes, each once and in the order given:
	/// for a fighting mission the ones within the unit's range of its base, and for a
	/// bombing mission the ones its own test lets on - which does not ask how far they
	/// are.
	/// </summary>
	public static void SetProvinces(CGameState state, CUnit unit, IEnumerable<int> provinces)
	{
		List<int> kept = unit.Mission.Provinces;
		kept.Clear();
		foreach (int id in provinces)
		{
			if (id <= 0 || id >= state.Provinces.Count || kept.Contains(id))
			{
				continue;
			}
			if (MayList(state, unit, unit.Order, state.Province(id)))
			{
				kept.Add(id);
			}
		}
	}

	/// <summary>
	/// Whether an air order may list a province, as the original's order answers it: a bombing
	/// mission by its own test, which asks no range, and anything else by the range.
	/// </summary>
	public static bool MayList(CGameState state, CUnit unit, EUnitOrder order, CProvince province)
	{
		if (order == EUnitOrder.AirSupply)
		{
			return CAirSupply.MayList(state, unit, province);
		}
		return CBombingOrders.IsBombingMission(order)
			? CBombingOrders.MayList(state, unit, order, province)
			: CUnitOrders.IsInRange(state, unit, order, province);
	}

	/// <summary>Sets a unit off on the mission it has just been given.</summary>
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		// Away from its base it is sent home first, whatever the mission.
		if (unit.Base != 0 && unit.Location != unit.Base)
		{
			ReturnToBase(state, country, unit);
		}
		switch (unit.Order)
		{
			case EUnitOrder.AirSuperiority:
				CAirSuperiority.RunHour(state, country, unit);
				break;
			case EUnitOrder.AirIntercept:
				CAirIntercept.BuildWatch(state, unit);
				CAirIntercept.RunHour(state, country, unit);
				break;
		}
	}

	/// <summary>
	/// A mission's hour, before the unit moves: the mission's own, and then the frame's.
	/// Answers whether the order is still going.
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		switch (unit.Order)
		{
			case EUnitOrder.AirSuperiority:
				CAirSuperiority.RunHour(state, country, unit);
				break;
			case EUnitOrder.AirIntercept:
				CAirIntercept.RunHour(state, country, unit);
				// Beyond its range where it stands it turns for home and forgets where it was going.
				if (!CUnitOrders.IsInRange(state, unit, unit.Order, state.Province(unit.Location)))
				{
					ReturnToBase(state, country, unit);
					unit.Mission.Destination = 0;
					return true;
				}
				break;
		}
		return IsStillGoing(state, country, unit);
	}

	/// <summary>
	/// What every air order asks each hour: the gate; a unit beyond its range from its
	/// base turns for home and loses its order; and a unit that has come home under
	/// "return to base" is no longer returning - and is done once its end hour is past.
	/// </summary>
	public static bool IsStillGoing(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (unit.Base == 0)
		{
			return false;
		}
		if (!mission.ReturnToBase)
		{
			DayNightGate(state, country, unit);
			if (!mission.ReturnToBase && !unit.IsFighting && unit.Bombing is null
				&& !CUnitOrders.IsInRange(state, unit, unit.Order, state.Province(unit.Location)))
			{
				ReturnToBase(state, country, unit);
				return false;
			}
		}
		if (mission.ReturnToBase)
		{
			if (unit.Location == unit.Base)
			{
				mission.ReturnToBase = false;
				return state.TotalHours <= mission.EndHour;
			}
			if (!unit.IsMoving && !unit.IsFighting)
			{
				ReturnToBase(state, country, unit);
			}
		}
		return true;
	}

	/// <summary>
	/// Whether a mission may fly this hour. One by day at night, or by night by day, is
	/// stopped where it is at its base and told to return otherwise.
	/// </summary>
	public static bool DayNightGate(CGameState state, CCountry country, CUnit unit)
	{
		int time = unit.Mission.Time;
		if (time != CMission.ByDay && time != CMission.ByNight)
		{
			return true;
		}
		bool isNight = CDaylight.IsNight(state, state.Province(unit.Location));
		if ((time == CMission.ByDay) != isNight)
		{
			return true;
		}
		if (unit.Location == unit.Base)
		{
			if (unit.IsMoving)
			{
				CMovement.Cancel(unit);
			}
		}
		else
		{
			ReturnToBase(state, country, unit);
		}
		return false;
	}

	/// <summary>
	/// Tells a unit to return to base: its order remembers it until it is home, and it
	/// is given the way there where it is not. A wing in a battle does not move, so the
	/// way waits for the battle to let it go.
	/// </summary>
	public static void ReturnToBase(CGameState state, CCountry country, CUnit unit)
	{
		unit.Mission.ReturnToBase = true;
		if (unit.Base != 0 && unit.Location != unit.Base)
		{
			CMovement.SetPath(unit, CRouteFinder.Find(state, country, unit, unit.Location, unit.Base) ?? new List<int>(),
				clear: true);
		}
	}

	/// <summary>
	/// Whether a unit given its way to a province would come into the next province in the wrong half of the day for
	/// its order - by day at night, by night by day - judged over the province it is bound for. One flying day and
	/// night never is.
	/// </summary>
	public static bool ArrivesInWrongLight(CGameState state, CCountry country, CUnit unit, CProvince goal)
	{
		int time = unit.Mission.Time;
		if (time != CMission.ByDay && time != CMission.ByNight)
		{
			return false;
		}
		long arrival = state.TotalHours + System.Math.Max(0, CMovement.HoursToNext(state, country, unit));
		bool isNight = CDaylight.IsNight(state, goal, (int)(arrival % CGameState.HoursPerDay),
			CDate.FromDayNumber((int)(arrival / CGameState.HoursPerDay)).Month - 1);
		return (time == CMission.ByDay && isNight) || (time == CMission.ByNight && !isNight);
	}

	/// <summary>
	/// Gives a unit the way to a province and judges the light as a strike does: where it would arrive in the wrong
	/// light, a unit standing in its base's province has its way cancelled and any other is told to return to base.
	/// </summary>
	/// <param name="judgeWithoutWay">Whether the light is judged where no way was found - a strike's is; a paradrop's not.</param>
	public static void FlyTo(CGameState state, CCountry country, CUnit unit, CProvince goal, bool judgeWithoutWay = true)
	{
		if (!GoTo(state, country, unit, goal.Id) && !judgeWithoutWay)
		{
			return;
		}
		if (!ArrivesInWrongLight(state, country, unit, goal))
		{
			return;
		}
		if (unit.Location != unit.Base)
		{
			ReturnToBase(state, country, unit);
		}
		else if (unit.IsMoving)
		{
			CMovement.Cancel(unit);
		}
	}

	/// <summary>Gives a unit the way to a province, where there is one. Answers whether there was.</summary>
	public static bool GoTo(CGameState state, CCountry country, CUnit unit, int province)
	{
		if (CRouteFinder.Find(state, country, unit, unit.Location, province) is not { } route)
		{
			return false;
		}
		CMovement.SetPath(unit, route, clear: true);
		return true;
	}

	/// <summary>
	/// Whether a unit is fit to fly: under an air order whose start hour has come, with
	/// the share of its strength and of its organisation that the order's stance asks
	/// for. A unit under no air order is never fit, which is what keeps one sitting idle
	/// at its base out of every air battle.
	/// </summary>
	public static bool IsFitToFly(CGameState state, CUnit unit)
	{
		if (!IsAirOrder(unit.Order) || state.TotalHours < unit.Mission.StartHour)
		{
			return false;
		}
		int stance = unit.Mission.Stance is CMission.Aggressive or CMission.Passive ? unit.Mission.Stance : CMission.Defensive;
		CDefines defines = state.Defines;

		long strength = 0;
		long full = 0;
		long most = 0;
		foreach (CSubUnit wing in unit.SubUnits)
		{
			strength += wing.Strength.Raw;
			full += wing.Stats[CSubUnitType.MaxStrength].Raw;
			most += wing.Stats[CSubUnitType.DefaultOrganisation].Raw;
		}
		long share = full == 0 ? -1 : strength * CFixedPoint.Scale / full;
		if (share < defines[CManpower.MilitarySection, StrengthLimits[stance]].Raw)
		{
			return false;
		}
		int count = unit.SubUnits.Count;
		long average = count == 0 ? 0 : most / count;
		long organisation = average == 0 ? -1 : CUnitTotals.AverageOrganisation(unit).Raw * CFixedPoint.Scale / average;
		return organisation >= defines[CManpower.MilitarySection, OrganisationLimits[stance]].Raw;
	}

	/// <summary>
	/// What the unit's own hour does to an air unit away from its base: with next to no
	/// strength left it is removed, and with no organisation at all it loses its order
	/// and is given a plain way home.
	/// </summary>
	public static void SendHomeEmpty(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Kind != EUnitKind.Air || unit.IsRemoved || IsAtOwnBase(unit))
		{
			return;
		}
		if (CUnitTotals.Strength(unit).Raw < 1)
		{
			Combat.CCombatRetreat.RemoveFromGame(state, unit);
			return;
		}
		// One already on a plain way somewhere is left to it.
		if (unit.Base == 0 || (unit.Order == EUnitOrder.None && unit.IsMoving)
			|| OrganisationShare(state, country, unit) >= 1)
		{
			return;
		}
		CUnitOrders.Clear(unit);
		GoTo(state, country, unit, unit.Base);
	}

	/// <summary>The average over a unit's wings of its organisation as a share of the most it may have, in thousandths.</summary>
	public static long OrganisationShare(CGameState state, CCountry country, CUnit unit)
	{
		long sum = 0;
		foreach (CSubUnit wing in unit.SubUnits)
		{
			long most = COrganisation.Ceiling(state, country, wing).Raw;
			sum += most == 0 ? -1 : wing.Organisation.Raw * CFixedPoint.Scale / most;
		}
		return unit.SubUnits.Count == 0 ? 0 : sum / unit.SubUnits.Count;
	}
}
