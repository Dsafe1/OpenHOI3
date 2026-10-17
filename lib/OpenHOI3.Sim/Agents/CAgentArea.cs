using System.Collections.Generic;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's area of operations: the provinces of the fronts it takes that face hostile land in a real area, or, with
/// none, the whole of the fronts that threaten enough; and what is worked out from it - the enemy's provinces it faces,
/// and whether the frontier is at war. The original's <c>CAIUnit::SetArea</c>, the first and fourth of its replan calls.
/// See <c>docs/mechanics/agents.md</c>, section 8.
/// </summary>
public static class CAgentArea
{
	/// <summary>The least infrastructure a province of an area, or the enemy's beside it, has, in thousandths.</summary>
	private const long LeastInfrastructure = 200;

	/// <summary>The threat from the enemy, and the enemy's effective neutrality, that leave a cold theatre's fronts open.</summary>
	private const long OpenThreat = 40000;

	private const long OpenNeutrality = 20000;

	/// <summary>Threat and antagonism together above which a front is taken whole, where nothing else was.</summary>
	private const int WholeFront = 100;

	public static void Set(CGameState state, CCountry country, CAgent agent)
	{
		agent.AreaDirty = false;
		if (agent.Unit is not { } unit || unit.Kind != Units.EUnitKind.Theatre
			|| CAgents.TheatreOf(state, country, agent) is not { } theatre)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CFocusAreas.Rebuild(state, country, agent, theatre);
		agent.Area.Clear();
		foreach (CFront front in Candidates(state, country, agent, theatre))
		{
			if (front.Enemy is not { } enemy || !IsTaken(state, country, theatre, front, enemy))
			{
				continue;
			}
			foreach (int id in front.Provinces)
			{
				if (!agent.Area.Contains(id) && MayHold(state, network, country, state.Province(id)))
				{
					agent.Area.Add(id);
				}
			}
		}
		if (agent.Area.Count == 0)
		{
			foreach (CFront front in theatre.Fronts)
			{
				if (front.Enemy is not { } enemy || Value(state, country, enemy) <= WholeFront)
				{
					continue;
				}
				foreach (int id in front.Provinces)
				{
					CProvince province = state.Province(id);
					if (!agent.Area.Contains(id) && CSupplyFigures.MostInfrastructure(state, province) >= LeastInfrastructure
						&& network.AreaOf(id) is not null)
					{
						agent.Area.Add(id);
					}
				}
			}
		}
		state.Outbox.Add(new CSetPlanAreaCommand(country.Tag, unit.Id, agent.Area));
		RebuildFrontProvinces(state, country, agent);
		Classify(state, country, agent);
		CCommandProvince.Choose(state, country, agent, force: false);
		agent.FullPassPending = true;
	}

