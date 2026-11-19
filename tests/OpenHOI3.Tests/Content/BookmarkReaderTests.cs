using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Frontend;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Content;

/// <summary><c>common/bookmarks.txt</c> as <c>docs/mechanics/bookmarks.md</c>, section 1, reads it.</summary>
public class BookmarkReaderTests
{
	private const string File =
		"bookmark = { icon = \"GFX_bg_startdate_1936\" name = \"ROAD_TO_WAR_NAME\" desc = \"ROAD_TO_WAR_DESC\" date = 1936.1.1\n" +
		"  country = ENG country = XXX country = GER }\n" +
		"bookmark = { is_cgm = yes cgm_folder = \"Blitzkrieg\" icon = \"GFX_bg_startdate_1939\" name = \"C\" desc = \"D\" date = 1939.9.1 }\n" +
		"bookmark = { icon = \"GFX_bg_startdate_1938\" name = \"GATHERING_STORM_NAME\" desc = \"D\" date = 1938.9.1.6 country = GER }\n";

	private static IReadOnlyList<CBookmark> Parse(string text, List<string> warnings)
	{
		return CBookmarkReader.Parse(ClausewitzParser.Parse(text), tag => tag.Value != "XXX", "bookmarks.txt", warnings.Add);
	}

	[Fact]
	public void Every_historical_bookmark_is_read_in_file_order_a_custom_game_left_out()
	{
		var warnings = new List<string>();

		IReadOnlyList<CBookmark> bookmarks = Parse(File, warnings);

		Assert.Equal(new[] { "ROAD_TO_WAR_NAME", "GATHERING_STORM_NAME" }, bookmarks.Select(bookmark => bookmark.Name));
		CBookmark first = bookmarks[0];
		Assert.Equal(("GFX_bg_startdate_1936", "ROAD_TO_WAR_DESC"), (first.Icon, first.Description));
		// A country that is none is dropped and said, as the original says it; the bookmark is kept.
		Assert.Equal(new[] { new CCountryTag("ENG"), new CCountryTag("GER") }, first.Countries);
		// At its file and line (logs.md, section 4).
		Assert.Equal(new[] { "bookmarks.txt:2: Bad tag XXX in bookmark ROAD_TO_WAR_NAME" }, warnings);
	}

	[Fact]
	public void A_date_is_at_its_midnight_unless_it_gives_an_hour()
	{
		IReadOnlyList<CBookmark> bookmarks = Parse(File, new List<string>());

		Assert.Equal((new CDate(1936, 1, 1), 0), (bookmarks[0].Date, bookmarks[0].Hour));
		Assert.Equal((new CDate(1938, 9, 1), 6), (bookmarks[1].Date, bookmarks[1].Hour));
	}

	[Fact]
	public void A_key_that_is_none_of_the_seven_loses_its_bookmark_and_every_one_after_it()
	{
		var warnings = new List<string>();
		string text = "bookmark = { name = \"A\" date = 1936.1.1 }\n" +
			"bookmark = { name = \"B\" date = 1938.9.1 scenario = yes }\n" +
			"bookmark = { name = \"C\" date = 1939.9.1 }\n";

		IReadOnlyList<CBookmark> bookmarks = Parse(text, warnings);

		Assert.Equal(new[] { "A" }, bookmarks.Select(bookmark => bookmark.Name));
		Assert.Equal(new[] { "bookmarks.txt:2: Failed to read bookmark" }, warnings);
	}
}
