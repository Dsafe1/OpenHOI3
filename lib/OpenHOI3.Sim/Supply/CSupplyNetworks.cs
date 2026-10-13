using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// Works out who draws supplies from where: the land is cut into areas, areas on one
/// side are grouped, each group picks a depot, and every province is given how far it
/// is from its own. See <c>docs/mechanics/supply.md</c>, sections 2 to 6.
///
/// The original keeps all of this up to date piece by piece as provinces change hands.
/// This makes it whole each time, which gives what the original has on the first hour
/// of a game and after a load.
/// </summary>
public static class CSupplyNetworks
{
	public const string NavalBase = "naval_base";
	public const string Industry = "industry";

	/// <summary>A province with this much infrastructure at most, in thousandths of a whole, is in no area.</summary>
	public const long LeastInfrastructure = 100;

	/// <summary>What a capital is worth as a depot, over any province that is none.</summary>
	private const long CapitalWorth = 2000;

	/// <summary>What the capital of a country that is nobody's puppet is worth on top.</summary>
	private const long FreeCapitalWorth = 1000000;

	/// <summary>
	/// The game's network, made again when a province has changed hands since it was
	/// made or when <paramref name="force"/> says so - a day has passed, say, and with it
	/// whatever else the network hangs on.
	/// </summary>
	public static CSupplyNetwork Refresh(CGameState state, bool force = false)
	{
		if (force || state.SupplyNetwork is not { } network || network.ControlChanges != state.ControlChanges
			|| network.ProvinceCount != state.Provinces.Count)
		{
			network = Build(state);
			state.SupplyNetwork = network;
		}
		return network;
	}

	/// <summary>
	/// The area one province is in, alone and without the rest of the network: the
	/// province and every province its holder controls that is joined to it through such
	/// provinces, each with infrastructure enough to count. Empty for a province that is
	/// in none. It is the rule <see cref="Build"/> cuts the whole map by, for a caller
	/// that asks about one country's home and nothing else.
	/// </summary>
	public static List<int> AreaAround(CGameState state, int start)
	{
		var area = new List<int>();
		CBuilding? roads = null;
		foreach (CBuilding building in state.Buildings)
		{
			if (building.ModifierKey.Equals(CSupplyFigures.Infrastructure, StringComparison.OrdinalIgnoreCase))
			{
				roads = building;
				break;
			}
		}
		bool Counts(CProvince province)
		{
			return province.Controller is not null && roads is not null
				&& province.BuildingMaxLevel(roads).Raw * roads.ModifierValue.Raw / CFixedPoint.Scale > LeastInfrastructure;
		}

		if (start <= 0 || start >= state.Provinces.Count || !Counts(state.Province(start)))
		{
			return area;
		}
		CCountryTag? holder = state.Province(start).Controller;
		var seen = new HashSet<int> { start };
		var waiting = new Stack<int>();
		waiting.Push(start);
		while (waiting.Count > 0)
		{
			int id = waiting.Pop();
			area.Add(id);
			foreach (CProvinceEdge edge in state.Province(id).Edges)
			{
				CProvince next = state.Province(edge.To);
				if (next.Controller == holder && Counts(next) && seen.Add(next.Id))
				{
					waiting.Push(next.Id);
				}
			}
		}
		return area;
	}

	public static CSupplyNetwork Build(CGameState state)
	{
		var network = new CSupplyNetwork(state.Provinces.Count) { ControlChanges = state.ControlChanges };
		var build = new CBuild(state, network);
		build.MakeAreas();
		build.MakeGroups();
		build.GiveDepots();
		build.MeasureDistances();
		return network;
	}

	/// <summary>One making of the network: what is looked up over and over, kept for the length of it.</summary>
	private sealed class CBuild
	{
		private readonly CGameState _state;
		private readonly CSupplyNetwork _network;
		private readonly long[] _mostInfrastructure;
		private readonly CBuilding? _navalBase;
		private readonly CBuilding? _industry;
		private readonly Dictionary<CCountryTag, List<CSupplyArea>> _areasOf = new();

		public CBuild(CGameState state, CSupplyNetwork network)
		{
			_state = state;
			_network = network;
			_mostInfrastructure = new long[state.Provinces.Count];
			foreach (CProvince province in state.Provinces)
			{
				_mostInfrastructure[province.Id] = CSupplyFigures.MostInfrastructure(state, province);
			}
			foreach (CBuilding building in state.Buildings)
			{
				if (building.Key.Equals(NavalBase, StringComparison.OrdinalIgnoreCase))
				{
					_navalBase = building;
				}
				else if (building.Key.Equals(Industry, StringComparison.OrdinalIgnoreCase))
				{
					_industry = building;
				}
			}
		}

