using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// An area's fronts for one country: its land fronts cut again by whom each province faces and whether the country is at
/// war across it. The original's <c>CollectAreaFrontsForCountry</c>. See <c>docs/mechanics/theatres.md</c>, section 3.
/// </summary>
public static class CCountryFronts
{
	public static List<CFront> Of(CGameState state, CCountry country, CSupplyArea area)
	{
		var made = new List<CFront>();
		foreach (List<int> front in CAreaFronts.Of(state, area))
		{
			foreach (int id in front)
			{
				Place(state, country, made, state.Province(id));
			}
		}
		Face(state, area, made);
		JoinFacing(made);
		return made;
	}

	/// <summary>A province's enemy and heat, and the fronts it joins (section 3, steps 1 and 2).</summary>
	private static void Place(CGameState state, CCountry country, List<CFront> made, CProvince province)
	{
		CCountryTag? enemy = null;
		bool met = false;
		bool hot = false;
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince next = state.Province(edge.To);
			if (next.IsSea || next.Controller == province.Controller)
			{
				continue;
			}
			CCountryTag? holder = next.Controller;
			if (!met || enemy is null)
			{
				enemy = holder;
				met = true;
			}
			else if (IsWorthy(state, country, holder) || !IsWorthy(state, country, enemy))
			{
				enemy = holder;
			}
			if (holder is { } other && other != country.Tag
				&& (CCountryLists.AtWar(state, country.Tag, other) || other.Value == "REB" || country.Tag.Value == "REB"))
			{
				hot = true;
				break;
			}
		}

		var joined = new List<CFront>();
		foreach (CFront front in made)
		{
			if (((hot && front.IsHot) || front.Enemy == enemy) && Touches(state, front, province) && !joined.Contains(front))
			{
				joined.Add(front);
			}
		}
		if (joined.Count == 0)
		{
			var front = new CFront { Enemy = enemy, IsHot = hot };
			front.Provinces.Add(province.Id);
			made.Add(front);
			return;
		}
		CFront first = joined[0];
		for (int last = joined.Count - 1; last > 0; last--)
		{
			CFront other = joined[last];
			first.Provinces.AddRange(other.Provinces);
			first.IsHot = first.IsHot || other.IsHot;
			made.Remove(other);
		}
		first.Provinces.Add(province.Id);
	}

	private static bool IsWorthy(CGameState state, CCountry country, CCountryTag? other)
	{
		return other is { } tag && CFrontWorthy.Is(state, country, tag);
	}

	/// <summary>Whether the province has an edge, not an impassable one, to a province of the front.</summary>
	public static bool Touches(CGameState state, CFront front, CProvince province)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind != EProvinceEdgeKind.Impassable && front.Provinces.Contains(edge.To))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Each hot front's area faced: the area other than its own most of its provinces' edges reach, the first of equals in
	/// the order the areas were made (section 3, step 3; ours: the original's map goes by address).
	/// </summary>
	private static void Face(CGameState state, CSupplyArea area, List<CFront> made)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CFront front in made)
		{
			if (!front.IsHot)
			{
				continue;
			}
			var counts = new Dictionary<CSupplyArea, int>();
			foreach (int id in front.Provinces)
			{
				foreach (CProvinceEdge edge in state.Province(id).Edges)
				{
					if (edge.Kind != EProvinceEdgeKind.Impassable && network.AreaOf(edge.To) is { } other
						&& !ReferenceEquals(other, area))
					{
						counts[other] = counts.GetValueOrDefault(other) + 1;
					}
				}
			}
			CSupplyArea? best = null;
			int most = 0;
			foreach (CSupplyArea other in network.Areas)
			{
				if (counts.TryGetValue(other, out int count) && count > most)
				{
					best = other;
					most = count;
				}
			}
			front.Faces = best;
		}
	}

	/// <summary>Each hot front onto the end of the first other hot front facing the same area (section 3, step 3).</summary>
	private static void JoinFacing(List<CFront> made)
	{
		foreach (CFront front in made.ToArray())
		{
			if (!front.IsHot || !made.Contains(front))
			{
				continue;
			}
			foreach (CFront into in made)
			{
				if (into.IsHot && !ReferenceEquals(into, front) && ReferenceEquals(into.Faces, front.Faces))
				{
					into.Provinces.AddRange(front.Provinces);
					made.Remove(front);
					break;
				}
			}
		}
	}
}
