using System;
using System.Collections.Generic;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

public class FixedPointTests
{
	[Theory]
	[InlineData("237964.731", 237964731)]
	[InlineData("22", 22000)]
	[InlineData("0.5", 500)]
	[InlineData(".5", 500)]
	[InlineData("5.", 5000)]
	[InlineData("1.25", 1250)]
	[InlineData(" 3.100 ", 3100)]
	[InlineData("+2.5", 2500)]
	[InlineData("0", 0)]
	public void ReadsDecimalsAsThousandths(string text, long raw)
	{
		Assert.True(CFixedPoint.TryParse(text, out CFixedPoint value));
		Assert.Equal(raw, value.Raw);
	}

	[Fact]
	public void ANegativeNumberIsNegativeWhateverItsWholePart()
	{
		// "Whole part times a thousand plus the fraction" gets this one wrong if done naively.
		Assert.Equal(-500, CFixedPoint.Parse("-0.5").Raw);
		Assert.Equal(-8000, CFixedPoint.Parse("-8").Raw);
		Assert.Equal(-1250, CFixedPoint.Parse("-1.25").Raw);
	}

	[Fact]
	public void DigitsPastTheThirdAreDroppedNotRounded()
	{
		Assert.Equal(83, CFixedPoint.Parse("0.0839").Raw);
		Assert.Equal(-83, CFixedPoint.Parse("-0.0839").Raw);
		Assert.Equal(1999, CFixedPoint.Parse("1.9999999").Raw);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("-")]
	[InlineData(".")]
	[InlineData("yes")]
	[InlineData("1.2.3")]
	[InlineData("1,5")]
	[InlineData("12a")]
	[InlineData("99999999999999999999999")]
	public void RejectsWhatIsNotANumber(string? text)
	{
		Assert.False(CFixedPoint.TryParse(text, out _));
	}

	[Theory]
	[InlineData(237964731, "237964.731")]
	[InlineData(5000, "5.000")]
	[InlineData(7, "0.007")]
	[InlineData(-500, "-0.500")]
	[InlineData(-1250, "-1.250")]
	[InlineData(0, "0.000")]
	public void WritesThreeDecimalsAsTheSavegameDoes(long raw, string text)
	{
		Assert.Equal(text, new CFixedPoint(raw).ToString());
	}

	[Fact]
	public void AddsAndSubtractsExactly()
	{
		CFixedPoint a = CFixedPoint.Parse("0.1");
		CFixedPoint b = CFixedPoint.Parse("0.2");

		Assert.Equal(CFixedPoint.Parse("0.3"), a + b);       // the sum a double gets wrong
		Assert.Equal(CFixedPoint.Parse("-0.1"), a - b);
		Assert.Equal(CFixedPoint.Parse("-0.1"), -a);
	}

	[Fact]
	public void MultiplyingAndDividingTruncateTowardZero()
	{
		CFixedPoint third = CFixedPoint.One / CFixedPoint.FromInt(3);
		Assert.Equal(333, third.Raw);
		Assert.Equal(-333, (-CFixedPoint.One / CFixedPoint.FromInt(3)).Raw);

		// 0.333 * 0.333 is 0.110889, which becomes 0.110 and not 0.111.
		Assert.Equal(110, (third * third).Raw);
		Assert.Equal(-110, (third * -third).Raw);

		Assert.Equal(CFixedPoint.Parse("7.5"), CFixedPoint.Parse("2.5") * 3);
		Assert.Equal(833, (CFixedPoint.Parse("2.5") / 3).Raw);
	}

	[Fact]
	public void HasRoomForProductsTheOriginalsThirtyTwoBitsWouldNot()
	{
		CFixedPoint large = CFixedPoint.FromInt(2_000_000);
		Assert.Equal(CFixedPoint.FromInt(4_000_000_000_000), large * large);
	}

	[Fact]
	public void ComparesAndClamps()
	{
		CFixedPoint low = CFixedPoint.Parse("0.5");
		CFixedPoint high = CFixedPoint.FromInt(2);

		Assert.True(low < high);
		Assert.True(high >= CFixedPoint.FromInt(2));
		Assert.Equal(low, CFixedPoint.Min(low, high));
		Assert.Equal(high, CFixedPoint.Clamp(CFixedPoint.FromInt(9), low, high));
		Assert.Equal(CFixedPoint.Zero, CFixedPoint.Max(CFixedPoint.Zero, CFixedPoint.Parse("-0.083")));
		Assert.Equal(2, CFixedPoint.Parse("2.9").ToInt());
		Assert.Equal(-2, CFixedPoint.Parse("-2.9").ToInt());
	}
}

public class CalendarTests
{
	[Fact]
	public void EveryYearHasThreeHundredAndSixtyFiveDays()
	{
		// 1936 was a leap year. Not here.
		Assert.Equal(new CDate(1936, 3, 1), new CDate(1936, 2, 28).AddDays(1));
		Assert.Equal(new CDate(1937, 1, 1), new CDate(1936, 1, 1).AddDays(365));
		Assert.Equal(365, new CDate(1937, 1, 1).DayNumber - new CDate(1936, 1, 1).DayNumber);
	}

	[Theory]
	[InlineData(1936, 1, 1)]
	[InlineData(1936, 1, 31)]
	[InlineData(1936, 2, 28)]
	[InlineData(1939, 9, 1)]
	[InlineData(1941, 12, 31)]
	[InlineData(0, 1, 1)]
	public void DayNumbersRoundTrip(int year, int month, int day)
	{
		var date = new CDate(year, month, day);
		Assert.Equal(date, CDate.FromDayNumber(date.DayNumber));
	}

	[Fact]
	public void StepsAcrossMonthsAndYearsInBothDirections()
	{
		Assert.Equal(new CDate(1936, 2, 1), new CDate(1936, 1, 31).AddDays(1));
		Assert.Equal(new CDate(1936, 1, 1), new CDate(1935, 12, 31).AddDays(1));
		Assert.Equal(new CDate(1935, 12, 31), new CDate(1936, 1, 1).AddDays(-1));
		Assert.Equal(new CDate(1936, 12, 31), new CDate(1936, 1, 1).AddDays(364));
	}

	[Fact]
	public void DayNumbersOrderTheSameWayDatesDo()
	{
		Assert.True(new CDate(1936, 1, 31).DayNumber < new CDate(1936, 2, 1).DayNumber);
		Assert.True(new CDate(1935, 12, 31).DayNumber < new CDate(1936, 1, 1).DayNumber);
	}

	[Fact]
	public void ShowsTheHourInFrontOfTheDate()
	{
		Assert.Equal("13:00, 1 January, 1936", new CDate(1936, 1, 1).ToDisplayString(key => key, 13));
		Assert.Equal("0:00, 1 January, 1936", new CDate(1936, 1, 1).ToDisplayString(key => key));
	}
}

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
