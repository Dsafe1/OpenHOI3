using System.Numerics;
using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>The minimap's frame, its press and its overlay, against <c>docs/mechanics/minimap.md</c>.</summary>
public class MinimapFrameTests
{
	/// <summary>The base game's minimap on a 1920 x 1080 canvas, and its map.</summary>
	private static readonly Vector2 Corner = new(1557, 945);
	private static readonly Vector2 Size = new(320, 128);
	private const float MapWidth = 5616f;
	private const float MapHeight = 2160f;

	[Fact]
	public void TheFrameIsAStraightLineInTheCamerasHeightCutToWholePixels()
	{
		// The camera over the middle of the map at the starting height of 330.
		Vector2[] corners = CMinimapFrame.Corners(2808f, 1080f, 330f, MapWidth, MapHeight, Corner, Size);

		Assert.Equal(5, corners.Length);
		Assert.Equal(corners[0], corners[^1]);
		// Centre (1717, 1005); half sides int(554.69) x 0.025 = 13.85 and int(361.88) x 0.025 = 9.025.
		Assert.Equal(new Vector2(1703, 995), corners[0]);
		Assert.Equal(new Vector2(1703, 1014), corners[1]);
		Assert.Equal(new Vector2(1730, 1014), corners[2]);
		Assert.Equal(new Vector2(1730, 995), corners[3]);

		// From higher up it is wider, and nothing keeps it on the picture.
		Vector2[] high = CMinimapFrame.Corners(10f, 1080f, 1000f, MapWidth, MapHeight, Corner, Size);
		Assert.True(high[2].X - high[0].X > corners[2].X - corners[0].X);
		Assert.True(high[0].X < Corner.X);
	}

	[Fact]
	public void APressStrictlyInsideThePictureTakesTheCameraThere()
	{
		Assert.True(CMinimapFrame.Press(new Vector2(1717, 1009), Corner, Size, MapWidth, MapHeight, out float x, out float y));
		Assert.Equal(159f * MapWidth / 320f, x, 3);
		Assert.Equal(65f * MapHeight / 128f, y, 3);

		// The border row and column do nothing.
		Assert.False(CMinimapFrame.Press(new Vector2(1557, 1000), Corner, Size, MapWidth, MapHeight, out _, out _));
		Assert.False(CMinimapFrame.Press(new Vector2(1600, 945 + 128), Corner, Size, MapWidth, MapHeight, out _, out _));
		Assert.True(CMinimapFrame.Press(new Vector2(1558, 946), Corner, Size, MapWidth, MapHeight, out _, out _));
	}

	[Fact]
	public void AMapModeButtonSaysItsOwnKeyAndTheSeventhTheRegions()
	{
		Assert.Equal("mapmode_12", CMapModeButtons.Name(12));
		Assert.Equal("MAPMODE_1", CMapModeButtons.TooltipKey(1));
		Assert.Equal("MAPMODE_7G", CMapModeButtons.TooltipKey(7));
		Assert.Equal("MAPMODE_16", CMapModeButtons.TooltipKey(16));
	}

	[Fact]
	public void TheOverlayIsSlidAStepAnHour()
	{
		Assert.Equal(0.5f, CMinimapFrame.OverlayShift(13, 24, 1f), 5);
		Assert.Equal(-1f / 24f, CMinimapFrame.OverlayShift(0, 24, 1f), 5);
	}
}
