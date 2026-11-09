using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The black dashed line of a headquarters under an AI: a walk along the foreign land beside its plan's area, the chains joined, each
/// point half way between two provinces' centres, softened and made a flat strip. Every constant is the original's. See
/// <c>docs/mechanics/mapunits.md</c>, section 11.
/// </summary>
public static class CFrontLines
{
	private const float HalfWidth = 3f;

	/// <summary>One line's vertices, each a corner on the map and its place along and across the line.</summary>
	public sealed record CStrip(Vector2[] Positions, Vector2[] Uvs, ushort[] Indices);

	private sealed class CChain
	{
		public List<int> Provinces { get; } = new();

		public List<int> Starts { get; } = new();

		public List<int> Neighbours { get; } = new();

		public void Reverse()
		{
			Provinces.Reverse();
			Neighbours.Reverse();
			Starts.Reverse();
			for (int index = 0; index < Starts.Count; index++)
			{
				Starts[index] = Neighbours.Count - Starts[index] + 1;
			}
		}

		public void Append(CChain other)
		{
			int shift = Neighbours.Count;
			Provinces.AddRange(other.Provinces);
			Starts.AddRange(other.Starts.Skip(1).Select(start => start + shift));
			Neighbours.AddRange(other.Neighbours);
		}
	}

	/// <summary>Every line of a unit's plan, as strips; none where its area has no foreign land beside it.</summary>
	public static List<CStrip> Build(CGameState state, CUnit unit, Func<int, Vector2> centre)
	{
		return Chains(state, unit).Select(chain => Points(chain, centre)).Where(points => points.Count >= 2)
			.Select(points => Strip(CutCorners(Relax(points)))).ToList();
	}

	/// <summary>Whether a province beside the area counts: land held by neither the unit's owner nor anyone allied, fighting beside or tied to it.</summary>
	private static bool IsFront(CGameState state, CCountry owner, CProvince province)
	{
		if (province.IsSea || province.Controller == owner.Tag)
		{
			return false;
		}
		if (province.Controller is not { } holder)
		{
			return true;
		}
		if (state.Wars.Any(war => (war.Attackers.Contains(holder) && war.Attackers.Contains(owner.Tag)) || (war.Defenders.Contains(holder) && war.Defenders.Contains(owner.Tag)))
			|| CCountryLists.HasAlliance(state, owner.Tag, holder))
		{
			return false;
		}
		return owner.Overlord != holder && state.Country(holder)?.Overlord != owner.Tag;
	}

	private static bool HasEdge(CProvince from, int to) => Array.Exists(from.Edges, edge => edge.To == to);

	private static List<CChain> Chains(CGameState state, CUnit unit)
	{
		var chains = new List<CChain>();
		if (state.Country(unit.Owner) is not { } owner)
		{
			return chains;
		}
		List<CProvince> area = unit.Plan.Area.Select(state.Province).ToList();
		bool Front(int province) => IsFront(state, owner, state.Province(province));
		int[] open = area.Select(province => province.Edges.Count(edge => Front(edge.To))).ToArray();
		bool[][] visited = area.Select(province => new bool[province.Edges.Length]).ToArray();
		var recorded = new int[area.Count];
		for (int pass = 1; ; pass++)
		{
			var chain = new CChain();
			if (!Walk(state, area, open, visited, recorded, pass, chain, Front))
			{
				break;
			}
			chains.Add(chain);
		}
		while (MergePass(state, chains))
		{
		}
		return chains;
	}

