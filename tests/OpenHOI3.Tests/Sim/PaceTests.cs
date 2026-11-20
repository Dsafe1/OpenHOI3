using OpenHOI3.Sim;
using Xunit;

namespace OpenHOI3.Tests.Sim;

public class PaceTests
{
	private static (CClock Clock, CPace Pace) Running()
	{
		var clock = new CClock { Paused = false, Speed = 2 };
		return (clock, new CPace(clock));
	}

	[Fact]
	public void APlayersPauseIsAnybodys()
	{
		(CClock clock, CPace pace) = Running();
		pace.Multiplayer = true;
		pace.IsHost = false;

		pace.TogglePause();
		Assert.True(clock.Paused);
		pace.TogglePause();
		Assert.False(clock.Paused);
	}

	[Fact]
	public void TheSpeedIsTheHostsAlone()
	{
		(CClock clock, CPace pace) = Running();
		Assert.True(pace.ChangeSpeed(1));
		Assert.Equal(3, clock.Speed);

		pace.Multiplayer = true;
		Assert.True(pace.ChangeSpeed(-1));
		Assert.Equal(2, clock.Speed);

		pace.IsHost = false;
		Assert.False(pace.MaySetSpeed);
		Assert.False(pace.ChangeSpeed(1));
		Assert.Equal(2, clock.Speed);
	}

	[Fact]
	public void AScreenPausesAGameOfOneAndGivesItBack()
	{
		(CClock clock, CPace pace) = Running();

		bool stopped = pace.PauseByItself();
		Assert.True(stopped);
		Assert.True(clock.Paused);
		pace.ResumeAfter(stopped);
		Assert.False(clock.Paused);
	}

	[Fact]
	public void AScreenLeavesAPlayersPauseStanding()
	{
		(CClock clock, CPace pace) = Running();
		pace.TogglePause();

		bool stopped = pace.PauseByItself();
		Assert.False(stopped);
		pace.ResumeAfter(stopped);
		Assert.True(clock.Paused);
	}

	[Fact]
	public void NothingPausesByItselfBetweenSeveralPlayers()
	{
		(CClock clock, CPace pace) = Running();
		pace.Multiplayer = true;

		Assert.False(pace.PauseByItself());
		Assert.False(clock.Paused);
	}

	[Fact]
	public void TheGamesEndStopsTheClockForEverybody()
	{
		(CClock clock, CPace pace) = Running();
		pace.Multiplayer = true;
		pace.IsHost = false;

		pace.Stop();
		Assert.True(clock.Paused);
	}
}
