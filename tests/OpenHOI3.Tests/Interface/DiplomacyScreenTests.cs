using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// The diplomacy screen's lists and what its panel says, against
/// <c>docs/mechanics/diplomacyscreen.md</c>, on <see cref="ScriptWorld"/>'s strip of ground: Germany
/// beside Poland, Italy alone in Africa, Slovakia and REB owning nothing; Germany and Italy in the Axis.
/// </summary>
public class DiplomacyScreenTests
{
	private static readonly Dictionary<string, string> Texts = new()
	{
		["AND"] = "and ",
		["ATWARWITH"] = "At war with $LIST$.",
		["FACTION_MEMBER_OF"] = "$NAME$ is a $FACTION$ member.",
		["ALLIEDWITH"] = "Allied with $LIST$.",
		["LEADVASSAL"] = "Has the following puppets: $LIST$.",
		["AMVASSAL"] = "Is a puppet of $LIST$.",
		["GUARANTEED"] = "Independence guaranteed by $LIST$.",
		["GUARANTING"] = "Guaranteeing the Independence of $LIST$.",
		["axis"] = "Axis",
		["national_socialist"] = "National Socialist",
		["national_socialist_GER"] = "NSDAP",
	};

	private static string Localise(string key) => Texts.GetValueOrDefault(key, key);

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static CCountry Named(string name) => new() { Tag = new CCountryTag("XXX"), Name = name, DefinitionFile = "" };

	private static CGameState State(bool atWar = false)
	{
		CIdeologyGroup[] groups = AlignmentTests.Groups();
		return ScriptWorld.State(atWar, groups);
	}

	// --- the lists ---

	[Fact]
	public void A_name_list_joins_with_commas_and_and_before_the_last()
	{
		Assert.Equal("A", CNameList.Join(Localise, new[] { "A" }));
		Assert.Equal("A and B", CNameList.Join(Localise, new[] { "A", "B" }));
		Assert.Equal("A, B and C", CNameList.Join(Localise, new[] { "A", "B", "C" }));
	}

	[Fact]
	public void By_name_compares_the_bytes_so_capitals_come_first_and_accents_last()
	{
		List<CCountry> sorted = CDiplomacyLists.ByName(new[] { Named("albania"), Named("Österreich"), Named("Zypern") });

		Assert.Equal(new[] { "Zypern", "albania", "Österreich" }, sorted.Select(country => country.Name));
	}

	[Fact]
	public void All_lists_every_country_owning_a_province_by_name()
	{
		CGameState state = State();

		Assert.Equal(new[] { "GER", "ITA", "POL" }, CDiplomacyLists.All(state).Select(country => country.Name));
	}

	[Fact]
	public void Majors_take_a_major_or_a_country_whose_provinces_come_to_more_than_100_IC()
	{
		CGameState state = State();
		C(state, ScriptWorld.Ger).BaseIc = 101;
		C(state, ScriptWorld.Pol).BaseIc = 100;

		Assert.Equal(new[] { ScriptWorld.Ger }, CDiplomacyLists.Majors(state).Select(country => country.Tag));
	}

	[Fact]
	public void Unaligned_neighbours_a_faction_and_a_continent()
	{
		CGameState state = State();
		CCountry germany = C(state, ScriptWorld.Ger);

		Assert.Equal(new[] { ScriptWorld.Pol }, CDiplomacyLists.Unaligned(state).Select(country => country.Tag));
		Assert.Equal(new[] { ScriptWorld.Pol }, CDiplomacyLists.Neighbours(state, germany).Select(country => country.Tag));
		Assert.Equal(new[] { "GER", "ITA" }, CDiplomacyLists.FactionMembers(state, "axis").Select(country => country.Name));
		Assert.Equal(new[] { "GER", "POL" }, CDiplomacyLists.OnContinent(state, "europe").Select(country => country.Name));
		Assert.Equal(new[] { "ITA" }, CDiplomacyLists.OnContinent(state, "africa").Select(country => country.Name));
		Assert.Equal(new[] { "europe", "africa" }, CDiplomacyLists.ListedContinents(state));
	}

