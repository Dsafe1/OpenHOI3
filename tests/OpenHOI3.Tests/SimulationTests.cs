using System;
using System.Collections.Generic;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

public class SimulationTests
{
	private static CSimulation StartAt(int year, int month, int day)
	{
		var provinces = new[] { new CProvince(0, "", isSea: false) };
		return new CSimulation(new CGameState(new CDate(year, month, day), provinces, Array.Empty<CCountry>()));
	}

	/// <summary>Records the order things happened in, and can refuse or post in turn.</summary>
	private sealed class Probe : CCommand
	{
		private readonly List<string> _log;
		private readonly string _name;

		public Probe(List<string> log, string name)
		{
			_log = log;
			_name = name;
		}

		public bool Allowed { get; init; } = true;

		public Action? OnExecute { get; init; }

		public override bool MayExecute(CGameState state) => Allowed;

		public override void Execute(CGameState state)
		{
			_log.Add(_name);
			OnExecute?.Invoke();
		}
	}

	[Fact]
	public void AGameStartsAtMidnightOnItsStartDate()
	{
		CSimulation simulation = StartAt(1936, 1, 1);

		Assert.Equal(new CDate(1936, 1, 1), simulation.State.Date);
		Assert.Equal(0, simulation.State.Hour);
	}

	[Fact]
	public void CommandsRunInTheOrderTheyWerePosted()
	{
		CSimulation simulation = StartAt(1936, 1, 1);
		var log = new List<string>();
		var first = new Probe(log, "first");
		var second = new Probe(log, "second");
		var third = new Probe(log, "third");

		simulation.Post(first);
		simulation.Post(second);
		simulation.Post(third);

		Assert.Empty(log);                                      // posting is not executing
		Assert.Equal(3, simulation.Pump());
		Assert.Equal(new[] { "first", "second", "third" }, log);
		Assert.Equal(new long[] { 0, 1, 2 }, new[] { first.Serial, second.Serial, third.Serial });
	}

	[Fact]
	public void ACommandThatMayNotRunIsDropped()
	{
		CSimulation simulation = StartAt(1936, 1, 1);
		var log = new List<string>();

		simulation.Post(new Probe(log, "refused") { Allowed = false });
		simulation.Post(new Probe(log, "ran"));

		Assert.Equal(1, simulation.Pump());
		Assert.Equal(new[] { "ran" }, log);
		Assert.Equal(0, simulation.Pump());                     // and it is not kept for later
	}

	[Fact]
	public void ACommandPostedDuringADrainWaitsForTheNextOne()
	{
		CSimulation simulation = StartAt(1936, 1, 1);
		var log = new List<string>();
		var follow = new Probe(log, "follow");
		var lead = new Probe(log, "lead") { OnExecute = () => simulation.Post(follow) };

		simulation.Post(lead);
		simulation.Pump();
		Assert.Equal(new[] { "lead" }, log);
		Assert.Equal(0, lead.Turn);
		Assert.Equal(1, follow.Turn);                           // posted after the first drain began

		simulation.Pump();
		Assert.Equal(new[] { "lead", "follow" }, log);
	}

	[Fact]
	public void AnHourIsACommandAndNothingMovesUntilItRuns()
	{
		CSimulation simulation = StartAt(1936, 1, 1);

		simulation.Post(new CHourlyTickCommand());
		Assert.Equal(0, simulation.State.Hour);

		simulation.Pump();
		Assert.Equal(1, simulation.State.Hour);
		Assert.Equal(new CDate(1936, 1, 1), simulation.State.Date);
	}

	[Fact]
	public void TwentyFourHoursMakeADay()
	{
		CSimulation simulation = StartAt(1936, 1, 1);

		simulation.RunHours(23);
		Assert.Equal(new CDate(1936, 1, 1), simulation.State.Date);
		Assert.Equal(23, simulation.State.Hour);

		simulation.RunHours(1);
		Assert.Equal(new CDate(1936, 1, 2), simulation.State.Date);
		Assert.Equal(0, simulation.State.Hour);
	}

