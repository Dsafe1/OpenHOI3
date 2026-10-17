using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The other countries' areas an agent's theatre borders most, in the blocks of one side's land its own areas are in:
/// where a subject's or a faction member's fronts may be taken into its area. The original's
/// <c>RebuildFocusOwnerAreas</c>, <c>CollectTheatreFocusAreas</c> and <c>TheatreBordersAreaMost</c>. See
/// <c>docs/mechanics/agents.md</c>, section 8.3.
/// </summary>
public static class CFocusAreas
{
	public static void Rebuild(CGameState state, CCountry country, CAgent agent, CTheatre theatre)
	{
		agent.FocusAreas.Clear();
		agent.FocusAreas.AddRange(Of(state, country, theatre));
	}

	/// <summary>The theatre's focus areas, worked out afresh.</summary>
	public static List<CSupplyArea> Of(CGameState state, CCountry country, CTheatre theatre)
	{
		var focus = new List<CSupplyArea>();
		var groups = new List<CSupplyGroup>();
		foreach (CSupplyArea area in theatre.Areas)
		{
			if (area.Group is { } group && !groups.Contains(group) && Provinces(group) > 1)
			{
				groups.Add(group);
			}
		}
		foreach (CSupplyGroup group in groups)
		{
			Collect(state, country, focus, theatre, group);
		}
		return focus;
	}

	private static int Provinces(CSupplyGroup group)
	{
		int count = 0;
		foreach (CSupplyArea area in group.Areas)
		{
			count += area.Provinces.Count;
		}
		return count;
	}

	/// <summary>Again while one is added, and while the block holds more provinces than the list has areas.</summary>
	private static void Collect(CGameState state, CCountry country, List<CSupplyArea> focus, CTheatre theatre, CSupplyGroup group)
	{
		int total = Provinces(group);
		bool added = true;
		while (added && total > focus.Count)
		{
			added = false;
			foreach (CSupplyArea area in group.Areas)
			{
				if (area.Controller == theatre.Country || focus.Contains(area))
				{
					continue;
				}
				if (state.Country(area.Controller) is { } other && other.Faction.Length > 0 && country.Faction.Length > 0
					&& other.Faction != country.Faction)
				{
					continue;
				}
				(bool most, bool anyEdge) = BordersMost(state, theatre, area);
				if (most || (!anyEdge && focus.Count > 0))
				{
					focus.Add(area);
					added = true;
				}
			}
		}
	}

	/// <summary>
	/// The edges, not impassable, from the area's fronts into the theatre's country's land, counted by the theatre each
	/// lands in; whether this theatre has strictly the most, the lowest number first among equals, and whether there was any.
	/// </summary>
	private static (bool Most, bool AnyEdge) BordersMost(CGameState state, CTheatre theatre, CSupplyArea area)
	{
		var counts = new SortedDictionary<int, int>();
		foreach (List<int> front in CAreaFronts.Of(state, area))
		{
			foreach (int id in front)
			{
				foreach (CProvinceEdge edge in state.Province(id).Edges)
				{
					if (edge.Kind == EProvinceEdgeKind.Impassable)
					{
						continue;
					}
					CProvince beside = state.Province(edge.To);
					if (beside.IsSea || beside.Controller != theatre.Country)
					{
						continue;
					}
					counts[beside.TheatreId] = counts.GetValueOrDefault(beside.TheatreId) + 1;
				}
			}
		}
		if (counts.Count == 0)
		{
			return (false, false);
		}
		int best = 0;
		int most = -1;
		foreach ((int id, int count) in counts)
		{
			if (count > most)
			{
				most = count;
				best = id;
			}
		}
		return (best == theatre.Id, true);
	}
}
