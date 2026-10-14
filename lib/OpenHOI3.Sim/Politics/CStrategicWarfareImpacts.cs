using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// What goes on a country's strategic-warfare lists: each value scaled by the country's IC over a hundred, held to
/// 0.1..10, a negative bombing entry also by <c>1 + sw_national_unity_effect</c>; each list summed to its impact,
/// held to -2.0..2.0; and each day the entries older than a week dropped. See
/// <c>docs/mechanics/politics.md</c>, section 6.
/// </summary>
public static class CStrategicWarfareImpacts
{
	public const string Section = "military";

	public const string BombingEffect = "sw_national_unity_effect";

	public static void AddConvoy(CGameState state, CCountry country, long value)
	{
		CStrategicWarfare warfare = country.StrategicWarfare;
		warfare.ConvoyImpact = Add(state, warfare.Convoys, Scaled(country, value), warfare.ConvoyImpact);
	}

	public static void AddAllies(CGameState state, CCountry country, long value)
	{
		CStrategicWarfare warfare = country.StrategicWarfare;
		warfare.AlliesImpact = Add(state, warfare.Allies, Scaled(country, value), warfare.AlliesImpact);
	}

	public static void AddBombing(CGameState state, CCountry country, long value)
	{
		long scaled = Scaled(country, value);
		if (scaled < 0)
		{
			scaled = scaled * (1000 + country.Modifier[BombingEffect].Raw) / 1000;
		}
		CStrategicWarfare warfare = country.StrategicWarfare;
		warfare.BombingImpact = Add(state, warfare.Bombing, scaled, warfare.BombingImpact);
	}

	/// <summary>A raid beaten off by a convoy's escorts: <c>-(STRAT_CONVOY_DAMAGE / its convoys) / 10</c> to the convoy's owner.</summary>
	public static void RaidBeatenOff(CGameState state, CCountry? owner)
	{
		if (owner is null || owner.Convoys.Count == 0)
		{
			return;
		}
		long damage = state.Defines[Section, "STRAT_CONVOY_DAMAGE"].Raw;
		AddConvoy(state, owner, -(damage / owner.Convoys.Count) / 10);
	}

	/// <summary>
	/// A raid that sank ships: the transports and escorts lost, in thousandths of a ship, times
	/// <c>STRAT_CONVOY_DAMAGE</c> over the owner's convoys, halved where the convoy had an escort.
	/// </summary>
	public static void RaidSank(CGameState state, CCountry? owner, long transportsLost, long escortsLost, bool hadEscort)
	{
		if (owner is null || owner.Convoys.Count == 0)
		{
			return;
		}
		long damage = state.Defines[Section, "STRAT_CONVOY_DAMAGE"].Raw;
		long value = (transportsLost + escortsLost) * (damage / owner.Convoys.Count) / 1000;
		AddConvoy(state, owner, hadEscort ? value / 2 : value);
	}

	/// <summary>A sea battle's first hour where a defending fleet raids convoys or patrols: <c>STRAT_RAIDER_FOUGHT_IMPACT</c> to the attacker's first country.</summary>
	public static void SeaBattleBegun(CGameState state, Combat.CCombat combat)
	{
		bool raiders = combat.Defender.Units.Exists(unit => unit.Order is Units.EUnitOrder.ConvoyRaid or Units.EUnitOrder.Patrol);
		if (raiders && combat.Attacker.Countries.Count > 0 && state.Country(combat.Attacker.Countries[0]) is { } attacker)
		{
			AddConvoy(state, attacker, state.Defines[Section, "STRAT_RAIDER_FOUGHT_IMPACT"].Raw);
		}
	}

	/// <summary>
	/// An air battle's first hour where a defending wing is on strategic bombing: the attacker's wings times
	/// <c>STRAT_BOMBER_FOUGHT_IMPACT</c> over five to its first country, times <c>STRAT_BOMBER_NONCRIT_IMPACT_FRACTION</c>
	/// where the province is land one of the defender's countries controls and is not its core.
	/// </summary>
	public static void AirBattleBegun(CGameState state, Combat.CCombat combat)
	{
		if (!combat.Defender.Units.Exists(unit => unit.Order == Units.EUnitOrder.StrategicBombing)
			|| combat.Attacker.Countries.Count == 0 || state.Country(combat.Attacker.Countries[0]) is not { } attacker)
		{
			return;
		}
		long wings = 0;
		foreach (Units.CUnit unit in combat.Attacker.Units)
		{
			wings += unit.SubUnits.Count;
		}
		long value = wings * state.Defines[Section, "STRAT_BOMBER_FOUGHT_IMPACT"].Raw / 5;
		Provinces.CProvince province = state.Province(combat.Province);
		if (!province.IsSea && province.Controller is { } holder && combat.Defender.Countries.Contains(holder)
			&& !province.Cores.Contains(holder))
		{
			value = value * state.Defines[Section, "STRAT_BOMBER_NONCRIT_IMPACT_FRACTION"].Raw / 1000;
		}
		AddBombing(state, attacker, value);
	}

	/// <summary>A strategic bombing starting: <c>STRAT_BOMBING_PENALTY</c> to the bombed unit's owner, else the province's controller.</summary>
	public static void StrategicBombingStarted(CGameState state, Provinces.CProvince province, Units.CUnit? target)
	{
		if (state.Country(target?.Owner ?? province.Controller) is { } bombed)
		{
			AddBombing(state, bombed, state.Defines[Section, "STRAT_BOMBING_PENALTY"].Raw);
		}
	}

	/// <summary>The owner's total IC over a hundred, held to 0.1..10, in thousandths.</summary>
	public static long Scale(CCountry country)
	{
		return Math.Clamp(country.TotalIc * 1000L / 100, 100, 10000);
	}

	/// <summary>Each list loses the entries older than a week from its head, and is summed again.</summary>
	public static void Prune(CGameState state, CStrategicWarfare warfare)
	{
		long oldest = state.TotalHours - CStrategicWarfare.KeptHours;
		Prune(warfare.Convoys, oldest);
		Prune(warfare.Allies, oldest);
		Prune(warfare.Bombing, oldest);
		warfare.ConvoyImpact = Sum(warfare.Convoys);
		warfare.AlliesImpact = Sum(warfare.Allies);
		warfare.BombingImpact = Sum(warfare.Bombing);
	}

	private static long Scaled(CCountry country, long value)
	{
		return value * Scale(country) / 1000;
	}

	/// <summary>The entry appended, and the impact with it added, held to -2.0..2.0.</summary>
	private static long Add(CGameState state, List<(long Hour, long Value)> list, long value, long impact)
	{
		list.Add((state.TotalHours, value));
		return Math.Clamp(impact + value, -CStrategicWarfare.MostImpact, CStrategicWarfare.MostImpact);
	}

	private static void Prune(List<(long Hour, long Value)> list, long oldest)
	{
		int stale = 0;
		while (stale < list.Count && list[stale].Hour < oldest)
		{
			stale++;
		}
		list.RemoveRange(0, stale);
	}

	private static long Sum(List<(long Hour, long Value)> list)
	{
		long sum = 0;
		foreach ((long _, long value) in list)
		{
			sum += value;
		}
		return Math.Clamp(sum, -CStrategicWarfare.MostImpact, CStrategicWarfare.MostImpact);
	}
}
