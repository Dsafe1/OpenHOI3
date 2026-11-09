using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Whether two provinces are joined by a run of the supply network's areas, each beside the next, none held by an enemy
/// of the country and each one the country may enter. The original's <c>CCountry_ProvincesConnectedByArea</c> with its enemies avoided. See
/// <c>docs/mechanics/agents.md</c>, section 6.5.
/// </summary>
public static class CAreaPaths
{
	public static bool Joined(CGameState state, CCountry country, int from, int to)
	{
		if (from <= 0 || to <= 0)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (network.AreaOf(from) is not { } start || network.AreaOf(to) is not { } end)
		{
			return false;
		}
		if (ReferenceEquals(start, end))
		{
			return true;
		}
		var seen = new HashSet<CSupplyArea> { start };
		var open = new Queue<CSupplyArea>();
		open.Enqueue(start);
		while (open.Count > 0)
		{
			CSupplyArea area = open.Dequeue();
			foreach (CSupplyArea beside in area.Neighbours)
			{
				if (seen.Contains(beside) || beside.Provinces.Count == 0 || CMovementRules.IsEnemy(state, country.Tag, beside.Controller)
					|| !CMovementRules.MayEnter(state, country, state.Province(beside.Provinces[0])))
				{
					continue;
				}
				if (ReferenceEquals(beside, end))
				{
					return true;
				}
				seen.Add(beside);
				open.Enqueue(beside);
			}
		}
		return false;
	}
}
