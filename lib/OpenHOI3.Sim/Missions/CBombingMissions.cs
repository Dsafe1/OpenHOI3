using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The hour of a bombing mission. There are two frames. A strike - ground attack,
/// interdiction, the naval strike - keeps the province it flies at from sortie to sortie
/// and picks another only when that one is no longer the enemy's or it arrived and
/// nothing started. A bombing - the strategic kinds and the port strike - forgets its
/// province the moment a bombing has it and picks afresh at its base. Either way a unit
/// flies out, bombs, and is told to return the hour after. Written to do what the
/// original does step for step. See <c>docs/mechanics/bombing.md</c>, sections 2 and 3.
/// </summary>
public static class CBombingMissions
{
	/// <summary>Sets a unit off on the mission it has just been given.</summary>
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		// Away from its base it is sent home first, whatever the mission.
		if (unit.Base != 0 && unit.Location != unit.Base)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		RunFrame(state, country, unit);
	}

	/// <summary>A mission's hour, before the unit moves. Answers whether the order is still going.</summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		RunFrame(state, country, unit);
		// A port strike with nowhere in mind and no port of an enemy's left on its list is done.
		if (unit.Order == EUnitOrder.PortStrike && unit.Mission.Destination == 0 && !HasListed(state, unit))
		{
			return false;
		}
		return CAirMissions.IsStillGoing(state, country, unit);
	}

	private static void RunFrame(CGameState state, CCountry country, CUnit unit)
	{
		if (CBombingOrders.IsStrike(unit.Order))
		{
			RunStrike(state, country, unit);
		}
		else
		{
			RunBombing(state, country, unit);
		}
	}

	/// <summary>The hour of a strike: section 2.</summary>
	public static void RunStrike(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (mission.ReturnToBase)
		{
			return;
		}
		bool isFit = CAirMissions.IsFitToFly(state, unit);
		if (unit.Base != 0 && unit.Location == unit.Base && !isFit)
		{
			return;
		}
		if (unit.IsFighting || unit.Bombing is not null)
		{
			return;
		}
		if (!isFit)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		if (unit.FuelReceived == CFixedPoint.Zero)
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}
		if (unit.IsMoving)
		{
			return;
		}
		if (state.TotalHours > mission.EndHour)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		if (unit.NoBombingHours > 0)
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}

		CProvince? strike = mission.Destination == 0 ? null : state.Province(mission.Destination);
		if (strike is null || (!strike.IsSea && !CMovementRules.IsEnemy(state, strike.Controller, unit.Owner)))
		{
			// Nowhere to go, or the place has changed hands: another is picked at its base.
			if (unit.Location == unit.Base && CAirMissions.IsFitToFly(state, unit))
			{
				mission.Destination = CBombingProvinces.Pick(state, unit);
			}
			else if (unit.Location == unit.Base)
			{
				CAirMissions.ReturnToBase(state, country, unit);
			}
			if (mission.Destination == 0)
			{
				CAirMissions.ReturnToBase(state, country, unit);
				return;
			}
			CProvince picked = state.Province(mission.Destination);
			if (!picked.IsSea && !CMovementRules.IsEnemy(state, picked.Controller, unit.Owner))
			{
				CAirMissions.ReturnToBase(state, country, unit);
			}
			return;
		}

		if (strike.Id != unit.Location || unit.Base == 0)
		{
			FlyOut(state, country, unit, strike);
			return;
		}

		// Standing in the province with no bombing: it arrived and nothing started.
		if (state.TotalHours > mission.EndHour)
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}
		mission.Destination = CBombingProvinces.Pick(state, unit);
		if (mission.Destination == 0)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
	}

	/// <summary>The hour of a bombing: section 3.</summary>
	public static void RunBombing(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (mission.ReturnToBase || unit.IsFighting)
		{
			return;
		}
		if (unit.Bombing is not null)
		{
			mission.Destination = 0;
			return;
		}
		if (!CAirMissions.IsFitToFly(state, unit))
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		if (unit.FuelReceived == CFixedPoint.Zero)
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}
		if (unit.IsMoving)
		{
			return;
		}
		if (unit.NoBombingHours > 0)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		if (state.TotalHours > mission.EndHour)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}

		if (mission.Destination == 0)
		{
			if (unit.Location == unit.Base && CAirMissions.IsFitToFly(state, unit))
			{
				mission.Destination = CBombingProvinces.Pick(state, unit);
			}
			else if (unit.Location == unit.Base)
			{
				CAirMissions.ReturnToBase(state, country, unit);
			}
			if (mission.Destination == 0)
			{
				CAirMissions.ReturnToBase(state, country, unit);
			}
			return;
		}
		if (mission.Destination == unit.Location && unit.Base != 0)
		{
			CAirMissions.ReturnToBase(state, country, unit);
			return;
		}
		FlyOut(state, country, unit, state.Province(mission.Destination));
	}

	/// <summary>
	/// Gives a unit the way to the province it is to bomb, and judges the light: where
	/// the hour it will come into the next province is the wrong half of the day for its
	/// mission, over the province it is bound for, it stays - stopped at its base, sent
	/// home from anywhere else.
	/// </summary>
	private static void FlyOut(CGameState state, CCountry country, CUnit unit, CProvince strike)
	{
		CAirMissions.GoTo(state, country, unit, strike.Id);
		int time = unit.Mission.Time;
		long arrival = state.TotalHours + System.Math.Max(0, CMovement.HoursToNext(state, country, unit));
		bool isNight = CDaylight.IsNight(state, strike, (int)(arrival % CGameState.HoursPerDay),
			CDate.FromDayNumber((int)(arrival / CGameState.HoursPerDay)).Month - 1);
		bool isWrongLight = (time == CMission.ByDay && isNight) || (time == CMission.ByNight && !isNight);
		if (!isWrongLight)
		{
			return;
		}
		if (unit.Location != unit.Base)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		else if (unit.IsMoving)
		{
			CMovement.Cancel(unit);
		}
	}

	private static bool HasListed(CGameState state, CUnit unit)
	{
		foreach (int id in unit.Mission.Provinces)
		{
			if (CBombingOrders.MayList(state, unit, unit.Order, state.Province(id)))
			{
				return true;
			}
		}
		return false;
	}
}
