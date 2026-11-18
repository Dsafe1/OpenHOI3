using System.Collections.Generic;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// How far it is by sea from any port or sea province to any other: what a fleet's range
/// is set against. The original works the whole table out when it builds its map cache;
/// here a row is worked out the first time it is asked for. See
/// <c>docs/mechanics/fleetsandair.md</c>, section 3:
///
///     the places are the ports and the sea provinces that are not lakes;
///     two of them are joined where their provinces have an edge;
///     a step is as long as the straight line between the two provinces' centres, in
///     quarters of a pixel;
///     the distance is the shortest sum of steps, kept in sixteen bits - and nothing
///     where there is no way at all.
/// </summary>
public sealed class CNavalDistances
{
	private readonly IReadOnlyList<CProvince> _provinces;
	private readonly int _mapWidth;
	private readonly int[] _provinceOfIndex;
	private readonly ushort[]?[] _rows;

	public CNavalDistances(IReadOnlyList<CProvince> provinces, int mapWidth)
	{
		_provinces = provinces;
		_mapWidth = mapWidth;
		int count = 0;
		foreach (CProvince province in provinces)
		{
			if (province.SeaIndex >= count)
			{
				count = province.SeaIndex + 1;
			}
		}
		_provinceOfIndex = new int[count];
		foreach (CProvince province in provinces)
		{
			if (province.SeaIndex >= 0)
			{
				_provinceOfIndex[province.SeaIndex] = province.Id;
			}
		}
		_rows = new ushort[count][];
	}

	/// <summary>How many ports and sea provinces the table has.</summary>
	public int Count => _provinceOfIndex.Length;

	/// <summary>The province a number of the table stands for.</summary>
	public int ProvinceOf(int index)
	{
		return _provinceOfIndex[index];
	}

	/// <summary>
	/// The number a province is looked up by: its own, or for a land province with none
	/// that of the sea province its port opens onto. -1 where it has neither.
	/// </summary>
	public int IndexOf(CProvince province)
	{
		if (province.SeaIndex >= 0)
		{
			return province.SeaIndex;
		}
		return province.PortSea > 0 && province.PortSea < _provinces.Count
			? _provinces[province.PortSea].SeaIndex
			: -1;
	}

	/// <summary>
	/// How far it is by sea between two provinces, in the units a range is given in:
	/// quarters of a pixel. Nothing where either is no place of the table, or there is
	/// no way between them.
	/// </summary>
	public long Between(CProvince a, CProvince b)
	{
		int from = IndexOf(a);
		int to = IndexOf(b);
		if (from < 0 || to < 0)
		{
			return 0;
		}
		return Row(from)[to];
	}

	/// <summary>The distances from one place of the table to every place of it.</summary>
	public ushort[] Row(int index)
	{
		return _rows[index] ??= Measure(index);
	}

	/// <summary>
	/// The steps between the places, made once: for each place, where its steps begin in <see cref="Steps.To"/> and
	/// <see cref="Steps.Length"/>, in the order of its province's edges. The map does not change in a game, so neither do
	/// they; a row is a walk over them alone.
	/// </summary>
	private sealed record Steps(int[] First, int[] To, long[] Length);

	private Steps? _steps;

	private Steps MakeSteps()
	{
		int count = _provinceOfIndex.Length;
		var first = new int[count + 1];
		var to = new List<int>();
		var length = new List<long>();
		for (int index = 0; index < count; index++)
		{
			first[index] = to.Count;
			CProvince here = _provinces[_provinceOfIndex[index]];
			foreach (CProvinceEdge edge in here.Edges)
			{
				if (edge.To <= 0 || edge.To >= _provinces.Count)
				{
					continue;
				}
				CProvince there = _provinces[edge.To];
				if (there.SeaIndex < 0)
				{
					continue;
				}
				to.Add(there.SeaIndex);
				length.Add(CMapDistance.Between(here, there, _mapWidth));
			}
		}
		first[count] = to.Count;
		return new Steps(first, to.ToArray(), length.ToArray());
	}

	private ushort[] Measure(int from)
	{
		// Made by whichever thread asks first; two making them at once make the same.
		Steps steps = _steps ??= MakeSteps();
		int count = _provinceOfIndex.Length;
		var distance = new long[count];
		System.Array.Fill(distance, long.MaxValue);
		distance[from] = 0;
		var open = new PriorityQueue<int, long>();
		open.Enqueue(from, 0);
		while (open.TryDequeue(out int current, out long reached))
		{
			if (reached > distance[current])
			{
				continue;
			}
			for (int k = steps.First[current]; k < steps.First[current + 1]; k++)
			{
				int there = steps.To[k];
				long further = reached + steps.Length[k];
				if (further < distance[there])
				{
					distance[there] = further;
					open.Enqueue(there, further);
				}
			}
		}

		var row = new ushort[count];
		for (int i = 0; i < count; i++)
		{
			// Sixteen bits, as the original keeps them; no way at all reads as nothing.
			row[i] = i == from || distance[i] == long.MaxValue ? (ushort)0 : unchecked((ushort)distance[i]);
		}
		return row;
	}
}
