using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The four lists of air bases an agent's air groups may go to - for most groups, for strategic bombers, for naval bombers
/// and for transport planes - each base scored by how far it is on average from what the list is for, nearest first. Made
/// on every full pass, and at the first after a replan. The original's <c>CAIUnit::BuildAirTargetProvinceLists</c>. See
/// <c>docs/mechanics/moving.md</c>, section 17.1.
/// </summary>
public static class CAirBaseLists
{
	/// <summary>The score of a base with nothing to measure against.</summary>
	private const float NothingToMeasure = 10000f;

	public static void Build(CGameState state, CCountry country, CAgent agent, bool full)
	{
		if (agent.Unit is not { } unit || (agent.AirBasesMade && !full))
		{
			return;
		}
		int theatre = agent.Theatre != 0 ? agent.Theatre : state.Province(unit.Location).TheatreId;
		agent.AirBasesDefault.Clear();
		agent.AirBasesStrategic.Clear();
		agent.AirBasesNaval.Clear();
		agent.AirBasesTransport.Clear();
		CBuilding? airBase = CTheatreFigures.AirBase(state);
		var candidates = new List<int>();
		var owners = new List<CCountry> { country };
		if (country.Overlord is { } overlord && state.Country(overlord) is { } master)
		{
			owners.Add(master);
		}
		foreach (CCountry other in state.Countries)
		{
			if (other.Overlord == country.Tag)
			{
				owners.Add(other);
			}
		}
		CAirBaseHolders bases = CAirBaseHolders.Of(state, airBase);
		// Only the country's own bases are held to the theatre; its overlord's and its puppets' are taken wherever they lie
		// (FINDINGS-liveread.md, section 27).
		foreach (CCountry owner in owners)
		{
			foreach (int id in bases.Of(owner.Tag))
			{
				if ((owner != country || state.Province(id).TheatreId == theatre) && !candidates.Contains(id))
				{
					candidates.Add(id);
				}
			}
		}
		var enemyBases = new List<int>();
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CCountry enemy in CCountryLists.Enemies(state, country))
		{
			if (enemy.ActingCapital <= 0 || network.AreaOf(enemy.ActingCapital) is not { } home)
			{
				continue;
			}
			foreach (int id in bases.Of(enemy.Tag))
			{
				if (ReferenceEquals(network.AreaOf(id), home))
				{
					enemyBases.Add(id);
				}
			}
		}
		foreach (int id in candidates)
		{
			CProvince province = state.Province(id);
			float score = Mean(state, unit.Plan, agent.FrontierAtWar ? agent.Area : enemyBases, province);
			if (agent.InvasionTargets.Count > 0)
			{
				score = (Mean(state, unit.Plan, agent.InvasionTargets, province) * 3f + score) * 0.25f;
			}
			Insert(agent.AirBasesDefault, id, score);
			// The enemy's war industry handed to the agent (CSeaTargets; FINDINGS-airstance.md, the strategic list).
			Insert(agent.AirBasesStrategic, id, Mean(state, unit.Plan, agent.StrategicTargets, province));
			Insert(agent.AirBasesNaval, id, Mean(state, unit.Plan, agent.EnemySightings, province));
		}
		agent.AirBasesMade = true;
	}

	/// <summary>
	/// The original's <c>CAIUnit::MeanDistanceToProvinceList</c>: the mean map distance from the base to the provinces,
	/// doubled where the base is one of them; with none, to the plan's objectives; with no objectives either, 10000.
	/// </summary>
	public static float Mean(CGameState state, CUnitPlan plan, IReadOnlyList<int> provinces, CProvince from)
	{
		if (provinces.Count > 0)
		{
			float total = 0;
			bool among = false;
			foreach (int id in provinces)
			{
				total += CMapDistance.Between(from, state.Province(id), state.MapWidth);
				among |= id == from.Id;
			}
			if (among)
			{
				total *= 2f;
			}
			return total / provinces.Count;
		}
		if (plan.Objectives.Count == 0)
		{
			return NothingToMeasure;
		}
		float sum = 0;
		foreach (CObjective objective in plan.Objectives)
		{
			sum += CMapDistance.Between(from, state.Province(objective.Province), state.MapWidth);
		}
		return sum / plan.Objectives.Count;
	}

	/// <summary>Lowest first, a base of equal score after those already there.</summary>
	private static void Insert(List<(int Province, float Score)> list, int province, float score)
	{
		int at = list.Count;
		while (at > 0 && list[at - 1].Score > score)
		{
			at--;
		}
		list.Insert(at, (province, score));
	}
}
