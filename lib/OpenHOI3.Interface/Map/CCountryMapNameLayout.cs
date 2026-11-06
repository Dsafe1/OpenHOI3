using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A country's name laid over its land in the far view, as the original lays it out (<c>docs/mechanics/mapnames.md</c>,
/// from <c>reversing/findings/FINDINGS-farmap.md</c>, B.3): the land joined to its capital that it controls, a
/// least-squares line through it - the longer of two, cut to the run that crosses no foreign land - the letters sized by
/// the line's length and the land's area and spaced with blanks, and bent along a spline through the middle of the land.
/// Map units are the province bitmap's pixels, y counted from the south.
/// </summary>
public static class CCountryMapNameLayout
{
	/// <summary>The largest size a name is given, and the least it is drawn at [exe].</summary>
	public const float LargestSize = 25f;
	public const float LeastSize = 2.5f;

	/// <summary>Map units to a glyph pixel at size 1 [exe].</summary>
	public const float GlyphScale = 0.05f;

	/// <summary>The most blanks between letters tried; the original's loop has no bound, a font with no blank would never end it.</summary>
	private const int MostBlanks = 64;

	/// <summary>
	/// The provinces joined to a capital that a country controls: a flood from the capital, taken without a test, over
	/// neighbours that are land the country controls [exe].
	/// </summary>
	public static HashSet<int> Piece(CMapNameWorld world, int capital, int country)
	{
		var piece = new HashSet<int>();
		if (capital <= 0)
		{
			return piece;
		}
		var open = new Queue<int>();
		piece.Add(capital);
		open.Enqueue(capital);
		while (open.Count > 0)
		{
			foreach (int neighbour in world.Neighbours(open.Dequeue()))
			{
				if (neighbour > 0 && world.IsLand(neighbour) && world.Controller(neighbour) == country && piece.Add(neighbour))
				{
					open.Enqueue(neighbour);
				}
			}
		}
		return piece;
	}

