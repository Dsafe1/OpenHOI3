using System;
using System.IO;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>The land borders' corner cache and its passes, as <c>docs/mechanics/mapdraw.md</c>, section 4, has them.</summary>
public class BorderCornerTests
{
	private const int Width = 4, Height = 4;

	/// <summary>The three cache files for a 4 by 4 map: one junction pixel of province 1 at (1, 2), every pixel's bytes (3, 64, 0, 0).</summary>
	private static CBorderCache Cache(byte cut = 0, Func<int, int, bool>? water = null)
	{
		var pairs = new MemoryStream();
		var writer = new BinaryWriter(pairs);
		writer.Write(3);
		writer.Write(0);
		writer.Write(1);
		writer.Write(1);
		writer.Write(2);
		writer.Write(0);
		int pixels = (Width + 1) * (Height + 1);
		var bitmap = new byte[pixels * 4];
		for (int i = 0; i < pixels; i++)
		{
			bitmap[i * 4] = 3;
			bitmap[i * 4 + 1] = 64;
		}
		var map = new byte[24 + pixels];
		"MAP_CACH"u8.CopyTo(map);
		map[24 + 2 * (Width + 1) + 2] = cut;
		return CBorderCache.Read(pairs.ToArray(), bitmap, map, Width, Height, water, _ => throw new InvalidOperationException())!;
	}

	/// <summary>Province 1 west of x = 2, province 2 east of it, both land.</summary>
	private static int Province(int x, int y) => x < 2 ? 1 : 2;

	[Fact]
	public void A_junction_pixel_and_its_four_neighbours_are_read_and_only_the_junction_is_visited()
	{
		CBorderCache cache = Cache();

		Assert.Equal(new[] { (1, 2) }, cache.Pairs[1]);
		Assert.Equal(5, cache.Corners.Count);
		CBorderCorner junction = cache.At(1, 2)!;
		Assert.True(junction.Active);
		Assert.False(cache.At(2, 2)!.Active);
		Assert.Equal((3, 64, 0, 0), ((int)junction.Province, (int)junction.Shade, (int)junction.Country, (int)junction.Diagonal));
		Assert.Same(cache.At(2, 2), junction.Right);
		// Sorted by rows from the south and then from the west.
		Assert.Equal((1, 1), (cache.Corners[0].X, cache.Corners[0].Y));
	}

	[Fact]
	public void A_copy_has_the_same_corners_linked_among_themselves_and_working_it_out_again_leaves_the_original_as_read()
	{
		CBorderCache read = Cache(water: (x, y) => x >= 2);

		CBorderCache copy = read.Copy();

		Assert.Equal(read.Corners.Select(c => (c.X, c.Y, c.Province, c.Shade, c.Country, c.Diagonal, c.Active, c.Cut)),
			copy.Corners.Select(c => (c.X, c.Y, c.Province, c.Shade, c.Country, c.Diagonal, c.Active, c.Cut)));
		Assert.NotSame(read.At(1, 2), copy.At(1, 2));
		Assert.Same(copy.At(2, 2), copy.At(1, 2)!.Right);
		Assert.Same(read.Pairs, copy.Pairs);
		Assert.Same(read.WaterBorders, copy.WaterBorders);

		CBorderCorners.Refresh(copy, Width, Height, Province, _ => true, id => id);
		Assert.Equal(1, copy.At(1, 2)!.Country);
		Assert.Equal(0, read.At(1, 2)!.Country);
	}

	[Fact]
	public void A_sea_zones_border_quad_is_every_coded_water_pixel_inside_the_edges_by_tile()
	{
		// Every pixel's bytes are (3, 64, 0, 0), so every water pixel is coded; water east of x = 2.
		CBorderCache cache = Cache(water: (x, y) => x >= 2);

		CWaterBorderQuad[] quads = cache.WaterBorders[0].ToArray();

		// x from 2 to the width less one and y from 2 to the height less one: (2..3) × (2..3), x outer.
		Assert.Equal(new[] { (2, 2), (2, 3), (3, 2), (3, 3) }, quads.Select(q => ((int)q.X, (int)q.Y)));
		Assert.All(quads, q => Assert.Equal((3, 0), ((int)q.Province, (int)q.Diagonal)));
	}

	[Fact]
	public void The_country_byte_marks_the_neighbour_held_by_another_unless_the_map_cache_cuts_that_way()
	{
		CBorderCache cache = Cache();
		CBorderCorners.Refresh(cache, Width, Height, Province, _ => true, id => id);
		Assert.Equal(1, cache.At(1, 2)!.Country);

		// The same holder on both sides: nothing.
		CBorderCorners.Refresh(cache, Width, Height, Province, _ => true, _ => 7);
		Assert.Equal(0, cache.At(1, 2)!.Country);

		// The map cache flagging a cut at the pixel to the east hides that side.
		CBorderCache cut = Cache(cut: 2);
		CBorderCorners.Refresh(cut, Width, Height, Province, _ => true, id => id);
		Assert.Equal(0, cut.At(1, 2)!.Country);
	}
}