	/// <summary>One chain, as far as it runs; false where it found no point.</summary>
	private static bool Walk(CGameState state, List<CProvince> area, int[] open, bool[][] visited, int[] recorded, int pass, CChain chain, Func<int, bool> front)
	{
		bool anything = false;
		CProvince? previous = null;
		int last = -1;
		int at = Array.FindIndex(open, count => count > 0);
		for (int index = at < 0 ? area.Count : at; index < area.Count; index++)
		{
			CProvince province = area[index];
			if (previous is not null && !HasEdge(province, previous.Id))
			{
				continue;
			}
			if (last >= 0)
			{
				int back = Array.FindIndex(province.Edges, edge => edge.To == last);
				if (back < 0 || visited[index][back])
				{
					break;
				}
				visited[index][back] = true;
				open[index]--;
				anything = true;
			}
			int start = chain.Neighbours.Count;
			bool took;
			do
			{
				took = false;
				for (int edge = 0; edge < province.Edges.Length; edge++)
				{
					int neighbour = province.Edges[edge].To;
					if (!visited[index][edge] && open[index] > 0)
					{
						if (front(neighbour) && (last == -1 || HasEdge(state.Province(neighbour), last)))
						{
							visited[index][edge] = true;
							open[index]--;
							last = neighbour;
							chain.Neighbours.Add(neighbour);
							anything = true;
							took = true;
						}
					}
					else if (recorded[index] != pass)
					{
						recorded[index] = pass;
						chain.Provinces.Add(province.Id);
						chain.Starts.Add(start);
					}
				}
			}
			while (took);
			previous = province;
		}
		chain.Starts.Add(chain.Neighbours.Count);
		chain.Starts[0] = 1;
		return anything;
	}

	/// <summary>One pass over every ordered pair, joining what meets; whether any chain went.</summary>
	private static bool MergePass(CGameState state, List<CChain> chains)
	{
		bool Beside(int a, int b) => HasEdge(state.Province(a), b);
		foreach (CChain r in chains)
		{
			foreach (CChain s in chains)
			{
				if (ReferenceEquals(r, s) || r.Provinces.Count == 0 || s.Provinces.Count == 0)
				{
					continue;
				}
				if (r.Provinces[^1] == s.Provinces[0] && r.Neighbours.Count > 0 && s.Neighbours.Count > 0 && Beside(r.Neighbours[^1], s.Neighbours[0]))
				{
					JoinOnProvince(r, s);
				}
				else if (r.Provinces[0] == s.Provinces[0] && r.Neighbours.Count > 0 && s.Neighbours.Count > 0 && Beside(r.Neighbours[0], s.Neighbours[0]))
				{
					r.Reverse();
					JoinOnProvince(r, s);
				}
				else if (r.Provinces[^1] == s.Provinces[^1] && r.Neighbours.Count > 0 && s.Neighbours.Count > 0 && Beside(r.Neighbours[^1], s.Neighbours[^1]))
				{
					s.Reverse();
					JoinOnProvince(r, s);
				}
				else if (r.Provinces[0] == s.Provinces[^1] && r.Neighbours.Count > 0 && s.Neighbours.Count > 0 && Beside(r.Neighbours[0], s.Neighbours[^1]))
				{
					r.Reverse();
					s.Reverse();
					JoinOnProvince(r, s);
				}
				else if (r.Neighbours.Count > 0 && s.Neighbours.Count > 0)
				{
					if (Beside(r.Provinces[^1], s.Provinces[0]) && r.Neighbours[^1] == s.Neighbours[0])
					{
						JoinOnNeighbour(r, s);
					}
					else if (Beside(r.Provinces[^1], s.Provinces[^1]) && r.Neighbours[^1] == s.Neighbours[^1])
					{
						s.Reverse();
						JoinOnNeighbour(r, s);
					}
					else if (Beside(r.Provinces[0], s.Provinces[0]) && r.Neighbours[0] == s.Neighbours[0])
					{
						r.Reverse();
						JoinOnNeighbour(r, s);
					}
					else if (Beside(r.Provinces[0], s.Provinces[^1]) && r.Neighbours[0] == s.Neighbours[^1])
					{
						r.Reverse();
						s.Reverse();
						JoinOnNeighbour(r, s);
					}
				}
			}
		}
		return chains.RemoveAll(chain => chain.Provinces.Count == 0) > 0;
	}

	private static void JoinOnProvince(CChain r, CChain s)
	{
		r.Provinces.RemoveAt(r.Provinces.Count - 1);
		r.Starts.RemoveAt(r.Starts.Count - 1);
		r.Append(s);
		s.Provinces.Clear();
	}

