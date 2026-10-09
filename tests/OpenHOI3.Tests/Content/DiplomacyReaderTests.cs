using System;
using System.Collections.Generic;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Content.History;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The reading of <c>history/diplomacy</c>, against <c>docs/mechanics/supply.md</c>,
/// section 3. The text is written here in the files' own shape and is not a copy of them.
/// </summary>
public class DiplomacyReaderTests
{
	private readonly Dictionary<string, CCountry> _countries = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<CAlliance> _alliances = new();
	private readonly SortedSet<string> _unknownKeys = new();
	private readonly SortedSet<string> _unknownTags = new();

	public DiplomacyReaderTests()
	{
		foreach (string tag in new[] { "JAP", "MAN", "MEN", "ENG", "FRA" })
		{
			_countries[tag] = new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "" };
		}
	}

	private void Read(string text, CDate date)
	{
		CDiplomacyReader.Read(ClausewitzParser.Parse(text), tag => tag is not null && _countries.TryGetValue(tag, out CCountry? found)
			? found : null, date, _alliances, _unknownKeys, _unknownTags);
	}

	[Fact]
	public void A_puppet_is_its_masters_from_the_first_day_and_not_on_the_last()
	{
		const string text = @"
			vassal = { first = JAP second = MAN start_date = 1936.1.1 end_date = 1945.8.20 }
			vassal = { first = JAP second = MEN start_date = 1936.5.12 end_date = 1945.9.2 }
		";

		Read(text, new CDate(1936, 1, 1));
		Assert.Equal(new CCountryTag("JAP"), _countries["MAN"].Overlord);
		Assert.Null(_countries["MEN"].Overlord);
		Assert.Null(_countries["JAP"].Overlord);

		Read(text, new CDate(1936, 5, 12));
		Assert.Equal(new CCountryTag("JAP"), _countries["MEN"].Overlord);

		_countries["MAN"].Overlord = null;
		Read(text, new CDate(1945, 8, 20));
		Assert.Null(_countries["MAN"].Overlord);
	}

	[Fact]
	public void An_alliance_is_kept_and_reads_the_same_from_either_end()
	{
		Read("alliance = { first = ENG second = FRA start_date = 1936.1.1 end_date = 1948.1.1 }", new CDate(1936, 1, 1));

		Assert.Single(_alliances);
		Assert.True(_alliances[0].Joins(new CCountryTag("FRA"), new CCountryTag("ENG")));
		Assert.False(_alliances[0].Joins(new CCountryTag("FRA"), new CCountryTag("JAP")));
	}

	[Fact]
	public void What_nothing_uses_is_read_past_and_what_is_not_known_is_reported()
	{
		Read(@"
			guarantee = { first = ENG second = FRA start_date = 1936.1.1 end_date = 1948.1.1 }
			nap = { first = ENG second = JAP start_date = 1936.1.1 end_date = 1948.1.1 }
			relation = { first = ENG second = JAP value = 50 }
			treaty = { first = ENG second = JAP }
			vassal = { first = ENG second = XXX start_date = 1936.1.1 end_date = 1948.1.1 }
		", new CDate(1936, 1, 1));

		Assert.Empty(_alliances);
		Assert.Equal(new[] { "treaty" }, _unknownKeys);
		Assert.Equal(new[] { "XXX" }, _unknownTags);
	}
}