	[Fact]
	public void A_sort_with_its_flag_set_puts_the_highest_first_and_names_Z_to_A()
	{
		CGameState state = State();
		CCountry germany = C(state, ScriptWorld.Ger);
		List<CCountry> all = CDiplomacyLists.All(state);
		CGameState.Status(germany, C(state, ScriptWorld.Pol)).Threat = CFixedPoint.FromInt(3);
		CGameState.Status(germany, C(state, ScriptWorld.Ita)).Threat = CFixedPoint.FromInt(1);

		Assert.Equal(new[] { "POL", "ITA", "GER" }, CDiplomacyLists.Sorted(state, germany, all, EDiplomacySort.Name, true).Select(c => c.Name));
		Assert.Equal(new[] { "POL", "ITA", "GER" }, CDiplomacyLists.Sorted(state, germany, all, EDiplomacySort.Threat, true).Select(c => c.Name));
		Assert.Equal(new[] { "GER", "ITA", "POL" }, CDiplomacyLists.Sorted(state, germany, all, EDiplomacySort.Threat, false).Select(c => c.Name));
		Assert.Equal(new[] { "GER", "ITA", "POL" }, all.Select(country => country.Name));      // the list is as it was
	}

	// --- the panel ---

	[Fact]
	public void A_row_and_the_panel_write_threats_and_relation_as_the_original_does()
	{
		CGameState state = State();
		CCountry germany = C(state, ScriptWorld.Ger);
		CCountry poland = C(state, ScriptWorld.Pol);
		CGameState.Status(poland, germany).Relation = CFixedPoint.Parse("-12.5");
		CGameState.Status(poland, germany).Threat = CFixedPoint.Parse("4.567");
		CGameState.Status(germany, poland).Threat = CFixedPoint.Parse("1.25");

		Assert.Equal("-12", CDiplomacyScreenText.Relation(poland, germany));
		Assert.Equal("4.56", CDiplomacyScreenText.ThreatUs(poland, germany));      // what Poland feels from the player
		Assert.Equal("1.25", CDiplomacyScreenText.ThreatThem(poland, germany));
		Assert.Equal("1.2", CDiplomacyScreenText.RowThreat(germany, poland));
	}

	[Fact]
	public void Our_neutrality_beside_another_country_is_red_over_the_ceiling_for_war_on_it()
	{
		CGameState state = ScriptWorld.State(groups: new[]
		{
			new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis", FactionNeutrality = CFixedPoint.FromInt(25) },
		});
		state.Defines.Set(CNeutrality.Section, CNeutrality.DifferentContinent, CFixedPoint.FromInt(10));
		CCountry germany = C(state, ScriptWorld.Ger);
		germany.EffectiveNeutrality = CFixedPoint.FromInt(60);

		// Italy is in Africa and no neighbour: 25 for the Axis, less 10.
		Assert.Equal("§R60%§W/15%", CDiplomacyScreenText.OurNeutrality(state, C(state, ScriptWorld.Ita), germany));
		Assert.Equal("60%", CDiplomacyScreenText.OurNeutrality(state, germany, germany));
		germany.EffectiveNeutrality = CFixedPoint.FromInt(15);
		// Poland is a neighbour: no penalty, and equal to the ceiling is allowed.
		Assert.Equal("§G15%§W/25%", CDiplomacyScreenText.OurNeutrality(state, C(state, ScriptWorld.Pol), germany));
	}

	[Fact]
	public void The_ruler_is_the_ideology_s_text_for_the_country_where_it_has_one()
	{
		CGameState state = State();
		C(state, ScriptWorld.Ger).RulingIdeology = "national_socialist";
		C(state, ScriptWorld.Ita).RulingIdeology = "national_socialist";

		Assert.Equal("NSDAP", CDiplomacyScreenText.Ruler(Localise, C(state, ScriptWorld.Ger)));
		Assert.Equal("National Socialist", CDiplomacyScreenText.Ruler(Localise, C(state, ScriptWorld.Ita)));
	}

	[Fact]
	public void The_surrender_bar_is_green_for_what_is_left()
	{
		Assert.Equal(100, CDiplomacyScreenText.SurrenderBar(0));
		Assert.Equal(99, CDiplomacyScreenText.SurrenderBar(5));
		Assert.Equal(0, CDiplomacyScreenText.SurrenderBar(1000));
	}

