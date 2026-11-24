using System;
using System.Globalization;
using OpenHOI3.Content.Saves;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Statistics;

/// <summary>
/// How large a game's statistics record is in a save, for the headless run's <c>statistics</c>: every country's
/// finished years and its running one as the letters a save holds them in, and what that comes to a year. A look.
/// </summary>
public static class CStatisticsSize
{
	public static void Report(CGameState state)
	{
		CStatistics statistics = state.Statistics;
		CStatisticSeries series = statistics.Series(state);
		long letters = 0;
		long days = 0;
		foreach (CCountryStatistics country in statistics.Countries.Values)
		{
			foreach (byte[] block in country.Years.Values)
			{
				letters += CSaveStatistics.Letters(block).Length;
				days += CStatistics.DaysInYear;
			}
			if (country.Year != 0 && country.EndDay > 0)
			{
				letters += CSaveStatistics.Letters(CStatisticsBlock.Pack(country.Rows, country.EndDay)).Length;
				days += country.EndDay;
			}
		}
		Console.WriteLine();
		Console.WriteLine($"Statistics record: {statistics.Countries.Count} countries, {series.Count} series each, {days} country-days kept.");
		Console.WriteLine($"  in a save                  {Size(letters)}");
		if (days > 0)
		{
			double perCountryYear = (double)letters / days * CStatistics.DaysInYear;
			Console.WriteLine($"  a country's year           {Size((long)perCountryYear)}");
			Console.WriteLine($"  {statistics.Countries.Count} countries over 12 years   {Size((long)(perCountryYear * statistics.Countries.Count * 12))}");
		}
	}

	private static string Size(long bytes) => bytes >= 1024 * 1024
		? (bytes / 1048576.0).ToString("0.00", CultureInfo.InvariantCulture) + " MiB"
		: (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KiB";
}
