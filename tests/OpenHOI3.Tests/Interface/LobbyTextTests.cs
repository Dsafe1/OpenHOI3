using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Interface;

/// <summary>What the lobby says, as <c>docs/mechanics/frontend.md</c>, section 3, has it.</summary>
public class LobbyTextTests
{
	private static readonly Dictionary<string, string> Texts = new()
	{
		["WORLD_YEAR"] = "§Y$NAME$§W, $DAY$ of $MONTH$, $YEAR$",
		["ATWAR"] = "At War.",
		["ATPEACE"] = "At Peace.",
		["NOT_SELECT"] = "All players must select a country.",
		["NOT_EXIST"] = "$TAG$ does not exist at this time.",
		["CANT_START_REASON_SELECT_SAVE_GAME"] = "Please select the Save Game first.",
		["CANT_START_REASON_SELECT_BOOKMARK"] = "Select a bookmark.",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CGameState World()
	{
		var germany = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var poland = new CCountry { Tag = new CCountryTag("POL"), Name = "Poland", DefinitionFile = "" };
		var austria = new CCountry { Tag = new CCountryTag("AUS"), Name = "Austria", DefinitionFile = "" };
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false),
			new CProvince(1, "Berlin", isSea: false) { Owner = germany.Tag, Controller = germany.Tag },
			new CProvince(2, "Warsaw", isSea: false) { Owner = poland.Tag, Controller = poland.Tag },
		};
		return new CGameState(new CDate(1936, 1, 1), provinces, new[] { germany, poland, austria });
	}

	[Fact]
	public void The_year_label_names_the_start_and_the_date_and_a_row_its_hour()
	{
		Assert.Equal("§YThe Road to War§W, 1 of January, 1936", CLobbyText.YearLabel(Localise, "The Road to War", new CDate(1936, 1, 1)));
		Assert.True(OpenHOI3.Content.Saves.CSaveValues.TryHours("1941.7.16.18", out long hours));
		Assert.Equal(" 18:00, 16 July, 1941", CLobbyText.Date(Localise, hours));
	}

	[Fact]
	public void At_war_with_anyone_says_At_War_and_names_nobody()
	{
		CGameState state = World();
		CCountry germany = state.Country(new CCountryTag("GER"))!;
		Assert.Equal("At Peace.", CLobbyText.Wars(Localise, state, germany));
		var war = new CWar("War", new CDate(1939, 9, 1));
		war.Attackers.Add(germany.Tag);
		war.Defenders.Add(new CCountryTag("POL"));
		state.Wars.Add(war);

		Assert.Equal("At War.", CLobbyText.Wars(Localise, state, germany));
	}

	[Fact]
	public void Play_wants_a_country_owning_land_and_on_each_tab_its_own_kind_of_start()
	{
		CGameState state = World();
		CCountry germany = state.Country(new CCountryTag("GER"))!;

		// In order: no country, a country of no land, the saves' tab with none loaded, the start's tab with one loaded.
		Assert.Equal("All players must select a country.", CLobbyText.WhyNotPlay(Localise, state, null, false, false));
		Assert.Equal("Austria does not exist at this time.",
			CLobbyText.WhyNotPlay(Localise, state, state.Country(new CCountryTag("AUS")), false, false));
		Assert.Equal("Please select the Save Game first.", CLobbyText.WhyNotPlay(Localise, state, germany, true, false));
		Assert.Equal("Select a bookmark.", CLobbyText.WhyNotPlay(Localise, state, germany, false, true));
		Assert.Null(CLobbyText.WhyNotPlay(Localise, state, germany, false, false));
		Assert.Null(CLobbyText.WhyNotPlay(Localise, state, germany, true, true));
	}
}
