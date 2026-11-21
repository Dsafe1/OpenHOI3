using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The area of a headquarters below a theatre that its player has handed to the AI: the front nearest one of its
/// objectives, as much of it as its divisions can hold beside the other agents' units, and the land of every other front
/// it found where its units outnumber theirs. The original's <c>CAIUnit::BuildPlan</c>, its main path and its fallback,
/// run as the box is ticked, as an objective is set or the path is drawn, and as its country's theatres are made again.
/// See <c>docs/mechanics/unitpanel.md</c>, section 9.1.
/// </summary>
public static class CPlanBuilder
{
	/// <summary>Added to a distance, so that an objective on the front itself scores above nought.</summary>
	private const float Near = 10.0f;

	/// <summary>What a front in the unit's own theatre, or its own area or one beside it, has its score multiplied by.</summary>
	private const float Closer = 0.1f;

	/// <summary>The fallback's reach from an objective, in the game's kilometres.</summary>
	private const long FallbackReach = 600;

	/// <summary>The fallback takes a front province only where the area beside it has fewer provinces than this.</summary>
	private const int FallbackSmallArea = 5;

	public static void Build(CGameState state, CCountry country, CAgent agent)
	{
		agent.PlanBuildPending = false;
		if (agent.Unit is not { } unit || unit.Kind == EUnitKind.Theatre || CAgents.HighestOver(unit) != agent)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		List<(CTheatre Theatre, CFront Front)> fronts = CandidateFronts(state, country);
		var collected = new List<CFront>();
		CFront? best = null;
		int chosen = 0;
		float bestScore = float.MaxValue;
		CProvince? here = unit.Location > 0 ? state.Province(unit.Location) : null;
		CSupplyArea? hereArea = here is not null ? network.AreaOf(here.Id) : null;
		foreach (CObjective objective in unit.Plan.Objectives)
		{
			CProvince target = state.Province(objective.Province);
			// Never a front against a friend's province.
			if (target.Controller is { } holder && CCountryLists.IsFriend(state, country, holder, withAccess: true))
			{
				continue;
			}
			foreach ((CTheatre theatre, CFront front) in fronts)
			{
				foreach (int id in front.Provinces)
				{
					CProvince province = state.Province(id);
					if (!FacesTheSameEnemy(state, network, country, province, target))
					{
						continue;
					}
					if (!collected.Contains(front))
					{
						collected.Add(front);
					}
					float score = CMapDistance.Between(province, target, state.MapWidth) + Near;
					if (here is not null && here.TheatreId == theatre.Id)
					{
						score *= Closer;
					}
					CSupplyArea? area = network.AreaOf(id);
					if (hereArea is not null && ReferenceEquals(hereArea, area))
					{
						score *= Closer;
					}
					else if (hereArea is not null && area is not null && hereArea.Neighbours.Contains(area))
					{
						score *= Closer;
					}
					if (score < bestScore)
					{
						bestScore = score;
						best = front;
						chosen = id;
					}
				}
			}
		}
		var area2 = new List<int>();
		if (best is null)
		{
			Fallback(state, network, country, agent, unit, fronts, area2);
			return;
		}
		Take(state, network, country, agent, unit, best, chosen, collected, area2);
		Post(state, country, agent, unit, area2);
	}

	/// <summary>Every front of every theatre of every country the agent may operate in, our own among them.</summary>
	private static List<(CTheatre, CFront)> CandidateFronts(CGameState state, CCountry country)
	{
		var fronts = new List<(CTheatre, CFront)>();
		foreach (CCountry other in state.Countries)
		{
			if (!CApproaches.MayOperateIn(state, country, other.Tag))
			{
				continue;
			}
			foreach (CTheatre theatre in other.Strategy.Theatres)
			{
				foreach (CFront front in theatre.Fronts)
				{
					fronts.Add((theatre, front));
				}
			}
		}
		return fronts;
	}