	[Fact]
	public void TheDayMonthAndYearAreAnnouncedInThatOrderOnTheHourThatTurnsThem()
	{
		CSimulation simulation = StartAt(1936, 12, 31);
		var log = new List<string>();
		simulation.HourPassed += () => log.Add("hour");
		simulation.DayPassed += () => log.Add("day");
		simulation.MonthPassed += () => log.Add("month");
		simulation.YearPassed += () => log.Add("year");

		simulation.RunHours(23);
		Assert.All(log, entry => Assert.Equal("hour", entry));

		log.Clear();
		simulation.RunHours(1);
		Assert.Equal(new[] { "hour", "day", "month", "year" }, log);
		Assert.Equal(new CDate(1937, 1, 1), simulation.State.Date);
	}

	[Fact]
	public void AMonthTurnsWithoutAYearAndADayWithoutAMonth()
	{
		CSimulation simulation = StartAt(1936, 1, 30);
		var log = new List<string>();
		simulation.DayPassed += () => log.Add("day");
		simulation.MonthPassed += () => log.Add("month");
		simulation.YearPassed += () => log.Add("year");

		simulation.RunHours(24);
		Assert.Equal(new[] { "day" }, log);

		simulation.RunHours(24);
		Assert.Equal(new[] { "day", "day", "month" }, log);
		Assert.Equal(new CDate(1936, 2, 1), simulation.State.Date);
	}

	[Fact]
	public void RunsTheWholeWarHeadless()
	{
		CSimulation simulation = StartAt(1936, 1, 1);
		int days = 0;
		int months = 0;
		int years = 0;
		simulation.DayPassed += () => days++;
		simulation.MonthPassed += () => months++;
		simulation.YearPassed += () => years++;

		simulation.RunHours(12 * CDate.DaysPerYear * CGameState.HoursPerDay);

		Assert.Equal(new CDate(1948, 1, 1), simulation.State.Date);
		Assert.Equal(0, simulation.State.Hour);
		Assert.Equal(12 * 365, days);
		Assert.Equal(144, months);
		Assert.Equal(12, years);
	}

	[Fact]
	public void NothingHappensWhilePausedButOrdersStillDo()
	{
		CSimulation simulation = StartAt(1936, 1, 1);
		var log = new List<string>();

		simulation.Post(new Probe(log, "order"));
		Assert.Equal(0, simulation.Advance(5));

		Assert.Equal(0, simulation.State.TotalHours - new CDate(1936, 1, 1).DayNumber * 24L);
		Assert.Equal(new[] { "order" }, log);
	}

	[Fact]
	public void RealTimeDrivesTheClockOnceUnpaused()
	{
		CSimulation simulation = StartAt(1936, 1, 1);
		simulation.Clock.Paused = false;
		simulation.Clock.Speed = 0;                             // half a second an hour

		Assert.Equal(0, simulation.Advance(0.3));
		Assert.Equal(1, simulation.Advance(0.3));
		Assert.Equal(1, simulation.State.Hour);
	}

	[Fact]
	public void TheTopSpeedRunsAtLeastAnHourAFrame()
	{
		CSimulation simulation = StartAt(1936, 1, 1);
		simulation.Clock.Paused = false;
		simulation.Clock.Speed = CClock.SpeedCount - 1;

		Assert.True(simulation.Advance(0.016) >= 1);
		Assert.True(simulation.State.TotalHours > new CDate(1936, 1, 1).DayNumber * 24L);
	}

	[Fact]
	public void TwoGamesDoNotShareAnything()
	{
		CSimulation first = StartAt(1936, 1, 1);
		CSimulation second = StartAt(1939, 9, 1);

		first.RunHours(48);

		Assert.Equal(new CDate(1936, 1, 3), first.State.Date);
		Assert.Equal(new CDate(1939, 9, 1), second.State.Date);
		Assert.Equal(0, second.Turn);
	}
}