	private static void JoinOnNeighbour(CChain r, CChain s)
	{
		r.Neighbours.RemoveAt(r.Neighbours.Count - 1);
		r.Append(s);
		s.Provinces.Clear();
	}

	private static Vector2 Halfway(Vector2 a, Vector2 b) => new(a.X * 0.5f + b.X * 0.5f, a.Y * 0.5f + b.Y * 0.5f);

	private static List<Vector2> Points(CChain chain, Func<int, Vector2> centre)
	{
		var points = new List<Vector2>();
		for (int place = 0; place + 1 < chain.Starts.Count && place < chain.Provinces.Count; place++)
		{
			Vector2 own = centre(chain.Provinces[place]);
			for (int k = chain.Starts[place] - 1; k <= chain.Starts[place + 1] - 1 && k < chain.Neighbours.Count; k++)
			{
				if (k >= 0)
				{
					points.Add(Halfway(own, centre(chain.Neighbours[k])));
				}
			}
		}
		return points;
	}

	private static Vector2 Unit(Vector2 v)
	{
		float length = v.Length();
		return length > 0f ? v / length : Vector2.Zero;
	}

	/// <summary>Two passes, the corners eased by the change of direction there, the two points at each end left alone.</summary>
	public static List<Vector2> Relax(List<Vector2> points)
	{
		int count = points.Count;
		if (count <= 4)
		{
			return points;
		}
		foreach (float factor in new[] { 1f, 0.5f })
		{
			var directions = new Vector2[count - 1];
			for (int i = 0; i < count - 1; i++)
			{
				directions[i] = Unit(points[i + 1] - points[i]);
			}
			for (int i = 2; i <= count - 3; i++)
			{
				points[i] += (directions[i] - directions[i - 1]) * factor;
			}
		}
		return points;
	}

	/// <summary>One round of corner cutting: every point but the ends becomes two, a quarter of the way along each side of it.</summary>
	public static List<Vector2> CutCorners(List<Vector2> points)
	{
		var cut = new List<Vector2>(2 * points.Count - 2) { points[0] };
		for (int i = 1; i + 1 < points.Count; i++)
		{
			cut.Add(points[i - 1] * 0.25f + points[i] * 0.75f);
			cut.Add(points[i] * 0.75f + points[i + 1] * 0.25f);
		}
		cut.Add(points[^1]);
		return cut;
	}

	/// <summary>A strip six wide: two vertices a point, across by the line's turning and along by the distance so far.</summary>
	public static CStrip Strip(List<Vector2> points)
	{
		int count = points.Count;
		var positions = new Vector2[count * 2];
		var uvs = new Vector2[count * 2];
		var indices = new ushort[(count - 1) * 6];
		float along = 0f;
		Vector2 Normal(int segment)
		{
			Vector2 side = points[segment + 1] - points[segment];
			return Unit(new Vector2(-side.Y, side.X));
		}
		for (int i = 0; i < count; i++)
		{
			Vector2 miter = i == count - 1 ? Normal(count - 2) : Unit(Normal(Math.Max(i - 1, 0)) + Normal(i));
			positions[2 * i] = points[i] + miter * HalfWidth;
			uvs[2 * i] = new Vector2(along, 0f);
			positions[2 * i + 1] = points[i] - miter * HalfWidth;
			uvs[2 * i + 1] = new Vector2(along, 1f);
			if (i < count - 1)
			{
				along += (points[i + 1] - points[i]).Length();
				int at = 2 * i;
				int index = 6 * i;
				indices[index] = (ushort)at;
				indices[index + 1] = (ushort)(at + 1);
				indices[index + 2] = (ushort)(at + 2);
				indices[index + 3] = (ushort)(at + 1);
				indices[index + 4] = (ushort)(at + 3);
				indices[index + 5] = (ushort)(at + 2);
			}
		}
		return new CStrip(positions, uvs, indices);
	}
}
