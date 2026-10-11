using OpenHOI3.Interface.Tooltips;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// When a tooltip's second half comes, against <c>docs/mechanics/tooltips.md</c>, section 4.
/// </summary>
public class TooltipStillTests
{
	[Fact]
	public void StillOnceTheClockIsMoreThanASecondPastTheWholeSecondOfTheStop()
	{
		var still = new CTooltipStill();

		Assert.False(still.Update(10f, 10f, 5.9));
		Assert.False(still.Update(10f, 10f, 6.0));
		Assert.True(still.Update(10f, 10f, 6.01));
	}

	[Fact]
	public void AStopEarlyInItsSecondWaitsAlmostASecond()
	{
		var still = new CTooltipStill();

		Assert.False(still.Update(10f, 10f, 5.01));
		Assert.False(still.Update(10f, 10f, 5.99));
		Assert.True(still.Update(10f, 10f, 6.02));
	}

	[Fact]
	public void MovingStartsTheWaitAgain()
	{
		var still = new CTooltipStill();
		still.Update(10f, 10f, 1.5);

		Assert.True(still.Update(10f, 10f, 3.0));
		Assert.False(still.Update(11f, 10f, 3.5));
		Assert.True(still.Update(11f, 10f, 4.1));
	}

	[Fact]
	public void ThePointerWhereItStartsIsStillFromTheStart()
	{
		// The original keeps the last point from nought, so a pointer that has never moved
		// counts from the clock's start.
		var still = new CTooltipStill();

		Assert.True(still.Update(0f, 0f, 1.5));
	}
}
