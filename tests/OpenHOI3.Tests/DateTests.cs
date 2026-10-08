using System;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

public class DateTests
{
	[Theory]
	[InlineData("1936.1.1", 1936, 1, 1)]
	[InlineData("1944.12.16", 1944, 12, 16)]
	[InlineData(" 1940.5.10 ", 1940, 5, 10)]
	public void ParsesGameDates(string text, int year, int month, int day)
	{
		Assert.True(CDate.TryParse(text, out CDate date));
		Assert.Equal(new CDate(year, month, day), date);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("1936")]
	[InlineData("1936.1")]
	[InlineData("1936.1.1.1")]
	[InlineData("1936.13.1")]      // month out of range
	[InlineData("1936.1.32")]      // day out of range
	[InlineData("nineteen.1.1")]
	public void RejectsMalformedDates(string? text)
	{
		Assert.False(CDate.TryParse(text, out _));
	}

	[Fact]
	public void OrdersChronologically()
	{
		var dates = new[]
		{
			new CDate(1939, 9, 1), new CDate(1936, 1, 1),
			new CDate(1939, 1, 1), new CDate(1936, 12, 31),
		};
		Array.Sort(dates);

		Assert.Equal(new[]
		{
			new CDate(1936, 1, 1), new CDate(1936, 12, 31),
			new CDate(1939, 1, 1), new CDate(1939, 9, 1),
		}, dates);
	}

	[Fact]
	public void RoundTripsThroughText()
	{
		Assert.Equal("1936.1.1", new CDate(1936, 1, 1).ToString());
	}

	[Theory]
	[InlineData(1, "January")]
	[InlineData(9, "September")]
	[InlineData(12, "December")]
	public void MapsMonthsToTheirLocalisationKeys(int month, string key)
	{
		// The game keys month names on the English word, not a number.
		Assert.Equal(key, new CDate(1936, month, 1).MonthKey);
	}

	[Fact]
	public void MonthKeyIsEmptyForAnImpossibleMonth()
	{
		Assert.Equal("", new CDate(1936, 13, 1).MonthKey);
	}
}
