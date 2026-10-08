
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Sim;

public class ClockTests
{
	[Fact]
	public void StartsPausedAndOwesNothingWhilePaused()
	{
		var clock = new CClock();

		Assert.True(clock.Paused);
		Assert.Equal(0, clock.HoursDue(10));

		// Time that passed during the pause is not repaid when it ends.
		clock.Paused = false;
		clock.Speed = 0;
		Assert.Equal(0, clock.HoursDue(0.1));
	}

	[Fact]
	public void AnHourComesDueWhenEnoughRealTimeHasBuiltUp()
	{
		var clock = new CClock { Paused = false, Speed = 0 };   // half a second an hour

		// Quarters and eighths, which a double holds exactly, so the test is about the
		// clock and not about rounding.
		Assert.Equal(0, clock.HoursDue(0.25));
		Assert.Equal(0, clock.HoursDue(0.125));
		Assert.Equal(1, clock.HoursDue(0.25));                  // 0.625 built up; 0.125 carried over
		Assert.Equal(0, clock.HoursDue(0.25));
		Assert.Equal(1, clock.HoursDue(0.125));
	}

	[Fact]
	public void FasterSpeedsOweMoreHoursForTheSameTime()
	{
		var slow = new CClock { Paused = false, Speed = 1 };
		var fast = new CClock { Paused = false, Speed = 3 };

		Assert.Equal(1, slow.HoursDue(0.25));
		Assert.Equal(6, fast.HoursDue(0.25));
	}

	[Fact]
	public void TheTopSpeedHasNoWaitAtAll()
	{
		var clock = new CClock { Paused = false, Speed = CClock.SpeedCount - 1 };

		Assert.Equal(CClock.Unlimited, clock.HoursDue(0.0001));
	}

	[Fact]
	public void ALongStallIsNotRepaidAsABurst()
	{
		var clock = new CClock { Paused = false, Speed = 2 };   // a tenth of a second an hour

		Assert.Equal(5, clock.HoursDue(60));                    // capped at half a second owed

		// At the slowest speed half a second is a single hour, and a cap that tight
		// would swallow what an ordinary frame carries over. It is two hours there.
		var slow = new CClock { Paused = false, Speed = 0 };
		Assert.Equal(2, slow.HoursDue(60));
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
