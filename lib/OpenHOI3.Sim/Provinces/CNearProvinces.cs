using System.Collections.Generic;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// Every province within two edges of one, the original's <c>near_provinces</c>: each once, in the order met - its
/// ways out in their order, each followed by that province's ways out - sea provinces too, and so the province itself,
/// once it has a neighbour. Worked out from the edges, which never change, once a province. The supply network scores
/// a depot over it (<c>docs/mechanics/supply.md</c>, section 4) and suppression is summed over it
/// (<c>docs/mechanics/revolt.md</c>, section 2).
/// </summary>
public static class CNearProvinces
{
	public static IReadOnlyList<int> Of(CGameState state, CProvince province)
	{
		if (province.NearProvinces is { } known)
		{
			return known;
		}
		var near = new List<int>();
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (!near.Contains(edge.To))
			{
				near.Add(edge.To);
			}
			foreach (CProvinceEdge further in state.Province(edge.To).Edges)
			{
				if (!near.Contains(further.To))
				{
					near.Add(further.To);
				}
			}
		}
		province.NearProvinces = near;
		return near;
	}
}
