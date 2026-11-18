using System;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// How solid each pixel of a font's glyph texture is - its alpha, 0 to 255, a row at a time from the top - which is
/// what a large font is drawn from (<c>docs/mechanics/interface.md</c>, section 6). Every glyph texture of the base
/// game and BlackICE keeps its glyphs' shapes in the alpha and paints the colour into the rest, or leaves it plain.
/// </summary>
public sealed class CGlyphPage
{
	/// <summary>A pixel at least this solid is inside a letter: the middle of an antialiased edge.</summary>
	public const byte Inside = 128;

	public CGlyphPage(int width, int height, byte[] coverage)
	{
		if (width <= 0 || height <= 0 || coverage.Length != width * height)
		{
			throw new ArgumentException($"A glyph page of {width} by {height} needs {width * height} pixels, not {coverage.Length}");
		}
		Width = width;
		Height = height;
		Coverage = coverage;
	}

	public int Width { get; }

	public int Height { get; }

	public byte[] Coverage { get; }

	/// <summary>How solid a pixel is, nought outside the page.</summary>
	public byte At(int x, int y)
	{
		return x < 0 || y < 0 || x >= Width || y >= Height ? (byte)0 : Coverage[y * Width + x];
	}

	/// <summary>
	/// How many rows of a glyph's box hold ink, from its first to its last row with a pixel at least half solid; nought
	/// for a box with none, as a space's.
	/// </summary>
	public int InkHeight(CFontGlyph glyph)
	{
		int first = -1, last = -1;
		for (int row = 0; row < glyph.Height; row++)
		{
			for (int column = 0; column < glyph.Width; column++)
			{
				if (At(glyph.X + column, glyph.Y + row) >= Inside)
				{
					if (first < 0)
					{
						first = row;
					}
					last = row;
					break;
				}
			}
		}
		return first < 0 ? 0 : last - first + 1;
	}
}
