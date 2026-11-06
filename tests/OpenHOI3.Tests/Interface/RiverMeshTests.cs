using System;
using System.Linq;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>A river's strip as <c>docs/mechanics/mapdraw.md</c>, section 4, has the original make it.</summary>
public class RiverMeshTests
{
	private static CRiverPiece Piece(params (int X, int Y, int Width)[] nodes) =>
		new(nodes.Select((n, i) => new CRiverNode(n.X, n.Y, n.Width, i == 0)).ToArray(), -1);

	[Fact]
	public void PairOfNodesMakesSixVerticesAcrossTheRiver()
	{
		var random = new CRandom(CRiverMeshes.Seed);
		var expected = new CRandom(CRiverMeshes.Seed);
		CRiverStrip strip = CRiverMeshes.Build(Piece((10, 20, 1), (11, 20, 3)), random, (_, _) => false, 0.25f);
		Assert.Equal(6, strip.Vertices.Count);
		Assert.Null(strip.Exit);
		for (int k = 0; k < 3; k++)
		{
			CRiverVertex plus = strip.Vertices[2 * k], minus = strip.Vertices[2 * k + 1];
			// Due east, so the pair stands north and south of the spline, the plus side north; at t = 0 and 1 the spline
			// passes through the nodes and its width is theirs.
			float draw = (expected.Next() & 0x7FFFFFFF) / 2147483648f;
			float width = k == 0 ? 1 : k == 2 ? 3 : float.NaN;
			if (!float.IsNaN(width))
			{
				Assert.Equal(10 + k / 2f, plus.Position.X, 4);
				Assert.Equal(20 + (0.1f * width + 0.15f + 0.1f * draw) / 2, plus.Position.Z, 4);
			}
			Assert.Equal(plus.Position.X, minus.Position.X, 4);
			Assert.Equal(40, plus.Position.Z + minus.Position.Z, 4);
			// Over land: 0.40 and 0.02.
			Assert.Equal(0.42f, plus.Position.Y, 5);
			Assert.Equal((1f, k / 8f), (plus.Body.X, plus.Body.Y));
			Assert.Equal((0f, k / 8f), (minus.Body.X, minus.Body.Y));
			Assert.Equal((1f, 2 * k / 3f), (plus.Flow.X, plus.Flow.Y));
			Assert.Equal((0f, (2 * k + 1) / 3f), (minus.Flow.X, minus.Flow.Y));
		}
		// One draw for each step, three for the segment.
		Assert.Equal(3, random.Draws);
	}

	[Fact]
	public void LaterSegmentRepeatsThePairAtTheSeam()
	{
		var random = new CRandom(CRiverMeshes.Seed);
		CRiverStrip strip = CRiverMeshes.Build(Piece((10, 20, 1), (11, 20, 2), (12, 20, 2)), random, (_, _) => true, 0.25f);
		Assert.Equal(12, strip.Vertices.Count);
		// The second segment's first pair is the first segment's last again, at v 0.25, and its others run 0.375 and 0.5.
		Assert.Equal(strip.Vertices[4].Position, strip.Vertices[6].Position);
		Assert.Equal(strip.Vertices[5].Position, strip.Vertices[7].Position);
		Assert.Equal(new[] { 0.25f, 0.375f, 0.5f }, new[] { 6, 8, 10 }.Select(i => strip.Vertices[i].Body.Y).ToArray());
		// Over the sea, the height is 0.40 alone; the repeat still drew.
		Assert.Equal(0.40f, strip.Vertices[8].Position.Y, 5);
		Assert.Equal(6, random.Draws);
	}

	[Fact]
	public void StripTrianglesRunOverEveryThreeInARow()
	{
		Assert.Equal(new[] { 0, 1, 2, 1, 2, 3 }, CRiverStrip.StripTriangles(4).ToArray());
		Assert.Empty(CRiverStrip.StripTriangles(2));
	}
}
