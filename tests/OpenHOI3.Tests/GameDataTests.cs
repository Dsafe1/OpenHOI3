using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Godot;
using OpenHOI3.Core;
using OpenHOI3.Game;

namespace OpenHOI3.Tests;

public class DateTests
{
	[Theory]
	[InlineData("1936.1.1", 1936, 1, 1)]
	[InlineData("1944.12.16", 1944, 12, 16)]
	[InlineData(" 1940.5.10 ", 1940, 5, 10)]
	public void ParsesGameDates(string text, int year, int month, int day)
	{
		Assert.True(CDate.TryParse(text, out CDate date));
		Assert.Equal(new CDate(year, month, day), date);
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("1936")]
	[InlineData("1936.1")]
	[InlineData("1936.1.1.1")]
	[InlineData("1936.13.1")]      // month out of range
	[InlineData("1936.1.32")]      // day out of range
	[InlineData("nineteen.1.1")]
	public void RejectsMalformedDates(string? text)
	{
		Assert.False(CDate.TryParse(text, out _));
	}

	[Fact]
	public void OrdersChronologically()
	{
		var dates = new[]
		{
			new CDate(1939, 9, 1), new CDate(1936, 1, 1),
			new CDate(1939, 1, 1), new CDate(1936, 12, 31),
		};
		Array.Sort(dates);

		Assert.Equal(new[]
		{
			new CDate(1936, 1, 1), new CDate(1936, 12, 31),
			new CDate(1939, 1, 1), new CDate(1939, 9, 1),
		}, dates);
	}

	[Fact]
	public void RoundTripsThroughText()
	{
		Assert.Equal("1936.1.1", new CDate(1936, 1, 1).ToString());
	}

	[Theory]
	[InlineData(1, "January")]
	[InlineData(9, "September")]
	[InlineData(12, "December")]
	public void MapsMonthsToTheirLocalisationKeys(int month, string key)
	{
		// The game keys month names on the English word, not a number.
		Assert.Equal(key, new CDate(1936, month, 1).MonthKey);
	}

	[Fact]
	public void MonthKeyIsEmptyForAnImpossibleMonth()
	{
		Assert.Equal("", new CDate(1936, 13, 1).MonthKey);
	}
}

public class CountryTagTests
{
	[Theory]
	[InlineData("GER")]
	[InlineData("SOV")]
	[InlineData("U01")]
	public void AcceptsRealTags(string text)
	{
		Assert.True(CCountryTag.TryParse(text, out CCountryTag tag));
		Assert.Equal(text, tag.ToString());
	}

	[Theory]
	[InlineData("")]
	[InlineData(null)]
	[InlineData("GE")]
	[InlineData("GERM")]
	[InlineData("ger")]            // the files are consistently upper case
	[InlineData("G R")]
	public void RejectsAnythingElse(string? text)
	{
		Assert.False(CCountryTag.TryParse(text, out _));
	}
}

public class RgbParsingTests
{
	[Fact]
	public void ReadsByteComponents()
	{
		// The shape used by every country file.
		ClausewitzNode root = ClausewitzParser.Parse("color = { 64  160  167 }");
		Color color = root.GetRgb("color");

		Assert.Equal(64 / 255f, color.R, 3);
		Assert.Equal(160 / 255f, color.G, 3);
		Assert.Equal(167 / 255f, color.B, 3);
	}

	[Fact]
	public void ReadsFractionalComponents()
	{
		ClausewitzNode root = ClausewitzParser.Parse("color = { 0.25 0.5 1.0 }");
		Color color = root.GetRgb("color");

		Assert.Equal(0.25f, color.R, 3);
		Assert.Equal(1.0f, color.B, 3);
	}

	[Fact]
	public void FallsBackWhenMissingOrShort()
	{
		ClausewitzNode root = ClausewitzParser.Parse("a = { color = { 1 2 } }");
		Assert.Equal(Colors.Magenta, root.GetRgb("nope", Colors.Magenta));
		Assert.Equal(Colors.Magenta, root.Get("a")!.GetRgb("color", Colors.Magenta));
	}
}

/// <summary>Loaders exercised against a fixture install.</summary>
public class GameDataLoadingTests : IDisposable
{
	private readonly string _root;

