using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// What a theatre's provinces and fronts are put on it and taken off it by, as the original's small routines do it - each
/// province stamped with the theatre it is in (<see cref="CProvince.TheatreId"/>). See <c>docs/mechanics/theatres.md</c>,
/// sections 5 to 8.
/// </summary>
public static class CTheatreProvinces
{
	/// <summary>Where a front's chain is started from: a province with fewer neighbours on it than this is.</summary>
	private const int FewNeighbours = 100;

	/// <summary>A new theatre with the game's next number, known to the game.</summary>
	public static CTheatre New(CGameState state, CCountryTag country)
	{
		var theatre = new CTheatre(state.NextTheatreId++, country);
		state.Theatres[theatre.Id] = theatre;
		return theatre;
	}

	/// <summary>A theatre forgotten: one merged away, set aside or dropped.</summary>
	public static void Delete(CGameState state, CTheatre theatre)
	{
		state.Theatres.Remove(theatre.Id);
	}

	/// <summary>The theatre a province is stamped with, where it still exists.</summary>
	public static CTheatre? Of(CGameState state, int province)
	{
		return state.Theatres.GetValueOrDefault(state.Province(province).TheatreId);
	}

	/// <summary>A province onto the theatre's own provinces, stamped with it.</summary>
	public static void Add(CGameState state, CTheatre theatre, int province)
	{
		theatre.Provinces.Add(province);
		theatre.AllProvinces.Add(province);
		state.Province(province).TheatreId = theatre.Id;
	}

	/// <summary>
	/// A front onto the theatre: with <paramref name="reorder"/> its provinces first put back as a chain (section 5.6); then
	/// each stamped with the theatre and counted among its provinces.
	/// </summary>
	public static void AddFront(CGameState state, CTheatre theatre, CFront front, bool reorder)
	{
		if (reorder)
		{
			Chain(state, front);
		}
		foreach (int id in front.Provinces)
		{
			state.Province(id).TheatreId = theatre.Id;
			theatre.AllProvinces.Add(id);
		}
		theatre.Fronts.Add(front);
	}

