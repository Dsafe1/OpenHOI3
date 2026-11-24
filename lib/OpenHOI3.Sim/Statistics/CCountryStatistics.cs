using System.Collections.Generic;

namespace OpenHOI3.Sim.Statistics;

/// <summary>
/// One country's part of the statistics record: its finished years packed, a block a year, and the year running as
/// plain rows, a row a series with a value for each of its days so far. Every value is the kept one - the sampled
/// thousandths over its series' step (<see cref="CStatisticSeries.Step"/>).
/// </summary>
public sealed class CCountryStatistics
{
	/// <summary>The finished years, each all its days packed (<see cref="CStatisticsBlock"/>), by the year.</summary>
	public SortedDictionary<int, byte[]> Years { get; } = new();

	/// <summary>The number of the first day a value was kept for (<see cref="CDate.DayNumber"/>); nought before any.</summary>
	public int FirstDayNumber { get; set; }

	/// <summary>The year the rows below are of; nought where no year is running.</summary>
	public int Year { get; set; }

	/// <summary>The running year's rows, a row a series, each <see cref="CStatistics.DaysInYear"/> long.</summary>
	public long[][] Rows { get; set; } = System.Array.Empty<long[]>();

	/// <summary>How many of the running year's days hold a value, from its first day.</summary>
	public int EndDay { get; set; }
}
