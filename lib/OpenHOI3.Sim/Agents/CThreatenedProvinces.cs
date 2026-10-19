using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's provinces an enemy is bound for from the sea: kept while a hostile land unit aboard a fleet beside one has
/// it as its next step, added for each enemy fleet handed to the agent whose passengers' ways end in a province of the
/// country's. The original's <c>CAIUnit_UpdateThreatenedProvinces</c>. See <c>docs/mechanics/moving.md</c>, section 6.
/// </summary>
public static class CThreatenedProvinces
{
	public static void Update(CGameState state, CCountry country, CAgent agent)
	{
		int theatre = agent.Theatre;
		if (agent.Threatened.Count > 0)
		{
			agent.Threatened.RemoveAll(id => state.Province(id).TheatreId != theatre || !Landing(state, country, id));
		}
		foreach (CUnit fleet in agent.EnemyFleets)
		{
			foreach (CUnit passenger in fleet.Passengers)
			{
				if (passenger.Path.Count == 0)
				{
					continue;
				}
				int end = passenger.Path[^1];
				if (state.Province(end).Owner == country.Tag && !agent.Threatened.Contains(end))
				{
					agent.Threatened.Add(end);
				}
			}
		}
	}

	/// <summary>A sea province beside it holds a hostile land unit aboard a fleet, its next step this one.</summary>
	private static bool Landing(CGameState state, CCountry country, int id)
	{
		foreach (CProvinceEdge edge in state.Province(id).Edges)
		{
			if (!state.Province(edge.To).IsSea)
			{
				continue;
			}
			foreach (CUnit unit in state.Positions.Of(edge.To))
			{
				foreach (CUnit passenger in unit.Passengers)
				{
					if (passenger.Path.Count > 0 && passenger.Path[0] == id && CAgentArea.IsHostile(state, country, passenger.Owner))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	/// <summary>The countries at war with it and those it prepares war on.</summary>
	public static List<CCountry> Hostile(CGameState state, CCountry country)
	{
		List<CCountry> list = CCountryLists.Enemies(state, country);
		foreach (Ai.CWarTarget target in country.WarTargets)
		{
			if (state.Country(target.Target) is { } other && !list.Contains(other))
			{
				list.Add(other);
			}
		}
		return list;
	}
}
