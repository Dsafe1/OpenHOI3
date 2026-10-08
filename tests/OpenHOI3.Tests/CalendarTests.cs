using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

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
		// The second is what the original's top bar read on the first hour of a game.
		Assert.Equal("13:00 1, January 1936", new CDate(1936, 1, 1).ToDisplayString(key => key, 13));
		Assert.Equal("0:00 1, January 1936", new CDate(1936, 1, 1).ToDisplayString(key => key));
	}
}
