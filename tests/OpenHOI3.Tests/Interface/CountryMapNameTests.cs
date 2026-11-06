using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenHOI3.Content.Gui;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>The far view's country names laid out as <c>docs/mechanics/mapnames.md</c> has it.</summary>
public class CountryMapNameTests
{
	/// <summary>A font of capitals ten wide and fourteen high on a twelve-pixel pen, lines of twenty, and a blank of six.</summary>
	private static CFontDescriptor Font()
	{
		var lines = new List<string> { "info face=\"Test\"", "common lineHeight=20 base=16" };
		for (char c = 'A'; c <= 'Z'; c++)
		{
			lines.Add($"char id={(int)c} x=0 y=0 width=10 height=14 xoffset=1 yoffset=2 xadvance=12 page=0");
		}
		lines.Add("char id=32 x=0 y=0 width=0 height=0 xoffset=0 yoffset=0 xadvance=6 page=0");
		return CFontDescriptor.Parse(string.Join("\n", lines));
	}

	/// <summary>
	/// A sea of province 1 with land in squares of 20 pixels, province 2 upward, wherever <paramref name="land"/> says,
	/// controlled by <paramref name="controller"/> of the square's corner.
	/// </summary>
	private static CMapNameWorld World(int width, int height, Func<int, int, bool> land, Func<int, int, int>? controller = null)
	{
		int columns = width / 20;
		int Square(int x, int y) => 2 + (y / 20) * columns + x / 20;
		int ProvinceAt(int x, int y) => land(x / 20 * 20, y / 20 * 20) ? Square(x, y) : 1;
		return new CMapNameWorld
		{
			Width = width,
			Height = height,
			ProvinceAt = ProvinceAt,
			IsLand = id => id >= 2,
			Controller = id => id < 2 ? 0 : controller?.Invoke((id - 2) % columns * 20, (id - 2) / columns * 20) ?? 7,
			Shape = id => id < 2 ? new CProvinceShape(0, 0, width - 1, height - 1, width * height)
				: new CProvinceShape((id - 2) % columns * 20, (id - 2) / columns * 20, (id - 2) % columns * 20 + 19,
					(id - 2) / columns * 20 + 19, 400),
			Neighbours = id =>
			{
				if (id < 2)
				{
					return Array.Empty<int>();
				}
				int x = (id - 2) % columns * 20, y = (id - 2) / columns * 20;
				return new[] { (x - 20, y), (x + 20, y), (x, y - 20), (x, y + 20) }
					.Where(p => p.Item1 >= 0 && p.Item2 >= 0 && p.Item1 < width && p.Item2 < height)
					.Select(p => ProvinceAt(p.Item1, p.Item2));
			},
		};
	}

	[Fact]
	public void A_level_strip_gets_its_name_along_its_middle_spaced_out_and_unbent()
	{
		CMapNameWorld world = World(400, 200, (x, y) => x >= 40 && x < 360 && y >= 80 && y < 120);
		HashSet<int> piece = CCountryMapNameLayout.Piece(world, world.ProvinceAt(200, 100), 7);

		CCountryMapName name = CCountryMapNameLayout.Lay(world, piece, "REICH", Font())!;

		Assert.Equal(32, piece.Count);
		Assert.Equal(5, name.Letters.Count);
		Assert.All(name.Letters, l => Assert.Equal((1f, 0f), (l.AlongX, l.AlongZ)));
		// The letters run left to right, about the land's middle, with blanks between them.
		Assert.True(name.Letters.Zip(name.Letters.Skip(1)).All(p => p.Second.X - p.First.X > 12 * p.First.Scale));
		Assert.InRange((name.Letters[0].X + name.Letters[^1].X) / 2f, 190f, 210f);
		Assert.InRange(name.Letters[2].Z, 90f, 110f);
		// The size by the land against the line: 3200 pixels counted over a line of 318, h 20 - (N / L × 40) / h, under 25.
		Assert.Equal(CCountryMapNameLayout.GlyphScale * MathF.Min(3200f / 318f * 40f / 20f, 318f / 60f * 10f), name.Letters[0].Scale, 0.01f);
	}

