using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.Frontend;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Content;

/// <summary>The start's bookmark out of <c>common/bookmarks.txt</c>, as <c>docs/mechanics/frontend.md</c>, section 3, reads it.</summary>
public class BookmarkReaderTests
{
	private const string File =
		"bookmark = { icon = \"GFX_bg_startdate_1938\" name = \"GATHERING_STORM_NAME\" desc = \"D\" date = 1938.9.1 country = GER }\n" +
		"bookmark = { icon = \"GFX_bg_startdate_1936\" name = \"ROAD_TO_WAR_NAME\" desc = \"ROAD_TO_WAR_DESC\" date = 1936.1.1\n" +
		"  country = ENG country = XXX country = GER }\n";

	[Fact]
	public void The_bookmark_of_the_start_date_is_the_one_offered_its_countries_in_order()
	{
		var warnings = new List<string>();

		CBookmark? start = CBookmarkReader.Start(ClausewitzParser.Parse(File), tag => tag.Value != "XXX", new CDate(1936, 1, 1),
			warnings.Add);

		// Another date's bookmark is passed over; a country that is none is dropped and said, as the original says it.
		Assert.NotNull(start);
		Assert.Equal(("GFX_bg_startdate_1936", "ROAD_TO_WAR_NAME", "ROAD_TO_WAR_DESC"), (start!.Icon, start.Name, start.Description));
		Assert.Equal(new[] { new CCountryTag("ENG"), new CCountryTag("GER") }, start.Countries);
		Assert.Equal(new[] { "Bad tag XXX in bookmark ROAD_TO_WAR_NAME" }, warnings);
	}

	[Fact]
	public void With_no_bookmark_of_the_start_date_there_is_none()
	{
		Assert.Null(CBookmarkReader.Start(ClausewitzParser.Parse(File), _ => true, new CDate(1939, 9, 1), _ => { }));
	}
}
