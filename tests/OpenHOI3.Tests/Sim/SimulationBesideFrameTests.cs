using System;
using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Hours run beside the client's frames (<see cref="CSimulation.BeginAdvance"/>): nothing they tell reaches whoever shows
/// the game until they are collected, on the collecting thread; what is posted meanwhile waits; and the game comes out as
/// it would have run in line.
/// </summary>
public class SimulationBesideFrameTests
{
	private static CSimulation Running()
	{
		var provinces = new[] { new CProvince(0, "", isSea: false) };
		// An end date, or the game is over from its first hour and the top speed's run stops at every hour.
		var simulation = new CSimulation(new CGameState(new CDate(1936, 1, 1), provinces, Array.Empty<CCountry>())
		{
			EndDate = new CDate(1948, 1, 1),
		});
		simulation.Clock.Paused = false;
		simulation.Clock.Speed = CClock.SpeedCount - 1;
		return simulation;
	}

	private sealed class Probe : CCommand
	{
		private readonly List<string> _log;
		private readonly string _name;

		public Probe(List<string> log, string name)
		{
			_log = log;
			_name = name;
		}

		public override void Execute(CGameState state) => _log.Add(_name);
	}

	[Fact]
	public void WhatTheHoursTellIsToldWhenTheyAreCollectedOnTheCollectingThread()
	{
		CSimulation simulation = Running();
		var told = new List<int>();
		simulation.HourPassed += () => told.Add(Environment.CurrentManagedThreadId);

		simulation.BeginAdvance(0.016);
		Assert.True(simulation.IsBusy);
		int hours = simulation.Finish();

		Assert.False(simulation.IsBusy);
		Assert.True(hours >= 1);
		Assert.Equal(hours, told.Count);
		Assert.All(told, thread => Assert.Equal(Environment.CurrentManagedThreadId, thread));
	}

	[Fact]
	public void BelowTheTopSpeedARunIsOneHourAndTheDayIsToldWithTheHourThatTurnsIt()
	{
		CSimulation simulation = Running();
		simulation.Clock.Speed = CClock.SpeedCount - 2;
		var days = 0;
		simulation.DayPassed += () => days++;

		for (int hour = 1; hour <= 24; hour++)
		{
			// Half a second an hour at this speed: a second's frame brings one, the excess dropped, as the original's.
			simulation.BeginAdvance(1.0);
			Assert.Equal(1, simulation.Finish());
			Assert.Equal(hour == 24 ? 1 : 0, days);
		}

		Assert.Equal(new CDate(1936, 1, 2), simulation.State.Date);
		Assert.Equal(0, simulation.State.Hour);
	}

	[Fact]
	public void AtTheTopSpeedARunGoesOnHourAfterHourAndEndsWithTheHourThatTurnsADay()
	{
		CSimulation simulation = Running();
		var days = 0;
		var hoursTold = 0;
		simulation.DayPassed += () => days++;
		simulation.HourPassed += () => hoursTold++;

		simulation.BeginAdvance(0.016);
		int hours = simulation.Finish();

		// A day of an empty game runs well inside CSimulation.BackToBackSeconds; the autosave and the midnight hash then
		// read the game as the hour that turned the day left it.
		Assert.Equal(24, hours);
		Assert.Equal(24, hoursTold);
		Assert.Equal(1, days);
		Assert.Equal(new CDate(1936, 1, 2), simulation.State.Date);
		Assert.Equal(0, simulation.State.Hour);
	}

	[Fact]
	public void AtTheTopSpeedARunStopsAtTheEndDate()
	{
		var provinces = new[] { new CProvince(0, "", isSea: false) };
		var simulation = new CSimulation(new CGameState(new CDate(1936, 1, 1), provinces, Array.Empty<CCountry>())
		{
			EndDate = new CDate(1936, 1, 1),
		});
		simulation.Clock.Paused = false;
		simulation.Clock.Speed = CClock.SpeedCount - 1;

		simulation.BeginAdvance(0.016);

		// The first hour of the end date is past it, by the hour, and the frame stops the clock there.
		Assert.Equal(1, simulation.Finish());
	}

	/// <summary>A command that tells whatever shows the game something, as a message to the player is told.</summary>
	private sealed class Teller : CCommand
	{
		private readonly CSimulation _simulation;
		private readonly List<string> _log;
		private readonly bool _mayStopTheClock;

		public Teller(CSimulation simulation, List<string> log, bool mayStopTheClock)
		{
			_simulation = simulation;
			_log = log;
			_mayStopTheClock = mayStopTheClock;
		}

		public override void Execute(CGameState state) => _simulation.OnMain(() => _log.Add("told"), _mayStopTheClock);
	}

	[Theory]
	[InlineData(true, 1)]
	[InlineData(false, 24)]
	public void AtTheTopSpeedARunStopsAfterAnHourThatTellsWhatMayStopTheClock(bool mayStopTheClock, int hoursRun)
	{
		CSimulation simulation = Running();
		var log = new List<string>();
		// Run in the first hour's drain: a message whose type pauses ends the run, so the clock stops at its hour; any
		// other - a trade between two other countries logged, a sound - lets the hours run on to the day's end.
		simulation.Post(new Teller(simulation, log, mayStopTheClock));

		simulation.BeginAdvance(0.016);
		int hours = simulation.Finish();

		Assert.Equal(hoursRun, hours);
		Assert.Equal(new[] { "told" }, log);
	}

	[Fact]
	public void ACommandPostedWhileTheHoursRunWaitsForThemAndKeepsItsOrder()
	{
		CSimulation simulation = Running();
		var log = new List<string>();

		simulation.BeginAdvance(0.016);
		simulation.Post(new Probe(log, "first"));
		simulation.Post(new Probe(log, "second"));
		simulation.Finish();
		Assert.Empty(log);                                      // queued as the hours were collected, not yet run

		simulation.Pump();
		Assert.Equal(new[] { "first", "second" }, log);
	}

	[Fact]
	public void PumpingFromOutsideWaitsForTheHours()
	{
		CSimulation simulation = Running();
		var log = new List<string>();
		simulation.BeginAdvance(0.016);
		simulation.Post(new Probe(log, "held"));

		simulation.Pump();                                      // the hours collected, the held command queued and run

		Assert.False(simulation.IsBusy);
		Assert.Equal(new[] { "held" }, log);
	}

	[Fact]
	public void AGameRunBesideTheFrameIsTheGameRunInLine()
	{
		CSimulation inLine = Running();
		CSimulation beside = Running();
		long start = beside.State.TotalHours;
		int hours = 0;
		for (int run = 0; run < 4; run++)
		{
			beside.BeginAdvance(0.016);
			hours += beside.Finish();
		}

		inLine.RunHours(hours);

		Assert.Equal(start + hours, beside.State.TotalHours);
		Assert.Equal(inLine.State.TotalHours, beside.State.TotalHours);
		Assert.Equal(inLine.State.Date, beside.State.Date);
		Assert.Equal(inLine.Turn, beside.Turn);
	}

	[Fact]
	public void WhatIsToldOnTheMainThreadWhileNothingRunsIsToldAtOnce()
	{
		CSimulation simulation = Running();
		var told = false;
		simulation.OnMain(() => told = true);
		Assert.True(told);

		var deferred = false;
		simulation.BeginAdvance(0.016);
		simulation.OnMain(() => deferred = true);
		Assert.False(deferred);
		simulation.Finish();
		Assert.True(deferred);
	}
}
