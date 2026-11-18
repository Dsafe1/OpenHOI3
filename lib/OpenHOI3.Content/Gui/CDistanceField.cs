using System;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// A glyph of the original's texture turned into a signed distance field, which can be drawn sharp at any size
/// (<c>docs/mechanics/interface.md</c>, section 6). Ours: nothing of the original's is read here.
///
/// The glyph's box, with a margin of nothing round it, is enlarged <c>supersample</c> times by bilinear sampling - which
/// puts the edge where an antialiased pixel's half-solid point lies - and split there into inside and outside. Each
/// sample's exact Euclidean distance to the other side is taken, half a sample short, the samples of each texel are
/// averaged, and the distance is stored as <c>0.5 + d / range</c>, clamped: a half is the edge, more is inside.
/// </summary>
public static class CDistanceField
{
	private const double Far = 1e20;

	/// <summary>
	/// The field of a glyph's box with <paramref name="padding"/> texels of margin on every side, a row at a time from the
	/// top, each 0 to 1. Only the glyph's own box is read: its neighbours on the page stand in the margin as nothing.
	/// </summary>
	public static float[] Make(CGlyphPage page, CFontGlyph glyph, int padding, int range, int supersample)
	{
		int width = glyph.Width + 2 * padding;
		int height = glyph.Height + 2 * padding;
		int fineWidth = width * supersample;
		int fineHeight = height * supersample;

		var inside = new bool[fineWidth * fineHeight];
		for (int y = 0; y < fineHeight; y++)
		{
			double sourceY = (y + 0.5) / supersample - 0.5;
			for (int x = 0; x < fineWidth; x++)
			{
				double sourceX = (x + 0.5) / supersample - 0.5;
				inside[y * fineWidth + x] = Sample(page, glyph, padding, width, height, sourceX, sourceY) >= 0.5;
			}
		}

		var outside = new bool[inside.Length];
		for (int i = 0; i < inside.Length; i++)
		{
			outside[i] = !inside[i];
		}
		double[] toOutside = SquaredDistances(outside, fineWidth, fineHeight);
		double[] toInside = SquaredDistances(inside, fineWidth, fineHeight);

		var field = new float[width * height];
		double perSample = 1.0 / (supersample * supersample);
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				double sum = 0;
				for (int fy = y * supersample; fy < (y + 1) * supersample; fy++)
				{
					for (int fx = x * supersample; fx < (x + 1) * supersample; fx++)
					{
						int at = fy * fineWidth + fx;
						sum += inside[at] ? Math.Sqrt(toOutside[at]) - 0.5 : -(Math.Sqrt(toInside[at]) - 0.5);
					}
				}
				double distance = sum * perSample / supersample;
				field[y * width + x] = (float)Math.Clamp(0.5 + distance / range, 0.0, 1.0);
			}
		}
		return field;
	}

	/// <summary>
	/// The squared distance from every cell to the nearest set cell, exact (Felzenszwalb and Huttenlocher's transform, a
	/// row at a time and then a column at a time); a very large number where no cell is set.
	/// </summary>
	public static double[] SquaredDistances(bool[] set, int width, int height)
	{
		var grid = new double[width * height];
		for (int i = 0; i < grid.Length; i++)
		{
			grid[i] = set[i] ? 0.0 : Far;
		}
		int longest = Math.Max(width, height);
		var line = new double[longest];
		var result = new double[longest];
		var parabolas = new int[longest];
		var bounds = new double[longest + 1];

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				line[x] = grid[y * width + x];
			}
			Transform(line, width, result, parabolas, bounds);
			for (int x = 0; x < width; x++)
			{
				grid[y * width + x] = result[x];
			}
		}
		for (int x = 0; x < width; x++)
		{
			for (int y = 0; y < height; y++)
			{
				line[y] = grid[y * width + x];
			}
			Transform(line, height, result, parabolas, bounds);
			for (int y = 0; y < height; y++)
			{
				grid[y * width + x] = result[y];
			}
		}
		return grid;
	}

	/// <summary>The one-dimensional transform: the lower envelope of the parabolas standing on each cell's value.</summary>
	private static void Transform(double[] f, int n, double[] d, int[] v, double[] z)
	{
		int k = 0;
		v[0] = 0;
		z[0] = double.NegativeInfinity;
		z[1] = double.PositiveInfinity;
		for (int q = 1; q < n; q++)
		{
			double s = Intersection(f, q, v[k]);
			while (s <= z[k])
			{
				k--;
				s = Intersection(f, q, v[k]);
			}
			k++;
			v[k] = q;
			z[k] = s;
			z[k + 1] = double.PositiveInfinity;
		}
		k = 0;
		for (int q = 0; q < n; q++)
		{
			while (z[k + 1] < q)
			{
				k++;
			}
			double offset = q - v[k];
			d[q] = offset * offset + f[v[k]];
		}
	}

	private static double Intersection(double[] f, int q, int p)
	{
		return (f[q] + (double)q * q - (f[p] + (double)p * p)) / (2.0 * q - 2.0 * p);
	}

	/// <summary>The glyph's coverage, 0 to 1, at a point of the padded box, by bilinear sampling with the edges held.</summary>
	private static double Sample(CGlyphPage page, CFontGlyph glyph, int padding, int width, int height, double x, double y)
	{
		x = Math.Clamp(x, 0.0, width - 1);
		y = Math.Clamp(y, 0.0, height - 1);
		int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
		int x1 = Math.Min(x0 + 1, width - 1), y1 = Math.Min(y0 + 1, height - 1);
		double fx = x - x0, fy = y - y0;
		double top = Cell(page, glyph, padding, x0, y0) * (1 - fx) + Cell(page, glyph, padding, x1, y0) * fx;
		double bottom = Cell(page, glyph, padding, x0, y1) * (1 - fx) + Cell(page, glyph, padding, x1, y1) * fx;
		return (top * (1 - fy) + bottom * fy) / 255.0;
	}

	/// <summary>A texel of the padded box: the glyph's own pixel inside its box, nothing in the margin.</summary>
	private static int Cell(CGlyphPage page, CFontGlyph glyph, int padding, int x, int y)
	{
		int column = x - padding, row = y - padding;
		if (column < 0 || row < 0 || column >= glyph.Width || row >= glyph.Height)
		{
			return 0;
		}
		return page.At(glyph.X + column, glyph.Y + row);
	}
}