	/// <summary>
	/// The name laid out, or null where none is drawn: no land counted, a name too long for its line - six units a
	/// character - or too small to read.
	/// </summary>
	public static CCountryMapName? Lay(CMapNameWorld world, ISet<int> piece, string text, CFontDescriptor font)
	{
		// 1. Every second pixel of each province's box that is that province or any sea.
		long n = 0, sx = 0, sy = 0, sxy = 0, sxx = 0, syy = 0;
		int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
		foreach (int province in piece)
		{
			CProvinceShape box = world.Shape(province);
			if (box.IsEmpty)
			{
				continue;
			}
			int across = ((box.MaxX - box.MinX) >> 1) + 1;
			int down = ((box.MaxY - box.MinY) >> 1) + 1;
			for (int j = 0; j < down; j++)
			{
				int y = box.MinY + 2 * j;
				for (int i = 0; i < across; i++)
				{
					int x = box.MinX + 2 * i;
					int here = world.ProvinceAt(x, y);
					if (here != province && (here <= 0 || world.IsLand(here)))
					{
						continue;
					}
					n++;
					sx += x;
					sy += y;
					sxy += (long)x * y;
					sxx += (long)x * x;
					syy += (long)y * y;
					minX = Math.Min(minX, x);
					maxX = Math.Max(maxX, x);
					minY = Math.Min(minY, y);
					maxY = Math.Max(maxY, y);
				}
			}
		}
		if (n == 0)
		{
			return null;
		}

		// 2. Two lines, y on x and x on y; the longer of the two once trimmed is kept, the first where they are equal.
		float meanX = (float)sx / n, meanY = (float)sy / n;
		float b1 = (float)(n * sxy - sx * sy) / (n * sxx - sx * sx);
		float a1 = meanY - b1 * meanX;
		float b2 = (float)(n * sxy - sx * sy) / (n * syy - sy * sy);
		float a2 = meanX - b2 * meanY;
		(Vector2 Start, Vector2 End)? lineA = Trimmed(world, piece, minX, b1 * minX + a1, maxX, b1 * maxX + a1, seaAllowed: true);
		(Vector2 Start, Vector2 End)? lineB = Trimmed(world, piece, b2 * minY + a2, minY, b2 * maxY + a2, maxY, seaAllowed: true);
		float lengthA = lineA is { } a ? Vector2.DistanceSquared(a.Start, a.End) : -1f;
		float lengthB = lineB is { } b ? Vector2.DistanceSquared(b.Start, b.End) : -1f;
		(Vector2 Start, Vector2 End)? line = lengthA > lengthB ? lineA : lineB;
		if (line is not { } kept)
		{
			return null;
		}
		Vector2 start = kept.Start, end = kept.End;
		float length = Vector2.Distance(start, end);

		// 3. The size, by the line's length against the name and the land against the line.
		float width = font.Width(text);
		float height = font.LineHeight;
		float size = MathF.Min(MathF.Min(length / width * 10f, n / length * 40f / height), LargestSize);
		if (6 * text.Length > length || size < LeastSize || float.IsNaN(size))
		{
			return null;
		}

		// 4. Blanks after every letter but the last until the spaced name spans 11 tenths of the line.
		string spaced = text;
		float spacedWidth = width;
		int blanks = 0;
		while (spacedWidth * size / length < 11f && blanks < MostBlanks)
		{
			blanks++;
			spaced = Spaced(text, blanks);
			spacedWidth = font.Width(spaced);
		}

		// 5. The anchor, below the line's middle by its left normal.
		Vector2 along = Vector2.Normalize(end - start);
		var up = new Vector2(-along.Y, along.X);
		float scale = GlyphScale * size;
		Vector2 anchor = (start + end) / 2f - up * (height * 0.5f * size * 0.04f);

		// 6. Each glyph's box along the line from a pen starting half the spaced width back; a glyph under one high only
		// moves the pen. Each box is remembered with the character of the name it stands for.
		var letters = new List<(int Index, char Character, Vector2 Centre)>();
		float pen = -spacedWidth / 2f;
		for (int j = 0; j < spaced.Length; j++)
		{
			char character = spaced[j];
			CFontGlyph glyph = font.Glyph(character) ?? default;
			// A character of the name stands every blanks + 1 places; the blanks between have no letter of their own.
			if (glyph.Height >= 1 && j % (blanks + 1) == 0)
			{
				float middleAlong = pen + glyph.XOffset + glyph.Width / 2f;
				float middleUp = height - glyph.YOffset - glyph.Height / 2f;
				letters.Add((j / (blanks + 1), character, anchor + along * (middleAlong * scale) + up * (middleUp * scale)));
			}
			pen += glyph.Advance;
		}

		var placed = new List<CMapNameLetter>(letters.Count);
		// 7. The bend, but for a level line.
		if (MathF.Abs(end.Y - start.Y) <= 0.1f || text.Length < 2 || letters.Count == 0)
		{
			foreach ((int _, char character, Vector2 centre) in letters)
			{
				placed.Add(new CMapNameLetter(character, centre.X, centre.Y, along.X, along.Y, scale));
			}
			return new CCountryMapName(text, placed);
		}
		float slope = -(end.X - start.X) / (end.Y - start.Y);
		var middles = new List<Vector2>(letters.Count);
		foreach ((int _, char _, Vector2 centre) in letters)
		{
			(Vector2 Start, Vector2 End)? across = Trimmed(world, piece, minX, centre.Y + slope * (minX - centre.X), maxX,
				centre.Y + slope * (maxX - centre.X), seaAllowed: false, ordered: false);
			middles.Add(across is { } run ? (run.Start + run.End) / 2f : centre);
		}
		Vector2 first = middles[0], last = middles[^1], mean = Vector2.Zero;
		foreach (Vector2 middle in middles)
		{
			mean += middle;
		}
		mean /= middles.Count;
		Vector2 p0 = first + Vector2.Distance(first, mean) * Direction(2f * Angle(first - mean) - Angle(mean - last));
		Vector2 p3 = last + Vector2.Distance(last, mean) * Direction(2f * Angle(last - mean) - Angle(mean - first));
		if (p0.X > p3.X)
		{
			(p0, p3) = (p3, p0);
			(first, last) = (last, first);
		}
		foreach ((int i, char character, Vector2 _) in letters)
		{
			float t = (float)i / (text.Length - 1);
			bool firstHalf = t <= 0.5f;
			float u = firstHalf ? 2f * t : 2f * t - 1f;
			Vector2 Sample(float s) => firstHalf ? CatmullRom(p0, first, mean, last, s) : CatmullRom(first, mean, last, p3, s);
			Vector2 centre = Sample(u);
			Vector2 tangent = u > 0.5f ? centre - Sample(u - 0.01f) : Sample(u + 0.01f) - centre;
			tangent = tangent.LengthSquared() > 0f ? Vector2.Normalize(tangent) : along;
			placed.Add(new CMapNameLetter(character, centre.X, centre.Y, tangent.X, tangent.Y, scale));
		}
		return new CCountryMapName(text, placed);
	}

