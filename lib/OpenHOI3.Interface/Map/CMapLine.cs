using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A straight line over the map's pixels for the country names (<c>docs/mechanics/mapnames.md</c>, section 3): rasterised
/// by Bresenham's walk and trimmed to the run of it that lies on a country's land.
/// </summary>
public static class CMapLine
{
	/// <summary>The most points a line is given [exe].</summary>
	public const int MostPoints = 256000;

	/// <summary>The pixels from one end to the other, both ends included.</summary>
	public static List<(int X, int Y)> Rasterise(int x0, int y0, int x1, int y1)
	{
		var points = new List<(int, int)>();
		int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
		int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
		int error = dx + dy;
		while (points.Count < MostPoints)
		{
			points.Add((x0, y0));
			if (x0 == x1 && y0 == y1)
			{
				break;
			}
			int twice = 2 * error;
			if (twice >= dy)
			{
				error += dy;
				x0 += sx;
			}
			if (twice <= dx)
			{
				error += dx;
				y0 += sy;
			}
		}
		return points;
	}

	/// <summary>
	/// The run of a line that a name may lie along, as the indices of its first and last point, or null: the line cut to
	/// its first and last pixels of the country's piece; none where a point of it is off the map; then the longest run of
	/// pixels of the piece, land nobody controls, or - where the sea is allowed - sea, the first of equal runs winning.
	/// Any other land breaks a run.
	/// </summary>
	public static (int First, int Last)? Trim(IReadOnlyList<(int X, int Y)> points, CMapNameWorld world, ISet<int> piece,
		bool seaAllowed)
	{
		int first = -1, last = -1;
		for (int i = 0; i < points.Count; i++)
		{
			if (OnMap(world, points[i]) && piece.Contains(world.ProvinceAt(points[i].X, points[i].Y)))
			{
				if (first < 0)
				{
					first = i;
				}
				last = i;
			}
		}
		if (first < 0)
		{
			return null;
		}
		for (int i = first; i <= last; i++)
		{
			if (!OnMap(world, points[i]))
			{
				return null;
			}
		}
		(int First, int Last)? best = null;
		int start = -1;
		for (int i = first; i <= last + 1; i++)
		{
			bool good = i <= last && Allowed(world, world.ProvinceAt(points[i].X, points[i].Y), piece, seaAllowed);
			if (good && start < 0)
			{
				start = i;
			}
			else if (!good && start >= 0)
			{
				if (best is null || i - 1 - start > best.Value.Last - best.Value.First)
				{
					best = (start, i - 1);
				}
				start = -1;
			}
		}
		return best;
	}

	private static bool OnMap(CMapNameWorld world, (int X, int Y) point)
	{
		return point.X >= 0 && point.Y >= 0 && point.X < world.Width && point.Y < world.Height;
	}

	private static bool Allowed(CMapNameWorld world, int province, ISet<int> piece, bool seaAllowed)
	{
		if (province <= 0)
		{
			return false;
		}
		if (!world.IsLand(province))
		{
			return seaAllowed;
		}
		return piece.Contains(province) || world.Controller(province) == 0;
	}
}
