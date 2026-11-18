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
		var simulation = new CSimulation(new CGameState(new CDate(1936, 1, 1), provinces, Array.Empty<CCountry>()));
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
		Assert.Equal(1, hours);
		Assert.Single(told);
		Assert.All(told, thread => Assert.Equal(Environment.CurrentManagedThreadId, thread));
	}

	[Fact]
	public void ARunIsOneHourAndTheDayIsToldWithTheHourThatTurnsIt()
	{
		CSimulation simulation = Running();
		var days = 0;
		simulation.DayPassed += () => days++;

		for (int hour = 1; hour <= 24; hour++)
		{
			simulation.BeginAdvance(0.016);
			Assert.Equal(1, simulation.Finish());
			// At the top speed an hour is due every frame, as the original's: one a run, the excess dropped.
			Assert.Equal(hour == 24 ? 1 : 0, days);
		}

		Assert.Equal(new CDate(1936, 1, 2), simulation.State.Date);
		Assert.Equal(0, simulation.State.Hour);
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
