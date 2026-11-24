using System;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Saves;
using OpenHOI3.Content.Statistics;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Rules;
using OpenHOI3.Sim.Statistics;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// An enhanced game's statistics record, against <c>docs/mechanics/statisticsrecord.md</c>: what is kept of a country
/// each day and how finely, a year packed, the record read across years, the save's keys and the files written out.
/// </summary>
public class StatisticsRecordTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private static CGameState State(CDate date, bool enhanced = true)
	{
		var state = new CGameState(date,
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "Breslau", isSea: false) { Owner = Ger, Controller = Ger },
			},
			new[]
			{
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = 1 },
				new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "" },
			});
		if (enhanced)
		{
			state.Rules.CopyFrom(new CRules(Array.Empty<EFix>()));
		}
		return state;
	}

	/// <summary>A day passed and its figures kept, as the hour that turns a day does it.</summary>
	private static void Day(CGameState state)
	{
		for (int hour = 0; hour < CGameState.HoursPerDay; hour++)
		{
			state.AdvanceOneHour();
		}
		state.Statistics.RunDaily(state);
	}

	[Fact]
	public void AYearPacksToItsDifferencesAndComesBackAsItWas()
	{
		var rows = new long[3][];
		rows[0] = Enumerable.Range(0, 365).Select(day => 18_000L + day * 3).ToArray();
		rows[1] = Enumerable.Repeat(-250L, 365).ToArray();
		rows[2] = Enumerable.Range(0, 365).Select(day => (long)(day % 7 == 0 ? 1_000_000_000_000 : -7)).ToArray();

		byte[] block = CStatisticsBlock.Pack(rows, 365);
		// A steady climb, a constant and a week's beat: far under the eight bytes a value.
		Assert.True(block.Length < 400, block.Length.ToString());
		long[][] back = CStatisticsBlock.Unpack(block)!;
		Assert.Equal(3, back.Length);
		Assert.All(Enumerable.Range(0, 3), index => Assert.Equal(rows[index], back[index]));

		// A year that has run forty days is packed as forty.
		Assert.Equal(40, CStatisticsBlock.Unpack(CStatisticsBlock.Pack(rows, 40))![0].Length);
		Assert.Null(CStatisticsBlock.Unpack(new byte[] { 9, 1, 2, 3 }));
		Assert.Null(CStatisticsBlock.Unpack(Array.Empty<byte>()));
	}

	[Fact]
	public void OnlyAnEnhancedGameWithItsRuleOnKeepsAnything()
	{
		CGameState faithful = State(new CDate(1936, 1, 1), enhanced: false);
		Day(faithful);
		Assert.Empty(faithful.Statistics.Countries);

		CGameState off = State(new CDate(1936, 1, 1));
		off.GamePlay.CollectStatistics = false;
		Day(off);
		Assert.Empty(off.Statistics.Countries);

		CGameState on = State(new CDate(1936, 1, 1));
		Day(on);
		// Germany owns land; Poland owns none and is in no exile, so nothing is kept of it.
		Assert.Equal(new[] { Ger }, on.Statistics.Countries.Keys);
	}

	[Fact]
	public void AStockpileIsKeptToWholeUnitsAndAnyOtherFigureToHundredths()
	{
		CGameState state = State(new CDate(1936, 1, 1));
		CCountry germany = state.Country(Ger)!;
		germany.Manpower = CFixedPoint.Parse("123.456");
		germany.TotalIc = 258;
		state.Province(1).Pool[OpenHOI3.Sim.Economy.EGoods.Supplies] = CFixedPoint.Parse("18819.612");
		Day(state);

		CStatistics statistics = state.Statistics;
		Assert.Equal(new long[] { 123460 }, statistics.Read(state, Ger, "other_ManpowerTotal")!.Value.Values);
		Assert.Equal(new long[] { 258000 }, statistics.Read(state, Ger, "prod__TotalIc")!.Value.Values);
		Assert.Equal(new long[] { 2000 }, statistics.Read(state, Ger, "OTHER_PROVINCESOWNED")!.Value.Values);
		Assert.Equal(new long[] { 18820000 }, statistics.Read(state, Ger, "res_pool_supplies")!.Value.Values);
		Assert.Equal(new CDate(1936, 1, 2).DayNumber, statistics.Read(state, Ger, "res_pool_supplies")!.Value.FirstDayNumber);
		Assert.Null(statistics.Read(state, Ger, "no_such_series"));
		Assert.Null(statistics.Read(state, Pol, "prod__TotalIc"));
	}

	[Fact]
	public void AYearTurnedIsPackedAndTheRecordReadsOnAcrossIt()
	{
		CGameState state = State(new CDate(1936, 12, 28));
		CCountry germany = state.Country(Ger)!;
		for (int day = 0; day < 8; day++)
		{
			germany.TotalIc = 100 + day;
			Day(state);
		}
		// 29, 30 and 31 December, then 1 to 5 January.
		CCountryStatistics kept = state.Statistics.Countries[Ger];
		Assert.Equal(new[] { 1936 }, kept.Years.Keys);
		Assert.Equal((1937, 5), (kept.Year, kept.EndDay));
		(int first, long[] values) = state.Statistics.Read(state, Ger, "prod__TotalIc")!.Value;
		Assert.Equal(new CDate(1936, 12, 29).DayNumber, first);
		Assert.Equal(Enumerable.Range(100, 8).Select(ic => ic * 1000L), values);
	}

	[Fact]
	public void TheRecordIsSavedAsABlockAYearAndLoadedAsItWas()
	{
		CGameState state = State(new CDate(1936, 12, 28));
		CCountry germany = state.Country(Ger)!;
		for (int day = 0; day < 8; day++)
		{
			germany.TotalIc = 100 + day;
			Day(state);
		}

		var faithful = new CSaveTextWriter();
		CSaveStatistics.Write(faithful, State(new CDate(1936, 1, 1), enhanced: false));
		Assert.Equal("", faithful.Text);

		var writer = new CSaveTextWriter();
		CSaveStatistics.Write(writer, state);
		Assert.Contains("openhoi_statistics=", writer.Text);
		Assert.Contains("collect=yes", writer.Text);
		Assert.Contains("\"prod__TotalIc\"", writer.Text);

		CGameState loaded = State(state.Date);
		loaded.GamePlay.CollectStatistics = false;
		var report = new CSaveReport();
		CSaveStatistics.Read(loaded, ClausewitzParser.Parse(writer.Text).Children.Single(node => node.Key == CSaveStatistics.Key), report);
		Assert.True(loaded.GamePlay.CollectStatistics);
		Assert.Equal(state.Statistics.Read(state, Ger, "prod__TotalIc")!.Value.Values,
			loaded.Statistics.Read(loaded, Ger, "prod__TotalIc")!.Value.Values);
		CCountryStatistics kept = loaded.Statistics.Countries[Ger];
		Assert.Equal((1937, 5), (kept.Year, kept.EndDay));

		// It goes on from where it was, and saves the same again.
		germany = loaded.Country(Ger)!;
		germany.TotalIc = 300;
		Day(loaded);
		Assert.Equal(300000, loaded.Statistics.Read(loaded, Ger, "prod__TotalIc")!.Value.Values[^1]);
		Assert.Equal(9, loaded.Statistics.Read(loaded, Ger, "prod__TotalIc")!.Value.Values.Length);
	}

	[Fact]
	public void TheNullIdeologyHasNoSeries()
	{
		CGameState state = State(new CDate(1936, 1, 1));
		// The list of ideologies begins with the null one, which has no name.
		Assert.Equal("", state.Ideologies[0].Name);
		Assert.DoesNotContain("pol_Popularity_Single_", state.Statistics.Series(state).Names);
	}

	[Fact]
	public void BytesStandInASaveAsABareWordOfLettersDigitsDashAndUnderscore()
	{
		byte[] bytes = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
		string letters = CSaveStatistics.Letters(bytes);
		Assert.All(letters, letter => Assert.True(char.IsAsciiLetterOrDigit(letter) || letter is '-' or '_'));
		Assert.Equal(bytes, CSaveStatistics.Bytes(letters));
		Assert.Equal(new byte[] { 1 }, CSaveStatistics.Bytes(CSaveStatistics.Letters(new byte[] { 1 })));
		Assert.Null(CSaveStatistics.Bytes("not base64 !"));
	}

	[Fact]
	public void ASeriesIsWrittenOutAsTheOldCollectorsFile()
	{
		// Day nought is 1 January 1936; a value is whole where it is whole.
		Assert.Equal("Date,prod__TotalIc\n1,258\n2,258.5\n3,0.25",
			CStatisticsExport.File("prod__TotalIc", new CDate(1936, 1, 2).DayNumber, new long[] { 258000, 258500, 250 }));
		Assert.Equal("prod_Upgrade_%", CStatisticsExport.FileName("prod_Upgrade_%"));
		Assert.Equal("a_b", CStatisticsExport.FileName("a/b"));
	}
}
