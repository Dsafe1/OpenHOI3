using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.History;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

public class HistoryFoldTests
{
	private static string Summary(string text, CDate date)
	{
		return string.Join(" ", CHistory.Fold(ClausewitzParser.Parse(text), date)
			.Select(entry => $"{entry.Key}={entry.Value}"));
	}

	[Fact]
	public void The_bookmarks_own_pass_marks_what_is_dated_after_the_defines_start_date()
	{
		// At a later start the passes to the defines' start date come first, the bookmark's own after: the same entries
		// in the same order as one fold, each marked by its pass (bookmarks.md, section 3).
		const string text = @"
			join_faction = allies
			1939.9.1 = { join_faction = axis }
			1936.1.1 = { owner = FRA }
			1941.6.22 = { leave_faction = axis }";
		ClausewitzNode root = ClausewitzParser.Parse(text);

		var passes = CHistory.FoldInPasses(root, new CDate(1936, 1, 1), new CDate(1941, 6, 22)).ToList();

		Assert.Equal(CHistory.Fold(root, new CDate(1941, 6, 22)), passes.Select(pass => pass.Entry));
		Assert.Equal(new[] { false, false, true, true }, passes.Select(pass => pass.Late));
		// With the date no later than the defines', nothing is late.
		Assert.All(CHistory.FoldInPasses(root, new CDate(1936, 1, 1), new CDate(1936, 1, 1)), pass => Assert.False(pass.Late));
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
	public void APassOfTheReplayTakesTheBlocksDatedInItsRangeAndTheFirstTheUndatedOnes()
	{
		ClausewitzNode root = ClausewitzParser.Parse(
			"owner = GER  1.1.1 = { owner = SOV }  1936.1.1 = { owner = FRA }  1937.1.1 = { owner = ENG }  1939.9.1 = { owner = ITA }");
		string Pass(CDate? after, CDate until) =>
			string.Join(" ", CHistory.FoldBetween(root, after, until).Select(entry => entry.Value));

		Assert.Equal("GER SOV", Pass(null, CHistory.FirstPassEnd));
		Assert.Equal("FRA", Pass(CHistory.FirstPassEnd, new CDate(1936, 1, 1)));
		Assert.Equal("ENG ITA", Pass(new CDate(1936, 1, 1), new CDate(1939, 9, 1)));
	}

	[Fact]
	public void BlocksSharingADateKeepFileOrder()
	{
		const string text = "1936.1.1 = { owner = FRA }  1936.1.1 = { owner = ENG }";

		Assert.Equal("owner=FRA owner=ENG", Summary(text, new CDate(1936, 1, 1)));
	}
}
