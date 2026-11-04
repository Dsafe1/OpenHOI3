using System.Collections.Generic;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// An area's fronts: the runs of its provinces on a land border, each flooded through the area from the first province
/// of it met. Made with the area and kept on it, as the original makes them whenever an area is. See
/// <c>docs/mechanics/theatres.md</c>, section 2.
/// </summary>
public static class CAreaFronts
{
	/// <summary>The area's fronts by land, each its provinces in the order the flood met them.</summary>
	public static IReadOnlyList<List<int>> Of(CGameState state, CSupplyArea area)
	{
		if (area.Fronts is { } made)
		{
			return made;
		}
		var fronts = new List<List<int>>();
		var inFront = new HashSet<int>();
		foreach (int id in area.Provinces)
		{
			if (!inFront.Contains(id) && BordersForeignLand(state, state.Province(id)))
			{
				var front = new List<int>();
				fronts.Add(front);
				Flood(state, area, front, inFront, id);
			}
		}
		area.Fronts = fronts;
		return fronts;
	}

	/// <summary>
	/// Whether a province touches, by any edge whatever its kind, land another holds - or land nobody holds.
	/// </summary>
	public static bool BordersForeignLand(CGameState state, CProvince province)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			CProvince next = state.Province(edge.To);
			if (!next.IsSea && next.Controller != province.Controller)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The province onto the front, and then, edge by edge, each neighbour of the same area on a land border and on no front
	/// yet, with its own neighbours before the next: the original's recursion, kept as an explicit walk.
	/// </summary>
	private static void Flood(CGameState state, CSupplyArea area, List<int> front, HashSet<int> inFront, int start)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var walk = new Stack<(int Province, int Edge)>();
		front.Add(start);
		inFront.Add(start);
		walk.Push((start, 0));
		while (walk.Count > 0)
		{
			(int id, int at) = walk.Pop();
			CProvince province = state.Province(id);
			if (at >= province.Edges.Length)
			{
				continue;
			}
			walk.Push((id, at + 1));
			CProvince next = state.Province(province.Edges[at].To);
			if (ReferenceEquals(network.AreaOf(next.Id), area) && !inFront.Contains(next.Id)
				&& BordersForeignLand(state, next))
			{
				front.Add(next.Id);
				inFront.Add(next.Id);
				walk.Push((next.Id, 0));
			}
		}
	}
}
