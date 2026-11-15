using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The AI strategy, against <c>docs/mechanics/aistrategy.md</c>: its lists' adding, cutting and scaling, the
/// personality, the threat, antagonism, rival and protection terms, the walk of the victory conditions, when a strategy
/// is made and the prepared wars it checks. Germany owns ten provinces in a row, 1 to 10, its capital Berlin (1), under a
/// dictatorship and in the axis; Poland owns 11 and 12, 11 beside Berlin; Luxembourg owns 13 beside 10; Britain owns 14,
/// far off, in the allies. Poland, Luxembourg and Britain are democracies. Nobody has a unit.
/// </summary>
public class AiStrategyTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Lux = new("LUX");
	private static readonly CCountryTag Eng = new("ENG");

	private static CGameState World(int tiny = 0, string germanGovernment = "dictatorship", CRegion[]? regions = null)
	{
		var fascism = new CIdeologyGroup("fascism", 200000, 200000) { Faction = "axis" };
		fascism.IdeologyOrder.Add("national_socialist");
		fascism.Ideologies.Add("national_socialist");
		var democracy = new CIdeologyGroup("democracy", 0, -146000) { Faction = "allies" };
		democracy.IdeologyOrder.Add("social_conservative");
		democracy.Ideologies.Add("social_conservative");
		var dictatorship = new CGovernment("dictatorship");
		dictatorship.Accepted.Add("national_socialist");
		var republic = new CGovernment("republic");
		republic.Accepted.Add("social_conservative");

		CCountry Country(CCountryTag tag, int capital, string government, string ruling) =>
			new() { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital, GovernmentType = government, RulingIdeology = ruling };
		CCountry germany = Country(Ger, 1, germanGovernment, germanGovernment == "dictatorship" ? "national_socialist" : "social_conservative");
		germany.Faction = "axis";
		CCountry britain = Country(Eng, 14, "republic", "social_conservative");
		britain.Faction = "allies";
		var countries = new List<CCountry>
		{
			germany, Country(Pol, 11, "republic", "social_conservative"), Country(Lux, 13, "republic", "social_conservative"), britain,
		};

		CProvince Land(int id, CCountryTag owner, int centre, params int[] beside) =>
			new(id, $"p{id}", isSea: false) { Owner = owner, Controller = owner, Neighbours = beside, CentreX = centre };
		var provinces = new List<CProvince> { new(0, "", isSea: false) };
		for (int id = 1; id <= 10; id++)
		{
			var beside = new List<int>();
			if (id > 1)
			{
				beside.Add(id - 1);
			}
			if (id < 10)
			{
				beside.Add(id + 1);
			}
			if (id == 1)
			{
				beside.Add(11);
			}
			if (id == 10)
			{
				beside.Add(13);
			}
			if (id <= tiny)
			{
				beside.Add(14 + id);
			}
			provinces.Add(Land(id, Ger, id * 10, beside.ToArray()));
		}
		provinces.Add(Land(11, Pol, 0, 1, 12));
		provinces.Add(Land(12, Pol, 0, 11));
		provinces.Add(Land(13, Lux, 110, 10));
		provinces.Add(Land(14, Eng, 4000));
		for (int i = 1; i <= tiny; i++)
		{
			var tag = new CCountryTag("T" + (char)('A' + i - 1) + "X");
			countries.Add(Country(tag, 14 + i, "republic", "social_conservative"));
			provinces.Add(Land(14 + i, tag, i * 10, i));
		}
		TestRoads.Pave(provinces);
		return new CGameState(new CDate(1936, 1, 1), provinces.ToArray(), countries.ToArray(), buildings: new[] { TestRoads.Infra })
		{
			IdeologyGroups = new[] { fascism, democracy },
			Governments = new Dictionary<string, CGovernment> { [dictatorship.Name] = dictatorship, [republic.Name] = republic },
			MapWidth = 100000,
			Regions = regions ?? System.Array.Empty<CRegion>(),
		};
	}

	private static CAiStrategy Made(CGameState state, CCountryTag tag)
	{
		CCountry country = state.Country(tag)!;
		Assert.True(CAiStrategies.Make(state, country));
		return country.Strategy;
	}

	private static string Text<T>(CStrategyList<T> list) where T : notnull =>
		string.Join(" ", list.Entries.Select(e => $"{e.Key} {e.Weight}"));

	// ---- the lists

	[Fact]
	public void A_list_adds_to_a_key_moves_it_below_its_equals_drops_it_under_1_and_cuts_its_lowest_to_the_cap()
	{
		var list = new CStrategyList<string>();
		list.Add("a", 50, 0);
		list.Add("b", 80, 0);
		list.Add("c", 50, 0);
		Assert.Equal("b 80 a 50 c 50", Text(list));

		// Added to: it goes below the equal it now meets.
		list.Add("a", 30, 0);
		Assert.Equal("b 80 a 80 c 50", Text(list));
		// A new key with nothing to add is left out; a nought changes nothing.
		list.Add("d", -5, 0);
		list.Add("c", 0, 0);
		Assert.Equal("b 80 a 80 c 50", Text(list));
		// Under 1, out.
		list.Add("c", -50, 0);
		Assert.Equal("b 80 a 80", Text(list));
		// The cap cuts the lowest, after the add.
		list.Add("e", 90, 2);
		Assert.Equal("e 90 b 80", Text(list));
	}

	[Fact]
	public void A_list_above_400_is_scaled_so_its_first_is_400_and_the_rest_rounded_half_to_even()
	{
		var list = new CStrategyList<string>();
		list.Add("a", 540, 0);
		list.Add("b", 397, 0);
		list.Add("c", 384, 0);
		list.ScaleTo400();
		// The ratio 1.350: 397 / 1.35 = 294.07..., 384 / 1.35 = 284.44...
		Assert.Equal("a 400 b 294 c 284", Text(list));

		var low = new CStrategyList<string>();
		low.Add("a", 400, 0);
		low.Add("b", 3, 0);
		low.ScaleTo400();
		Assert.Equal("a 400 b 3", Text(low));
		Assert.Equal(2, CStrategyFigures.Rounded(2500));
		Assert.Equal(4, CStrategyFigures.Rounded(3500));
	}

	// ---- the personality

	[Fact]
	public void The_personality_is_the_first_score_at_four_tenths_of_the_total_over_the_governments_ideologies()
	{
		// A fascist corner: military 300, capital 0, diplomacy -100; a democratic one: -73, -73, 173.
		Assert.Equal(300000, CStrategyPersonality.Military(200000, 200000));
		Assert.Equal(0, CStrategyPersonality.Capital(200000, 200000));
		Assert.Equal(-100000, CStrategyPersonality.Diplomacy(200000, 200000));
		Assert.Equal(-73000, CStrategyPersonality.Military(0, -146000));
		Assert.Equal(-73000, CStrategyPersonality.Capital(0, -146000));
		Assert.Equal(173000, CStrategyPersonality.Diplomacy(0, -146000));
		// A communist corner: the military score's 0.33 and the capital score's -x.
		Assert.Equal(100000 + 132000, CStrategyPersonality.Military(-200000, 200000));
		Assert.Equal(200000, CStrategyPersonality.Capital(-200000, 200000));
		// One over three, as the original's single precision makes it.
		Assert.Equal(333, CStrategyPersonality.ShareOf(3));
		Assert.Equal(1000, CStrategyPersonality.ShareOf(1));

		CGameState state = World();
		Assert.Equal(EAiPersonality.Military, CStrategyPersonality.Choose(state, state.Country(Ger)!));
		Assert.Equal(EAiPersonality.Diplomacy, CStrategyPersonality.Choose(state, state.Country(Pol)!));
		// No known government: balanced.
		state.Country(Lux)!.GovernmentType = "none";
		Assert.Equal(EAiPersonality.Balanced, CStrategyPersonality.Choose(state, state.Country(Lux)!));
	}

	// ---- threat, antagonism, rivals, protection

	[Fact]
	public void A_neighbour_threatens_by_its_border_share_times_its_strength_and_one_further_off_only_within_reach()
	{
		CGameState state = World();

		CAiStrategy germany = Made(state, Ger);

		// Poland: half the border, 25 x 0.5 + 100 = 112.5, x1.5 for the capital's area, x1.5 for the capital beside it.
		// Luxembourg: the same but for the capital. Britain: a faction's 300, but out of reach and smaller.
		Assert.Equal("POL 253 LUX 169", Text(germany.Threat));
		Assert.Equal(EAiPersonality.Military, germany.Personality);
		Assert.Equal("1 200", Text(germany.DefendProvinces));
	}

	[Fact]
	public void A_province_with_a_strategic_resource_is_to_be_taken_but_never_one_the_country_owns_itself()
	{
		// Province 5, Germany's own, held by Poland - as Norway's Vemork is held by Germany at 1941.6.22 - and Poland's 11,
		// beside Germany's land, both with a resource.
		CGameState state = World();
		state.Province(5).Controller = Pol;
		state.Province(5).StrategicResource = "heavy_water";
		state.Province(11).StrategicResource = "rubber";

		CAiStrategy germany = Made(state, Ger);

		Assert.Equal("11 50", Text(germany.ConquerProvinces));
	}

	[Fact]
	public void A_capital_another_country_holds_is_not_defended_and_the_provinces_held_go_in_the_order_they_were_taken()
	{
		CGameState state = World();
		// Poland's capital, 11, held by Germany: the adder takes no province Poland does not hold. Germany lost 2 and took it
		// back, so 2 is on the end of its list and its resource goes on the strategy's after 3's.
		state.Province(11).Controller = Ger;
		state.Province(2).StrategicResource = "steel";
		state.Province(3).StrategicResource = "steel";
		state.Province(2).Controller = Lux;
		state.Province(2).Controller = Ger;

		Assert.Equal("", Text(Made(state, Pol).DefendProvinces));
		Assert.Equal("1 200 3 50 2 50", Text(Made(state, Ger).DefendProvinces));
	}

	[Fact]
	public void The_faction_term_weighs_a_fascist_leader_400_a_communist_member_100_and_a_democracy_by_its_viewer()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CCountry britain = state.Country(Eng)!;

		// Britain leads the allies, a democracy; Germany is fascist, so 300.
		Assert.Equal(300000, CStrategyThreats.FactionTerm(state, germany, britain));
		// Germany leads the axis: 400 to Britain; and nothing to a country of the same faction.
		Assert.Equal(400000, CStrategyThreats.FactionTerm(state, britain, germany));
		Assert.Equal(0, CStrategyThreats.FactionTerm(state, germany, germany));
		// A democracy seen from a country in no faction: nothing.
		Assert.Equal(0, CStrategyThreats.FactionTerm(state, state.Country(Pol)!, britain));
	}

	[Fact]
	public void Antagonism_weighs_the_border_the_ideology_the_faction_and_the_smaller_country_then_scales()
	{
		CGameState state = World();

		CAiStrategy germany = Made(state, Ger);

		// Britain 50 + 400 + 90 = 540; Poland 117.187 + 50 + 150 + 80 = 397; Luxembourg 93.75 + 50 + 150 + 90 = 384;
		// scaled by 1.35.
		Assert.Equal("ENG 400 POL 294 LUX 284", Text(germany.Antagonize));
	}

	[Fact]
	public void A_rival_is_an_evenly_matched_neighbour_weighed_by_the_power_between_them()
	{
		CGameState state = World();

		CAiStrategy germany = Made(state, Ger);

		// Poland: power (0.2 + 1) / 2 = 0.6, 125 x 0.6 = 75; Luxembourg: 0.55, 68.75; Britain out of reach.
		Assert.Equal("POL 75 LUX 69", Text(germany.Rival));
	}

	[Fact]
	public void Protection_is_for_a_weak_neighbour_of_a_country_of_ten_provinces_capped_at_six_or_twelve()
	{
		CGameState state = World();
		CAiStrategy germany = Made(state, Ger);
		// Luxembourg a tenth of Germany's provinces, and one: 25 + 10; Poland a fifth: 25.
		Assert.Equal("LUX 35 POL 25", Text(germany.Protect));

		CGameState crowded = World(tiny: 7);
		CAiStrategy military = Made(crowded, Ger);
		Assert.Equal(6, military.Protect.Count);
		CGameState diplomatic = World(tiny: 7, germanGovernment: "republic");
		CAiStrategy diplomat = Made(diplomatic, Ger);
		Assert.Equal(EAiPersonality.Diplomacy, diplomat.Personality);
		Assert.Equal(9, diplomat.Protect.Count);
	}

	[Fact]
	public void A_country_of_nine_provinces_protects_nobody()
	{
		CGameState state = World();
		state.Province(10).Owner = Lux;
		state.Province(10).Controller = Lux;

		Assert.Equal(0, Made(state, Ger).Protect.Count);
	}

	// ---- the walk of the victory conditions

	private static void Choose(CGameState state, string text)
	{
		state.VictoryConditions = new() { ("axis", new List<CVictoryCondition> { new("test", ScriptWorld.Condition(text), true) { Chosen = true } }) };
	}

	[Fact]
	public void A_province_held_outside_the_faction_antagonises_its_holder_by_200_and_an_or_shares_it()
	{
		CGameState state = World();
		var strategy = new CAiStrategy();
		Choose(state, "11 = { controller = { faction = axis } } 13 = { controller = { faction = axis } }");

		CStrategyWalk.Walk(state, state.Country(Ger)!, strategy);
		Assert.Equal("POL 200 LUX 200", Text(strategy.Antagonize));

		var shared = new CAiStrategy();
		Choose(state, "OR = { 11 = { controller = { faction = axis } } 13 = { controller = { faction = axis } } 2 = { controller = { faction = axis } } }");
		CStrategyWalk.Walk(state, state.Country(Ger)!, shared);
		// Three children, 66 each; Germany's own province holds.
		Assert.Equal("POL 66 LUX 66", Text(shared.Antagonize));
	}

	[Fact]
	public void Under_a_not_or_for_a_country_outside_the_faction_the_walk_reports_nothing()
	{
		CGameState state = World();
		Choose(state, "NOT = { 11 = { controller = { faction = axis } } }");
		var strategy = new CAiStrategy();
		CStrategyWalk.Walk(state, state.Country(Ger)!, strategy);
		Assert.Equal(0, strategy.Antagonize.Count);

		Choose(state, "11 = { controller = { faction = axis } }");
		var allied = new CAiStrategy();
		CStrategyWalk.Walk(state, state.Country(Eng)!, allied);
		Assert.Equal(0, allied.Antagonize.Count);
	}

	[Fact]
	public void A_region_is_walked_once_for_each_of_its_provinces()
	{
		CGameState state = World(regions: new[] { new CRegion("poland_region", new[] { 11, 12 }, false) });
		var region = new CTrigger(ETriggerKind.Region, "poland_region", CScriptArgument.None);
		region.Children.AddRange(ScriptWorld.Condition("controller = { faction = axis }").Children);
		var block = CTrigger.All();
		block.Children.Add(region);
		state.VictoryConditions = new() { ("axis", new List<CVictoryCondition> { new("test", block, true) { Chosen = true } }) };
		var strategy = new CAiStrategy();

		CStrategyWalk.Walk(state, state.Country(Ger)!, strategy);

		Assert.Equal("POL 400", Text(strategy.Antagonize));
	}

	[Fact]
	public void A_played_country_leaves_its_victory_conditions_out_of_its_strategy()
	{
		CGameState state = World();
		Choose(state, "14 = { controller = { faction = axis } }");
		CAiStrategy walked = Made(state, Ger);
		int withWalk = walked.Antagonize.WeightOf(Eng);

		CGameState played = World();
		Choose(played, "14 = { controller = { faction = axis } }");
		played.Played.Add(Ger);
		CAiStrategy plain = Made(played, Ger);

		// 540 + 200 = 740, scaled to the top; without the walk Britain's 540 is the top too, but Poland's share differs.
		Assert.Equal(400, withWalk);
		Assert.NotEqual(Text(walked.Antagonize), Text(plain.Antagonize));
		Assert.Equal("ENG 400 POL 294 LUX 284", Text(plain.Antagonize));
	}

	// ---- when it is made

	[Fact]
	public void Nothing_is_made_for_a_country_owning_nothing()
	{
		CGameState state = World();
		foreach (int id in new[] { 11, 12 })
		{
			state.Province(id).Owner = Ger;
			state.Province(id).Controller = Ger;
		}

		Assert.False(CAiStrategies.Make(state, state.Country(Pol)!));
		Assert.False(state.Country(Pol)!.Strategy.IsMade);
	}

	[Fact]
	public void A_running_game_makes_a_strategy_again_every_ten_days_and_one_never_made_is_made_in_any_game()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;

		CAiStrategies.RunDaily(state);
		Assert.True(germany.Strategy.IsMade);
		long first = germany.Strategy.MadeHour;

		// Not running: it is left alone however old.
		for (int day = 0; day < 12; day++)
		{
			for (int hour = 0; hour < 24; hour++)
			{
				state.AdvanceOneHour();
			}
		}
		CAiStrategies.RunDaily(state);
		Assert.Equal(first, germany.Strategy.MadeHour);

		state.IsRunning = true;
		CAiStrategies.RunDaily(state);
		Assert.Equal(state.TotalHours, germany.Strategy.MadeHour);
		long second = germany.Strategy.MadeHour;
		for (int hour = 0; hour < 9 * 24; hour++)
		{
			state.AdvanceOneHour();
		}
		CAiStrategies.RunDaily(state);
		Assert.Equal(second, germany.Strategy.MadeHour);
	}

	[Fact]
	public void A_war_joined_in_a_running_game_makes_both_sides_strategies_again()
	{
		CGameState state = World();
		CAiStrategies.MakeAtStart(state);
		CCountry poland = state.Country(Pol)!;
		long before = poland.Strategy.MadeHour;
		state.AdvanceOneHour();
		state.IsRunning = true;

		var war = new CWar("test") { Attackers = { Ger } };
		state.Wars.Add(war);
		CWarSides.Join(state, war, Pol, attacking: false);

		Assert.Equal(state.TotalHours, poland.Strategy.MadeHour);
		Assert.NotEqual(before, poland.Strategy.MadeHour);
		Assert.Equal(state.TotalHours, state.Country(Ger)!.Strategy.MadeHour);
	}

	// ---- the prepared wars checked

	[Fact]
	public void A_prepared_war_on_a_puppet_or_a_decision_whose_potential_fails_is_dropped_as_the_strategy_is_made()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		var failing = new CDecision("never", EDecisionGroup.Diplomatic, "decisions/test.txt", 1);
		failing.Potential.Children.AddRange(ScriptWorld.Condition("tag = ENG").Children);
		var holding = new CDecision("always", EDecisionGroup.Diplomatic, "decisions/test.txt", 2);
		germany.WarTargets.Add(new CWarTarget(Pol, failing, false, 0));
		germany.WarTargets.Add(new CWarTarget(Lux, holding, false, 0));
		state.Country(Eng)!.Overlord = Ger;
		germany.WarTargets.Add(new CWarTarget(Eng, holding, false, 0));

		CAiStrategies.Make(state, germany);

		Assert.Equal(new[] { Lux }, germany.WarTargets.Select(t => t.Target));
	}
}
