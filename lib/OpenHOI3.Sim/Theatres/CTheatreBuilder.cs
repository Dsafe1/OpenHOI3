using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// A country's theatres made from nothing: area by area, then merged, then set beside the old ones. The original's
/// <c>BuildTheatres</c>. See <c>docs/mechanics/theatres.md</c>, sections 5 to 7.
/// </summary>
public static class CTheatreBuilder
{
	/// <summary>Spill alone that gives a front a theatre: eight provinces' worth.</summary>
	private const long EnoughSpill = 8000;

	/// <summary>Spill over home that does, in thousandths.</summary>
	private const long EnoughRatio = 250;

	/// <summary>A theatre smaller than this merges with its nearest whatever it faces.</summary>
	private const int SmallTheatre = 30;

	/// <summary>The most theatres the merging keeps without asking more of a candidate.</summary>
	private const int MostTheatres = 100000;

	/// <summary>The overlap, in thousandths, by which a new theatre is taken for an old one.</summary>
	private const long SameTheatre = 500;

	/// <summary>The least score a merge candidate starts against.</summary>
	private const long NoCandidate = 0x540be400;

	/// <summary>Squared distance a nearest theatre starts against.</summary>
	private const long Far = 1_000_000_000;

	private const int Paris = 2613;
	private const int Warszawa = 1928;
	private const int SanFrancisco = 3658;
	private const int Washington = 4530;

