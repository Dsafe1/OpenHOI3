using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The original's camera heights as zooms, against <c>docs/mechanics/mapunits.md</c>,
/// section 3. The camera sees 0.785 radians from the top of the screen to the bottom, so
/// from a height h it sees 0.828 h map units, and on a screen 1080 high a map unit is
/// 1304 / h pixels.
/// </summary>
public class MapCameraTests
{
	[Fact]
	public void AHeightIsAZoomAndBackAgain()
	{
		Assert.Equal(2.609f, CMapCamera.ZoomAt(CMapCamera.FarViewHeight, 1080f), 3);
		Assert.Equal(13.044f, CMapCamera.ZoomAt(CMapCamera.CounterDistance, 1080f), 3);
		Assert.Equal(32.61f, CMapCamera.ZoomAt(CMapCamera.LowestHeight, 1080f), 2);
		Assert.Equal(330f, CMapCamera.HeightAt(CMapCamera.ZoomAt(330f, 1440f), 1440f), 2);
		// What counts is the share of the screen, so a higher screen shows the same at a higher zoom.
		Assert.Equal(CMapCamera.ZoomAt(500f, 1080f) * 2f, CMapCamera.ZoomAt(500f, 2160f), 3);
	}

	[Fact]
	public void TheMapIsBareFromFiveHundredUpAndUnitsAreSeenFromNearUnderAHundred()
	{
		Assert.False(CMapCamera.IsFarView(499f));
		Assert.True(CMapCamera.IsFarView(500f));
		Assert.True(CMapCamera.IsFarView(CMapCamera.HeightAt(0f, 1080f)));
		Assert.True(CMapCamera.IsNear(99f));
		Assert.False(CMapCamera.IsNear(100f));
	}

	[Fact]
	public void UnitsAreSeenFromNearUnderThePlayersCounterDistance()
	{
		// The game options' slider runs the setting from 0 to 500: at 250 a height of 200 is near, at 0 nothing is.
		Assert.True(CMapCamera.IsNear(200f, 250f));
		Assert.False(CMapCamera.IsNear(250f, 250f));
		Assert.False(CMapCamera.IsNear(CMapCamera.LowestHeight, 0f));
	}
}
