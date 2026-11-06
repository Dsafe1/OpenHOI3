using System;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The land borders' junctions made again for who controls what, as the original does at every change of a controller
/// (<c>docs/mechanics/mapdraw.md</c>, section 4; <c>reversing/findings/FINDINGS-mapdraw.md</c>, B.7): three passes over the
/// active corners, by rows from the south and then from the west - the country byte from the controllers of the pixel's
/// four neighbours, the diagonal cuts the map cache flags, and the bits spread to the neighbours. Each pass reads the
/// bytes as they stand, and what a pass does not reset carries over to the next time, as the original's does.
/// </summary>
public static class CBorderCorners
{
	/// <param name="province">The province at a pixel, x a column and y counted from the south; 0 for none.</param>
	/// <param name="land">Whether a province is land.</param>
	/// <param name="controller">A province's controller, any number standing for a country, 0 for none.</param>
	public static void Refresh(CBorderCache cache, int width, int height, Func<int, int, int> province, Func<int, bool> land,
		Func<int, int> controller)
	{
		int At(int x, int y) => x >= 0 && y >= 0 && x < width && y < height ? province(x, y) : 0;
		bool Valid(int id) => id > 0 && land(id);
		var visited = new System.Collections.Generic.List<CBorderCorner>(Visited(cache, width, height));

		// 1. The country byte: a bit for each neighbour held by another, where the map cache marks no cut that way.
		foreach (CBorderCorner c in visited)
		{
			int x = c.X, y = c.Y;
			int here = At(x, y), right = At(x + 1, y), up = At(x, y + 1);
			int left = x >= 2 ? At(x - 1, y) : 0;
			int down = y >= 2 ? At(x, y - 1) : 0;
			int missing = (here == 0 ? 1 : 0) + (right == 0 ? 1 : 0) + (left == 0 ? 1 : 0) + (up == 0 ? 1 : 0) + (down == 0 ? 1 : 0);
			if (missing >= 3)
			{
				continue;
			}
			byte bits = 0;
			if (Valid(here))
			{
				int held = controller(here);
				if (Valid(right) && controller(right) != held && ((c.Right?.Cut ?? 0) & 0x0E) == 0)
				{
					bits |= 1;
				}
				if (Valid(left) && controller(left) != held && ((c.Left?.Cut ?? 0) & 0x0B) == 0)
				{
					bits |= 2;
				}
				if (Valid(up) && controller(up) != held && ((c.Up?.Cut ?? 0) & 0x0A) == 0)
				{
					bits |= 4;
				}
				if (Valid(down) && controller(down) != held && ((c.Down?.Cut ?? 0) & 0x0F) == 0)
				{
					bits |= 8;
				}
			}
			c.Country = bits;
		}

		// 2. The diagonal cuts.
		foreach (CBorderCorner c in visited)
		{
			if (c.Country == 0)
			{
				c.Diagonal = (byte)(c.Diagonal & ~8);
				continue;
			}
			int cut = c.Cut;
			if (c.Diagonal == 0 || (cut & 0xF) == 0)
			{
				continue;
			}
			byte n = c.Up?.Country ?? 0, s = c.Down?.Country ?? 0;
			byte l = c.Left?.Country ?? 0, r = c.Right?.Country ?? 0;
			if ((c.Country & 5) != 0 && (cut & 1) != 0)
			{
				c.Diagonal |= 8;
				Clear(c, (n & 8) == 0, 4);
				Clear(c, (r & 2) == 0, 1);
			}
			if ((c.Country & 0xA) != 0 && (cut & 2) != 0)
			{
				c.Diagonal |= 8;
				Clear(c, (s & 4) == 0, 8);
				Clear(c, (l & 1) == 0, 2);
			}
			if ((c.Country & 9) != 0 && (cut & 8) != 0)
			{
				c.Diagonal |= 8;
				Clear(c, (s & 4) == 0, 8);
				Clear(c, (r & 2) == 0, 1);
			}
			if ((c.Country & 6) != 0 && (cut & 4) != 0)
			{
				c.Diagonal |= 8;
				Clear(c, (n & 8) == 0, 4);
				Clear(c, (l & 1) == 0, 2);
			}
		}

		// 3. The spread to the neighbours, and the diagonals that follow a neighbour's whole byte.
		foreach (CBorderCorner c in visited)
		{
			CBorderCorner? left = c.Left, right = c.Right;
			CBorderCorner? up = c.Up, down = c.Down;
			int type = c.Diagonal & 7;
			switch (type)
			{
				case 1 or 2:
					if ((c.Country & 2) != 0 && left is not null)
					{
						left.Country |= 1;
					}
					if ((c.Country & 1) != 0 && right is not null)
					{
						right.Country |= 2;
					}
					if ((c.Country & 4) != 0 && up is not null)
					{
						up.Country |= 8;
					}
					if ((c.Country & 8) != 0 && down is not null)
					{
						down.Country |= 4;
					}
					break;
				case 3:
					SetDiagonal(c, left?.Diagonal == 9 || up?.Diagonal == 9);
					break;
				case 4:
					SetDiagonal(c, left?.Diagonal == 10 || down?.Diagonal == 10);
					break;
				case 5:
					SetDiagonal(c, right?.Diagonal == 9 || down?.Diagonal == 9);
					break;
				case 6:
					SetDiagonal(c, right?.Diagonal == 10 || up?.Diagonal == 10);
					break;
			}
		}
	}

	/// <summary>The active corners a pass visits: all but the map's last column and row of them [exe].</summary>
	private static System.Collections.Generic.IEnumerable<CBorderCorner> Visited(CBorderCache cache, int width, int height)
	{
		foreach (CBorderCorner corner in cache.Corners)
		{
			if (corner.Active && corner.X <= width - 2 && corner.Y <= height - 2)
			{
				yield return corner;
			}
		}
	}

	private static void Clear(CBorderCorner corner, bool when, int bit)
	{
		if (when)
		{
			corner.Country = (byte)(corner.Country & ~bit);
		}
	}

	private static void SetDiagonal(CBorderCorner corner, bool on)
	{
		corner.Diagonal = on ? (byte)(corner.Diagonal | 8) : (byte)(corner.Diagonal & ~8);
	}
}