	/// <summary>
	/// Whether a front province faces the objective's enemy: a land neighbour in an area, held by the objective's holder,
	/// or both it and the objective the country's enemies.
	/// </summary>
	private static bool FacesTheSameEnemy(CGameState state, CSupplyNetwork network, CCountry country, CProvince province, CProvince target)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince next = state.Province(edge.To);
			if (next.IsSea || network.AreaOf(next.Id) is null)
			{
				continue;
			}
			if (next.Controller == target.Controller
				|| (CMovementRules.IsEnemyIn(state, country.Tag, target.Controller, target.Id)
					&& CMovementRules.IsEnemyIn(state, country.Tag, next.Controller, next.Id)))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// How much of the chosen front the agent takes: its divisions' share of it beside the other agents' units on it - at
	/// least one province and no more than its divisions - the chosen province and those nearest it, and then the land of
	/// every other front it found in an area where its own units are at least as many as the others'.
	/// </summary>
	private static void Take(CGameState state, CSupplyNetwork network, CCountry country, CAgent agent, CUnit unit, CFront front,
		int chosen, List<CFront> collected, List<int> area)
	{
		int mine = (CMovementRules.IsLandUnit(unit) && unit.Kind == EUnitKind.Division ? 1 : 0) + CountDivisions(unit);
		var onFront = new HashSet<int>(front.Provinces);
		int others = 0;
		foreach (CUnit other in country.AllUnits())
		{
			if (other.Children.Count == 0 && CMovementRules.IsLandUnit(other) && IsOtherAgents(other, agent)
				&& (onFront.Contains(other.Location) || onFront.Contains(CAgentUnits.DestinationOrPlace(other))))
			{
				others++;
			}
		}
		float density = (float)(mine + others) / front.Provinces.Count;
		int share = density > 0 ? System.Math.Max(1, (int)System.MathF.Round(mine / density)) : 1;
		share = System.Math.Min(share, mine);
		area.Add(chosen);
		CProvince from = state.Province(chosen);
		var rest = new List<int>(front.Provinces);
		rest.Remove(chosen);
		rest.Sort((a, b) => CMapDistance.Between(from, state.Province(a), state.MapWidth)
			.CompareTo(CMapDistance.Between(from, state.Province(b), state.MapWidth)));
		for (int index = 0; index < share - 1 && index < rest.Count; index++)
		{
			area.Add(rest[index]);
		}
		foreach (CFront other in collected)
		{
			if (other == front)
			{
				continue;
			}
			foreach (int id in other.Provinces)
			{
				if (!area.Contains(id) && network.AreaOf(id) is { } held && OutnumbersIn(state, network, country, agent, held))
				{
					area.Add(id);
				}
			}
		}
	}

	/// <summary>Whether the agent's own leaf units in the area are at least as many as every other agent's there.</summary>
	private static bool OutnumbersIn(CGameState state, CSupplyNetwork network, CCountry country, CAgent agent, CSupplyArea area)
	{
		int mine = 0;
		int others = 0;
		foreach (CUnit other in country.AllUnits())
		{
			if (other.Children.Count > 0 || !CMovementRules.IsLandUnit(other) || other.Location <= 0
				|| !ReferenceEquals(network.AreaOf(other.Location), area))
			{
				continue;
			}
			if (CAgents.HighestOver(other) == agent)
			{
				mine++;
			}
			else if (IsOtherAgents(other, agent))
			{
				others++;
			}
		}
		return mine >= others;
	}

	/// <summary>
	/// No front faced an objective: the original's looser pass, whose two commands leave a plan that had an area with none,
	/// and give one that had none the front provinces in each objective's area within 600 of it beside a small area.
	/// </summary>
	private static void Fallback(CGameState state, CSupplyNetwork network, CCountry country, CAgent agent, CUnit unit,
		List<(CTheatre Theatre, CFront Front)> fronts, List<int> area)
	{
		if (unit.Plan.Area.Count > 0)
		{
			Post(state, country, agent, unit, area);
			return;
		}
		foreach (CObjective objective in unit.Plan.Objectives)
		{
			CProvince target = state.Province(objective.Province);
			if (!CApproaches.MayOperateIn(state, country, target.Controller) || network.AreaOf(target.Id) is not { } targetArea)
			{
				continue;
			}
			foreach ((CTheatre _, CFront front) in fronts)
			{
				foreach (int id in front.Provinces)
				{
					CProvince province = state.Province(id);
					if (area.Contains(id) || !ReferenceEquals(network.AreaOf(id), targetArea)
						|| CMapDistance.Between(province, target, state.MapWidth) > FallbackReach || !BesideSmallArea(state, network, province))
					{
						continue;
					}
					area.Add(id);
				}
			}
		}
		if (area.Count > 0)
		{
			Post(state, country, agent, unit, area);
		}
	}

	/// <summary>Whether a land neighbour of the province lies in an area of fewer than five provinces.</summary>
	private static bool BesideSmallArea(CGameState state, CSupplyNetwork network, CProvince province)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (network.AreaOf(edge.To) is { } beside && !ReferenceEquals(beside, network.AreaOf(province.Id))
				&& beside.Provinces.Count < FallbackSmallArea)
			{
				return true;
			}
		}
		return false;
	}

	private static void Post(CGameState state, CCountry country, CAgent agent, CUnit unit, List<int> area)
	{
		agent.Area.Clear();
		agent.Area.AddRange(area);
		state.Outbox.Add(new CSetPlanAreaCommand(country.Tag, unit.Id, area));
		CAgentArea.AfterArea(state, country, agent, unit);
		agent.HoldProvincesMade = false;
		agent.AirBasesMade = false;
	}

	/// <summary>The divisions under a unit, at every level.</summary>
	private static int CountDivisions(CUnit unit)
	{
		int count = 0;
		foreach (CUnit child in unit.Children)
		{
			count += (child.Kind == EUnitKind.Division ? 1 : 0) + CountDivisions(child);
		}
		return count;
	}

	/// <summary>Whether the unit belongs to an agent other than this one.</summary>
	private static bool IsOtherAgents(CUnit unit, CAgent agent)
	{
		return CAgents.HighestOver(unit) is { } over && over != agent;
	}
}