		// --- areas ---

		/// <summary>Whether a province can be in an area at all: somebody holds it and it has roads enough.</summary>
		private bool Counts(CProvince province)
		{
			return province.Controller is not null && _mostInfrastructure[province.Id] > LeastInfrastructure;
		}

		private long PortLevel(CProvince province)
		{
			return _navalBase is null ? 0 : province.BuildingMaxLevel(_navalBase).Raw;
		}

		private bool HasWorkingPort(CProvince province)
		{
			return province.Owner is not null && province.PortSea != 0 && PortLevel(province) > 0;
		}

		private CSupplyArea? CapitalArea(CCountryTag? tag)
		{
			return _state.Country(tag) is { ActingCapital: > 0 } country ? _network.AreaOf(country.ActingCapital) : null;
		}

		public void MakeAreas()
		{
			// What each country holds, in the order of the provinces.
			var held = new Dictionary<CCountryTag, List<int>>();
			foreach (CProvince province in _state.Provinces)
			{
				if (province.Id > 0 && province.Controller is { } holder)
				{
					if (!held.TryGetValue(holder, out List<int>? list))
					{
						held[holder] = list = new List<int>();
					}
					list.Add(province.Id);
				}
			}

			foreach (CCountry country in _state.Countries)
			{
				var areas = new List<CSupplyArea>();
				_areasOf[country.Tag] = areas;
				if (!held.TryGetValue(country.Tag, out List<int>? provinces))
				{
					continue;
				}
				foreach (int id in provinces)
				{
					if (_network.AreaOf(id) is null && Counts(_state.Province(id)))
					{
						var area = new CSupplyArea(country.Tag);
						Flood(area, id);
						areas.Add(area);
						_network.Areas.Add(area);
					}
				}
			}

			foreach (CSupplyArea area in _network.Areas)
			{
				FindPorts(area);
			}
			foreach (CSupplyArea area in _network.Areas)
			{
				FindNeighbours(area);
			}
		}

		/// <summary>
		/// Takes a province into an area and then, edge by edge, every neighbour the same
		/// country holds that counts and is in none yet, each with its own neighbours
		/// before the next: the order the original's own walk leaves them in.
		/// </summary>
		private void Flood(CSupplyArea area, int start)
		{
			var walk = new Stack<(int Province, int Edge)>();
			Take(area, start);
			walk.Push((start, 0));
			while (walk.Count > 0)
			{
				(int id, int at) = walk.Pop();
				CProvince province = _state.Province(id);
				if (at >= province.Edges.Count)
				{
					continue;
				}
				walk.Push((id, at + 1));
				CProvince next = _state.Province(province.Edges[at].To);
				if (next.Controller == province.Controller && _network.AreaOf(next.Id) is null && Counts(next))
				{
					Take(area, next.Id);
					walk.Push((next.Id, 0));
				}
			}
		}

		private void Take(CSupplyArea area, int province)
		{
			area.Provinces.Add(province);
			_network.SetArea(province, area);
		}

		private void FindPorts(CSupplyArea area)
		{
			CSupplyArea? home = CapitalArea(area.Controller);
			foreach (int id in area.Provinces)
			{
				CProvince province = _state.Province(id);
				if (!HasWorkingPort(province))
				{
					continue;
				}
				// The largest naval base first, and of two alike the one found first: a new
				// one goes behind the last that is no smaller.
				long level = PortLevel(province);
				int at = area.Ports.Count;
				while (at > 0 && PortLevel(_state.Province(area.Ports[at - 1])) < level)
				{
					at--;
				}
				area.Ports.Insert(at, id);
			}
			area.IsLandlocked = area.Ports.Count == 0;
			area.MayHoldDepot = area.Ports.Count > 0;
			if (!area.MayHoldDepot && ReferenceEquals(home, area) && _industry is not null)
			{
				foreach (int id in area.Provinces)
				{
					if (_state.Province(id).BuildingMaxLevel(_industry) > CFixedPoint.Zero)
					{
						area.MayHoldDepot = true;
						break;
					}
				}
			}
		}

		private void FindNeighbours(CSupplyArea area)
		{
			foreach (int id in area.Provinces)
			{
				foreach (CProvinceEdge edge in _state.Province(id).Edges)
				{
					if (_network.AreaOf(edge.To) is { } other && !ReferenceEquals(other, area)
						&& !area.Neighbours.Contains(other))
					{
						area.Neighbours.Add(other);
					}
				}
			}
		}

		// --- groups ---

