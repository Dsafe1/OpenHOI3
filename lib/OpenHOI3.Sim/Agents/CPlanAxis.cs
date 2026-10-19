using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A theatre plan's axis at an attacking stance: of every pair of a province of its area and an objective, the nearest
/// one a way joins, the way between them from the area's province on; posted only where it differs. The original's
/// <c>CAIUnit::UpdatePlanAxis</c>. See <c>docs/mechanics/moving.md</c>, section 11.6.
/// </summary>
public static class CPlanAxis
{
	public static void Update(CGameState state, CCountry country, CAgent agent, int stance)
	{
		if (agent.Unit is not { } headquarters || headquarters.Kind != EUnitKind.Theatre || !country.UnitAi.RunsUnits)
		{
			return;
		}
		agent.PlanPosted = true;
		var path = new List<int>();
		if (stance is CUnitPlan.Attacking or CUnitPlan.Blitzing)
		{
			int from = 0;
			int to = 0;
			long least = long.MaxValue;
			foreach (int id in agent.Area)
			{
				foreach (CObjective objective in headquarters.Plan.Objectives)
				{
					long distance = CMapDistance.Between(state.Province(id), state.Province(objective.Province), state.MapWidth);
					if (distance < least)
					{
						least = distance;
						from = id;
						to = objective.Province;
					}
				}
			}
			if (from > 0 && CRouteFinder.Find(state, country, headquarters, from, to) is { } route)
			{
				path.AddRange(route);
			}
		}
		if (!Same(headquarters.Plan.Path, path) || headquarters.Plan.Path2.Count > 0)
		{
			state.Outbox.Add(new CSetPlanAxisCommand(country.Tag, headquarters.Id, path, new List<int>()));
		}
	}

	private static bool Same(List<int> had, List<int> now)
	{
		if (had.Count != now.Count)
		{
			return false;
		}
		for (int index = 0; index < had.Count; index++)
		{
			if (had[index] != now[index])
			{
				return false;
			}
		}
		return true;
	}
}
