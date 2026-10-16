using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The air supply: an air unit carries supplies and fuel from its base's province to a friend's province of its area
/// that is short of them, on the bombing frame's hour with a pick and a cargo of its own. See
/// <c>docs/mechanics/transport.md</c>, section 13.5.
/// </summary>
public static class CAirSupply
{
	public const string Factor = "AIR_SUPPLY_FACTOR";

	/// <summary>An air supply may be given for land, to a unit with a <c>transport_capability</c>.</summary>
	public static bool MayGive(CUnit unit, CProvince province)
	{
		return unit.Kind == EUnitKind.Air && !province.IsSea && Capability(unit) > 0;
	}

	/// <summary>Its list keeps the provinces whose holder counts the unit's country a friend, military access counting.</summary>
	public static bool MayList(CGameState state, CUnit unit, CProvince province)
	{
		return state.Country(province.Controller) is { } holder && CCountryLists.IsFriend(state, holder, unit.Owner, true);
	}

	/// <summary>The unit's summed <c>transport_capability</c>, in thousandths and not cut.</summary>
	public static long Capability(CUnit unit)
	{
		long sum = 0;
		foreach (CSubUnit wing in unit.SubUnits)
		{
			sum += wing.Stats[Transport.CTransport.TransportCapability].Raw;
		}
		return sum;
	}

	/// <summary>What a province went short of today, fuel and supplies added: what was drawn less what passed through.</summary>
	public static long Shortfall(CProvince province)
	{
		return Lack(province, EGoods.Fuel) + Lack(province, EGoods.Supplies);
	}

	private static long Lack(CProvince province, EGoods goods)
	{
		return province.SupplyDrawn[goods].Raw - province.SupplyThroughput[goods].Raw;
	}

	/// <summary>
	/// The province to fly to: over the list in order, the first allowed whatever it lacks, replaced by every later one
	/// that went short today - so the last short one wins. 0 for none.
	/// </summary>
	public static int Pick(CGameState state, CUnit unit)
	{
		int picked = 0;
		foreach (int id in unit.Mission.Provinces)
		{
			CProvince province = state.Province(id);
			if (!MayList(state, unit, province))
			{
				continue;
			}
			if (picked == 0 || Shortfall(province) > 0)
			{
				picked = id;
			}
		}
		return picked;
	}

	/// <summary>
	/// The hour: over its target the cargo is delivered; at its base with a target an empty cargo is loaded; at its
	/// base with a target and nothing aboard it waits; otherwise the bombing frame runs and the air orders' own test
	/// answers.
	/// </summary>
	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		CMission mission = unit.Mission;
		if (mission.Destination != 0)
		{
			if (unit.Location == mission.Destination)
			{
				CProvince target = state.Province(mission.Destination);
				target.Pool[EGoods.Supplies] = new CFixedPoint(target.Pool[EGoods.Supplies].Raw + mission.Supplies.Raw);
				target.Pool[EGoods.Fuel] = new CFixedPoint(target.Pool[EGoods.Fuel].Raw + mission.Fuel.Raw);
				mission.Supplies = CFixedPoint.Zero;
				mission.Fuel = CFixedPoint.Zero;
				mission.Destination = 0;
			}
			else if (CAirMissions.IsAtOwnBase(unit))
			{
				Load(state, unit);
			}
		}
		if (CAirMissions.IsAtOwnBase(unit) && mission.Destination != 0
			&& mission.Supplies == CFixedPoint.Zero && mission.Fuel == CFixedPoint.Zero)
		{
			return true;
		}
		CBombingMissions.RunBombing(state, country, unit);
		return CAirMissions.IsStillGoing(state, country, unit);
	}

	/// <summary>
	/// Loads what is not aboard, supplies and then fuel: what the target went short of today, at most the cap, counted
	/// into the base province's drawn, then at most what lies there, and taken out of it.
	/// </summary>
	private static void Load(CGameState state, CUnit unit)
	{
		CMission mission = unit.Mission;
		CProvince target = state.Province(mission.Destination);
		CProvince home = state.Province(unit.Base);
		long cap = state.Defines[CManpower.MilitarySection, Factor].Raw * Capability(unit) / CFixedPoint.Scale;
		if (mission.Supplies == CFixedPoint.Zero)
		{
			mission.Supplies = new CFixedPoint(Take(target, home, EGoods.Supplies, cap));
		}
		if (mission.Fuel == CFixedPoint.Zero)
		{
			mission.Fuel = new CFixedPoint(Take(target, home, EGoods.Fuel, cap));
		}
	}

	private static long Take(CProvince target, CProvince home, EGoods goods, long cap)
	{
		long wanted = Math.Min(Math.Max(Lack(target, goods), 0), cap);
		home.SupplyDrawn[goods] = new CFixedPoint(home.SupplyDrawn[goods].Raw + wanted);
		long taken = Math.Min(wanted, home.Pool[goods].Raw);
		home.Pool[goods] = new CFixedPoint(home.Pool[goods].Raw - taken);
		return taken;
	}
}
