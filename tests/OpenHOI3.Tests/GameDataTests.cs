using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

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
	/// <summary>A fallback no game file would produce, so a hit on it is unmistakable.</summary>
	private static readonly CColor Magenta = new(1f, 0f, 1f);

	[Fact]
	public void ReadsByteComponents()
	{
		// The shape used by every country file.
		ClausewitzNode root = ClausewitzParser.Parse("color = { 64  160  167 }");
		CColor color = root.GetRgb("color");

		Assert.Equal(64 / 255f, color.R, 3);
		Assert.Equal(160 / 255f, color.G, 3);
		Assert.Equal(167 / 255f, color.B, 3);
	}

	[Fact]
	public void ReadsFractionalComponents()
	{
		ClausewitzNode root = ClausewitzParser.Parse("color = { 0.25 0.5 1.0 }");
		CColor color = root.GetRgb("color");

		Assert.Equal(0.25f, color.R, 3);
		Assert.Equal(1.0f, color.B, 3);
	}

	[Fact]
	public void FallsBackWhenMissingOrShort()
	{
		ClausewitzNode root = ClausewitzParser.Parse("a = { color = { 1 2 } }");
		Assert.Equal(Magenta, root.GetRgb("nope", Magenta));
		Assert.Equal(Magenta, root.Get("a")!.GetRgb("color", Magenta));
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
	/// Diagnostics are captured rather than left to the default sink, so a test can
	/// assert on what a loader reported.
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

}
