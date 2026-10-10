using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// How a fleet gets out of a battle at sea that it has not lost: the count it keeps
/// while it breaks away, the order to leave that only starts the count, the battle's own
/// hourly roll that sets every fleet counting, and the wait a fleet let out of a battle
/// has before it will start another. See <c>docs/mechanics/navalcombat.md</c>, section 6.
/// </summary>
public static class CNavalDisengage
{
	/// <summary>The count at which a fleet has had enough. The original scales it by a leader's trait.</summary>
	public const int CountToLeave = 5;

	/// <summary>A battle at sea lets nobody out before it has been fought for more than this many rounds.</summary>
	public const int RoundsBeforeLeaving = 3;

	/// <summary>The roll to break off is not made before this many rounds have been fought.</summary>
	private const int RoundsBeforeBreakingOff = 3;

	/// <summary>What the roll is against besides the rounds: so much always, and so much more by day or by night.</summary>
	private const long BreakOffBase = 10000;
	private const long BreakOffByDay = 1000;
	private const long BreakOffByNight = 11000;

	/// <summary>The hours a fleet waits after a battle: so many, and a roll of as many again.</summary>
	private const int LeastWait = 8;
	private const int WaitRoll = 8;

	public static bool Wants(CUnit fleet)
	{
		return fleet.Disengage >= CountToLeave;
	}

	/// <summary>Whether a fleet in a battle at sea may be given a path at all yet.</summary>
	public static bool MayLeave(CUnit fleet)
	{
		foreach (CCombat combat in fleet.Combats)
		{
			if (combat.Kind != ECombatKind.Naval || combat.Duration > RoundsBeforeLeaving)
			{
				return true;
			}
		}
		return fleet.Combats.Count == 0;
	}

	/// <summary>
	/// A fleet in a battle is told to go somewhere. Until it has counted long enough the
	/// order is only kept and the count started; answers whether that is what happened.
	/// </summary>
	public static bool Defer(CUnit fleet, int province)
	{
		if (fleet.Kind != EUnitKind.Navy || !fleet.IsFighting || Wants(fleet))
		{
			return false;
		}
		fleet.DisengageTo = province;
		if (fleet.Disengage == 0)
		{
			fleet.Disengage = 1;
		}
		return true;
	}

	/// <summary>
	/// The last thing a side does when it has shot: each of its fleets that is counting
	/// counts an hour more, and one that has counted long enough and was told where to
	/// go sets off.
	/// </summary>
	public static void CountHour(CGameState state, CCombat combat, CCombatSide side)
	{
		foreach (CUnit fleet in new List<CUnit>(side.Units))
		{
			if (fleet.Disengage > 0)
			{
				fleet.Disengage++;
			}
			if (!Wants(fleet) || fleet.DisengageTo == 0 || state.Country(fleet.Owner) is not { } country)
			{
				continue;
			}
			List<int> route = CRouteFinder.Find(state, country, fleet, fleet.Location, fleet.DisengageTo) ?? new List<int>();
			if (!MayLeave(fleet))
			{
				continue;
			}
			CCombats.LeaveForMarch(state, fleet);
			CMovement.SetPath(fleet, route, clear: true);
			// A raider that breaks away from a convoy keeps its order.
			if (!combat.IsConvoyBattle)
			{
				CUnitOrders.Clear(fleet);
			}
		}
	}

	/// <summary>
	/// The battle's roll to break off, after the round. When it passes every fleet's
	/// count is started or goes up, the attacker's first, and the battle is over at the
	/// first fleet that has counted long enough.
	/// </summary>
	public static bool BreakOff(CGameState state, CCombat combat)
	{
		if (combat.Duration < RoundsBeforeBreakingOff)
		{
			return false;
		}
		CProvince province = state.Province(combat.Province);
		long chance = combat.Duration * (long)CFixedPoint.Scale + BreakOffBase
			+ (CDaylight.IsNight(state, province) ? BreakOffByNight : BreakOffByDay)
			+ province.Weather.WindSpeed.Raw * state.Defines[CWeatherEffects.WeatherSection, CNavalModifiers.WindModifier].Raw / CFixedPoint.Scale
			+ province.Weather.Precipitation.Raw * state.Defines[CWeatherEffects.WeatherSection, CNavalModifiers.RainModifier].Raw / CFixedPoint.Scale;
		if (state.Random.Next(1000) >= chance / CFixedPoint.Scale)
		{
			return false;
		}
		foreach (CCombatSide side in new[] { combat.Attacker, combat.Defender })
		{
			foreach (CUnit fleet in side.Units)
			{
				fleet.Disengage = fleet.Disengage == 0 ? 1 : fleet.Disengage + 1;
				if (Wants(fleet))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>A fleet is let out of a battle, and will start no other for some hours.</summary>
	public static void StartWait(CGameState state, CUnit fleet)
	{
		if (fleet.Kind == EUnitKind.Navy)
		{
			fleet.EngageWait = LeastWait + (state.Random.Next() & (WaitRoll - 1));
		}
	}
}
