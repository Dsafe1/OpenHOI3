using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What an agent's area of operations leads to: each front province's nearness to the plan's objectives; the country's
/// own areas cut off behind enemy land and the enemy provinces on the ways to them; and the friendly land two steps out.
/// The original's <c>CAIUnit::RebuildFrontAndApproaches</c> after its first step, and
/// <c>CAIUnit::RebuildAreaNeighbourProvinces</c>. See <c>docs/mechanics/moving.md</c>, section 4.
/// </summary>
public static class CApproaches
{
	private const int Nearest = 3;

	public static void Rebuild(CGameState state, CCountry country, CAgent agent)
	{
		Nearness(state, agent);
		WayOut(state, country, agent);
		SecondRing(state, country, agent);
	}

	/// <summary>Each front province's mean distance to the objectives in its area, put on 0 to 100, the nearest 100.</summary>
	private static void Nearness(CGameState state, CAgent agent)
	{
		agent.Nearness.Clear();
		if (agent.Unit is not { } unit)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		long most = long.MinValue;
		long least = long.MaxValue;
		foreach (int id in agent.FrontProvinces)
		{
			CSupplyArea? area = network.AreaOf(id);
			long sum = 0;
			int count = 0;
			foreach (CObjective objective in unit.Plan.Objectives)
			{
				if (area is not null && ReferenceEquals(network.AreaOf(objective.Province), area))
				{
					sum += CMapDistance.Between(state.Province(id), state.Province(objective.Province), state.MapWidth);
					count++;
				}
			}
			long mean = count > 0 ? sum / count : 0;
			agent.Nearness[id] = mean;
			most = System.Math.Max(most, mean);
			least = System.Math.Min(least, mean);
		}
		if (most > least)
		{
			foreach (int id in new List<int>(agent.Nearness.Keys))
			{
				agent.Nearness[id] = (most - agent.Nearness[id]) * 100 / (most - least);
			}
		}
	}

	/// <summary>The country's own areas the area of operations reaches only through enemy land, and the ways there.</summary>
	private static void WayOut(CGameState state, CCountry country, CAgent agent)
	{
		agent.TargetProvinces.Clear();
		agent.HostileRoutes.Clear();
		if (agent.Unit is not { } headquarters)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var from = new List<CSupplyArea>();
		foreach (int id in agent.Area)
		{
			if (state.Province(id).Controller == country.Tag && network.AreaOf(id) is { } area && !from.Contains(area)
				&& CAreaHostility.TouchesHostileArea(state, area))
			{
				from.Add(area);
			}
		}
		if (from.Count == 0)
		{
			return;
		}
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller != country.Tag || ReferenceEquals(area, capitalArea) || from.Contains(area)
				|| !CAreaHostility.TouchesHostileArea(state, area)
				|| !Reaches(state, country, from, area, throughEnemies: true)
				|| Reaches(state, country, from, area, throughEnemies: false))
			{
				continue;
			}
			foreach (int id in area.Provinces)
			{
				agent.TargetProvinces.Add(id);
			}
			Routes(state, country, agent, headquarters, network, area);
		}
	}

	/// <summary>Whether one of the areas reaches the target within two steps of area, and only through friends' unless allowed.</summary>
	private static bool Reaches(CGameState state, CCountry country, List<CSupplyArea> from, CSupplyArea target,
		bool throughEnemies)
	{
		foreach (CSupplyArea start in from)
		{
			foreach (CSupplyArea step in start.Neighbours)
			{
				if (ReferenceEquals(step, target))
				{
					return true;
				}
				if (!throughEnemies && CCountryLists.IsEnemy(state, country, step.Controller))
				{
					continue;
				}
				if (step.Neighbours.Contains(target))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>From the three provinces of the area of operations nearest the target, the enemy provinces on the way.</summary>
	private static void Routes(CGameState state, CCountry country, CAgent agent, CUnit headquarters, CSupplyNetwork network,
		CSupplyArea target)
	{
		CProvince goal = state.Province(target.Provinces[0]);
		var starts = new List<(int Province, long Distance)>();
		foreach (int id in agent.Area)
		{
			if (state.Province(id).Controller != country.Tag)
			{
				continue;
			}
			long distance = CMapDistance.Between(state.Province(id), goal, state.MapWidth);
			int at = starts.Count;
			while (at > 0 && starts[at - 1].Distance > distance)
			{
				at--;
			}
			starts.Insert(at, (id, distance));
		}
		for (int index = 0; index < starts.Count && index < Nearest; index++)
		{
			List<int>? route = CRouteFinder.Find(state, country, headquarters, starts[index].Province, goal.Id);
			if (route is null)
			{
				continue;
			}
			foreach (int id in route)
			{
				CProvince province = state.Province(id);
				if (province.Controller is not { } holder || holder == country.Tag || agent.HostileRoutes.Contains(id))
				{
					continue;
				}
				if (holder.Value == CRebelFactions.Rebels || CCountryLists.IsEnemy(state, country, holder))
				{
					agent.HostileRoutes.Add(id);
				}
			}
		}
	}

	/// <summary>Friendly land two steps out from the area: the step beside it thrown away.</summary>
	private static void SecondRing(CGameState state, CCountry country, CAgent agent)
	{
		agent.SecondRing.Clear();
		var area = new HashSet<int>(agent.Area);
		var first = new List<int>();
		foreach (int id in agent.Area)
		{
			foreach (int beside in FriendlyBeside(state, country, id))
			{
				if (!area.Contains(beside) && !first.Contains(beside))
				{
					first.Add(beside);
				}
			}
		}
		foreach (int id in first)
		{
			foreach (int beside in FriendlyBeside(state, country, id))
			{
				if (!area.Contains(beside) && !first.Contains(beside) && !agent.SecondRing.Contains(beside))
				{
					agent.SecondRing.Add(beside);
				}
			}
		}
	}

	private static IEnumerable<int> FriendlyBeside(CGameState state, CCountry country, int id)
	{
		foreach (CProvinceEdge edge in state.Province(id).Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (!beside.IsSea && IsFriendly(state, country, beside.Controller))
			{
				yield return edge.To;
			}
		}
	}

	/// <summary>Held by the country's side, or by a country fighting beside it that is not its enemy.</summary>
	public static bool IsFriendly(CGameState state, CCountry country, CCountryTag? tag)
	{
		return tag is { } holder && holder.Value != CRebelFactions.Rebels && !CAgentArea.IsHostile(state, country, holder);
	}
}
