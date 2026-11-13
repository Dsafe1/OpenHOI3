using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// A running game's theatres brought up to date without making them again: each province the country now holds placed,
/// each it lost dropped, and every theatre's fronts made again. The original's <c>RefreshTheatreProvinces</c>. See
/// <c>docs/mechanics/theatres.md</c>, section 8.
/// </summary>
public static class CTheatreRefresh
{
	/// <summary>The nearest theatre is looked for from this, in thousandths of a distance.</summary>
	private const long Far = 0x4876e800;

	/// <summary>
	/// On entering a loaded game: the refresh's provinces placed and dropped, stamped, areas and fronts made again, and not its
	/// agents' or headquarters' upkeep (FINDINGS-liveread.md, section 44).
	/// </summary>
	public static void FrontsOfLoad(CGameState state, CCountry country)
	{
		Refresh(state, country, ofLoad: true);
	}

	public static void Refresh(CGameState state, CCountry country) => Refresh(state, country, ofLoad: false);

	/// <summary>
	/// The refresh with its flag off, as the theatre editor's command asks it: no province placed and none the country no
	/// longer holds dropped - both sit behind the flag - and the rest as ever (<c>docs/mechanics/attaching.md</c>, section 7).
	/// </summary>
	public static void RefreshKeepingProvinces(CGameState state, CCountry country) => Refresh(state, country, ofLoad: false, place: false);

	private static void Refresh(CGameState state, CCountry country, bool ofLoad, bool place = true)
	{
		List<CTheatre> theatres = country.Strategy.Theatres;
		foreach (CProvince province in state.Provinces)
		{
			if (!place || province.Controller != country.Tag)
			{
				continue;
			}
			if (CTheatreProvinces.Of(state, province.Id) is not { } theatre || theatre.Country != country.Tag)
			{
				Place(state, country, province);
			}
		}
		foreach (CTheatre theatre in theatres)
		{
			if (!place)
			{
				break;
			}
			foreach (int id in theatre.AllProvinces.ToArray())
			{
				CProvince province = state.Province(id);
				if (province.Controller == country.Tag)
				{
					continue;
				}
				CTheatreProvinces.Drop(theatre, id);
				if (province.TheatreId == theatre.Id)
				{
					province.TheatreId = 0;
				}
			}
		}
		foreach (CTheatre theatre in theatres)
		{
			CTheatreProvinces.Stamp(state, theatre);
			CTheatreProvinces.RebuildAreas(state, theatre);
			RebuildFronts(state, country, theatre);
			if (ofLoad)
			{
				continue;
			}
			// Its agent sets its area again, or goes with a theatre left with no provinces (agents.md, section 1).
			Agents.CAgents.Ensure(state, country, theatre, going: theatre.AllProvinces.Count == 0);
		}
		if (ofLoad)
		{
			return;
		}
		CTheatreUpkeep.Maintain(state, country, isMyHour: true);
		country.Strategy.TheatresDirty = false;
	}

	/// <summary>
	/// A province the country holds into one of its theatres: in an area of one province, the theatre heading a land unit of
	/// the country's standing there; else the one most of its neighbours are in; else the one with the nearest province.
	/// </summary>
	private static void Place(CGameState state, CCountry country, CProvince province)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (network.AreaOf(province.Id) is not { } area)
		{
			return;
		}
		if (area.Provinces.Count == 1)
		{
			foreach (CUnit unit in state.Positions.Of(province.Id))
			{
				if (!CTheatres.IsLand(unit) || unit.Owner != country.Tag)
				{
					continue;
				}
				CUnit top = unit;
				while (top.Parent is not null)
				{
					top = top.Parent;
				}
				if (top.Kind == EUnitKind.Theatre && CTheatres.FindFor(state, top) is { } heads)
				{
					CTheatreProvinces.Add(state, heads, province.Id);
					return;
				}
			}
		}
		var counts = new Dictionary<CTheatre, int>();
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince next = state.Province(edge.To);
			if (next.Controller == country.Tag && CTheatreProvinces.Of(state, next.Id) is { } theirs && theirs.Country == country.Tag)
			{
				counts[theirs] = counts.GetValueOrDefault(theirs) + 1;
			}
		}
		CTheatre? best = null;
		int most = 0;
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (counts.TryGetValue(theatre, out int count) && count > most)
			{
				best = theatre;
				most = count;
			}
		}
		if (best is not null)
		{
			CTheatreProvinces.Add(state, best, province.Id);
			return;
		}
		long least = Far;
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			foreach (int id in theatre.AllProvinces)
			{
				long distance = CMapDistance.Between(province, state.Province(id), state.MapWidth) * 1000;
				if (distance < least)
				{
					least = distance;
					best = theatre;
				}
			}
		}
		if (best is not null)
		{
			CTheatreProvinces.Add(state, best, province.Id);
		}
	}

	/// <summary>
	/// A theatre's fronts made again from its areas: the country's fronts of each, keeping only the theatre's provinces.
	/// </summary>
	private static void RebuildFronts(CGameState state, CCountry country, CTheatre theatre)
	{
		theatre.IsHot = false;
		theatre.Fronts.Clear();
		foreach (CSupplyArea area in theatre.Areas.ToArray())
		{
			if (area.Provinces.Count == 0)
			{
				continue;
			}
			foreach (CFront front in CCountryFronts.Of(state, country, area))
			{
				var others = new List<int>();
				foreach (int id in front.Provinces)
				{
					if (state.Province(id).TheatreId == theatre.Id)
					{
						theatre.AllProvinces.Remove(id);
					}
					else
					{
						others.Add(id);
					}
				}
				foreach (int id in others)
				{
					front.Provinces.Remove(id);
				}
				if (front.Provinces.Count == 0)
				{
					continue;
				}
				CTheatreProvinces.AddFront(state, theatre, front, reorder: true);
				if (front.IsHot)
				{
					theatre.IsHot = true;
				}
			}
		}
	}
}
