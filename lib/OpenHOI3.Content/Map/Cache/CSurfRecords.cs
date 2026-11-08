using System;
using System.Collections.Generic;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// A land pixel's records of the surf: at each water pixel beside it one or two records of a kind that says how the coast lies there,
/// each with the four offsets that kind always has. See <c>docs/mechanics/mapcache.md</c>, section 9.
/// </summary>
public static class CSurfRecords
{
	/// <summary>The kinds' four offsets [*measured* from the caches, the same in both maps].</summary>
	private static readonly (float A, float B, float C, float D)[] Offsets = MakeOffsets();

	private static (float, float, float, float)[] MakeOffsets()
	{
		const float H = 0.5f;
		var offsets = new (float, float, float, float)[52];
		(float, float, float, float)[] first =
		{
			(0, -H, 0, -H), (0, -H, 0, -H), (-H, -H, H, -H), (-H, -H, 0, -H), (0, -H, H, -H), (0, -H, 0, -H), (0, -H, 0, -H),
			(0, H, 0, H), (0, H, 0, H), (-H, H, H, H), (-H, H, 0, H), (0, H, H, H), (0, H, 0, H), (0, H, 0, H),
			(H, 0, H, 0), (H, 0, H, 0), (H, H, H, -H), (H, H, H, 0), (H, 0, H, -H), (H, 0, H, 0), (H, 0, H, 0),
			(-H, 0, -H, 0), (-H, 0, -H, 0), (-H, H, -H, -H), (-H, H, -H, 0), (-H, 0, -H, -H), (-H, 0, -H, 0), (-H, 0, -H, 0),
		};
		first.CopyTo(offsets, 0);
		foreach ((int kind, float x, float y) in new[] { (28, H, -H), (34, -H, -H), (40, H, H), (46, -H, H) })
		{
			for (int i = 0; i < 6; i++)
			{
				offsets[kind + i] = (x, y, x, y);
			}
		}
		return offsets;
	}

	/// <summary>A kind's four offsets, which the file holds beside each record.</summary>
	public static (float A, float B, float C, float D) OffsetsOf(int kind) => Offsets[kind];

	/// <summary>Adds a land pixel's records to a tile's: the water pixel and the kind.</summary>
	public static void Add(CMapCacheSource source, int x, int y, List<CWavePixel> records)
	{
		if (x <= 0 || x >= source.Width - 1 || y <= 0 || y >= source.Height - 1)
		{
			return;
		}
		bool W(int a, int b) => source.IsWaterAt(a, b);
		void Emit(int px, int py, int kind)
		{
			records.Add(new CWavePixel(px, py, kind));
		}
		Side(W(x, y + 1), x, y + 1, W(x - 1, y + 1), W(x + 1, y + 1), W(x - 1, y), W(x + 1, y), 9, 11, 10, 7, 8, 12, 13, Emit);
		Side(W(x, y - 1), x, y - 1, W(x - 1, y - 1), W(x + 1, y - 1), W(x - 1, y), W(x + 1, y), 2, 4, 3, 0, 1, 5, 6, Emit);
		Column(W(x + 1, y), x + 1, y, W(x + 1, y + 1), W(x + 1, y - 1), W(x, y + 1), W(x, y - 1), 16, 17, 18, 14, 15, 20, 19, Emit);
		Column(W(x - 1, y), x - 1, y, W(x - 1, y + 1), W(x - 1, y - 1), W(x, y + 1), W(x, y - 1), 23, 24, 25, 21, 22, 27, 26, Emit);
		Diagonal(W(x - 1, y - 1), x - 1, y - 1, 34, W(x - 1, y), W(x, y - 1), W(x, y + 1) && W(x - 1, y + 1), W(x + 1, y - 1) && W(x + 1, y), Emit);
		Diagonal(W(x + 1, y - 1), x + 1, y - 1, 28, W(x + 1, y), W(x, y - 1), W(x, y + 1) && W(x + 1, y + 1), W(x - 1, y) && W(x - 1, y - 1), Emit);
		Diagonal(W(x - 1, y + 1), x - 1, y + 1, 46, W(x - 1, y), W(x, y + 1), W(x + 1, y + 1) && W(x + 1, y), W(x - 1, y - 1) && W(x, y - 1), Emit);
		Diagonal(W(x + 1, y + 1), x + 1, y + 1, 40, W(x + 1, y), W(x, y + 1), W(x - 1, y + 1) && W(x - 1, y), W(x, y - 1) && W(x + 1, y - 1), Emit);
	}

	/// <summary>The water north or south of the pixel, by its two ends and the pixel's own two sides.</summary>
	private static void Side(bool water, int px, int py, bool lEnd, bool rEnd, bool l, bool r, int both, int rOnly, int lOnly, int neither,
		int noEnds, int rEndKind, int lEndKind, Action<int, int, int> emit)
	{
		if (!water)
		{
			return;
		}
		if (lEnd && rEnd)
		{
			emit(px, py, l && r ? both : r ? rOnly : l ? lOnly : neither);
		}
		else if (!lEnd && !rEnd)
		{
			emit(px, py, noEnds);
		}
		else if (rEnd)
		{
			if (!r)
			{
				emit(px, py, rEndKind);
			}
		}
		else if (!l)
		{
			emit(px, py, lEndKind);
		}
	}

	/// <summary>The water east or west of the pixel, likewise.</summary>
	private static void Column(bool water, int px, int py, bool uEnd, bool dEnd, bool u, bool d, int both, int uOnly, int dOnly, int neither,
		int noEnds, int uEndKind, int dEndKind, Action<int, int, int> emit)
	{
		if (!water)
		{
			return;
		}
		if (uEnd && dEnd)
		{
			emit(px, py, u && d ? both : u ? uOnly : d ? dOnly : neither);
		}
		else if (!uEnd && !dEnd)
		{
			emit(px, py, noEnds);
		}
		else if (uEnd)
		{
			if (!u)
			{
				emit(px, py, uEndKind);
			}
		}
		else if (!d)
		{
			emit(px, py, dEndKind);
		}
	}

	private static void Diagonal(bool water, int px, int py, int kind, bool o1, bool o2, bool a, bool b, Action<int, int, int> emit)
	{
		if (!water)
		{
			return;
		}
		if (o1 && o2)
		{
			if (a && b)
			{
				emit(px, py, kind + 1);
				emit(px, py, kind + 3);
			}
			else if (a)
			{
				emit(px, py, kind + 1);
				emit(px, py, kind + 4);
			}
			else if (b)
			{
				emit(px, py, kind + 2);
				emit(px, py, kind + 3);
			}
			else
			{
				emit(px, py, kind);
			}
		}
		else if (!o1 && !o2)
		{
			emit(px, py, kind + 5);
		}
	}
}
