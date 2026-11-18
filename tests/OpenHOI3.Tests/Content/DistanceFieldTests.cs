using System.Linq;
using OpenHOI3.Content.Gui;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// A large font's glyphs made into distance fields, against <c>docs/mechanics/interface.md</c>, section 6. The glyph
/// pages here are drawn for the test; none is an install's.
/// </summary>
public class DistanceFieldTests
{
	/// <summary>A page of the given size with a solid rectangle on it, every other pixel empty.</summary>
	private static CGlyphPage Page(int width, int height, int left, int top, int right, int bottom, byte solid = 255)
	{
		var coverage = new byte[width * height];
		for (int y = top; y < bottom; y++)
		{
			for (int x = left; x < right; x++)
			{
				coverage[y * width + x] = solid;
			}
		}
		return new CGlyphPage(width, height, coverage);
	}

	[Fact]
	public void TheSquaredDistancesAreExact()
	{
		var set = new bool[7 * 5];
		set[2 * 7 + 3] = true;                                 // one cell, in the middle

		double[] distances = CDistanceField.SquaredDistances(set, 7, 5);

		Assert.Equal(0, distances[2 * 7 + 3]);
		Assert.Equal(1, distances[2 * 7 + 4]);
		Assert.Equal(2, distances[1 * 7 + 4]);
		Assert.Equal(9 + 4, distances[0 * 7 + 0]);
		Assert.Equal(9 + 4, distances[4 * 7 + 6]);
	}

	[Fact]
	public void TheEdgeIsHalfWayAndInsideIsMore()
	{
		// A glyph box of 20 by 20 whose middle 10 by 10 is solid.
		CGlyphPage page = Page(20, 20, 5, 5, 15, 15);
		var glyph = new CFontGlyph(20, 20, 0, 0, 20);

		float[] field = CDistanceField.Make(page, glyph, 2, 8, 4);
		int width = 24;
		float At(int x, int y) => field[(y + 2) * width + x + 2];

		Assert.True(At(10, 10) > 0.9f);                        // deep inside: as far as the range goes
		Assert.True(At(0, 0) < 0.01f);                         // far outside: nothing
		Assert.InRange(At(5, 10), 0.5f, 0.6f);                 // the first solid column: half a pixel inside
		Assert.InRange(At(4, 10), 0.4f, 0.5f);                 // the last empty one: half a pixel outside
		Assert.Equal(At(5, 10), At(14, 10), 3);                // the same on both sides
		Assert.Equal(At(10, 5), At(10, 14), 3);
	}

	[Fact]
	public void AHalfSolidPixelIsTheEdge()
	{
		// Columns 0 to 4 solid, column 5 half solid, the rest empty: the edge is in the middle of column 5.
		var coverage = new byte[12 * 4];
		for (int y = 0; y < 4; y++)
		{
			for (int x = 0; x < 5; x++)
			{
				coverage[y * 12 + x] = 255;
			}
			coverage[y * 12 + 5] = 128;
		}
		var page = new CGlyphPage(12, 4, coverage);

		float[] field = CDistanceField.Make(page, new CFontGlyph(12, 4, 0, 0, 12), 0, 8, 4);

		Assert.InRange(field[1 * 12 + 5], 0.47f, 0.53f);
	}

	[Fact]
	public void OnlyTheGlyphsOwnBoxIsRead()
	{
		// The page is solid everywhere; the glyph's box is 4 by 4 at (8, 8). Its margin stays empty.
		CGlyphPage page = Page(20, 20, 0, 0, 20, 20);

		float[] field = CDistanceField.Make(page, new CFontGlyph(4, 4, 0, 0, 4, 8, 8), 6, 8, 4);

		Assert.True(field[0] < 0.01f);                         // the margin's corner: far from the box
		Assert.True(field[(6 + 2) * 16 + 6 + 2] > 0.6f);       // the box's middle
	}

	[Fact]
	public void AFontIsLargeWhenItsCapitalHStandsThirtyEightRows()
	{
		CFontDescriptor tall = CFontDescriptor.Parse("char id=72 x=0 y=0 width=30 height=50 xoffset=0 yoffset=0 xadvance=30 page=0");
		CGlyphPage at38 = Page(64, 64, 5, 6, 25, 44);         // 38 rows of ink
		CGlyphPage at37 = Page(64, 64, 5, 6, 25, 43);
		CGlyphPage faint = Page(64, 64, 5, 6, 25, 44, 127);    // under half solid: no ink

		Assert.Equal(38, CDistanceFieldAtlas.CapHeight(tall, at38));
		Assert.True(CDistanceFieldAtlas.Suits(tall, at38));
		Assert.False(CDistanceFieldAtlas.Suits(tall, at37));
		Assert.False(CDistanceFieldAtlas.Suits(tall, faint));
		Assert.False(CDistanceFieldAtlas.Suits(CFontDescriptor.Parse("char id=73 x=0 y=0 width=30 height=50 xoffset=0 yoffset=0 xadvance=30 page=0"), at38));
	}

	[Fact]
	public void TheAtlasHoldsEachInkedGlyphWithItsMarginAndOffsetsMovedBack()
	{
		CFontDescriptor font = CFontDescriptor.Parse("""
			common lineHeight=60 base=48 scaleW=64 scaleH=64 pages=1
			char id=32 x=60 y=60 width=3 height=3 xoffset=-1 yoffset=-1 xadvance=12 page=0
			char id=72 x=0 y=0 width=30 height=50 xoffset=2 yoffset=4 xadvance=34 page=0
			char id=73 x=40 y=0 width=10 height=20 xoffset=1 yoffset=6 xadvance=12 page=0
			""");
		CGlyphPage page = Page(64, 64, 0, 0, 50, 50);

		CDistanceFieldAtlas atlas = CDistanceFieldAtlas.Build(font, page);

		Assert.Equal(new[] { 32, 72, 73 }, atlas.Glyphs.Select(glyph => glyph.Code));
		CDistanceFieldGlyph space = atlas.Glyphs[0];
		Assert.Equal((0, 0, 12), (space.Width, space.Height, space.Advance));   // no ink, no field; its advance kept
		CDistanceFieldGlyph h = atlas.Glyphs[1];
		int p = CDistanceFieldAtlas.Padding;
		Assert.Equal((30 + 2 * p, 50 + 2 * p, 2 - p, 4 - p, 34), (h.Width, h.Height, h.XOffset, h.YOffset, h.Advance));
		CDistanceFieldGlyph i = atlas.Glyphs[2];
		Assert.True(i.AtlasX >= h.AtlasX + h.Width || i.AtlasY >= h.AtlasY + h.Height);   // they do not overlap
		Assert.True(atlas.Field[(h.AtlasY + p + 25) * atlas.Width + h.AtlasX + p + 15] > 200);
		Assert.Equal(atlas.Width * atlas.Height, atlas.Field.Length);
	}
}
