using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Statistics;

/// <summary>
/// The statistics record of a game - an enhanced game's, where its rule is on: every country's figures, kept once a
/// day for the whole game. A finished year is packed; the running one is plain. Nothing in the game reads it: it is
/// what a page plots. See <c>docs/mechanics/statisticsrecord.md</c>.
/// </summary>
public sealed class CStatistics
{
	/// <summary>The days of the game's year, every year alike.</summary>
	public const int DaysInYear = 365;

	private CStatisticSeries? _series;

	/// <summary>Kept whatever the game's rules say: a tool's switch.</summary>
	public bool Forced { get; set; }

	/// <summary>Each country's part, by its tag.</summary>
	public Dictionary<CCountryTag, CCountryStatistics> Countries { get; } = new();

	/// <summary>The series kept, made from the game's ideologies the first time it is asked.</summary>
	public CStatisticSeries Series(CGameState state) => _series ??= new CStatisticSeries(state);

	/// <summary>Whether the day's figures are kept in this game: an enhanced one whose rule is on.</summary>
	public bool IsCollecting(CGameState state) => Forced || (state.Rules.Enhanced && state.GamePlay.CollectStatistics);

	/// <summary>
	/// The day's figures of every country that owns land or is in exile, kept under today's date - as the day's pass
	/// ends, so that they are the day's as it closed.
	/// </summary>
	public void RunDaily(CGameState state)
	{
		if (!IsCollecting(state))
		{
			return;
		}
		CStatisticSeries series = Series(state);
		var controlled = new Dictionary<CCountryTag, int>();
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller is { } controller)
			{
				controlled[controller] = controlled.GetValueOrDefault(controller) + 1;
			}
		}
		int year = state.Date.Year;
		int day = state.Date.DayNumber - new CDate(year, 1, 1).DayNumber;
		var today = new long[series.Count];
		CCountryFacts facts = CCountryFacts.Of(state);
		foreach (CCountry country in state.Countries)
		{
			if (facts.OwnedCount(country.Tag) <= 0 && !country.InExile)
			{
				continue;
			}
			if (!Countries.TryGetValue(country.Tag, out CCountryStatistics? kept))
			{
				Countries[country.Tag] = kept = new CCountryStatistics();
			}
			Turn(kept, series, year);
			if (kept.FirstDayNumber == 0)
			{
				kept.FirstDayNumber = state.Date.DayNumber;
			}
			series.Sample(state, country, controlled, today);
			// A day passed over - the country was not there - holds the day before's figures.
			for (int gap = kept.EndDay; gap < day; gap++)
			{
				for (int index = 0; index < today.Length; index++)
				{
					kept.Rows[index][gap] = gap > 0 ? kept.Rows[index][gap - 1] : 0;
				}
			}
			for (int index = 0; index < today.Length; index++)
			{
				kept.Rows[index][day] = series.Kept(index, today[index]);
			}
			kept.EndDay = Math.Max(kept.EndDay, day + 1);
		}
	}

	/// <summary>A country's running year made the one asked: the year before packed away where the year has turned.</summary>
	private static void Turn(CCountryStatistics kept, CStatisticSeries series, int year)
	{
		if (kept.Year == year && kept.Rows.Length == series.Count)
		{
			return;
		}
		Seal(kept);
		kept.Year = year;
		kept.Rows = new long[series.Count][];
		for (int index = 0; index < kept.Rows.Length; index++)
		{
			kept.Rows[index] = new long[DaysInYear];
		}
		kept.EndDay = 0;
	}

	/// <summary>The running year packed away with the finished ones, its last days filled with its last figures.</summary>
	public static void Seal(CCountryStatistics kept)
	{
		if (kept.Year == 0 || kept.Rows.Length == 0)
		{
			return;
		}
		foreach (long[] row in kept.Rows)
		{
			for (int day = Math.Max(kept.EndDay, 1); day < DaysInYear; day++)
			{
				row[day] = row[day - 1];
			}
		}
		kept.Years[kept.Year] = CStatisticsBlock.Pack(kept.Rows, DaysInYear);
		kept.Year = 0;
		kept.Rows = Array.Empty<long[]>();
		kept.EndDay = 0;
	}

	/// <summary>
	/// A country's series over the whole game in thousandths, a value a day from the first day kept, with that day's
	/// number - or none where the country or the series is not kept. A year the country was not there for holds the
	/// figures it last had.
	/// </summary>
	public (int FirstDayNumber, long[] Values)? Read(CGameState state, CCountryTag country, string name)
	{
		CStatisticSeries series = Series(state);
		int index = series.IndexOf(name);
		if (index < 0 || !Countries.TryGetValue(country, out CCountryStatistics? kept) || kept.FirstDayNumber == 0)
		{
			return null;
		}
		long step = series.Step(index);
		int firstYear = CDate.FromDayNumber(kept.FirstDayNumber).Year;
		int lastYear = kept.Year != 0 ? kept.Year : firstYear;
		foreach (int year in kept.Years.Keys)
		{
			lastYear = Math.Max(lastYear, year);
		}
		var values = new List<long>();
		long last = 0;
		for (int year = firstYear; year <= lastYear; year++)
		{
			int from = year == firstYear ? kept.FirstDayNumber - new CDate(year, 1, 1).DayNumber : 0;
			long[]? row = null;
			int to = DaysInYear;
			if (year == kept.Year && index < kept.Rows.Length)
			{
				row = kept.Rows[index];
				to = kept.EndDay;
			}
			else if (kept.Years.TryGetValue(year, out byte[]? block) && CStatisticsBlock.Unpack(block) is { } rows && index < rows.Length)
			{
				row = rows[index];
				to = Math.Min(DaysInYear, row.Length);
			}
			for (int day = from; day < to; day++)
			{
				last = row is null ? last : row[day] * step;
				values.Add(last);
			}
		}
		return (kept.FirstDayNumber, values.ToArray());
	}
}
