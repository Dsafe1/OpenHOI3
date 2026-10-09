using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Movement;

/// <summary>
/// Finds a unit's way from one province to another, as the original does - which is
/// not quite the shortest way. See <c>docs/mechanics/movement.md</c>, section 3:
///
///     the provinces to look at next are kept in order of what each has cost so far
///     plus the straight line still to go, times 1.25;
///     a province is settled the first time a step reaches it, and a cheaper way to it
///     found later does not replace the first;
///     a province's neighbours are tried in the order of its edges.
/// </summary>
public static class CRouteFinder
{
	/// <summary>What the straight line still to go is multiplied by, in thousandths.</summary>
	private const long StraightLineWeight = 1250;

	/// <summary>
	/// The provinces to go through, the first step first and the goal last, or null
	/// when there is no way. Empty when the unit is to go where it starts.
	/// </summary>
	public static List<int>? Find(CGameState state, CCountry country, CUnit unit, int from, int to)
	{
		int count = state.Provinces.Count;
		if (from <= 0 || to <= 0 || from >= count || to >= count)
		{
			return null;
		}
		CProvince goal = state.Province(to);
		if (!CMovementRules.MayStandIn(state, country, unit, goal))
		{
			return null;
		}

		var settled = new bool[count];
		var cost = new long[count];
		var estimate = new long[count];
		var cameFrom = new int[count];
		var next = new int[count];
		System.Array.Fill(next, -1);
		System.Array.Fill(cameFrom, -1);

		settled[from] = true;
		int head = from;
		while (head != -1)
		{
			int current = head;
			head = next[current];
			if (current == to)
			{
				return PathTo(cameFrom, from, to);
			}

			CProvince here = state.Province(current);
			foreach (CProvinceEdge edge in here.Edges)
			{
				if (edge.To <= 0 || edge.To >= count || settled[edge.To])
				{
					continue;
				}
				CProvince there = state.Province(edge.To);
				if (!CMovementRules.MayStep(state, country, unit, here, there, edge))
				{
					continue;
				}
				settled[edge.To] = true;

				long step = StepCost(state, country, there, edge);
				cost[edge.To] = cost[current] + step;
				estimate[edge.To] = cost[edge.To] + StraightLine(state, there, goal) * StraightLineWeight;
				cameFrom[edge.To] = current;

				// Before the first that is not cheaper, so the newest of equals is taken first.
				int before = -1;
				int after = head;
				while (after != -1 && estimate[edge.To] > estimate[after])
				{
					before = after;
					after = next[after];
				}
				next[edge.To] = after;
				if (before == -1)
				{
					head = edge.To;
				}
				else
				{
					next[before] = edge.To;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// What a step costs the search: the edge's distance - a tenth more into ground an
	/// enemy holds. The original doubles it instead where it knows of enemy units there.
	/// </summary>
	public static long StepCost(CGameState state, CCountry country, CProvince to, CProvinceEdge edge)
	{
		long distance = edge.Distance.Raw;
		return CMovementRules.IsEnemy(state, country.Tag, to.Controller) ? distance * 1100 / 1000 : distance;
	}

	/// <summary>
	/// The whole pixels between the centres of two provinces, the short way round the
	/// map, rounded down.
	/// </summary>
	public static long StraightLine(CGameState state, CProvince a, CProvince b)
	{
		long across = b.CentreX - a.CentreX;
		long up = b.CentreY - a.CentreY;
		int width = state.MapWidth;
		if (width > 0)
		{
			if (across < -(width / 2))
			{
				across += width;
			}
			if (across > width / 2)
			{
				across -= width;
			}
		}
		return WholeRoot(across * across + up * up);
	}

	/// <summary>The square root of a whole number, rounded down, with no fractions on the way.</summary>
	public static long WholeRoot(long value)
	{
		if (value <= 1)
		{
			return value < 0 ? 0 : value;
		}
		long root = value / 2;
		while (true)
		{
			long better = (value / root + root) / 2;
			if (better >= root)
			{
				return root;
			}
			root = better;
		}
	}

	private static List<int> PathTo(int[] cameFrom, int from, int to)
	{
		var path = new List<int>();
		for (int at = to; at != from && at != -1; at = cameFrom[at])
		{
			path.Add(at);
		}
		path.Reverse();
		return path;
	}
}
