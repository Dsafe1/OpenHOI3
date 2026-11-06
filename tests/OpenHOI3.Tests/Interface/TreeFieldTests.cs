using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>The trees planted and drawn, as <c>docs/mechanics/mapdraw.md</c>, section 4, has the original plant them.</summary>
public class TreeFieldTests
{
	private const int Width = 60, Height = 10;

	/// <summary>Index 1 a tree of type 0 at row d 3; index 2 of type 1 at d 1; index 3 a tree type at d 0; index 0 none.</summary>
	private static readonly int[] TypeOfIndex = { -1, 0, 1, 1 };
	private static readonly int[] RowOfIndex = { 0, 3, 1, 0 };

	private static CTreeField Build(byte[] terrain, float[] densities, Func<int, int, int>? province = null) =>
		CTreeField.Build(terrain, Width, Height, province ?? ((x, _) => 1 + x / 25), id => id == 3, TypeOfIndex, RowOfIndex,
			densities);

	[Fact]
	public void DensityOfOneOrMorePlantsEveryPixelThatMay()
	{
		var terrain = new byte[Width * Height];
		terrain[0] = 1;                  // (0, 0)
		terrain[5 * Width + 7] = 2;      // (7, 5)
		terrain[2 * Width + 9] = 3;      // d 0: no tree
		terrain[3 * Width + 52] = 1;     // the second tile, a sea province
		CTreeField field = Build(terrain, new[] { 1f, 7f });
		Assert.Equal(3, field.Count);
		CTreePoint first = field.Tiles[0][0];
		Assert.Equal((0, 0, 0, 1, true), (first.X, first.Y, first.Type, first.Kind, first.OnLand));
		// Type 1's row d 1 is the sheet's row 3.
		Assert.Equal(3, field.Tiles[0].Single(p => p.Type == 1).Kind);
		Assert.False(field.Tiles[1].Single().OnLand);
	}

	[Fact]
	public void EveryPixelThatMayDrawsOnceAndTreesGoWhereTheDensityHoldsTheDraw()
	{
		var terrain = new byte[Width * Height];
		for (int x = 0; x < 20; x++)
		{
			terrain[x] = 1;
		}
		CTreeField field = Build(terrain, new[] { 0.5f, 1f });
		var random = new CRandom(CTreeField.Seed);
		var expected = Enumerable.Range(0, 20).Where(_ => 0.5f >= (float)(random.Next() & 0x7FFFFFFF) * (1f / 2147483648f)).ToArray();
		Assert.Equal(expected, field.Tiles[0].Select(p => p.X).ToArray());
	}

	[Fact]
	public void PixelOfNoProvinceIsPassedWithoutADraw()
	{
		var terrain = new byte[Width * Height];
		terrain[0] = 1;
		terrain[1] = 1;
		CTreeField none = Build(terrain, new[] { 1f, 1f }, (x, _) => x == 0 ? 0 : 1);
		Assert.Equal(new[] { 1 }, none.Tiles[0].Select(p => p.X).ToArray());
	}

	[Fact]
	public void TileKeepsItsTreesByTypeThenProvinceThenAsWalked()
	{
		var terrain = new byte[Width * Height];
		terrain[0 * Width + 20] = 2;     // type 1, province 1
		terrain[0 * Width + 30] = 1;     // type 0, province 2 - the first tree of type 0
		terrain[1 * Width + 5] = 1;      // type 0, province 1, walked after but its province's first tree came first
		CTreeField field = Build(terrain, new[] { 1f, 1f }, (x, _) => x < 25 ? 1 : 2);
		Assert.Equal(new[] { (5, 1), (30, 0), (20, 0) }, field.Tiles[0].Select(p => (p.X, p.Y)).ToArray());
	}

	[Fact]
	public void TemplateTreeIsFourFlatCardsOfDiagonalTwoPointFour()
	{
		CTreeField field = Build(new byte[Width * Height], new[] { 1f, 1f });
		ReadOnlySpan<float> tree = field.TemplateTree(0);
		float[] heights = { 0.5f, 0.375f, 0.225f, 0.05f };
		for (int card = 0; card < 4; card++)
		{
			for (int corner = 0; corner < 4; corner++)
			{
				int at = (card * 4 + corner) * CTreeField.Stride;
				Assert.Equal(heights[card], tree[at + 1]);
				Assert.Equal(1.2f, MathF.Sqrt(tree[at] * tree[at] + tree[at + 2] * tree[at + 2]), 4);
			}
			// The column of the sheet's left half, the shadow lowest: the card's u from (3 − card)/8 to (4 − card)/8.
			Assert.Equal((4 - card) / 8f, tree[card * 4 * CTreeField.Stride + 3]);
			Assert.Equal(0.25f, tree[(card * 4 + 2) * CTreeField.Stride + 4]);
		}
	}

	[Fact]
	public void VerticesMoveTheTemplateOntoThePixelWithItsRowAndSnow()
	{
		var terrain = new byte[Width * Height];
		terrain[2 * Width + 3] = 1;
		CTreeField field = Build(terrain, new[] { 1f, 1f });
		ReadOnlySpan<float> template = field.TemplateTree(0);
		List<float> plain = field.Vertices(0, 0, null);
		List<float> snowed = field.Vertices(0, 0, _ => true);
		Assert.Equal(CTreeField.VerticesPerTree * CTreeField.Stride, plain.Count);
		Assert.Equal(template[0] + 3, plain[0]);
		Assert.Equal(template[1] + 0.02f, plain[1]);
		Assert.Equal(template[2] + 2, plain[2]);
		Assert.Equal(template[4] + 0.25f, plain[4]);
		Assert.Equal(plain[3] + 0.5f, snowed[3]);
		Assert.Empty(field.Vertices(0, 1, null));
	}
}
