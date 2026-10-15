using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map;

/// <summary>
/// Works out the ways between provinces: which there are and in what order, what kind
/// each is, and how far it is. All of it is the original's, which does this once and
/// keeps the answer in a cache file; here it is done on every start. See
/// <c>docs/mechanics/movement.md</c>, section 2.
/// </summary>
public static class CMapGraph
{
	/// <summary>A river pixel is any byte of the river bitmap under this.</summary>
	private const byte NoRiver = 0xFE;

	/// <summary>What a pixel costs where the terrain bitmap paints nothing the definition knows.</summary>
	private static readonly CFixedPoint UnknownCost = CFixedPoint.Zero;

	/// <summary>
	/// The edges of every province, indexed by province id.
	/// </summary>
	/// <param name="raster"><c>[y * width + x]</c> with y counting north.</param>
	/// <param name="terrain">The terrain bitmap, or null: then every distance is zero.</param>
	/// <param name="rivers">The river bitmap, or null: then no edge is a river from it.</param>
	public static CProvinceEdge[][] Build(ReadOnlySpan<ushort> raster, int width, int height,
		int provinceCount, IReadOnlyList<CExplicitAdjacency> explicitAdjacencies, Func<int, bool> isSea,
		CMapPositions positions, CBitmapFile? terrain, CTerrainReader.Definition terrainDefinition,
		CBitmapFile? rivers)
	{
		List<CProvinceEdge>[] edges = FromRaster(raster, width, height, provinceCount);
		ApplyExplicit(edges, explicitAdjacencies);

		var result = new CProvinceEdge[provinceCount][];
		for (int id = 0; id < provinceCount; id++)
		{
			List<CProvinceEdge> list = edges[id];
			if (terrain is not null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					list[i] = Measure(id, list[i], width, isSea, positions, terrain, terrainDefinition, rivers);
				}
			}
			result[id] = list.ToArray();
		}
		return result;
	}

	/// <summary>
	/// Two provinces are joined when two of their pixels sit side by side or one above
	/// the other, the last column counting as beside the first. The order is the
	/// original's: a province's edges are first those to lower-numbered neighbours,
	/// ascending, and then those to higher-numbered ones in the order its own pixels
	/// meet them - going through the map row by row from the south, and at each pixel
	/// looking east, west, north and south.
	/// </summary>
	private static List<CProvinceEdge>[] FromRaster(ReadOnlySpan<ushort> raster, int width, int height,
		int provinceCount)
	{
		var higher = new List<int>[provinceCount];
		var lower = new List<int>[provinceCount];
		var seen = new HashSet<long>();
		for (int id = 0; id < provinceCount; id++)
		{
			higher[id] = new List<int>();
			lower[id] = new List<int>();
		}

		int lastHere = 0;
		int lastOther = 0;
		void Touch(int here, int other)
		{
			if (other <= here || here == 0 || other >= provinceCount || (here == lastHere && other == lastOther))
			{
				return;
			}
			lastHere = here;
			lastOther = other;
			if (seen.Add(((long)here << 32) | (uint)other))
			{
				higher[here].Add(other);
			}
		}

		for (int y = 0; y < height; y++)
		{
			int row = y * width;
			for (int x = 0; x < width; x++)
			{
				int here = raster[row + x];
				if (here == 0 || here >= provinceCount)
				{
					continue;
				}
				Touch(here, raster[row + (x + 1 == width ? 0 : x + 1)]);
				Touch(here, raster[row + (x == 0 ? width - 1 : x - 1)]);
				if (y + 1 < height)
				{
					Touch(here, raster[row + width + x]);
				}
				if (y > 0)
				{
					Touch(here, raster[row - width + x]);
				}
			}
		}

		for (int id = 1; id < provinceCount; id++)
		{
			foreach (int other in higher[id])
			{
				lower[other].Add(id);                         // filled in ascending order of id
			}
		}

		var edges = new List<CProvinceEdge>[provinceCount];
		for (int id = 0; id < provinceCount; id++)
		{
			var list = new List<CProvinceEdge>(lower[id].Count + higher[id].Count);
			foreach (int other in lower[id])
			{
				list.Add(new CProvinceEdge(other, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
			}
			foreach (int other in higher[id])
			{
				list.Add(new CProvinceEdge(other, EProvinceEdgeKind.Plain, 0, CFixedPoint.Zero));
			}
			edges[id] = list;
		}
		return edges;
	}

	/// <summary>
	/// The adjacency file, a row at a time: where the two already have an edge its kind
	/// and the province it goes through are written over, both ways; where they have
	/// none, one is added each way.
	/// </summary>
	private static void ApplyExplicit(List<CProvinceEdge>[] edges, IReadOnlyList<CExplicitAdjacency> rows)
	{
		foreach (CExplicitAdjacency row in rows)
		{
			EProvinceEdgeKind kind = KindOf(row.Kind);
			if (!Overwrite(edges[row.From], row.To, kind, row.Through))
			{
				edges[row.From].Add(new CProvinceEdge(row.To, kind, row.Through, CFixedPoint.Zero));
				edges[row.To].Add(new CProvinceEdge(row.From, kind, row.Through, CFixedPoint.Zero));
				continue;
			}
			Overwrite(edges[row.To], row.From, kind, row.Through);
		}
	}

	/// <summary>The kind a row's third column names. Anything not listed, <c>canal</c> included, is plain.</summary>
	public static EProvinceEdgeKind KindOf(string type)
	{
		return type switch
		{
			"river" => EProvinceEdgeKind.River,
			"sea" => EProvinceEdgeKind.Crossing,
			"impassable" => EProvinceEdgeKind.Impassable,
			_ => EProvinceEdgeKind.Plain,
		};
	}

	private static bool Overwrite(List<CProvinceEdge> list, int to, EProvinceEdgeKind kind, int through)
	{
		bool found = false;
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].To == to)
			{
				list[i] = list[i] with { Kind = kind, Through = through };
				found = true;
			}
		}
		return found;
	}

	/// <summary>
	/// How far it is along one edge, and whether a river is crossed on the way. A walk
	/// goes from this province's point to the neighbour's a pixel at a time - east or
	/// west and north or south in the same step, the short way round the map - and each
	/// pixel left behind adds the movement cost of the terrain painted on it. Between
	/// land and sea the land end is the province's port and not its unit point.
	/// </summary>
	private static CProvinceEdge Measure(int from, CProvinceEdge edge, int width, Func<int, bool> isSea,
		CMapPositions positions, CBitmapFile terrain, CTerrainReader.Definition definition, CBitmapFile? rivers)
	{
		bool fromSea = isSea(from);
		bool toSea = isSea(edge.To);
		CMapPosition start = !fromSea && toSea ? positions.NavalBase[from] : positions.Unit[from];
		CMapPosition end = fromSea && !toSea ? positions.NavalBase[edge.To] : positions.Unit[edge.To];

		int bearing = Bearing(start, end);
		int x = start.X;
		int y = start.Y;
		int across = x - end.X;
		long distance = 0;
		EProvinceEdgeKind kind = edge.Kind;

		while (x != end.X || y != end.Y)
		{
			int category = CTerrainReader.CategoryAt(terrain, definition, x, y);
			distance += (category < 0 ? UnknownCost : definition.Categories[category].MovementCost).Raw;
			if (kind == EProvinceEdgeKind.Plain && rivers is not null
				&& x >= 0 && y >= 0 && x < rivers.Width && y < rivers.Height
				&& rivers.StoredRow(rivers.Height - 1 - y)[x] < NoRiver)
			{
				kind = EProvinceEdgeKind.River;
			}

			if (Math.Abs(across) > width / 2)
			{
				// The other way round the map is shorter: step away, and wrap.
				if (end.X < x)
				{
					x++;
					across++;
				}
				else if (x < end.X)
				{
					x--;
					across--;
				}
				if (x < 0)
				{
					x += width;
					across += width;
				}
				if (x >= width)
				{
					x -= width;
					across -= width;
				}
			}
			else if (x < end.X)
			{
				x++;
				across++;
			}
			else if (x > end.X)
			{
				x--;
				across--;
			}

			if (y < end.Y)
			{
				y++;
			}
			else if (y > end.Y)
			{
				y--;
			}
		}
		return edge with { Kind = kind, Distance = new CFixedPoint(distance), Bearing = bearing };
	}

	/// <summary>
	/// Which way one point lies from another, in thousandths of a turn, as the original works it out in single precision
	/// from the two points the distance is walked between: the angle of the rise against the step east turned about, a
	/// half turn added, and a thousandth's half, and floored - so due west is 1000. Nothing is wrapped round the map, so
	/// an edge across the seam points the long way round (<c>docs/mechanics/weather.md</c>, section 3).
	/// </summary>
	public static int Bearing(CMapPosition from, CMapPosition to)
	{
		float y = from.Y - to.Y;
		float x = to.X - from.X;
		float angle = (float)Math.Atan2(y, x);
		float turn = (float)(angle / 6.2831854820251465);
		float half = (float)(turn + 0.5);
		float thousandths = (float)((float)(half + 0.0005000000237487257) * 1000.0);
		return (int)MathF.Floor(thousandths);
	}
}