	[Fact]
	public void A_name_too_long_for_its_line_or_too_small_is_not_drawn()
	{
		CMapNameWorld world = World(400, 200, (x, y) => x >= 40 && x < 100 && y >= 80 && y < 100);
		HashSet<int> piece = CCountryMapNameLayout.Piece(world, world.ProvinceAt(60, 90), 7);

		// Six units a character: twelve letters want 72 and the line is 58.
		Assert.Null(CCountryMapNameLayout.Lay(world, piece, "CZECHOSLOVAK", Font()));
		Assert.NotNull(CCountryMapNameLayout.Lay(world, piece, "BEL", Font()));
	}

	[Fact]
	public void The_trim_keeps_the_longest_run_that_crosses_no_foreign_land_but_may_cross_sea()
	{
		// Land of 7 from 40 to 120, sea to 160, land of 7 to 360 but a square of country 8 at 280.
		CMapNameWorld world = World(400, 200, (x, y) => y >= 80 && y < 100 && x >= 40 && x < 360 && (x < 120 || x >= 160),
			(x, y) => x == 280 ? 8 : 7);
		var piece = new HashSet<int>(Enumerable.Range(0, 20).Select(i => world.ProvinceAt(i * 20, 80))
			.Where(id => id >= 2 && world.Controller(id) == 7));
		List<(int X, int Y)> points = CMapLine.Rasterise(0, 90, 399, 90);

		(int First, int Last) run = CMapLine.Trim(points, world, piece, seaAllowed: true)!.Value;
		(int First, int Last) dry = CMapLine.Trim(points, world, piece, seaAllowed: false)!.Value;

		// Over the sea from 40 to 279; without it the longest piece of land is 160 to 279.
		Assert.Equal((40, 279), (points[run.First].X, points[run.Last].X));
		Assert.Equal((160, 279), (points[dry.First].X, points[dry.Last].X));
	}

	[Fact]
	public void A_slanting_land_gets_its_name_bent_through_its_middle_and_read_from_the_left()
	{
		// A band from the south west to the north east.
		CMapNameWorld world = World(600, 600, (x, y) => Math.Abs(x - y) <= 40 && x >= 60 && x < 540);
		HashSet<int> piece = CCountryMapNameLayout.Piece(world, world.ProvinceAt(300, 300), 7);

		CCountryMapName name = CCountryMapNameLayout.Lay(world, piece, "POLAND", Font())!;

		Assert.Equal(6, name.Letters.Count);
		Assert.True(name.Letters.Zip(name.Letters.Skip(1)).All(p => p.Second.X > p.First.X && p.Second.Z > p.First.Z));
		Assert.All(name.Letters, l => Assert.InRange(l.AlongZ / l.AlongX, 0.5f, 2f));
		// The letters lie along the band's middle.
		Assert.All(name.Letters, l => Assert.InRange(l.Z - l.X, -30f, 30f));
	}

	[Fact]
	public void The_spline_runs_through_its_middle_points_and_the_upper_case_is_windows_ansi()
	{
		var (a, b, c, d) = (new Vector2(0, 0), new Vector2(1, 2), new Vector2(3, 3), new Vector2(5, 1));
		Assert.Equal(b, CCountryMapNameLayout.CatmullRom(a, b, c, d, 0f));
		Assert.Equal(c, CCountryMapNameLayout.CatmullRom(a, b, c, d, 1f));
		Assert.Equal("Sverige, España, Ålandÿ œ ß".Length, CCountryMapNameLayout.Upper("Sverige, España, Ålandÿ œ ß").Length);
		Assert.Equal("SVERIGE ESPAÑA \u009F \u008C ß", CCountryMapNameLayout.Upper("Sverige España ÿ \u009C ß"));
		Assert.Equal("R E I C H", CCountryMapNameLayout.Spaced("REICH", 1));
	}
}
