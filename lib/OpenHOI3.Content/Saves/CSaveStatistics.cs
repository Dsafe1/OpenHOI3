using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Statistics;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// The statistics record in an enhanced save, <c>openhoi_statistics</c>: whether the game keeps it, the series' names
/// in their order, and for each country the first day kept and a packed block a year - the bytes of
/// <see cref="CStatisticsBlock"/> in the letters of base64 a bare word may hold. A faithful game writes none of it.
/// See <c>docs/mechanics/statisticsrecord.md</c>, section 5.
/// </summary>
public static class CSaveStatistics
{
	public const string Key = "openhoi_statistics";

	private const string CollectKey = "collect";
	private const string SeriesKey = "series";
	private const string FirstKey = "first";
	private const string YearKey = "y";

	public static void Write(CSaveTextWriter writer, CGameState state)
	{
		if (!state.Rules.Enhanced)
		{
			return;
		}
		CStatistics statistics = state.Statistics;
		writer.Begin(Key);
		writer.Bool(CollectKey, state.GamePlay.CollectStatistics);
		if (statistics.Countries.Count > 0)
		{
			writer.Words(SeriesKey, statistics.Series(state).Names.Select(name => "\"" + name + "\""));
		}
		foreach ((CCountryTag tag, CCountryStatistics country) in statistics.Countries.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
		{
			writer.Begin(tag.Value);
			writer.Int(FirstKey, country.FirstDayNumber);
			foreach ((int year, byte[] block) in country.Years)
			{
				writer.Words(YearKey, new[] { Whole(year), Whole(CStatistics.DaysInYear), Letters(block) });
			}
			// The running year, as many days as it has.
			if (country.Year != 0 && country.EndDay > 0)
			{
				writer.Words(YearKey, new[] { Whole(country.Year), Whole(country.EndDay), Letters(CStatisticsBlock.Pack(country.Rows, country.EndDay)) });
			}
			writer.End();
		}
		writer.End();
	}

	public static void Read(CGameState state, ClausewitzNode node, CSaveReport report)
	{
		CStatistics statistics = state.Statistics;
		statistics.Countries.Clear();
		CStatisticSeries series = statistics.Series(state);
		// Where each saved series stands today: a save of another list of ideologies has other series.
		int[]? places = null;
		foreach (ClausewitzNode child in node.Children)
		{
			if (child.Key == CollectKey && !child.IsBlock)
			{
				state.GamePlay.CollectStatistics = child.Value == "yes";
			}
			else if (child.Key == SeriesKey && child.IsBlock)
			{
				places = child.Children.Select(name => series.IndexOf(name.Value ?? "")).ToArray();
			}
		}
		foreach (ClausewitzNode block in node.Children)
		{
			if (block.Key is null || !block.IsBlock || block.Key == SeriesKey)
			{
				continue;
			}
			if (places is null || block.Key.Length != 3)
			{
				report.Unread(Key, block.Key);
				continue;
			}
			var country = new CCountryStatistics { FirstDayNumber = block.GetInt(FirstKey) };
			var years = new List<(int Year, int Days, long[][] Rows)>();
			foreach (ClausewitzNode entry in block.All(YearKey))
			{
				List<string> words = entry.Children.Select(word => word.Value ?? "").ToList();
				if (words.Count == 3 && int.TryParse(words[0], NumberStyles.None, CultureInfo.InvariantCulture, out int year)
					&& int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int days)
					&& days >= 1 && days <= CStatistics.DaysInYear && Bytes(words[2]) is { } bytes
					&& CStatisticsBlock.Unpack(bytes) is { } rows && rows.Length == places.Length)
				{
					years.Add((year, days, Placed(rows, places, series.Count, days)));
				}
				else
				{
					report.Unread(Key, block.Key + ", a year");
				}
			}
			if (country.FirstDayNumber <= 0 || years.Count == 0)
			{
				continue;
			}
			years.Sort((a, b) => a.Year.CompareTo(b.Year));
			for (int at = 0; at < years.Count; at++)
			{
				(int year, int days, long[][] rows) = years[at];
				country.Year = year;
				country.Rows = rows;
				country.EndDay = days;
				// The last year goes on where it is this year's or has days to come; any other is finished.
				bool running = at == years.Count - 1 && (year == state.Date.Year || days < CStatistics.DaysInYear) && year >= state.Date.Year;
				if (!running)
				{
					CStatistics.Seal(country);
				}
			}
			statistics.Countries[new CCountryTag(block.Key)] = country;
		}
	}

	/// <summary>A year's rows as the game's series stand today, each a whole year long with the days it has.</summary>
	private static long[][] Placed(long[][] saved, int[] places, int count, int days)
	{
		var rows = new long[count][];
		for (int index = 0; index < count; index++)
		{
			rows[index] = new long[CStatistics.DaysInYear];
		}
		for (int index = 0; index < saved.Length; index++)
		{
			if (places[index] >= 0)
			{
				Array.Copy(saved[index], rows[places[index]], Math.Min(days, saved[index].Length));
			}
		}
		return rows;
	}

	private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>Bytes as a bare word of the save: base64 with - and _ for its + and /, and no = at its end.</summary>
	public static string Letters(byte[] bytes)
	{
		return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	/// <summary>The bytes of such a word, or none where it is no such word.</summary>
	public static byte[]? Bytes(string letters)
	{
		string padded = letters.Replace('-', '+').Replace('_', '/');
		padded += new string('=', (4 - padded.Length % 4) % 4);
		try
		{
			return Convert.FromBase64String(padded);
		}
		catch (FormatException)
		{
			return null;
		}
	}
}