		public void MakeGroups()
		{
			foreach (CCountry country in _state.Countries)
			{
				foreach (CSupplyArea area in _areasOf[country.Tag])
				{
					if (area.Group is null)
					{
						var group = new CSupplyGroup();
						Collect(group, area);
						PickDepot(group);
						_network.Groups.Add(group);
					}
				}
			}
		}

		/// <summary>
		/// Puts an area in a group and with it every area beside it that belongs: one held
		/// by a country on the side of whoever holds the group's first area, or one with
		/// no port, away from its holder's capital, whose holder may use that country's
		/// ground.
		/// </summary>
		private void Collect(CSupplyGroup group, CSupplyArea start)
		{
			var waiting = new Stack<(CSupplyArea Area, int Neighbour)>();
			Join(group, start);
			waiting.Push((start, 0));
			while (waiting.Count > 0)
			{
				(CSupplyArea area, int at) = waiting.Pop();
				if (at >= area.Neighbours.Count)
				{
					continue;
				}
				waiting.Push((area, at + 1));
				CSupplyArea next = area.Neighbours[at];
				if (next.Group is null && Belongs(group, next))
				{
					Join(group, next);
					waiting.Push((next, 0));
				}
			}
		}

		private static void Join(CSupplyGroup group, CSupplyArea area)
		{
			area.Group = group;
			group.Areas.Add(area);
		}

		private bool Belongs(CSupplyGroup group, CSupplyArea area)
		{
			if (_state.Country(area.Controller) is not { } holder
				|| _state.Country(group.Areas[0].Controller) is not { } first)
			{
				return false;
			}
			if (CSupplyRelations.IsSameSide(holder, first))
			{
				return true;
			}
			return area.IsLandlocked && !ReferenceEquals(area, CapitalArea(holder.Tag))
				&& CSupplyRelations.CanOperateIn(_state, holder, first);
		}

		/// <summary>The provinces within two edges of one, itself among them, each once.</summary>
		private List<int> Ring(CProvince province)
		{
			var ring = new List<int>();
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (!ring.Contains(edge.To))
				{
					ring.Add(edge.To);
				}
				foreach (CProvinceEdge further in _state.Province(edge.To).Edges)
				{
					if (!ring.Contains(further.To))
					{
						ring.Add(further.To);
					}
				}
			}
			return ring;
		}

		/// <summary>
		/// Gives a group its depot: the province worth most, of all its areas'. Nothing
		/// for a group of one area that may hold no depot on its own.
		/// </summary>
		private void PickDepot(CSupplyGroup group)
		{
			group.Depot = 0;
			if (group.Areas.Count <= 1 && !group.Areas[0].MayHoldDepot)
			{
				return;
			}

			long best = 0;
			foreach (CSupplyArea area in group.Areas)
			{
				if (area.Provinces.Count == 0)
				{
					continue;
				}
				CSupplyArea? home = CapitalArea(_state.Province(area.Provinces[0]).Owner);
				foreach (int id in area.Provinces)
				{
					CProvince province = _state.Province(id);
					long port = PortLevel(province);
					long worth = _mostInfrastructure[id];
					if (!ReferenceEquals(area, home))
					{
						worth += port;
					}

					List<int> ring = Ring(province);
					foreach (int near in ring)
					{
						if (_network.AreaOf(near) is not { } theirs || !ReferenceEquals(theirs.Group, group))
						{
							continue;
						}
						worth += _mostInfrastructure[near];
						if (!ReferenceEquals(theirs, CapitalArea(_state.Province(near).Owner)))
						{
							worth += port;
						}
					}
					worth /= ring.Count + 1;

					if (_state.Country(province.Controller) is { } holder && holder.ActingCapital == id)
					{
						worth += CapitalWorth;
						if (holder.Overlord is null)
						{
							worth += FreeCapitalWorth;
						}
					}
					if (worth > best)
					{
						best = worth;
						group.Depot = id;
					}
				}
			}
		}

		// --- depots ---

		public void GiveDepots()
		{
			foreach (CProvince province in _state.Provinces)
			{
				if (province.Owner is null || _network.AreaOf(province.Id) is not { } area)
				{
					continue;
				}
				CSupplyGroup? group = area.Group;
				if (group is not null && (group.Areas.Count >= 2 || area.MayHoldDepot) && group.Depot != 0)
				{
					_network.SetDepot(province.Id, group.Depot);
				}
				else if (_state.Country(province.Controller) is { } holder)
				{
					_network.SetDepot(province.Id, Find(holder, province, new List<CSupplyArea>(), null));
				}
			}
		}