	/// <summary>
	/// A line rasterised from its ends, truncated to whole pixels, and trimmed (<see cref="CMapLine.Trim"/>), its ends put
	/// in order from the left where asked; or null where nothing is left.
	/// </summary>
	private static (Vector2 Start, Vector2 End)? Trimmed(CMapNameWorld world, ISet<int> piece, float x0, float y0, float x1,
		float y1, bool seaAllowed, bool ordered = true)
	{
		List<(int X, int Y)> points = CMapLine.Rasterise((int)x0, (int)y0, (int)x1, (int)y1);
		if (CMapLine.Trim(points, world, piece, seaAllowed) is not { } run)
		{
			return null;
		}
		var start = new Vector2(points[run.First].X, points[run.First].Y);
		var end = new Vector2(points[run.Last].X, points[run.Last].Y);
		return ordered && end.X < start.X ? (end, start) : (start, end);
	}

	/// <summary>A name with so many blanks after every character but the last, a space among them [exe].</summary>
	public static string Spaced(string text, int blanks)
	{
		var spaced = new StringBuilder(text.Length * (blanks + 1));
		for (int i = 0; i < text.Length; i++)
		{
			spaced.Append(text[i]);
			if (i < text.Length - 1)
			{
				spaced.Append(' ', blanks);
			}
		}
		return spaced.ToString();
	}

	/// <summary>
	/// A name upper-cased as the original does where its font says <c>effect</c>, by Windows' ANSI table for the
	/// Windows-1252 bytes a text is held in: a to z, the accented small letters and their four in 0x9A to 0xFF [exe].
	/// </summary>
	public static string Upper(string text)
	{
		var upper = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			upper.Append(c switch
			{
				>= 'a' and <= 'z' => (char)(c - 32),
				>= 'à' and <= 'þ' when c != '÷' => (char)(c - 32),
				'\u009A' => '\u008A',
				'\u009C' => '\u008C',
				'\u009E' => '\u008E',
				'ÿ' => '\u009F',
				_ => c,
			});
		}
		return upper.ToString();
	}

	/// <summary>D3DX's Catmull-Rom spline through the middle two of four points, at s from nought to one.</summary>
	public static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float s)
	{
		float s2 = s * s, s3 = s2 * s;
		return 0.5f * (2f * p1 + (p2 - p0) * s + (2f * p0 - 5f * p1 + 4f * p2 - p3) * s2 + (3f * p1 - p0 - 3f * p2 + p3) * s3);
	}

	private static float Angle(Vector2 v) => MathF.Atan2(v.Y, v.X);

	private static Vector2 Direction(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));
}
