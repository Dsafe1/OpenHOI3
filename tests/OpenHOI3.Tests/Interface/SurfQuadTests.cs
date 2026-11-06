using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>The surf's records and quads, as <c>docs/mechanics/mapdraw.md</c>, section 4, has the original build them.</summary>
public class SurfQuadTests
{
	[Fact]
	public void RecordsReadAsTheCacheHoldsThem()
	{
		var bytes = new byte[2 * CWavePixel.RecordSize];
		BitConverter.GetBytes(100).CopyTo(bytes, 0);
		BitConverter.GetBytes(200).CopyTo(bytes, 4);
		BitConverter.GetBytes(7).CopyTo(bytes, 8);
		BitConverter.GetBytes(-0.5f).CopyTo(bytes, 12);
		BitConverter.GetBytes(101).CopyTo(bytes, CWavePixel.RecordSize);
		Assert.Equal(new[] { new CWavePixel(100, 200, 7), new CWavePixel(101, 0, 0) }, CWavePixel.Parse(bytes));
	}

	[Fact]
	public void FirstOfEveryFourTakesTheLargerCoordinates()
	{
		// Type 0, water south of land, at the tile's middle: its corners (−½, −½), (−½, 1), (½, 1), (½, −½) in half pixels.
		var pixels = Enumerable.Repeat(new CWavePixel(25, 25, 0), 5).ToArray();
		List<short> shorts = CSurfQuads.Vertices(pixels, 25, 25, out int skipped);
		Assert.Equal(0, skipped);
		Assert.Equal(5 * 16, shorts.Count);
		Assert.Equal(new short[] { -1, -1, 0, 0, -1, 2, 0, 110, 1, 2, 110, 110, 1, -1, 110, 0 }, shorts.Take(16).ToArray());
		Assert.Equal(new short[] { -1, -1, 0, 0, -1, 2, 0, 100, 1, 2, 100, 100, 1, -1, 100, 0 }, shorts.Skip(16).Take(16).ToArray());
		Assert.Equal(110, shorts[4 * 16 + 7]);
	}

	[Fact]
	public void QuarterCornersAreCutTowardsNought()
	{
		// Type 29's last corner is (¼, −¼): three pixels west and south of the middle that is 2 × (−3 + ¼) = −5.5 and
		// 2 × (−3 − ¼) = −6.5, cut to −5 and −6.
		List<short> shorts = CSurfQuads.Vertices(new[] { new CWavePixel(22, 22, 29), new CWavePixel(28, 28, 29) }, 25, 25, out _);
		Assert.Equal((-5, -6), ((int)shorts[12], (int)shorts[13]));
		// Three east and north: 6.5 and 5.5, cut to 6 and 5.
		Assert.Equal((6, 5), ((int)shorts[16 + 12], (int)shorts[16 + 13]));
		// The alternative set's quirk: u 60 where a tenth more than 50 is 55, and 0.825 cut to 82.
		Assert.Equal(new short[] { 60, 82 }, new[] { shorts[10], shorts[11] });
	}

	[Fact]
	public void UnknownTypeMakesNoQuad()
	{
		List<short> shorts = CSurfQuads.Vertices(new[] { new CWavePixel(25, 25, 52), new CWavePixel(25, 25, 0) }, 25, 25, out int skipped);
		Assert.Equal(1, skipped);
		Assert.Equal(16, shorts.Count);
		// The record keeps its place in the count of four: the second of the tile takes the plain coordinates.
		Assert.Equal(100, shorts[7]);
	}
}
