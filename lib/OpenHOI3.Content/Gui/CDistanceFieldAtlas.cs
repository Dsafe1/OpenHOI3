using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenHOI3.Content.Gui;

/// <summary>
/// A large font's glyphs as distance fields, packed into one picture: what such a font is drawn from in place of a
/// stand-in typeface (<c>docs/mechanics/interface.md</c>, section 6). Decided 2026-11-18 by the maintainer: a font whose
/// capital H stands at least <see cref="LeastCapHeight"/> pixels is drawn from its own glyphs, every smaller one from
/// a stand-in - below that a distance field shows the pixels it was made from.
/// </summary>
public sealed class CDistanceFieldAtlas
{
	/// <summary>The least height of a capital H, in the texture's pixels, for a font to be drawn from its own glyphs.</summary>
	public const int LeastCapHeight = 38;

	/// <summary>Texels between the field's nought and its one, across the edge.</summary>
	public const int Range = 8;

	/// <summary>Texels of margin round each glyph's box, so that its field fades out before the next one.</summary>
	public const int Padding = 6;

	/// <summary>How many times finer than the texture the edge is found and the distances measured.</summary>
	public const int Supersample = 4;

	/// <summary>The atlas's width, the least: glyphs are laid in rows, tallest first.</summary>
	public const int LeastWidth = 1024;

	private CDistanceFieldAtlas(int width, int height, byte[] field, IReadOnlyList<CDistanceFieldGlyph> glyphs)
	{
		Width = width;
		Height = height;
		Field = field;
		Glyphs = glyphs;
	}

	public int Width { get; }

	public int Height { get; }

	/// <summary>A byte a texel, a row at a time from the top: 128 is the edge, more is inside.</summary>
	public byte[] Field { get; }

	/// <summary>Every glyph the descriptor gives, in its order.</summary>
	public IReadOnlyList<CDistanceFieldGlyph> Glyphs { get; }

	/// <summary>How tall the font's capital H is, in rows of ink; nought where it has none.</summary>
	public static int CapHeight(CFontDescriptor descriptor, CGlyphPage page)
	{
		return descriptor.Glyph('H') is { } glyph ? page.InkHeight(glyph) : 0;
	}

	/// <summary>Whether a font is large enough to be drawn from its own glyphs.</summary>
	public static bool Suits(CFontDescriptor descriptor, CGlyphPage page)
	{
		return CapHeight(descriptor, page) >= LeastCapHeight;
	}

	public static CDistanceFieldAtlas Build(CFontDescriptor descriptor, CGlyphPage page)
	{
		KeyValuePair<int, CFontGlyph>[] glyphs = descriptor.Glyphs.ToArray();
		var fields = new float[glyphs.Length][];
		Parallel.For(0, glyphs.Length, index =>
		{
			CFontGlyph glyph = glyphs[index].Value;
			if (glyph.Width > 0 && glyph.Height > 0 && page.InkHeight(glyph) > 0)
			{
				fields[index] = CDistanceField.Make(page, glyph, Padding, Range, Supersample);
			}
		});

		// Rows, tallest first and then in the descriptor's order, a texel apart.
		int width = LeastWidth;
		foreach (KeyValuePair<int, CFontGlyph> pair in glyphs)
		{
			width = Math.Max(width, pair.Value.Width + 2 * Padding + 1);
		}
		int[] order = Enumerable.Range(0, glyphs.Length)
			.Where(index => fields[index] is not null)
			.OrderByDescending(index => glyphs[index].Value.Height)
			.ThenBy(index => index)
			.ToArray();
		var places = new (int X, int Y)[glyphs.Length];
		int x = 0, y = 0, row = 0;
		foreach (int index in order)
		{
			int w = glyphs[index].Value.Width + 2 * Padding;
			int h = glyphs[index].Value.Height + 2 * Padding;
			if (x + w > width)
			{
				x = 0;
				y += row + 1;
				row = 0;
			}
			places[index] = (x, y);
			x += w + 1;
			row = Math.Max(row, h);
		}
		int height = Math.Max(1, y + row);

		var atlas = new byte[width * height];
		var placed = new List<CDistanceFieldGlyph>(glyphs.Length);
		for (int index = 0; index < glyphs.Length; index++)
		{
			(int code, CFontGlyph glyph) = (glyphs[index].Key, glyphs[index].Value);
			float[]? field = fields[index];
			if (field is null)
			{
				placed.Add(new CDistanceFieldGlyph(code, 0, 0, 0, 0, glyph.XOffset, glyph.YOffset, glyph.Advance));
				continue;
			}
			int w = glyph.Width + 2 * Padding;
			int h = glyph.Height + 2 * Padding;
			(int px, int py) = places[index];
			for (int line = 0; line < h; line++)
			{
				for (int column = 0; column < w; column++)
				{
					atlas[(py + line) * width + px + column] = (byte)Math.Round(field[line * w + column] * 255.0);
				}
			}
			placed.Add(new CDistanceFieldGlyph(code, px, py, w, h, glyph.XOffset - Padding, glyph.YOffset - Padding,
				glyph.Advance));
		}
		return new CDistanceFieldAtlas(width, height, atlas, placed);
	}
}
