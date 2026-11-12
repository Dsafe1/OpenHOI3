using OpenHOI3.Interface.Map;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The original's zoom, against <c>reversing/findings/FINDINGS-camerazoom.md</c>: a notch's target and pull, the ease of
/// a frame and the buttons' fifth.
/// </summary>
public class MapZoomTests
{
	[Fact]
	public void ANotchIsATenthInAndAFifthOutOfTheCurrentHeightAtTheDefaultSpeed()
	{
		Assert.Equal(297f, CMapZoom.NotchTarget(330f, 330f, CMapZoom.DefaultSpeed, true, 40f, 1000f), 2);
		Assert.Equal(396f, CMapZoom.NotchTarget(330f, 330f, CMapZoom.DefaultSpeed, false, 40f, 1000f), 2);
		// At zoom_speed 100, a fifth in and two fifths out.
		Assert.Equal(264f, CMapZoom.NotchTarget(330f, 330f, 100f, true, 40f, 1000f), 2);
	}

	[Fact]
	public void ANotchIsSizedByTheCurrentHeightButTakenOffTheTarget()
	{
		// Mid-ease: the camera at 330, the target already at 297; the next notch takes 33 more off the target.
		Assert.Equal(264f, CMapZoom.NotchTarget(297f, 330f, CMapZoom.DefaultSpeed, true, 40f, 1000f), 2);
	}

	[Fact]
	public void TheTargetIsHeldToTheLimits()
	{
		Assert.Equal(40f, CMapZoom.NotchTarget(42f, 42f, CMapZoom.DefaultSpeed, true, 40f, 1000f));
		Assert.Equal(1000f, CMapZoom.NotchTarget(950f, 950f, CMapZoom.DefaultSpeed, false, 40f, 1000f));
	}

	[Fact]
	public void ANotchPullsTheLookTargetAboutAnEighthOfTheWayTowardsThePointer()
	{
		float left = CMapZoom.PullLeft(330f, CMapZoom.DefaultSpeed);
		Assert.Equal(1f - 4f / 15f * 0.5f, left, 3);
		// The same whatever the height.
		Assert.Equal(left, CMapZoom.PullLeft(80f, CMapZoom.DefaultSpeed), 3);
		// Target 1000, pointer over 1300: 40 of the 300 moved.
		Assert.Equal(1040f, CMapZoom.Pull(1000f, 1300f, left), 0);
	}

	[Fact]
	public void TheEaseClosesNearlyAFifthOfTheGapAFrame()
	{
		Assert.Equal(330f - 33f * 0.197f, CMapZoom.EaseTowards(330f, 297f), 3);
		// Half the way in a little over three frames, nine tenths in about ten and a half.
		float height = 330f;
		for (int frame = 0; frame < 11; frame++)
		{
			height = CMapZoom.EaseTowards(height, 297f);
		}
		Assert.InRange(330f - height, 33f * 0.9f, 33f);
	}

	[Fact]
	public void TheButtonsMoveTheTargetByAFifthOfItself()
	{
		Assert.Equal(264f, CMapZoom.ButtonTarget(330f, true, 40f, 1000f), 2);
		Assert.Equal(396f, CMapZoom.ButtonTarget(330f, false, 40f, 1000f), 2);
		Assert.Equal(1000f, CMapZoom.ButtonTarget(900f, false, 40f, 1000f));
	}
}