	/// <summary>The theatre's own fronts, then those of its subject's theatres in its focus areas.</summary>
	private static List<CFront> Candidates(CGameState state, CCountry country, CAgent agent, CTheatre theatre)
	{
		var fronts = new List<CFront>(theatre.Fronts);
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CSupplyArea area in agent.FocusAreas)
		{
			if (state.Country(area.Controller) is not { } other || !MayWiden(state, country, other))
			{
				continue;
			}
			foreach (CTheatre theirs in other.Strategy.Theatres)
			{
				foreach (CFront front in theirs.Fronts)
				{
					if (front.Provinces.Count == 0 || front.Enemy == country.Tag || fronts.Contains(front)
						|| !ReferenceEquals(network.AreaOf(front.Provinces[0]), area))
					{
						continue;
					}
					if (country.Overlord is not null && front.Enemy is { } enemy && !CCountryLists.IsEnemy(state, country, enemy))
					{
						continue;
					}
					fronts.Add(front);
				}
			}
		}
		return fronts;
	}

	/// <summary>The original's <c>AreaMayWidenFrontSearch</c> for an AI country: the area's controller is its subject, or it leads its faction.</summary>
	private static bool MayWiden(CGameState state, CCountry country, CCountry other)
	{
		if (other.Overlord == country.Tag)
		{
			return true;
		}
		return other.Faction.Length > 0 && other.Faction == country.Faction
			&& Agreements.COfferRules.Leader(state, country.Faction) == country.Tag;
	}

	/// <summary>Hot, a war candidate, or - with the fronts open - a front-worthy enemy.</summary>
	private static bool IsTaken(CGameState state, CCountry country, CTheatre theatre, CFront front, Countries.CCountryTag enemy)
	{
		if (front.IsHot || CFrontWorthy.IsWarCandidate(state, country, enemy))
		{
			return true;
		}
		bool open = !theatre.IsHot || CWarTargets.IsPreparingWarWith(state, country, enemy);
		if (!open && state.Country(enemy) is { } other
			&& CGameState.Status(country, other).Threat.Raw >= OpenThreat
			&& other.EffectiveNeutrality.Raw <= OpenNeutrality)
		{
			open = true;
		}
		return open && CFrontWorthy.Is(state, country, enemy);
	}

	/// <summary>
	/// Infrastructure enough, in a real area, the country's own, a front province or its subject's, and beside hostile land
	/// that is itself in a real area.
	/// </summary>
	private static bool MayHold(CGameState state, CSupplyNetwork network, CCountry country, CProvince province)
	{
		if (CSupplyFigures.MostInfrastructure(state, province) < LeastInfrastructure || network.AreaOf(province.Id) is null)
		{
			return false;
		}
		bool ours = province.Controller == country.Tag || CTheatreFigures.IsFrontProvince(state, province, false)
			|| (state.Country(province.Controller) is { } holder && holder.Overlord == country.Tag);
		if (!ours)
		{
			return false;
		}
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (!beside.IsSea && IsHostile(state, country, beside.Controller) && network.AreaOf(beside.Id) is not null)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Nobody's, the rebels', or not of the country's side and either not fighting beside it or its enemy.</summary>
	public static bool IsHostile(CGameState state, CCountry country, Countries.CCountryTag? tag)
	{
		if (tag is not { } holder || holder.Value == Revolts.CRebelFactions.Rebels)
		{
			return true;
		}
		if (state.Country(holder) is not { } other)
		{
			return true;
		}
		if (CSupplyRelations.IsSameSide(country, other))
		{
			return false;
		}
		return !CCountryLists.FightingTogether(state, country.Tag, holder) || CCountryLists.IsEnemy(state, country, holder);
	}

	/// <summary>The enemy's threat towards the country in its strategy, and the country's antagonism of it.</summary>
	private static int Value(CGameState state, CCountry country, Countries.CCountryTag enemy)
	{
		int value = state.Country(enemy)?.Strategy.Threat.WeightOf(country.Tag) ?? 0;
		if (country.UnitAi.RunsUnits)
		{
			value += country.Strategy.Antagonize.WeightOf(enemy);
		}
		return value;
	}

	/// <summary>
	/// The original's first replan call: the provinces beside the area, of a front-worthy enemy, with infrastructure
	/// enough - which the enemy's power is counted over.
	/// </summary>
	private static void RebuildFrontProvinces(CGameState state, CCountry country, CAgent agent)
	{
		agent.FrontProvinces.Clear();
		foreach (int id in agent.Area)
		{
			foreach (CProvinceEdge edge in state.Province(id).Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable)
				{
					continue;
				}
				CProvince beside = state.Province(edge.To);
				if (beside.IsSea || beside.Controller is not { } holder || !CFrontWorthy.Is(state, country, holder)
					|| Supply.CSupplyFigures.EffectiveInfrastructure(beside) < LeastInfrastructure
					|| agent.FrontProvinces.Contains(beside.Id))
				{
					continue;
				}
				agent.FrontProvinces.Add(beside.Id);
			}
		}
	}

	/// <summary>
	/// The original's <c>ClassifyOpsAreaFrontier</c>: the frontier is at war where a province of the area is beside land
	/// whose controller the country is at war with - which is also a war candidate's - and a war candidate's where it is
	/// beside a war candidate's land.
	/// </summary>
	private static void Classify(CGameState state, CCountry country, CAgent agent)
	{
		agent.FrontierAtWar = false;
		agent.FrontierWarCandidate = false;
		foreach (int id in agent.Area)
		{
			foreach (CProvinceEdge edge in state.Province(id).Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable)
				{
					continue;
				}
				CProvince beside = state.Province(edge.To);
				if (beside.IsSea || beside.Controller is not { } holder)
				{
					continue;
				}
				if (CCountryLists.AtWar(state, country.Tag, holder))
				{
					agent.FrontierAtWar = true;
					agent.FrontierWarCandidate = true;
				}
				else if (CFrontWorthy.IsWarCandidate(state, country, holder))
				{
					agent.FrontierWarCandidate = true;
				}
			}
		}
	}
}
