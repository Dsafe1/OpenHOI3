using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

public class HistoryFoldTests
{
	private static string Summary(string text, CDate date)
	{
		return string.Join(" ", CHistory.Fold(ClausewitzParser.Parse(text), date)
			.Select(entry => $"{entry.Key}={entry.Value}"));
	}

	[Fact]
	public void UndatedEntriesComeFirstThenDatedOnesInDateOrder()
	{
		// The 1935 block is written after the 1936 one and still applies before it.
		const string text = @"
			owner = GER
			1936.1.1 = { owner = FRA }
			1935.6.1 = { owner = ITA }
			controller = GER";

		Assert.Equal("owner=GER controller=GER owner=ITA owner=FRA", Summary(text, new CDate(1936, 1, 1)));
	}

	[Fact]
	public void ABlockOnTheDateCountsAndOneAfterItDoesNot()
	{
		const string text = "owner = GER  1936.1.1 = { owner = FRA }  1936.1.2 = { owner = ENG }";

		Assert.Equal("owner=GER owner=FRA", Summary(text, new CDate(1936, 1, 1)));
		Assert.Equal("owner=GER", Summary(text, new CDate(1935, 12, 31)));
		Assert.Equal("owner=GER owner=FRA owner=ENG", Summary(text, new CDate(1936, 1, 2)));
	}

	[Fact]
	public void BlocksSharingADateKeepFileOrder()
	{
		const string text = "1936.1.1 = { owner = FRA }  1936.1.1 = { owner = ENG }";

		Assert.Equal("owner=FRA owner=ENG", Summary(text, new CDate(1936, 1, 1)));
	}
}