	[Fact]
	public void The_status_lines_come_in_order_white_and_yellow_in_turn()
	{
		CGameState state = State(atWar: true);
		CCountry germany = C(state, ScriptWorld.Ger);
		state.Alliances.Add(new CAlliance(ScriptWorld.Ger, ScriptWorld.Ita));
		state.Guarantees.Add(new CGuarantee(ScriptWorld.Ger, ScriptWorld.Ita));
		C(state, ScriptWorld.Ita).Overlord = ScriptWorld.Ger;

		Assert.Equal(new[]
		{
			"§WAt war with POL*.",
			"§YGER is a Axis member.",
			"§WAllied with ITA.",
			"§YHas the following puppets: ITA.",
			"§WGuaranteeing the Independence of ITA.",
		}, CDiplomacyScreenText.StatusLines(Localise, state, germany));
		Assert.Equal(new[]
		{
			"§WAt war with GER*.",
		}, CDiplomacyScreenText.StatusLines(Localise, state, C(state, ScriptWorld.Pol)));
		Assert.Equal(new[]
		{
			"§WITA is a Axis member.",
			"§YAllied with GER.",
			"§WIs a puppet of GER.",
			"§YIndependence guaranteed by GER.",
		}, CDiplomacyScreenText.StatusLines(Localise, state, C(state, ScriptWorld.Ita)));
	}

	[Fact]
	public void The_relation_list_sorts_by_relation_or_by_name_and_keeps_equals_in_order()
	{
		CGameState state = State();
		CCountry germany = C(state, ScriptWorld.Ger);
		CGameState.Status(germany, C(state, ScriptWorld.Pol)).Relation = CFixedPoint.FromInt(10);

		Assert.Equal(new[] { ("POL", 10L), ("ITA", 0L) },
			CDiplomacyScreenText.RelationRows(state, germany, byName: false, flag: true).Select(row => (row.Country.Name, row.Value)));
		Assert.Equal(new[] { "ITA", "POL" },
			CDiplomacyScreenText.RelationRows(state, germany, byName: false, flag: false).Select(row => row.Country.Name));
		Assert.Equal(new[] { "POL", "ITA" },
			CDiplomacyScreenText.RelationRows(state, germany, byName: true, flag: true).Select(row => row.Country.Name));
		Assert.Equal("@POLPOL", CDiplomacyScreenText.RelationName(C(state, ScriptWorld.Pol)));
	}

	[Fact]
	public void A_relation_is_coloured_from_red_through_olive_to_green()
	{
		Assert.Equal(new CColor(1f, 0f, 0.5f), CDiplomacyScreenText.RelationColour(-200));
		Assert.Equal(new CColor(0.5f, 0.5f, 0f), CDiplomacyScreenText.RelationColour(0));
		Assert.Equal(new CColor(0f, 1f, 0.5f), CDiplomacyScreenText.RelationColour(200));
	}

	[Fact]
	public void A_marker_stands_at_the_origin_and_the_place_on_the_triangle_cut_toward_nought()
	{
		CGameState state = State();
		CCountry germany = C(state, ScriptWorld.Ger);
		germany.AlignmentX = -1500;
		germany.AlignmentY = 146999;

		Assert.Equal((804, 611), CDiplomacyScreenText.MarkerPosition(805, 465, germany));
	}

	[Fact]
	public void The_continent_buttons_go_three_to_a_row()
	{
		Assert.Equal(new[] { (57, 133), (157, 133), (257, 133), (57, 153), (157, 153), (257, 153) },
			CDiplomacyScreenText.ContinentPlaces(6, (57, 133), (100, 0), (356, 20)));
	}

	[Fact]
	public void A_faction_s_frame_is_its_icon_and_none_outside_a_faction()
	{
		CGameState state = ScriptWorld.State(groups: new[] { new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis", FactionIcon = 2 } });

		Assert.Equal(2, CDiplomacyScreenText.FactionFrame(state, C(state, ScriptWorld.Ger)));
		Assert.Equal(0, CDiplomacyScreenText.FactionFrame(state, C(state, ScriptWorld.Pol)));
	}
}
