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

	/// <summary>Brigades for each interceptor and tactical bomber wanted.</summary>
	private const int PerAircraft = 40;

	/// <summary>Brigades each division the theatre calls for counts as.</summary>
	private const int PerDivision = 3;

	/// <summary>
	/// The unit types in the original's order: the unit files as the folder lists them, which on NTFS is by their names
	/// folded to capitals - taken here as the types' keys so ordered (inferred).
	/// </summary>
	public static List<CSubUnitType> Order(CGameState state)
	{
		var types = new List<CSubUnitType>(state.SubUnitTypes.Values);
		types.Sort((a, b) => string.CompareOrdinal(a.Key.ToUpperInvariant(), b.Key.ToUpperInvariant()));
		return types;
	}

	/// <summary>A type's place in the plan's counts: one past its place in the order.</summary>
	public static int IndexOf(CGameState state, string key)
	{
		List<CSubUnitType> types = Order(state);
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
		if (Available(state, country, MultiRole))
		{
			// Half the tactical bombers, a half rounded to even.
			int half = tactical / 2;
			min[IndexOf(state, MultiRole)] = tactical % 2 == 1 && half % 2 == 1 ? half + 1 : half;
		}
		if (!Same(unit.Plan.Min, min))
		{
			state.Outbox.Add(new CSetPlanForcesCommand(country.Tag, unit.Id, min));
		}
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
