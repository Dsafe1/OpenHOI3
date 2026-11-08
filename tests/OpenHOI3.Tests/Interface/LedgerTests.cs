using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Ledger;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>The Statistics screen's pages and tables, against <c>docs/mechanics/statistics.md</c>.</summary>
public class LedgerTests
{
	private static CLedgerRow Row(string key, CLedgerCell cell) => new(key, new[] { cell });

	private static CLedgerPage Table(params CLedgerRow[] rows) => new("t", new[] { new CLedgerColumn("c") }, rows);

	[Fact]
	public void PagesGoRoundBothWays()
	{
		Assert.Equal(1, CLedgerPages.Step(0, forward: true));
		Assert.Equal(0, CLedgerPages.Step(25, forward: true));
		Assert.Equal(25, CLedgerPages.Step(0, forward: false));
	}

	[Theory]
	[InlineData(21, false)]
	[InlineData(22, true)]
	[InlineData(25, true)]
	public void TheLastFourPagesArePies(int page, bool pies)
	{
		Assert.Equal(pies, CLedgerPages.IsPies(page));
	}

	[Fact]
	public void ATableStartsInTheOrderOfItsRowNames()
	{
		CLedgerPage page = Table(Row("b", CLedgerCell.Int(1)), Row("a", CLedgerCell.Int(2)));
		Assert.Equal(new[] { "a", "b" }, page.Rows.Select(row => row.Key));
	}

	[Fact]
	public void WholeNumbersSortAsNumbersAndNotAsText()
	{
		CLedgerPage page = Table(Row("a", CLedgerCell.Int(10)), Row("b", CLedgerCell.Int(9)));
		page.Sort(0, ascending: true);
		Assert.Equal(new[] { "b", "a" }, page.Rows.Select(row => row.Key));
		page.Sort(0, ascending: false);
		Assert.Equal(new[] { "a", "b" }, page.Rows.Select(row => row.Key));
	}

	[Fact]
	public void FiguresSortAsFigures()
	{
		CLedgerPage page = Table(Row("a", CLedgerCell.Float(10.5f)), Row("b", CLedgerCell.Float(9.5f)));
		page.Sort(0, ascending: true);
		Assert.Equal("b", page.Rows[0].Key);
	}

	[Fact]
	public void EqualCellsKeepTheOrderTheyStoodIn()
	{
		CLedgerPage page = Table(Row("a", CLedgerCell.Int(1)), Row("b", CLedgerCell.Int(1)));
		page.Sort(0, ascending: false);
		Assert.Equal(new[] { "a", "b" }, page.Rows.Select(row => row.Key));
	}

	[Fact]
	public void AFigureIsWrittenToOnePlaceAndAShareAsAWholePercent()
	{
		Assert.Equal("2.5", CLedgerCell.Float(2.5f).Text);
		Assert.Equal("45%", CLedgerCell.Percent(0.455f).Text);
	}

	[Fact]
	public void TheShipsSunkPageHasItsOwnColumnsGapsAndAll()
	{
		Assert.Equal(new[] { (0, 150), (150, 375), (385, 450), (500, 700), (700, 800), (800, 900) }, CLedgerColumns.Of(5, 6));
	}

	[Fact]
	public void TheFirstPageTakesTheFilesTwoWidths()
	{
		Assert.Equal(new[] { (0, 130), (130, 205), (205, 280) }, CLedgerColumns.Of(0, 3));
	}

	[Fact]
	public void TheStrategicPagesShareTheirRoomAndStopTwelveShort()
	{
		IReadOnlyList<(int Left, int Right)> spans = CLedgerColumns.Of(9, 9);
		Assert.Equal((0, 98), spans[0]);
		Assert.Equal((110, 208), spans[1]);
		Assert.Equal(880, spans[8].Left);
	}

	[Fact]
	public void TheBrigadeFiguresStand43Apart()
	{
		IReadOnlyList<(int Left, int Right)> spans = CLedgerColumns.Of(16, 19);
		Assert.Equal((275, 318), spans[3]);
		Assert.Equal(19, spans.Count);
	}

	[Fact]
	public void TheShipFiguresLastThreeAre42Wide()
	{
		IReadOnlyList<(int Left, int Right)> spans = CLedgerColumns.Of(17, 19);
		Assert.Equal((287, 327), spans[3]);
		Assert.Equal((820, 862), spans[16]);
		Assert.Equal((863, 905), spans[17]);
	}

	[Fact]
	public void TheAlliedComparisonsStepFromTheirFirstColumn()
	{
		Assert.Equal(new[] { (-12, 72), (73, 102), (101, 130) }, CLedgerColumns.Of(2, 3));
		Assert.Equal((158, 218), CLedgerColumns.Of(3, 2)[1]);
		Assert.Equal((193, 253), CLedgerColumns.Of(4, 2)[1]);
	}

	[Fact]
	public void APageNeverHasFewerColumnsThanItsTable()
	{
		Assert.Equal(9, CLedgerColumns.Of(1, 9).Count);
	}

	private static CGameState World()
	{
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "", Capital = 1 };
		var nobody = new CCountry { Tag = new CCountryTag("XXX"), Name = "Nobody", DefinitionFile = "", Capital = 2 };
		var provinces = new[]
		{
			new CProvince(0, "p0", isSea: false),
			new CProvince(1, "Berlin", isSea: false) { Owner = germany.Tag, Controller = germany.Tag, Points = 7 },
			new CProvince(2, "p2", isSea: false),
		};
		return new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany, nobody });
	}

	[Fact]
	public void TheFirstPageListsOnlyCountriesWithLandAndWhatTheyHold()
	{
		CGameState state = World();
		CLedgerPage page = new CLedgerPages(state, state.Country(new CCountryTag("GER"))!, key => key).Build(0);
		CLedgerRow row = Assert.Single(page.Rows);
		Assert.Equal("Germany", row.Cells[0].Text);
		Assert.Equal("7.0", row.Cells[2].Text);
	}

	[Fact]
	public void APieWithNothingInItHasNoSlices()
	{
		CGameState state = World();
		CLedgerPage page = new CLedgerPages(state, state.Country(new CCountryTag("GER"))!, key => key).Build(22);
		Assert.Equal(4, page.Pies!.Count);
		Assert.All(page.Pies, pie => Assert.Empty(pie.Slices));
	}

	[Fact]
	public void EveryPageCanBeBuilt()
	{
		CGameState state = World();
		var pages = new CLedgerPages(state, state.Country(new CCountryTag("GER"))!, key => key);
		for (int page = 0; page < CLedgerPages.Count; page++)
		{
			Assert.NotNull(pages.Build(page));
		}
	}
}
