using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// An air unit's reserve: it makes an airfield its base at once and flies there, and once a day, lying there, its sound
/// wings change places with the worn ones of the other air units of its country based at the same airfield. It flies
/// no sortie and starts no battle, and no date ends it. See <c>docs/mechanics/fleetsandair.md</c>, section 5.1.
/// </summary>
public static class CAirReserve
{
	/// <summary>The message the day's exchange posts to the reserve's player.</summary>
	public const string Message = "REINFORCE_AIR";

	/// <summary>
	/// The window's own test of the row, beside the range: the unit in no battle, an air base built in the province -
	/// whatever bombing has left of it - and the unit free to stand there. Nothing is asked of who holds it beyond that.
	/// </summary>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		return unit.Kind == EUnitKind.Air && !unit.IsFighting
			&& CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero
			&& CMovementRules.MayStandIn(state, country, unit, province);
	}

	/// <summary>
	/// The order begun: the province's airfield is the unit's base from this hour, and a unit elsewhere is told to return
	/// to it. Unlike a change of base, nothing is taken off its organisation on arrival.
	/// </summary>
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base != unit.OrderProvince)
		{
			unit.Base = unit.OrderProvince;
		}
		if (unit.Location != unit.OrderProvince)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
	}

	/// <summary>
	/// The order's hour: every air order's own runs for what it does - the gate, the range home, a return to base ended
	/// on arrival - and its answer is thrown away, so neither being out of range nor the order's end date ends a reserve.
	/// It goes on while the unit is returning to base, and otherwise only while it lies in its province with no way.
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		CAirMissions.IsStillGoing(state, country, unit);
		if (unit.Mission.ReturnToBase)
		{
			return true;
		}
		return !unit.IsMoving && unit.Location == unit.OrderProvince;
	}

	/// <summary>
	/// The day's exchange, for a reserve lying in its province: every other unit based where it is that has an order,
	/// in the order of the units' numbers, until the reserve has no wing left. Answers the pairs that changed units.
	/// </summary>
	public static int RunDaily(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Location != unit.OrderProvince || unit.Base == 0)
		{
			return 0;
		}
		var others = new List<CUnit>();
		// A unit another air unit has been ordered to join is passed over. The original marks the unit as the order is
		// given and never takes the mark off; here it is read off the orders standing, so it ends with the order
		// (fleetsandair.md, section 5.1).
		var awaited = new HashSet<CUnit>();
		foreach (CUnit other in country.AllUnits())
		{
			if (other.Order == EUnitOrder.JoinAir && !other.IsRemoved && Agents.CJoinOrders.SeenPartner(other) is { } partner)
			{
				awaited.Add(partner);
			}
		}
		foreach (CUnit other in country.AllUnits())
		{
			if (other != unit && other.Base == unit.Base && !other.IsRemoved && other.Order != EUnitOrder.None
				&& !awaited.Contains(other))
			{
				others.Add(other);
			}
		}
		others.Sort((a, b) => a.Id.CompareTo(b.Id));
		int moved = 0;
		foreach (CUnit other in others)
		{
			if (unit.SubUnits.Count == 0)
			{
				break;
			}
			int pairs = CReserve.Exchange(state, unit, other);
			moved += pairs;
			// The player is told of each unit served. The install declares no such message type, so it reaches him as the
			// original's does: as no type at all, a line in the log (fleetsandair.md, section 5.1).
			if (pairs > 0 && state.Played.Contains(country.Tag))
			{
				state.RaisePactMessage(new Agreements.CPactMessage(Message, "", "", country.Tag, country.Tag)
				{
					Told = country.Tag,
					Values = new[]
					{
						("RESERVE", unit.Name), ("TARGET", other.Name),
						("NUM", pairs.ToString(System.Globalization.CultureInfo.InvariantCulture)),
					},
				});
			}
		}
		return moved;
	}
}
