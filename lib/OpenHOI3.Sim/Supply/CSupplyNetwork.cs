using System.Collections.Generic;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// Which depot every province draws on, and how far from it. Worked out whole by
/// <see cref="CSupplyNetworks"/> and not changed afterwards: when a province changes
/// hands a new one is made. See <c>docs/mechanics/supply.md</c>.
/// </summary>
public sealed class CSupplyNetwork
{
	/// <summary>What a province that no depot has reached is far by.</summary>
	public const int Unreached = 100000;

	private readonly CSupplyArea?[] _areaOf;
	private readonly int[] _depot;
	private readonly int[] _distance;
	private readonly int[] _hops;

	public CSupplyNetwork(int provinceCount)
	{
		_areaOf = new CSupplyArea?[provinceCount];
		_depot = new int[provinceCount];
		_distance = new int[provinceCount];
		_hops = new int[provinceCount];
	}

	/// <summary>Every area, country by country.</summary>
	public List<CSupplyArea> Areas { get; } = new();

	/// <summary>Every group, in the order they were started.</summary>
	public List<CSupplyGroup> Groups { get; } = new();

	/// <summary>What the game state's count of changes of control was when this was made.</summary>
	public int ControlChanges { get; init; }

	public int ProvinceCount => _depot.Length;

	/// <summary>The area a province is in, or null: the sea, land nobody holds, and land with too little infrastructure.</summary>
	public CSupplyArea? AreaOf(int province)
	{
		return province >= 0 && province < _areaOf.Length ? _areaOf[province] : null;
	}

	/// <summary>The province a province draws on, or 0 for one that draws on nothing.</summary>
	public int DepotOf(int province)
	{
		return province >= 0 && province < _depot.Length ? _depot[province] : 0;
	}

	/// <summary>A depot is a province that draws on itself.</summary>
	public bool IsDepot(int province)
	{
		return province > 0 && DepotOf(province) == province;
	}

	/// <summary>
	/// How far a province is from its depot: 0 at the depot and for a province with
	/// none, <see cref="Unreached"/> for one whose depot cannot be got to.
	/// </summary>
	public int DistanceOf(int province)
	{
		return province >= 0 && province < _distance.Length ? _distance[province] : 0;
	}

	/// <summary>How many provinces the cheapest way to the depot goes through.</summary>
	public int HopsOf(int province)
	{
		return province >= 0 && province < _hops.Length ? _hops[province] : 0;
	}

	private int[]? _depots;

	/// <summary>Every province that is a depot, in the order of their numbers.</summary>
	public IReadOnlyList<int> Depots
	{
		get
		{
			if (_depots is null)
			{
				var depots = new List<int>();
				for (int province = 1; province < _depot.Length; province++)
				{
					if (_depot[province] == province)
					{
						depots.Add(province);
					}
				}
				_depots = depots.ToArray();
			}
			return _depots;
		}
	}

	/// <summary>How far the province furthest from its depot is.</summary>
	public int MostDistance
	{
		get
		{
			int most = 0;
			foreach (int distance in _distance)
			{
				if (distance > most)
				{
					most = distance;
				}
			}
			return most;
		}
	}

	internal void SetArea(int province, CSupplyArea area)
	{
		_areaOf[province] = area;
	}

	internal void SetDepot(int province, int depot)
	{
		_depot[province] = depot;
	}

	internal void SetDistance(int province, int distance, int hops)
	{
		_distance[province] = distance;
		_hops[province] = hops;
	}
}