	private static void Chain(CGameState state, CFront front)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var taken = new List<int>(front.Provinces);
		var takenSet = new HashSet<int>(taken);
		front.Provinces.Clear();
		var put = new HashSet<int>();
		while (front.Provinces.Count < taken.Count)
		{
			int start = 0;
			int least = FewNeighbours;
			foreach (int id in taken)
			{
				if (put.Contains(id))
				{
					continue;
				}
				CProvince province = state.Province(id);
				CSupplyArea? area = network.AreaOf(id);
				int count = 0;
				foreach (CProvinceEdge edge in province.Edges)
				{
					if (edge.Kind != EProvinceEdgeKind.Impassable && ReferenceEquals(network.AreaOf(edge.To), area)
						&& takenSet.Contains(edge.To))
					{
						count++;
					}
				}
				if (count < least)
				{
					start = id;
					least = count;
					if (count < 2)
					{
						break;
					}
				}
			}
			if (start == 0)
			{
				break;
			}
			Walk(state, network, front, put, takenSet, start);
		}
	}

	/// <summary>The original's recursion from a chain's start, kept as an explicit walk.</summary>
	private static void Walk(CGameState state, CSupplyNetwork network, CFront front, HashSet<int> put, HashSet<int> taken,
		int start)
	{
		var walk = new Stack<(int Province, int Edge)>();
		front.Provinces.Add(start);
		put.Add(start);
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
			CProvinceEdge edge = province.Edges[at];
			if (edge.Kind == EProvinceEdgeKind.Impassable || !ReferenceEquals(network.AreaOf(edge.To), network.AreaOf(id))
				|| put.Contains(edge.To) || !taken.Contains(edge.To))
			{
				continue;
			}
			front.Provinces.Add(edge.To);
			put.Add(edge.To);
			walk.Push((edge.To, 0));
		}
	}

	/// <summary>A front off the theatre, its provinces' stamps cleared.</summary>
	public static void RemoveFront(CGameState state, CTheatre theatre, CFront front)
	{
		theatre.Fronts.Remove(front);
		foreach (int id in front.Provinces)
		{
			state.Province(id).TheatreId = 0;
		}
	}

	/// <summary>A province off the theatre's own, its key provinces and its count, its stamp cleared.</summary>
	public static void Remove(CGameState state, CTheatre theatre, int province)
	{
		theatre.Provinces.Remove(province);
		theatre.AllProvinces.Remove(province);
		theatre.KeyProvinces.Remove(province);
		state.Province(province).TheatreId = 0;
	}

	/// <summary>A province out of every front of the theatre and off its lists, its stamp left.</summary>
	public static void Drop(CTheatre theatre, int province)
	{
		foreach (CFront front in theatre.Fronts)
		{
			while (front.Provinces.Remove(province))
			{
			}
		}
		theatre.Provinces.Remove(province);
		theatre.KeyProvinces.Remove(province);
		theatre.AllProvinces.Remove(province);
	}

	/// <summary>Provinces onto the theatre's own, stamped with it: what a merge hands the winner.</summary>
	public static void Take(CGameState state, CTheatre theatre, IEnumerable<int> provinces)
	{
		foreach (int id in new List<int>(provinces))
		{
			Add(state, theatre, id);
		}
	}

	/// <summary>
	/// Each of its provinces the country holds stamped with it; then each key province no longer stamped with it dropped.
	/// </summary>
	public static void Stamp(CGameState state, CTheatre theatre)
	{
		foreach (int id in theatre.AllProvinces)
		{
			CProvince province = state.Province(id);
			if (province.Controller == theatre.Country)
			{
				province.TheatreId = theatre.Id;
			}
		}
		theatre.KeyProvinces.RemoveAll(id => state.Province(id).TheatreId != theatre.Id);
	}

	/// <summary>
	/// An old theatre given a new one's contents, keeping its own number and headquarters (section 7).
	/// </summary>
	public static void TakeOver(CGameState state, CTheatre old, CTheatre made)
	{
		old.IsHot = made.IsHot;
		old.Fronts.Clear();
		old.KeyProvinces.Clear();
		old.Provinces.Clear();
		old.AllProvinces.Clear();
		foreach (CFront front in made.Fronts)
		{
			AddFront(state, old, front, reorder: false);
		}
		made.Fronts.Clear();
		Take(state, old, made.Provinces);
		old.KeyProvinces.AddRange(made.KeyProvinces);
		old.Areas.Clear();
		old.Areas.AddRange(made.Areas);
		old.Ports.Clear();
		old.Ports.AddRange(made.Ports);
		old.Priority = made.Priority;
	}

	/// <summary>
	/// Its areas, in the order they were made (ours: the original's go by address), and its ports, the largest naval base
	/// first and of two alike the one met first.
	/// </summary>
	public static void RebuildAreas(CGameState state, CTheatre theatre)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var areas = new HashSet<CSupplyArea>();
		theatre.Ports.Clear();
		CBuilding? navalBase = CTheatreFigures.NavalBase(state);
		foreach (int id in theatre.AllProvinces)
		{
			if (network.AreaOf(id) is not { } area)
			{
				continue;
			}
			areas.Add(area);
			long level = CTheatreFigures.WholeLevel(state.Province(id), navalBase);
			if (level <= 0)
			{
				continue;
			}
			int at = theatre.Ports.Count;
			while (at > 0 && CTheatreFigures.WholeLevel(state.Province(theatre.Ports[at - 1]), navalBase) < level)
			{
				at--;
			}
			theatre.Ports.Insert(at, id);
		}
		theatre.Areas.Clear();
		foreach (CSupplyArea area in network.Areas)
		{
			if (areas.Contains(area))
			{
				theatre.Areas.Add(area);
			}
		}
	}

	/// <summary>Whether the province has an edge, not an impassable one, to one of the theatre's provinces.</summary>
	public static bool Touches(CGameState state, CTheatre theatre, int province)
	{
		foreach (CProvinceEdge edge in state.Province(province).Edges)
		{
			if (edge.Kind != EProvinceEdgeKind.Impassable && theatre.AllProvinces.Contains(edge.To))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// How far apart two theatres are: the whole root of the least squared distance between a province of each, from a
	/// hundred million.
	/// </summary>
	public static long Distance(CGameState state, CTheatre one, CTheatre other)
	{
		long least = 100_000_000;
		foreach (int a in one.AllProvinces)
		{
			CProvince first = state.Province(a);
			foreach (int b in other.AllProvinces)
			{
				long squared = CMapDistance.Squared(first, state.Province(b), state.MapWidth);
				if (squared < least)
				{
					least = squared;
				}
			}
		}
		return CMapDistance.WholeRoot(least);
	}

	/// <summary>The front-worthy enemies its fronts face, each once, in the fronts' order.</summary>
	public static List<CCountryTag> FrontEnemies(CGameState state, CTheatre theatre)
	{
		var enemies = new List<CCountryTag>();
		CCountry? country = state.Country(theatre.Country);
		foreach (CFront front in theatre.Fronts)
		{
			if (front.Enemy is { } enemy && country is not null && CFrontWorthy.Is(state, country, enemy)
				&& !enemies.Contains(enemy))
			{
				enemies.Add(enemy);
			}
		}
		return enemies;
	}

	/// <summary>
	/// Whether the first or the last province of one of the other's front-worthy fronts touches the first or the last of one
	/// of the theatre's.
	/// </summary>
	public static bool FrontsMeet(CGameState state, CTheatre theatre, CTheatre other)
	{
		CCountry? country = state.Country(theatre.Country);
		if (country is null)
		{
			return false;
		}
		foreach (CFront front in theatre.Fronts)
		{
			if (front.Enemy is not { } enemy || front.Provinces.Count == 0 || !CFrontWorthy.Is(state, country, enemy))
			{
				continue;
			}
			int first = front.Provinces[0];
			int last = front.Provinces[^1];
			foreach (CFront theirs in other.Fronts)
			{
				if (theirs.Enemy is not { } them || theirs.Provinces.Count == 0 || !CFrontWorthy.Is(state, country, them))
				{
					continue;
				}
				foreach (int end in new[] { theirs.Provinces[0], theirs.Provinces[^1] })
				{
					foreach (CProvinceEdge edge in state.Province(end).Edges)
					{
						if (edge.Kind != EProvinceEdgeKind.Impassable && (edge.To == first || edge.To == last))
						{
							return true;
						}
					}
				}
			}
		}
		return false;
	}

	/// <summary>Its area with most provinces, the first of equals; null where it lies in none.</summary>
	public static CSupplyArea? LargestArea(CTheatre theatre)
	{
		CSupplyArea? largest = null;
		int most = 0;
		foreach (CSupplyArea area in theatre.Areas)
		{
			if (area.Provinces.Count > most)
			{
				largest = area;
				most = area.Provinces.Count;
			}
		}
		return largest;
	}

	/// <summary>
	/// How much of an old theatre a new one holds, in thousandths of the old one's provinces: nought where either holds
	/// none.
	/// </summary>
	public static long Overlap(CTheatre made, CTheatre old)
	{
		if (old.AllProvinces.Count < 1 || made.AllProvinces.Count < 1)
		{
			return 0;
		}
		var theirs = new HashSet<int>(old.AllProvinces);
		long shared = 0;
		foreach (int id in made.AllProvinces)
		{
			if (theirs.Contains(id))
			{
				shared++;
			}
		}
		return shared * 1000 / old.AllProvinces.Count;
	}
}
