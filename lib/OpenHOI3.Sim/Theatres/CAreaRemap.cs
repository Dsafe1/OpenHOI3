using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// What the AI keeps of the supply network's areas, carried over to a network made again. The original keeps its areas as
/// lasting objects and updates them as provinces change hands; here the network is made whole each time (`supply.md`), so
/// every area kept from the last one - a theatre's, an agent's focus areas, a front's facing area, the invasions' lists -
/// is looked up again in the new one, or a test of "is this area that one" would answer no for the same land. See
/// <c>docs/mechanics/theatres.md</c>, section 12.
/// </summary>
public static class CAreaRemap
{
	public static void Remap(CGameState state, CSupplyNetwork network)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CTheatre theatre in country.Strategy.Theatres)
			{
				CTheatreProvinces.RebuildAreas(state, theatre);
				foreach (CFront front in theatre.Fronts)
				{
					front.Faces = front.Faces is null ? null : Find(network, front.Faces);
				}
			}
			foreach (Agents.CAgent agent in country.UnitAi.Agents)
			{
				Replace(network, agent.FocusAreas);
			}
			Replace(network, country.UnitAi.FailedAreas);
			List<(CSupplyArea Area, long Score)> ranked = country.UnitAi.RankedAreas;
			for (int index = ranked.Count - 1; index >= 0; index--)
			{
				if (Find(network, ranked[index].Area) is { } now)
				{
					ranked[index] = (now, ranked[index].Score);
				}
				else
				{
					ranked.RemoveAt(index);
				}
			}
		}
	}

	private static void Replace(CSupplyNetwork network, List<CSupplyArea> areas)
	{
		for (int index = areas.Count - 1; index >= 0; index--)
		{
			if (Find(network, areas[index]) is { } now && !areas.Contains(now))
			{
				areas[index] = now;
			}
			else
			{
				areas.RemoveAt(index);
			}
		}
	}

	/// <summary>The new network's area holding the first of the old area's provinces still held by the same country.</summary>
	private static CSupplyArea? Find(CSupplyNetwork network, CSupplyArea old)
	{
		foreach (int id in old.Provinces)
		{
			if (network.AreaOf(id) is { } now && now.Controller == old.Controller)
			{
				return now;
			}
		}
		return null;
	}
}
