using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// A reserve: the fleet makes a port its base and lies there, and once a day its sound
/// ships change places with the worn ones of the fleets lying in the same port, those of
/// the highest priority first. See <c>docs/mechanics/navalmissions.md</c>, section 2.
/// </summary>
public static class CReserve
{
	public const string DefensiveLimit = "DEFENSIVE_STRENGTH_LIMIT";
	public const string PassiveLimit = "PASSIVE_STRENGTH_LIMIT";

	/// <summary>The highest priority a reserve sees to. The orders window lets one be set a step higher.</summary>
	private const int HighestServed = 3;

	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base != unit.OrderProvince)
		{
			unit.Base = unit.OrderProvince;
		}
		if (unit.Location != unit.OrderProvince)
		{
			CMissions.ReturnToBase(state, country, unit);
		}
	}

	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		return unit.Base != 0
			&& (unit.Mission.ReturnToBase || (!unit.IsMoving && unit.Location == unit.OrderProvince));
	}

	/// <summary>
	/// The day's exchange, for a reserve lying in its port: every fleet based where it
	/// is, from priority 3 down to 0 and within a priority in the order of the units'
	/// numbers, until the reserve has no ship left.
	/// </summary>
	public static void RunDaily(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Location != unit.OrderProvince || unit.Base == 0)
		{
			return;
		}
		var others = new List<CUnit>();
		foreach (CUnit other in country.AllUnits())
		{
			if (other != unit && other.Base == unit.Base && !other.IsRemoved)
			{
				others.Add(other);
			}
		}
		others.Sort((a, b) => a.Id.CompareTo(b.Id));
		for (int priority = HighestServed; priority >= 0; priority--)
		{
			foreach (CUnit other in others)
			{
				if (other.Mission.Priority == priority)
				{
					Exchange(state, unit, other);
				}
				if (unit.SubUnits.Count == 0)
				{
					return;
				}
			}
		}
	}

	/// <summary>
	/// A reserve's sound ships for another fleet's worn ones, one pair after another:
	/// the other's most worn ship under its stance's limit, for the reserve's soundest of
	/// the same type that is sounder than it. Only for a fleet of the reserve's country
	/// lying in the same province.
	/// </summary>
	public static void Exchange(CGameState state, CUnit reserve, CUnit other)
	{
		if (other.Kind != EUnitKind.Navy || other.Owner != reserve.Owner || other.Location != reserve.Location)
		{
			return;
		}
		// An aggressive stance reads the defensive limit too: the original never reads the third.
		long limit = state.Defines[CManpower.MilitarySection,
			other.Mission.Stance == CMission.Passive ? PassiveLimit : DefensiveLimit].Raw;
		while (true)
		{
			CSubUnit? worn = null;
			long least = CFixedPoint.Scale;
			foreach (CSubUnit ship in other.SubUnits)
			{
				long share = Share(ship);
				if (share < limit && share < least)
				{
					worn = ship;
					least = share;
				}
			}
			if (worn is null)
			{
				return;
			}
			CSubUnit? sound = null;
			long most = 0;
			foreach (CSubUnit ship in reserve.SubUnits)
			{
				long share = Share(ship);
				if (ship.Type == worn.Type && share > least && share > most)
				{
					sound = ship;
					most = share;
				}
			}
			if (sound is null)
			{
				return;
			}
			reserve.SubUnits.Remove(sound);
			other.SubUnits.Remove(worn);
			other.SubUnits.Add(sound);
			reserve.SubUnits.Add(worn);
			sound.Unit = other;
			worn.Unit = reserve;
		}
	}

	/// <summary>A ship's strength over its type's, in thousandths; -1 for a type with none.</summary>
	private static long Share(CSubUnit ship)
	{
		long full = ship.Stats[CSubUnitType.MaxStrength].Raw;
		return full == 0 ? -1 : ship.Strength.Raw * CFixedPoint.Scale / full;
	}
}