	public static void Build(CGameState state, CCountry country)
	{
		if (country.Tag.Value == "REB")
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var made = new List<CTheatre>();
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller == country.Tag && area.Provinces.Count > 0)
			{
				made.AddRange(BuildArea(state, network, country, area));
			}
		}
		foreach (CTheatre theatre in made)
		{
			CTheatreProvinces.RebuildAreas(state, theatre);
			Tell(state, country, $"made {theatre.Id}: {theatre.AllProvinces.Count} provinces, fronts " +
				string.Join(" ", theatre.Fronts.ConvertAll(f => $"{f.Enemy?.Value ?? "---"}{f.Provinces.Count}")) +
				$", largest area from {CTheatreProvinces.LargestArea(theatre)?.Provinces[0]}");
		}
		Merge(state, country, made);
		SetBesideOld(state, country, made);
		CTheatreUpkeep.Maintain(state, country, isMyHour: true);
		CTheatreFriends.Tell(state, country);
		country.Strategy.TheatresDirty = false;
	}

	private static List<CTheatre> BuildArea(CGameState state, CSupplyNetwork network, CCountry country, CSupplyArea area)
	{
		var leftover = new List<int>(area.Provinces);
		List<CFront> fronts = CCountryFronts.Of(state, country, area);
		var made = new List<CTheatre>();
		Tell(state, country, $"area from {area.Provinces[0]}, {area.Provinces.Count} provinces, {fronts.Count} fronts: " +
			string.Join(" | ", fronts));

		// 5.1: a front of its own.
		foreach (CFront front in fronts.ToArray())
		{
			if (front.Enemy is not { } enemy || enemy.Value == "REB" || !CFrontWorthy.Is(state, country, enemy))
			{
				continue;
			}
			(long home, long spill) = Measure(state, network, front, enemy);
			long ratio = home == 0 ? -1 : spill * 1000 / home;
			bool own = spill >= EnoughSpill || ratio >= EnoughRatio;
			Tell(state, country, $"  front {front.Enemy} ({front.Provinces.Count}): home {home} spill {spill} ratio {ratio}" +
				(own ? $" - theatre {state.NextTheatreId}" : ""));
			if (!own)
			{
				continue;
			}
			CTheatre theatre = CTheatreProvinces.New(state, country.Tag);
			CTheatreProvinces.AddFront(state, theatre, front, reorder: true);
			fronts.Remove(front);
			theatre.IsHot = front.IsHot;
			foreach (int id in front.Provinces)
			{
				leftover.Remove(id);
			}
			made.Add(theatre);
		}

		// 5.2: the four seats.
		switch (country.Tag.Value)
		{
			case "GER":
				if (made.Count < 2)
				{
					Seat(state, network, country, area, leftover, made, Paris);
				}
				if (made.Count == 1)
				{
					Seat(state, network, country, area, leftover, made, Warszawa);
				}
				break;
			case "USA":
				Seat(state, network, country, area, leftover, made, SanFrancisco);
				Seat(state, network, country, area, leftover, made, Washington);
				break;
		}

		// 5.3 and 5.4: the whole area, or the rest of it.
		if (leftover.Count == area.Provinces.Count)
		{
			CTheatre theatre = CTheatreProvinces.New(state, country.Tag);
			made.Add(theatre);
			foreach (int id in area.Provinces)
			{
				CTheatreProvinces.Add(state, theatre, id);
			}
			leftover.Clear();
		}
		else
		{
			PlaceNearest(state, country, leftover, made);
			PlaceTouching(state, leftover, made);
		}

		// 5.5: the fronts left.
		foreach (CFront front in fronts)
		{
			GiveLeftFront(state, front);
		}
		return made;
	}

	/// <summary>Home and spill of a front (5.1), each 1000 for each province of each distinct area.</summary>
	private static (long Home, long Spill) Measure(CGameState state, CSupplyNetwork network, CFront front, CCountryTag enemy)
	{
		var homeAreas = new HashSet<CSupplyArea>();
		var spillAreas = new HashSet<CSupplyArea>();
		long home = 0;
		long spill = 0;
		foreach (int id in front.Provinces)
		{
			if (network.AreaOf(id) is not { } area)
			{
				continue;
			}
			if (homeAreas.Add(area))
			{
				home += area.Provinces.Count * 1000L;
			}
			foreach (CProvinceEdge edge in state.Province(id).Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable || network.AreaOf(edge.To) is not { } beyond
					|| state.Province(edge.To).Controller != enemy)
				{
					continue;
				}
				if (spillAreas.Add(beyond))
				{
					spill += beyond.Provinces.Count * 1000L;
				}
			}
		}
		return (home, spill);
	}

	private static void Seat(CGameState state, CSupplyNetwork network, CCountry country, CSupplyArea area, List<int> leftover,
		List<CTheatre> made, int seat)
	{
		if (seat >= state.Provinces.Count)
		{
			return;
		}
		CProvince province = state.Province(seat);
		if (!ReferenceEquals(network.AreaOf(seat), area) || province.Controller != country.Tag
			|| CTheatreFigures.IsFrontProvince(state, province, withUnits: false))
		{
			return;
		}
		CTheatre theatre = CTheatreProvinces.New(state, country.Tag);
		made.Add(theatre);
		theatre.KeyProvinces.Add(seat);
		CTheatreProvinces.Add(state, theatre, seat);
		leftover.Remove(seat);
	}

	/// <summary>
	/// 5.4's first pass, again while it places something: each left province to the theatre with the nearest province of a
	/// front that is hot or faces a front-worthy enemy, or a key province, where it touches that theatre.
	/// </summary>
	private static void PlaceNearest(CGameState state, CCountry country, List<int> leftover, List<CTheatre> made)
	{
		bool changed = true;
		while (changed && leftover.Count > 0)
		{
			changed = false;
			foreach (int id in leftover.ToArray())
			{
				CProvince province = state.Province(id);
				CTheatre? best = null;
				long least = Far;
				foreach (CTheatre theatre in made)
				{
					foreach (CFront front in theatre.Fronts)
					{
						if (!front.IsHot && (front.Enemy is not { } enemy || !CFrontWorthy.Is(state, country, enemy)))
						{
							continue;
						}
						foreach (int other in front.Provinces)
						{
							long squared = CMapDistance.Squared(province, state.Province(other), state.MapWidth);
							if (squared < least)
							{
								least = squared;
								best = theatre;
							}
						}
					}
					foreach (int other in theatre.KeyProvinces)
					{
						long squared = CMapDistance.Squared(province, state.Province(other), state.MapWidth);
						if (squared < least)
						{
							least = squared;
							best = theatre;
						}
					}
				}
				if (best is not null && CTheatreProvinces.Touches(state, best, id))
				{
					CTheatreProvinces.Add(state, best, id);
					leftover.Remove(id);
					changed = true;
				}
			}
		}
	}

	/// <summary>5.4's second pass, again while it places something: each left province to the first theatre it touches.</summary>
	private static void PlaceTouching(CGameState state, List<int> leftover, List<CTheatre> made)
	{
		bool changed = true;
		while (changed && leftover.Count > 0)
		{
			changed = false;
			foreach (int id in leftover.ToArray())
			{
				foreach (CTheatre theatre in made)
				{
					if (CTheatreProvinces.Touches(state, theatre, id))
					{
						CTheatreProvinces.Add(state, theatre, id);
						leftover.Remove(id);
						changed = true;
						break;
					}
				}
			}
		}
	}

	/// <summary>
	/// 5.5: a front with no theatre of its own split by the theatre each of its provinces is in, and each part given to its
	/// theatre in the order of their numbers.
	/// </summary>
	private static void GiveLeftFront(CGameState state, CFront front)
	{
		if (front.Provinces.Count == 0)
		{
			return;
		}
		int first = front.Provinces[0];
		int firstTheatre = state.Province(first).TheatreId;
		var parts = new SortedDictionary<int, CFront>();
		var moved = new List<int>();
		foreach (int id in front.Provinces.ToArray())
		{
			if (id == first)
			{
				continue;
			}
			int theatreId = state.Province(id).TheatreId;
			if (theatreId != firstTheatre)
			{
				if (!parts.TryGetValue(theatreId, out CFront? part))
				{
					parts[theatreId] = part = new CFront { Enemy = front.Enemy };
				}
				part.Provinces.Add(id);
				moved.Add(id);
			}
			if (state.Theatres.GetValueOrDefault(theatreId) is { } theatre)
			{
				CTheatreProvinces.Remove(state, theatre, id);
			}
		}
		foreach (int id in moved)
		{
			front.Provinces.Remove(id);
		}
		parts[firstTheatre] = front;
		if (state.Theatres.GetValueOrDefault(firstTheatre) is { } own)
		{
			CTheatreProvinces.Remove(state, own, first);
		}
		foreach ((int theatreId, CFront part) in parts)
		{
			if (state.Theatres.GetValueOrDefault(theatreId) is { } theatre)
			{
				CTheatreProvinces.AddFront(state, theatre, part, reorder: true);
			}
		}
	}

	/// <summary>Section 6: while the smallest has a candidate, it goes into the best of them.</summary>
	private static void Merge(CGameState state, CCountry country, List<CTheatre> made)
	{
		int limit = MostTheatres;
		if (state.Played.Contains(country.Tag) && CCountryLists.Enemies(state, country).Count > 0
			&& country.Strategy.Theatres.Count >= 1)
		{
			limit = country.Strategy.Theatres.Count;
		}
		while (made.Count > 0)
		{
			CTheatre? smallest = null;
			foreach (CTheatre theatre in made)
			{
				if (smallest is null || theatre.AllProvinces.Count < smallest.AllProvinces.Count)
				{
					smallest = theatre;
				}
			}
			List<CCountryTag> enemies = CTheatreProvinces.FrontEnemies(state, smallest!);
			CTheatre? best = null;
			long least = NoCandidate;
			foreach (CTheatre other in made)
			{
				if (ReferenceEquals(other, smallest) || !IsCandidate(state, made.Count, limit, smallest!, other, enemies))
				{
					continue;
				}
				long score = CTheatreProvinces.Distance(state, smallest!, other) * 1000
					+ (other.AllProvinces.Count + smallest!.AllProvinces.Count) * 1000L;
				if (score < least)
				{
					least = score;
					best = other;
				}
			}
			if (best is null)
			{
				Tell(state, country, $"merging over: theatre {smallest!.Id} ({smallest.AllProvinces.Count}) has no candidate");
				return;
			}
			Tell(state, country, $"merge {smallest!.Id} ({smallest.AllProvinces.Count}) into {best.Id} ({best.AllProvinces.Count}), score {least}");
			foreach (CFront front in smallest!.Fronts.ToArray())
			{
				CTheatreProvinces.RemoveFront(state, smallest, front);
				CTheatreProvinces.AddFront(state, best, front, reorder: false);
				if (front.IsHot)
				{
					best.IsHot = true;
				}
			}
			CTheatreProvinces.Take(state, best, smallest.Provinces);
			CTheatreProvinces.RebuildAreas(state, best);
			made.Remove(smallest);
			CTheatreProvinces.Delete(state, smallest);
		}
	}

	private static void Tell(CGameState state, CCountry country, string line)
	{
		if (state.TheatreTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Tell(line);
		}
	}

	private static bool IsCandidate(CGameState state, int count, int limit, CTheatre smallest, CTheatre other,
		List<CCountryTag> enemies)
	{
		if (smallest.AllProvinces.Count < SmallTheatre || count > limit)
		{
			return true;
		}
		if (!ReferenceEquals(CTheatreProvinces.LargestArea(other), CTheatreProvinces.LargestArea(smallest)) || enemies.Count <= 0)
		{
			return false;
		}
		List<CCountryTag> theirs = CTheatreProvinces.FrontEnemies(state, other);
		int found = 0;
		if (enemies.Count == theirs.Count)
		{
			foreach (CCountryTag enemy in enemies)
			{
				if (theirs.Contains(enemy))
				{
					found++;
				}
			}
		}
		return found == enemies.Count || CTheatreProvinces.FrontsMeet(state, smallest, other);
	}

	/// <summary>Section 7: each new theatre set beside the old ones, an old one taking a new one's place where it overlaps it.</summary>
	private static void SetBesideOld(CGameState state, CCountry country, List<CTheatre> made)
	{
		List<CTheatre> theatres = country.Strategy.Theatres;
		var old = new List<CTheatre>(theatres);
		theatres.Clear();
		foreach (CTheatre theatre in made)
		{
			long best = 0;
			CTheatre? bestOld = null;
			foreach (CTheatre candidate in old)
			{
				long overlap = CTheatreProvinces.Overlap(theatre, candidate);
				if (overlap > best)
				{
					best = overlap;
					bestOld = candidate;
					if (overlap >= SameTheatre)
					{
						break;
					}
				}
			}
			if (best < SameTheatre || bestOld is null)
			{
				theatres.Add(theatre);
				CTheatreProvinces.Stamp(state, theatre);
				continue;
			}
			CTheatreProvinces.TakeOver(state, bestOld, theatre);
			old.Remove(bestOld);
			theatres.Add(bestOld);
			CTheatreProvinces.Stamp(state, bestOld);
			CTheatreProvinces.Delete(state, theatre);
		}
		foreach (CTheatre dropped in old)
		{
			Agents.CAgents.Ensure(state, country, dropped, going: true);
			CTheatres.RemoveHeadquarters(state, dropped);
			CTheatreProvinces.Delete(state, dropped);
		}
		// The agents of the theatres made or kept set their areas again (agents.md, section 1).
		foreach (CTheatre theatre in theatres)
		{
			Agents.CAgents.Ensure(state, country, theatre, going: false);
		}
	}
}
