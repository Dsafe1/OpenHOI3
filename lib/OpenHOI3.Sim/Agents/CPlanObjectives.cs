using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A plan's objectives: on a hot theatre, every province of the areas beyond its fronts that a front-worthy enemy holds,
/// at twenty a victory point with the strategy's wish to take it and a hundred for what a friend asked; and the provinces
/// friends at war have asked the country to take. The original's <c>GenerateUnitObjectives</c> and
/// <c>RebuildPriorityProvinces</c>. See <c>docs/mechanics/agents.md</c>, section 7.4.
/// </summary>
public static class CPlanObjectives
{
	private const int PerPoint = 20;

	private const int Asked = 100;

	/// <summary>
	/// At war only: the provinces the country's faction members, themselves at war, have asked it to take. Ours: the
	/// original's second list of askers, its <c>non_hostile_countries</c>, is not read.
	/// </summary>
	public static void RebuildPriorityProvinces(CGameState state, CCountry country)
	{
		List<int> provinces = country.UnitAi.PriorityProvinces;
		provinces.Clear();
		if (!state.IsAtWar(country.Tag) || country.Faction.Length == 0)
		{
			return;
		}
		foreach ((int province, CCountryTag requester) in state.AlliedObjectives.AskedOf(country.Tag))
		{
			if (requester != country.Tag && state.Country(requester) is { } asker && asker.Faction == country.Faction
				&& state.IsAtWar(requester) && !provinces.Contains(province))
			{
				provinces.Add(province);
			}
		}
	}

	public static void Generate(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } unit || CAgents.TheatreOf(state, country, agent) is not { } theatre)
		{
			return;
		}
		var objectives = new List<CObjective>();
		if (theatre.IsHot)
		{
			foreach (CSupplyArea area in AreasBeyond(state, country, theatre))
			{
				foreach (int id in area.Provinces)
				{
					int priority = state.Province(id).Points * PerPoint + country.Strategy.ConquerProvinces.WeightOf(id)
						+ (country.UnitAi.PriorityProvinces.Contains(id) ? Asked : 0);
					if (priority >= 1)
					{
						objectives.Add(new CObjective(id, priority));
					}
				}
			}
		}
		if (!Same(unit.Plan.Objectives, objectives))
		{
			state.Outbox.Add(new CSetPlanObjectivesCommand(country.Tag, unit.Id, objectives));
		}
	}

	/// <summary>The areas beyond a front, hot or facing a front-worthy enemy, whose first province a front-worthy enemy holds.</summary>
	private static List<CSupplyArea> AreasBeyond(CGameState state, CCountry country, CTheatre theatre)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var areas = new List<CSupplyArea>();
		foreach (CFront front in theatre.Fronts)
		{
			if (!front.IsHot && (front.Enemy is not { } enemy || !CFrontWorthy.Is(state, country, enemy)))
			{
				continue;
			}
			foreach (int id in front.Provinces)
			{
				foreach (CProvinceEdge edge in state.Province(id).Edges)
				{
					if (edge.Kind == EProvinceEdgeKind.Impassable || network.AreaOf(edge.To) is not { } area
						|| areas.Contains(area) || area.Provinces.Count == 0)
					{
						continue;
					}
					if (state.Province(area.Provinces[0]).Controller is { } holder && CFrontWorthy.Is(state, country, holder))
					{
						areas.Add(area);
					}
				}
			}
		}
		return areas;
	}

	private static bool Same(List<CObjective> had, List<CObjective> now)
	{
		if (had.Count != now.Count)
		{
			return false;
		}
		for (int index = 0; index < had.Count; index++)
		{
			if (had[index].Province != now[index].Province || had[index].Priority != now[index].Priority)
			{
				return false;
			}
		}
		return true;
	}
}
