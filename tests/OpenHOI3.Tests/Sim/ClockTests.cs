using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Sim;

/// <summary>The clock against <c>docs/mechanics/tick.md</c>, section 2: the original's five thresholds and its one hour a frame.</summary>
public class ClockTests
{
	[Fact]
	public void StartsPausedAndOwesNothingWhilePaused()
	{
		var clock = new CClock();

		Assert.True(clock.Paused);
		Assert.False(clock.HourDue(10));

		// Time that passed during the pause is not repaid when it ends.
		clock.Paused = false;
		clock.Speed = 3;
		Assert.False(clock.HourDue(0.25));
	}

	[Fact]
	public void AnHourComesDueWhenTheSpeedsSecondsHaveBuiltUp()
	{
		var clock = new CClock { Paused = false, Speed = 3 };   // half a second an hour

		// Quarters and eighths, which a double holds exactly, so the test is about the clock and not about rounding.
		Assert.False(clock.HourDue(0.25));
		Assert.False(clock.HourDue(0.125));
		Assert.True(clock.HourDue(0.125));                      // 0.5 reached: due, and back to nought
		Assert.False(clock.HourDue(0.25));
	}

	[Fact]
	public void TheExcessOverTheThresholdIsDropped()
	{
		var clock = new CClock { Paused = false, Speed = 3 };

		Assert.True(clock.HourDue(0.75));                       // a quarter past the threshold, and not carried
		Assert.False(clock.HourDue(0.25));
		Assert.True(clock.HourDue(0.25));
	}

	[Fact]
	public void TheFiveSpeedsAreTheOriginals()
	{
		double[] seconds = { 5.0, 2.0, 1.0, 0.5, 0.0001 };
		for (int speed = 0; speed < CClock.SpeedCount; speed++)
		{
			var clock = new CClock { Paused = false, Speed = speed };
			Assert.False(clock.HourDue(seconds[speed] * 0.9));
			Assert.True(clock.HourDue(seconds[speed] * 0.1 + 1e-9));
		}
	}

	[Fact]
	public void ALongStallBringsOneHourAndNoMore()
	{
		var clock = new CClock { Paused = false, Speed = 4 };

		Assert.True(clock.HourDue(60));
		Assert.False(clock.HourDue(0));
	}

	[Fact]
	public void TheTopSpeedIsAnHourEveryFrame()
	{
		var clock = new CClock { Paused = false, Speed = CClock.SpeedCount - 1 };

		Assert.True(clock.HourDue(1.0 / 60));
		Assert.True(clock.HourDue(1.0 / 60));
	}

	[Fact]
	public void SpeedStaysInRange()
	{
		var clock = new CClock { Speed = 99 };
		Assert.Equal(CClock.SpeedCount - 1, clock.Speed);

		clock.Speed = -3;
		Assert.Equal(0, clock.Speed);
	}
}