		/// <summary>
		/// The depot of a province whose group gave it none: the capital where the area is
		/// the capital's, and otherwise whatever an area beside it draws on - a friend's
		/// first where the area has no port, then the master's, the country's own and its
		/// fellow puppets', and a friend's again.
		/// </summary>
		private int Find(CCountry self, CProvince province, List<CSupplyArea> visited, CCountry? origin)
		{
			CSupplyArea? area = _network.AreaOf(province.Id);
			if (area is null || visited.Contains(area) || province.Owner is null)
			{
				return 0;
			}
			visited.Add(area);
			origin ??= self;

			if (province.Controller == self.Tag)
			{
				if (!area.MayHoldDepot)
				{
					foreach (CSupplyArea next in area.Neighbours)
					{
						if (!visited.Contains(next) && _state.Country(next.Controller) is { } theirs
							&& CSupplyRelations.IsFriendly(_state, origin, theirs)
							&& Find(theirs, _state.Province(next.Provinces[0]), visited, theirs) is > 0 and int found)
						{
							return found;
						}
					}
				}

				if (origin.Overlord is { } master)
				{
					foreach (CSupplyArea next in area.Neighbours)
					{
						if (!visited.Contains(next) && next.Controller == master && _state.Country(master) is { } theirs
							&& Find(theirs, _state.Province(next.Provinces[0]), visited, theirs) is > 0 and int found)
						{
							return found;
						}
					}
				}
				if (origin.ActingCapital > 0 && ReferenceEquals(area, _network.AreaOf(origin.ActingCapital)))
				{
					return origin.ActingCapital;
				}

				if (ReferenceEquals(origin, self) && area.Ports.Count > 0 && BestBuilt(area) is > 0 and int built)
				{
					return built;
				}

				foreach (CSupplyArea next in area.Neighbours)
				{
					if (visited.Contains(next) || _state.Country(next.Controller) is not { } theirs)
					{
						continue;
					}
					bool ours = theirs.Tag == origin.Tag || theirs.Overlord == origin.Tag
						|| (theirs.Overlord is not null && theirs.Overlord == origin.Overlord);
					if (ours && Find(theirs, _state.Province(next.Provinces[0]), visited, theirs) is > 0 and int found)
					{
						return found;
					}
				}
				foreach (CSupplyArea next in area.Neighbours)
				{
					if (!visited.Contains(next) && _state.Country(next.Controller) is { } theirs
						&& CSupplyRelations.IsFriendly(_state, origin, theirs)
						&& Find(theirs, _state.Province(next.Provinces[0]), visited, theirs) is > 0 and int found)
					{
						return found;
					}
				}
			}

			return ReferenceEquals(origin, self) && area.Ports.Count > 0 ? area.Ports[0] : 0;
		}

		/// <summary>The province of an area with the most infrastructure in and beside it, within the area.</summary>
		private int BestBuilt(CSupplyArea area)
		{
			long best = 0;
			int at = 0;
			foreach (int id in area.Provinces)
			{
				long worth = _mostInfrastructure[id];
				foreach (CProvinceEdge edge in _state.Province(id).Edges)
				{
					if (ReferenceEquals(_network.AreaOf(edge.To), area))
					{
						worth += _mostInfrastructure[edge.To];
					}
				}
				if (worth > best)
				{
					best = worth;
					at = id;
				}
			}
			return at;
		}

		// --- distances ---

		public void MeasureDistances()
		{
			int count = _state.Provinces.Count;
			for (int id = 0; id < count; id++)
			{
				_network.SetDistance(id, _network.DepotOf(id) != 0 ? CSupplyNetwork.Unreached : 0, 0);
			}
			for (int id = 1; id < count; id++)
			{
				if (_network.DepotOf(id) == id)
				{
					Spread(id);
				}
			}
		}

		/// <summary>
		/// From a depot outwards: a province that draws on it is as far as the nearest
		/// province beside it that does, and its own step further. A province that
		/// turns out nearer is looked at again, so what is left is the least there is.
		/// </summary>
		private void Spread(int depot)
		{
			_network.SetDistance(depot, 0, _network.HopsOf(depot));
			var waiting = new Queue<int>();
			waiting.Enqueue(depot);
			while (waiting.Count > 0)
			{
				int from = waiting.Dequeue();
				int distance = _network.DistanceOf(from);
				int hops = _network.HopsOf(from);
				foreach (CProvinceEdge edge in _state.Province(from).Edges)
				{
					int further = distance + CSupplyFigures.Step(_state.Province(edge.To));
					if (further < _network.DistanceOf(edge.To) && _network.DepotOf(edge.To) == depot)
					{
						_network.SetDistance(edge.To, further, hops + 1);
						waiting.Enqueue(edge.To);
					}
				}
			}
		}
	}
}
