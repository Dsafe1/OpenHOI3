using OpenHOI3.Sim.Combat;
using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The aircraft a plan wants, one count a unit type - the null type first, then the unit files' types in the order the
/// folder lists them: interceptors and tactical bombers for every forty brigades the theatre has and calls for, and
/// multi-role fighters for half of those. The AI's arm of the original's force planner, <c>CAIUnit_ComputePlanForces</c>.
/// See <c>docs/mechanics/agents.md</c>, section 9.
/// </summary>
public static class CAircraftWants
{
	private const string Interceptor = "interceptor";
	private const string Tactical = "tactical_bomber";
	private const string Cas = "cas";
	private const string MultiRole = "multi_role";
	private const string NavalBomber = "naval_bomber";
	private const string HeavyCruiser = "heavy_cruiser";
	private const long HeavyCruiserWeight = 70;
	private const long CapitalWeight = 150;
	private const long AnyShip = 20;

	/// <summary>Brigades for each interceptor and tactical bomber wanted.</summary>
	private const int PerAircraft = 40;

	/// <summary>Brigades each division the theatre calls for counts as.</summary>
	private const int PerDivision = 3;

	/// <summary>
	/// The unit types in the original's order: the unit files as the folder lists them, which on NTFS is by their names
	/// folded to capitals - taken here as the types' keys so ordered (inferred). Made once a game, the types never
	/// changing in one, each key folded once and not at every comparison.
	/// </summary>
	public static IReadOnlyList<CSubUnitType> Order(CGameState state)
	{
		if (state.PlanTypeOrder is { } made)
		{
			return made;
		}
		var folded = new List<(string Key, CSubUnitType Type)>(state.SubUnitTypes.Count);
		foreach (CSubUnitType type in state.SubUnitTypes.Values)
		{
			folded.Add((type.Key.ToUpperInvariant(), type));
		}
		folded.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
		var types = new List<CSubUnitType>(folded.Count);
		foreach ((_, CSubUnitType type) in folded)
		{
			types.Add(type);
		}
		state.PlanTypeOrder = types;
		return types;
	}

	/// <summary>A type's place in the plan's counts: one past its place in the order.</summary>
	public static int IndexOf(CGameState state, string key)
	{
		IReadOnlyList<CSubUnitType> types = Order(state);
		for (int index = 0; index < types.Count; index++)
		{
			if (types[index].Key.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return index + 1;
			}
		}
		return -1;
	}

	/// <summary>The plan's counts padded with noughts to one a type, as setting its area does.</summary>
	public static void Pad(CGameState state, CUnitPlan plan)
	{
		int size = state.SubUnitTypes.Count + 1;
		while (plan.Min.Count < size)
		{
			plan.Min.Add(0);
		}
	}

	public static void Update(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } unit)
		{
			return;
		}
		var min = new int[state.SubUnitTypes.Count + 1];
		int brigades = 0;
		foreach (CUnit land in agent.Land)
		{
			brigades += land.SubUnits.Count;
		}
		int want = (int)((brigades + agent.DivisionNeed * PerDivision) / PerAircraft);
		int tactical = 0;
		if (Available(state, country, Interceptor))
		{
			min[IndexOf(state, Interceptor)] = want;
		}
		if (Available(state, country, Tactical))
		{
			min[IndexOf(state, Tactical)] = want;
			tactical = want;
		}
		else if (Available(state, country, Cas))
		{
			min[IndexOf(state, Cas)] = want;
		}
		long naval = NavalScore(agent);
		agent.EnemySightings.Clear();
		foreach (CUnit fleet in agent.EnemyFleets)
		{
			agent.EnemySightings.Add(fleet.Location);
		}
		if (Available(state, country, NavalBomber))
		{
			min[IndexOf(state, NavalBomber)] = (int)System.Math.Round(naval / 1000m, System.MidpointRounding.ToEven);
		}
		if (Available(state, country, MultiRole))
		{
			// Half the tactical bombers and half the naval score, rounded to even.
			min[IndexOf(state, MultiRole)] = (int)System.Math.Round(tactical / 2m + naval / 2000m, System.MidpointRounding.ToEven);
		}
		if (!Same(unit.Plan.Min, min))
		{
			state.Outbox.Add(new CSetPlanForcesCommand(country.Tag, unit.Id, min));
		}
	}

	/// <summary>
	/// The enemy's fleets handed to the agent less its side's, in thousandths: a heavy cruiser 70, a capital ship 150 and any
	/// ship but a submarine 20 of theirs; the same off for its own side's but for a transport.
	/// </summary>
	private static long NavalScore(CAgent agent)
	{
		long score = 0;
		foreach (CUnit fleet in agent.EnemyFleets)
		{
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				score += Weight(ship, CShipCensus.IsSubmarine(ship));
			}
		}
		foreach (CUnit fleet in agent.FriendlyFleets)
		{
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				score -= Weight(ship, CShipCensus.IsTransport(ship));
			}
		}
		return score;
	}

	/// <summary>
	/// One ship's weight, one of three and never more: a heavy cruiser 70, else a capital ship 150, else any ship but the
	/// one left out 20 - a heavy cruiser, itself a capital ship in the unit files, counted once (0x4B5BF2 and 0x4B5E38 on
	/// the listing; reversing/findings/FINDINGS-liveread.md, section 78).
	/// </summary>
	private static long Weight(CSubUnit ship, bool leftOut)
	{
		if (ship.Type.Key == HeavyCruiser)
		{
			return HeavyCruiserWeight;
		}
		if (CShipCensus.IsCapital(ship))
		{
			return CapitalWeight;
		}
		return leftOut ? 0 : AnyShip;
	}

	private static bool Available(CGameState state, CCountry country, string key)
	{
		return state.SubUnitTypes.TryGetValue(key, out CSubUnitType? type) && CUnitAvailability.MayUse(state, country, type);
	}

	private static bool Same(List<int> had, int[] now)
	{
		for (int index = 0; index < now.Length; index++)
		{
			if ((index < had.Count ? had[index] : 0) != now[index])
			{
				return false;
			}
		}
		return true;
	}
}
