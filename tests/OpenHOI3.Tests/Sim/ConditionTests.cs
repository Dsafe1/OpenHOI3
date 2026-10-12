using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;
using OpenHOI3.Tests.Content;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a condition answers, against <c>docs/mechanics/triggers.md</c>, on <see cref="ScriptWorld"/>'s
/// strip of ground.
/// </summary>
public class ConditionTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;
	private static readonly CCountryTag Slo = ScriptWorld.Slo;
	private static readonly CCountryTag Reb = ScriptWorld.Reb;

	private static CGameState State(bool atWar = false) => ScriptWorld.State(atWar);

	private static CTrigger Read(string text) => ScriptWorld.Condition(text);

	private static bool Holds(CGameState state, string text, CEventScope scope, CScriptReport? report = null)
	{
		return CConditions.Holds(state, Read(text), scope, report);
	}

	private static bool Holds(CGameState state, string text, CCountryTag? country = null, int province = 0)
	{
		return Holds(state, text, CEventScope.Start(country ?? Ger, province));
	}

	[Fact]
	public void An_empty_and_holds_an_empty_or_does_not_and_not_is_a_nor()
	{
		CGameState state = State();

		Assert.True(Holds(state, "and = { }"));
		Assert.False(Holds(state, "or = { }"));
		Assert.True(Holds(state, "not = { }"));
		Assert.False(Holds(state, "not = { always = no always = yes }"));
		Assert.True(Holds(state, "not = { always = no tag = POL }"));
	}

	[Fact]
	public void Only_the_word_yes_is_yes()
	{
		CGameState state = State();

		Assert.True(Holds(state, "always = yes"));
		Assert.False(Holds(state, "always = 1"));
	}

	[Fact]
	public void What_is_not_built_answers_false_whatever_is_written_and_is_reported()
	{
		CGameState state = State();
		var report = new CScriptReport();

		Assert.False(Holds(state, "war_exhaustion = 0", CEventScope.Start(Ger), report));
		Assert.False(Holds(state, "government_in_exile = no", CEventScope.Start(Ger), report));
		Assert.True(Holds(state, "not = { war_exhaustion = 0 }", CEventScope.Start(Ger), report));
		Assert.False(Holds(state, "is_attacker = yes", CEventScope.Start(Ger), report));

		Assert.Equal((EScriptGap.Unbuilt, 2), report.Gaps["war_exhaustion"]);
		Assert.Equal(EScriptGap.Unbuilt, report.Gaps["government_in_exile"].Why);
		Assert.Equal(EScriptGap.BattleOnly, report.Gaps["is_attacker"].Why);
	}

	[Fact]
	public void A_provinces_number_throws_the_country_away_and_owner_brings_it_back()
	{
		CGameState state = State(atWar: true);

		Assert.False(Holds(state, "4 = { war = yes }"));
		Assert.True(Holds(state, "4 = { owner = { war = yes } }"));
		Assert.True(Holds(state, "4 = { owns = 6 }"));
	}

	[Fact]
	public void A_scope_that_cannot_be_made_is_false_even_with_nothing_inside()
	{
		CGameState state = State();

		Assert.False(Holds(state, "6 = { owner = { } }"));
		Assert.True(Holds(state, "4 = { owner = { } }"));
		Assert.False(Holds(state, "POL = { ally = { } }"));
	}

	[Fact]
	public void From_and_this_keep_their_meaning_at_any_depth()
	{
		CGameState state = State();
		CEventScope scope = CEventScope.Start(Ger, fromCountry: Pol);

		Assert.True(Holds(state, "ITA = { 4 = { from = { tag = POL } this = { tag = GER } } }", scope));
		Assert.True(Holds(state, "ITA = { tag = this }", CEventScope.Start(Ita)));
		Assert.False(Holds(state, "POL = { tag = this }", scope));
	}

	[Fact]
	public void The_month_is_counted_from_nought_and_the_date_may_look_ahead()
	{
		CGameState state = State();

		Assert.True(Holds(state, "month = 5 year = 1936"));
		Assert.False(Holds(state, "month = 6"));
		Assert.False(Holds(state, "date = 1936.6.20"));
		Assert.True(Holds(state, "date = 1936.6.20", CEventScope.Start(Ger, lookAheadDays: 30)));
		Assert.True(Holds(state, "date = 1936.june.20"));
	}

	[Fact]
	public void War_with_the_rebels_holds_for_every_country_but_the_rebels()
	{
		CGameState state = State();

		Assert.True(Holds(state, "war_with = REB"));
		Assert.False(Holds(state, "war_with = REB", Reb));
		Assert.False(Holds(state, "war_with = POL"));
		Assert.True(Holds(State(atWar: true), "war_with = POL"));
		Assert.False(Holds(State(atWar: true), "war_with = GER"));
	}

	[Fact]
	public void A_neighbouring_province_must_have_an_owner()
	{
		CGameState state = State();

		Assert.False(Holds(state, "any_neighbor_province = { empty = yes }", Pol, 4));
		Assert.True(Holds(state, "any_neighbor_province = { owned_by = GER }", Pol, 4));
		Assert.True(Holds(state, "has_empty_adjacent_province = no", Pol, 4));
		Assert.True(Holds(state, "has_empty_adjacent_province = yes", Ger, 1));
	}

	[Fact]
	public void Neighbour_asks_the_other_countrys_list_which_an_occupied_province_leaves_out()
	{
		CGameState state = State();
		state.Province(3).Controller = Pol;

		Assert.True(Holds(state, "neighbour = GER", Pol));
		Assert.False(Holds(state, "neighbour = POL", Ger));
		Assert.True(Holds(state, "any_neighbor_country = { tag = POL }", Ger));
	}

	[Fact]
	public void Any_core_is_over_the_countries_with_a_core_on_the_province()
	{
		CGameState state = State();

		Assert.True(Holds(state, "any_core = { tag = SLO }", Ger, 3));
		Assert.False(Holds(state, "any_core = { tag = SLO }", Ger, 0));
		Assert.True(Holds(state, "is_core = 3"));
		Assert.False(Holds(state, "is_core = 4"));
		Assert.True(Holds(state, "is_core = SLO", Ger, 3));
	}

	[Fact]
	public void A_nation_could_be_released_from_a_core_the_country_holds_other_than_its_capital()
	{
		CGameState state = State();

		Assert.True(Holds(state, "can_create_vassals = no"));
		Assert.True(Holds(state, "is_possible_vassal = SLO"));
		Assert.False(Holds(state, "can_create_vassals = yes", Pol));
	}

	[Fact]
	public void A_faction_is_asked_only_of_a_country_and_none_is_never_one()
	{
		CGameState state = State();

		Assert.True(Holds(state, "faction = axis"));
		Assert.False(Holds(state, "faction = none", Pol));
		Assert.False(Holds(state, "4 = { faction = axis }"));
		Assert.True(Holds(state, "ITA = { faction = this }"));
		Assert.True(Holds(state, "num_in_faction = 2 num_of_allies = 1 is_in_any_faction = yes"));
	}

	[Fact]
	public void Ally_draws_by_chance_and_moves_the_state_of_the_scope_it_was_asked_in()
	{
		CGameState state = State();
		CEventScope scope = CEventScope.Start(Ger, randomState: 7);

		Assert.True(Holds(state, "ally = { tag = ITA }", scope));
		Assert.NotEqual(7u, scope.RandomState);
	}

	[Fact]
	public void A_draw_is_the_product_of_two_steps_over_the_count()
	{
		// From 12345 the two steps' outputs multiply to 145339776, and the state is left at 1255958651.
		CEventScope scope = CEventScope.Start(Ger, randomState: 12345);

		Assert.Equal(776, scope.Draw(1000));
		Assert.Equal(1255958651u, scope.RandomState);
	}

	[Fact]
	public void Manpower_percentage_is_a_share_and_max_manpower_greater_than_reads_the_manpower()
	{
		CGameState state = State();

		Assert.True(Holds(state, "manpower_percentage = 0.25"));
		Assert.False(Holds(state, "manpower_percentage = 25"));
		Assert.False(Holds(state, "max_manpower_greater_than = 100"));
		Assert.True(Holds(state, "max_manpower = 100"));
		Assert.False(Holds(state, "manpower_percentage = 0", Pol));
	}

	[Fact]
	public void Names_ask_for_a_law_a_post_and_a_level_built()
	{
		CGameState state = State();

		Assert.True(Holds(state, "press_laws = free_press head_of_state = 1001"));
		Assert.False(Holds(state, "conscription_law = service_by_requirement"));
		Assert.True(Holds(state, "industry = 3 has_building = industry", Ger, 2));
		Assert.False(Holds(state, "has_building = industry", Ger, 1));
		Assert.True(Holds(state, "infantry_brigade = 0"));
		Assert.False(Holds(state, "infantry_brigade = 1"));
	}

	[Fact]
	public void Controlled_by_owner_asks_whether_the_province_is_occupied()
	{
		CGameState state = State();
		state.Province(3).Controller = Pol;

		Assert.False(Holds(state, "controlled_by = owner", Ger, 3));
		Assert.True(Holds(state, "controlled_by = owner", Ger, 2));
		Assert.False(Holds(state, "controlled_by = owner", Ger, 0));
		Assert.True(Holds(state, "owned_by = owner", Ger, 6));
	}

	[Fact]
	public void Exists_with_a_tag_does_not_read_what_else_is_written()
	{
		CGameState state = State();

		Assert.False(Holds(state, "exists = SLO"));
		Assert.True(Holds(state, "exists = POL"));
		Assert.True(Holds(state, "exists = no", Slo));
	}

	[Fact]
	public void A_region_is_a_set_and_a_membership()
	{
		CGameState state = State();

		Assert.True(Holds(state, "sudetenland = { province_id = 2 }"));
		Assert.False(Holds(state, "sudetenland = { province_id = 3 }"));
		Assert.True(Holds(state, "region = sudetenland", Ger, 1));
		Assert.False(Holds(state, "region = sudetenland", Ger, 0));
	}

	[Fact]
	public void Enemy_is_false_where_there_is_no_battle()
	{
		Assert.False(Holds(State(), "enemy = { }"));
	}

	[Fact]
	public void Is_capital_no_holds_where_there_is_no_province()
	{
		CGameState state = State();

		Assert.True(Holds(state, "is_capital = yes", Ger, 1));
		Assert.True(Holds(state, "is_capital = no", Ger, 0));
		Assert.True(Holds(state, "capital = 1"));
	}
}