	public GameDataLoadingTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-data-" + Guid.NewGuid().ToString("N"));
		Write("localisation/countries.csv",
			"GER;Germany;;;;;;;;;;;;;x\nENG;United Kingdom;;;;;;;;;;;;;x\n");
		Write("common/countries.txt", """
			# Please keep tags sorted
			GER	= "countries/Germany.txt"
			ENG	= "countries/United Kingdom.txt"
			""");
		Write("common/countries/Germany.txt", "color = { 64 160 167 }\ngraphical_culture = Western\n");
		Write("common/countries/United Kingdom.txt", "color = { 200 10 10 }\ngraphical_culture = Western\n");
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
		}
		GC.SuppressFinalize(this);
	}

	private void Write(string relative, string content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, Encoding.Latin1.GetBytes(content));
	}

	private CDirectorySettings Directories => CDirectorySettings.At(_root);

	private CLocalisation Localisation => CLocalisation.Load(Directories);

	/// <summary>
	/// Diagnostics are captured instead of reaching the engine. Godot's logging is a
	/// native call and takes the test host down when there is no engine behind it.
	/// </summary>
	private readonly List<string> _warnings = new();

	[Fact]
	public void LoadsCountriesWithNamesAndColours()
	{
		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);

		Assert.Equal(2, countries.Count);
		CCountry germany = countries.Find("GER")!;
		Assert.Equal("Germany", germany.Name);
		Assert.Equal("Western", germany.GraphicalCulture);
		Assert.Equal(64 / 255f, germany.Color.R, 3);
		Assert.Equal("gfx/flags/GER.tga", germany.FlagFile);
	}

	[Fact]
	public void CountryLookupIsCaseInsensitiveAndSafe()
	{
		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);
		Assert.NotNull(countries.Find("ger"));
		Assert.Null(countries.Find("ZZZ"));
		Assert.Null(countries.Find(""));
	}

	[Fact]
	public void CountriesComeBackSortedByName()
	{
		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);
		Assert.Equal(new[] { "Germany", "United Kingdom" },
			countries.All.Select(c => c.Name).ToArray());
	}

	[Fact]
	public void AnUnlocalisedCountryKeepsItsTagAsAName()
	{
		Write("common/countries.txt", "ZZZ = \"countries/Nowhere.txt\"\n");
		Write("common/countries/Nowhere.txt", "color = { 1 2 3 }\n");

		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);
		Assert.Equal("ZZZ", countries.Find("ZZZ")!.Name);
	}

	[Fact]
	public void LoadsBookmarksWithTheirCountryLists()
	{
		Write("common/bookmarks.txt", """
			bookmark = {
				icon = "GFX_bg_startdate_1939"
				name = "BLITZKRIEG_NAME"
				desc = "BLITZKRIEG_DESC"
				date = 1939.9.1
				country = ENG
				country = GER
			}
			bookmark = {
				icon = "GFX_bg_startdate_1936"
				name = "ROAD_TO_WAR_NAME"
				desc = "ROAD_TO_WAR_DESC"
				date = 1936.1.1
				country = GER
			}
			""");

		CBookmarks bookmarks = CBookmarks.Load(Directories, Localisation, _warnings.Add);

		Assert.Equal(2, bookmarks.Count);
		// Sorted oldest first, not file order.
		Assert.Equal(new CDate(1936, 1, 1), bookmarks.All[0].Date);
		// Repeated "country = TAG" lines, all of which must survive.
		Assert.Equal(2, bookmarks.All[1].Countries.Count);
		Assert.Equal("ENG", bookmarks.All[1].Countries[0].ToString());
	}

	[Fact]
	public void SkipsBookmarksWithoutAUsableDate()
	{
		Write("common/bookmarks.txt", """
			bookmark = { name = "BROKEN" date = "not a date" }
			bookmark = { name = "GOOD" date = 1936.1.1 }
			""");

		Assert.Equal(1, CBookmarks.Load(Directories, Localisation, _warnings.Add).Count);
		Assert.Contains(_warnings, w => w.Contains("BROKEN"));
	}

	[Fact]
	public void LoadsScenariosAndIgnoresSupportingFiles()
	{
		Write("scenarios/Battle_of_the_Bulge.txt", """
			name = "SCENARIO_bulge"
			desc = "SCENARIO_bulge_DESC"
			icon = "GFX_select_bulge"
			startdate = 1944.12.16
			enddate = 1945.4.1
			""");
		Write("scenarios/desert_fox.txt", "name = \"fox\"\nstartdate = 1941.3.24\n");
		// Supporting data lives beside the scenarios and carries no start date.
		Write("scenarios/faction_aims.txt", "some_other_data = { a = 1 }\n");

		CScenarios scenarios = CScenarios.Load(Directories, Localisation, _warnings.Add);

		Assert.Equal(2, scenarios.Count);
		Assert.Equal("desert_fox", scenarios.All[0].Id);          // earliest first
		CScenario bulge = scenarios.All[1];
		Assert.Equal("Battle_of_the_Bulge", bulge.Id);
		Assert.Equal(new CDate(1944, 12, 16), bulge.StartDate);
		Assert.Equal(new CDate(1945, 4, 1), bulge.EndDate);
	}

	[Fact]
	public void ExpansionScenarioReplacesTheBaseOne()
	{
		Write("scenarios/Fall_Blau.txt", "name = \"base\"\nstartdate = 1942.6.28\n");
		Write("tfh/scenarios/Fall_Blau.txt", "name = \"expansion\"\nstartdate = 1942.7.1\n");

		CScenarios scenarios = CScenarios.Load(Directories, Localisation, _warnings.Add);

		Assert.Equal(1, scenarios.Count);
		Assert.Equal(new CDate(1942, 7, 1), scenarios.All[0].StartDate);
	}
}
