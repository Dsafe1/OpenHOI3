using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Map;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>The rivers traced out of the river bitmap, as <c>docs/mechanics/mapdraw.md</c>, section 4, has the original trace them.</summary>
public class RiverTraceTests
{
	private const int Size = 8;

	/// <summary>A map of nothing but land (index 255), with the pixels given.</summary>
	private static byte[] Raster(params (int X, int Y, byte Index)[] pixels)
	{
		var raster = Enumerable.Repeat((byte)255, Size * Size).ToArray();
		foreach ((int x, int y, byte index) in pixels)
		{
			raster[y * Size + x] = index;
		}
		return raster;
	}

	private static (int, int)[] Places(CRiverPiece piece) => piece.Nodes.Select(n => (n.X, n.Y)).ToArray();

	/// <summary>The trace from the sources map.bin's builder would take: every index-0 pixel, by rows from the south.</summary>
	private static List<CRiverPiece> Trace(byte[] raster, Func<int, int, bool> isWater) =>
		CRiverTracer.Trace(raster, Size, Size, CMapBin.BitmapSources(raster, Size, Size), isWater);

	[Fact]
	public void TurnDropsTheCornerAndWaterStopsTheRiverOnePixelOn()
	{
		// East, east, then north twice into water at (3, 3).
		byte[] raster = Raster((1, 1, 0), (2, 1, 4), (3, 1, 4), (3, 2, 4), (3, 3, 4));
		var pieces = Trace(raster, (x, y) => (x, y) == (3, 3));
		CRiverPiece piece = Assert.Single(pieces);
		// The corner (3, 1) dropped where the walk turned north; the water pixel kept; one more pixel on northward.
		Assert.Equal(new[] { (1, 1), (2, 1), (3, 2), (3, 3), (3, 4) }, Places(piece));
		Assert.Equal(new[] { 1, 2, 2, 2, 2 }, piece.Nodes.Select(n => n.Width).ToArray());
		Assert.True(piece.Nodes[0].IsSource);
		Assert.Equal(-1, piece.ExitFrame);
	}

	[Fact]
	public void JunctionEndsTheRiverWithThePixelBeyondAndNothingMore()
	{
		byte[] raster = Raster((1, 1, 0), (2, 1, 3), (3, 1, 1));
		CRiverPiece piece = Assert.Single(Trace(raster, (_, _) => false));
		// The junction and the pixel beyond it, with the last node's width; no step after a junction.
		Assert.Equal(new[] { (1, 1), (2, 1), (3, 1), (4, 1) }, Places(piece));
		Assert.Equal(1, piece.Nodes[3].Width);
	}

	[Fact]
	public void JunctionIsTakenOnce()
	{
		// Two sources on either side of one junction: the first, by rows from the south, takes it.
		byte[] raster = Raster((1, 1, 0), (2, 1, 3), (3, 1, 1), (5, 1, 0), (4, 1, 3));
		var pieces = Trace(raster, (_, _) => false);
		Assert.Equal(2, pieces.Count);
		Assert.Equal(4, pieces[0].Nodes.Count);
		// The second steps west onto (4, 1) and finds the junction visited: it ends there, one pixel on westward.
		Assert.Equal(new[] { (5, 1), (4, 1), (3, 1) }, Places(pieces[1]));
	}

	[Fact]
	public void LoneSourceMakesNothing()
	{
		Assert.Empty(Trace(Raster((4, 4, 0)), (_, _) => false));
	}

	[Fact]
	public void ExitPixelSetsTheFrameAndDropsTwoNodes()
	{
		byte[] raster = Raster((1, 1, 0), (2, 1, 4), (3, 1, 4), (4, 1, 15));
		CRiverPiece piece = Assert.Single(Trace(raster, (_, _) => false));
		Assert.Equal(3, piece.ExitFrame);
		// Four nodes less two, and then one pixel on eastward.
		Assert.Equal(new[] { (1, 1), (2, 1), (3, 1) }, Places(piece));
	}
}
