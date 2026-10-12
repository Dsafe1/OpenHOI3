using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What an effect does, against <c>docs/mechanics/effects.md</c>, on <see cref="ScriptWorld"/>'s strip
/// of ground.
/// </summary>
public class EffectTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;

	private static void Run(CGameState state, string text, CEventScope scope, CScriptReport? report = null)
	{
		CEffects.Run(state, ScriptWorld.Effects(text), scope, report);
	}

	private static void Run(CGameState state, string text, CCountryTag? country = null, int province = 0)
	{
		Run(state, text, CEventScope.Start(country ?? Ger, province));
	}

	private static bool Holds(CGameState state, string text, CCountryTag? country = null, int province = 0)
	{
		return CConditions.Holds(state, ScriptWorld.Condition(text), CEventScope.Start(country ?? Ger, province));
	}

	private static CCountry Country(CGameState state, CCountryTag tag) => state.Country(tag)!;

	[Fact]
	public void A_scope_that_cannot_be_made_runs_nothing_and_what_follows_runs()
	{
		CGameState state = State();

		Run(state, "6 = { owner = { set_country_flag = lost } } set_country_flag = after");

		Assert.True(Holds(state, "has_country_flag = after"));
		Assert.False(Holds(state, "has_country_flag = lost"));
	}

	[Fact]
	public void A_limit_inside_a_scope_does_nothing_and_the_effects_beside_it_run()
	{
		CGameState state = State();

		Run(state, "GER = { limit = { tag = POL } set_country_flag = anyway }", Pol);

		Assert.True(Holds(state, "has_country_flag = anyway"));
	}

	[Fact]
	public void A_flag_is_matched_without_case_and_a_cleared_one_answers_no()
	{
		CGameState state = State();

		Run(state, "set_country_flag = Spanish_War clr_country_flag = never_set set_global_flag = g set_province_flag = p", Ger, 2);

		Assert.True(Holds(state, "has_country_flag = spanish_war"));
		Assert.True(Holds(state, "has_global_flag = G", Pol));
		Assert.True(Holds(state, "has_province_flag = p", Pol, 2));
		Assert.False(Holds(state, "has_province_flag = p", Pol, 3));
		Run(state, "clr_country_flag = SPANISH_WAR");
		Assert.False(Holds(state, "has_country_flag = spanish_war"));
		Assert.False(Country(state, Ger).Flags.All.ContainsKey("never_set"));
	}

	[Fact]
	public void A_variable_is_set_added_to_and_made_by_asking_for_it()
	{
		CGameState state = State();

		Run(state, "set_variable = { which = BaseIC value = 2.5 } change_variable = { which = baseic value = 1 }");

		Assert.Equal(CFixedPoint.Parse("3.5"), Country(state, Ger).Variables["BaseIC"]);
		Assert.True(Holds(state, "check_variable = { which = never value = 0 }"));
		Assert.True(Country(state, Ger).Variables.ContainsKey("never"));
		Assert.False(Holds(state, "check_variable = { which = BaseIC value = 4 }"));
	}

	[Fact]
	public void A_modifier_given_twice_is_held_once_and_a_negative_time_lasts_for_ever()
	{
		CGameState state = State();

		Run(state, "add_country_modifier = { name = us_new_deal duration = -1 } add_country_modifier = { name = us_new_deal duration = 5 }");
		CCountry germany = Country(state, Ger);

		Assert.Single(germany.EventModifiers);
		Assert.True(Holds(state, "has_country_modifier = us_new_deal"));
		for (int i = 0; i < 24 * 400; i++)
		{
			state.AdvanceOneHour();
		}
		CCountryModifiers.Rebuild(state);
		Assert.Equal(CFixedPoint.Parse("0.1"), germany.Modifier["ic"]);

		Run(state, "remove_country_modifier = us_new_deal");
		Assert.Empty(germany.EventModifiers);
	}

	[Fact]
	public void A_modifier_for_some_days_goes_at_the_first_rebuild_after_them()
	{
		CGameState state = State();

		Run(state, "add_country_modifier = { name = us_new_deal duration = 2 } add_country_modifier = { name = no_such duration = 2 }");
		CCountry germany = Country(state, Ger);
		Assert.Single(germany.EventModifiers);

		for (int i = 0; i < 24 * 2 + 1; i++)
		{
			state.AdvanceOneHour();
		}
		CCountryModifiers.Rebuild(state);
		Assert.Empty(germany.EventModifiers);
		Assert.Equal(CFixedPoint.Zero, germany.Modifier["ic"]);
	}

	[Fact]
	public void Figures_are_floored_and_national_unity_is_held_between_1_and_100()
	{
		CGameState state = State();
		CCountry germany = Country(state, Ger);
		germany.NationalUnity = CFixedPoint.FromInt(50);

		Run(state, "dissent = -3 manpower = -80 national_unity = 70 officer_pool = 2.5");

		Assert.Equal(CFixedPoint.Zero, germany.Dissent);
		Assert.Equal(CFixedPoint.Zero, germany.Manpower);
		Assert.Equal(CFixedPoint.FromInt(100), germany.NationalUnity);
		Assert.Equal(CFixedPoint.Parse("2.5"), germany.Officers);
		Run(state, "national_unity = -200");
		Assert.Equal(CFixedPoint.One, germany.NationalUnity);
	}

	[Fact]
	public void This_and_from_are_nought_to_a_figure_and_capital_this_is_province_0()
	{
		CGameState state = State();

		Run(state, "manpower = this capital = this");

		Assert.Equal(CFixedPoint.FromInt(50), Country(state, Ger).Manpower);
		Assert.Equal(0, Country(state, Ger).Capital);
	}

	[Fact]
	public void Goods_go_to_a_provinces_pool_or_its_yield_and_otherwise_to_the_capital()
	{
		CGameState state = State();

		Run(state, "money = 2.5 metal = 3", Ger, 2);
		Run(state, "supplies = 4", Ger);

		Assert.Equal(CFixedPoint.FromInt(2), state.Province(2).Pool[EGoods.Money]);
		Assert.Equal(CFixedPoint.FromInt(3), state.Province(2).MaxProducing[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Zero, state.Province(2).Pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(4), state.Province(1).Pool[EGoods.Supplies]);
	}

	[Fact]
	public void A_core_by_number_is_the_countrys_and_by_tag_is_on_the_scopes_province()
	{
		CGameState state = State();

		Run(state, "add_core = 4 add_core = 4", Ger);
		Run(state, "add_core = ITA", Ger, 2);
		Run(state, "remove_core = SLO", Ger, 3);

		Assert.Single(state.Province(4).Cores, Ger);
		Assert.Contains(ScriptWorld.Ita, state.Province(2).Cores);
		Assert.DoesNotContain(ScriptWorld.Slo, state.Province(3).Cores);
	}

	[Fact]
	public void Seceding_gives_the_province_and_its_control_and_the_controller_needs_an_owner()
	{
		CGameState state = State();

		Run(state, "secede_province = POL", Ger, 3);
		Run(state, "change_controller = GER", Pol, 6);

		Assert.Equal(Pol, state.Province(3).Owner);
		Assert.Equal(Pol, state.Province(3).Controller);
		Assert.Null(state.Province(6).Controller);
		Run(state, "change_controller = GER", Pol, 4);
		Assert.Equal(Ger, state.Province(4).Controller);
		Assert.Equal(Pol, state.Province(4).Owner);
	}

	[Fact]
	public void A_building_adds_whole_levels_and_a_naval_base_needs_a_port()
	{
		CGameState state = State();
		state.Province(2).SetBuildingCurrentLevel(ScriptWorld.Industry, CFixedPoint.One);

		Run(state, "industry = 2 naval_base = 1", Ger, 2);
		Run(state, "naval_base = 1", Ger, 1);
		Run(state, "industry = 4", Ger);

		Assert.Equal(CFixedPoint.FromInt(5), state.Province(2).BuildingMaxLevel(ScriptWorld.Industry));
		Assert.Equal(CFixedPoint.FromInt(5), state.Province(2).BuildingLevel(ScriptWorld.Industry));
		Assert.Equal(CFixedPoint.Zero, state.Province(2).BuildingMaxLevel(ScriptWorld.NavalBase));
		Assert.Equal(CFixedPoint.One, state.Province(1).BuildingMaxLevel(ScriptWorld.NavalBase));
		Assert.Equal(CFixedPoint.Zero, state.Province(0).BuildingMaxLevel(ScriptWorld.Industry));
	}

	[Fact]
	public void A_post_is_given_to_a_minister_declared_for_it()
	{
		CGameState state = State();

		Run(state, "head_of_state = 1002");
		Assert.Equal(1002, Country(state, Ger).Government["head_of_state"].Id);
		Run(state, "head_of_state = 9999");
		Assert.Equal(1002, Country(state, Ger).Government["head_of_state"].Id);
	}

	[Fact]
	public void End_war_takes_the_named_country_out_and_leaves_the_scopes()
	{
		CGameState state = State(atWar: true);

		Run(state, "end_war = GER", Pol);

		Assert.Empty(state.Wars[0].Attackers);
		Assert.Contains(Pol, state.Wars[0].Defenders);
	}

	[Fact]
	public void Random_list_runs_the_first_entry_whose_running_sum_the_roll_does_not_pass()
	{
		// From 12345 the roll over 1000 is 776: past the first 500, within the second.
		CGameState state = State();
		CEventScope scope = CEventScope.Start(Ger, randomState: 12345);

		Run(state, "random_list = { 500 = { set_country_flag = first } 500 = { set_country_flag = second } }", scope);

		Assert.True(Holds(state, "has_country_flag = second"));
		Assert.False(Holds(state, "has_country_flag = first"));
		Assert.Equal(1255958651u, scope.RandomState);
	}

	[Fact]
	public void Random_moves_the_state_whether_it_runs_or_not()
	{
		CGameState state = State();
		CEventScope scope = CEventScope.Start(Ger, randomState: 7);

		Run(state, "random = { chance = 0 set_country_flag = never }", scope);

		Assert.NotEqual(7u, scope.RandomState);
		Assert.False(Holds(state, "has_country_flag = never"));
	}

	[Fact]
	public void Any_owned_gives_every_province_the_same_roll()
	{
		for (uint seed = 1; seed < 40; seed++)
		{
			CGameState state = State();
			CEventScope scope = CEventScope.Start(Ger, randomState: seed);

			Run(state, "any_owned = { random = { chance = 50 set_province_flag = hit } }", scope);

			int hit = 0;
			for (int province = 1; province <= 3; province++)
			{
				hit += state.Province(province).Flags.IsSet("hit") ? 1 : 0;
			}
			Assert.True(hit is 0 or 3);
			Assert.Equal(seed, scope.RandomState);
		}
	}

	[Fact]
	public void Any_country_takes_in_countries_that_own_nothing_and_random_country_does_not()
	{
		CGameState state = State();

		Run(state, "any_country = { set_country_flag = seen }");
		Run(state, "random_country = { limit = { tag = SLO } set_country_flag = drawn }");

		Assert.False(Country(state, Ger).Flags.IsSet("seen"));
		Assert.True(Country(state, ScriptWorld.Slo).Flags.IsSet("seen"));
		Assert.True(Country(state, Pol).Flags.IsSet("seen"));
		Assert.False(Country(state, ScriptWorld.Slo).Flags.IsSet("drawn"));
	}

	[Fact]
	public void What_is_not_built_does_nothing_and_is_reported()
	{
		CGameState state = State();
		var report = new CScriptReport();

		Run(state, "relation = { who = POL value = 10 } neutrality = 5 country_event = 1001", CEventScope.Start(Ger), report);

		Assert.Equal(EScriptGap.Unbuilt, report.Gaps["relation"].Why);
		Assert.Equal(EScriptGap.Unbuilt, report.Gaps["country_event"].Why);
		Assert.Equal(3, report.Gaps.Count);
	}

	private static CGameState State(bool atWar = false) => ScriptWorld.State(atWar);
}
