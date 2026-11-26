using System.Collections.Generic;
using OpenHOI3.Interface.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Script;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// Conditions and effects as words, against <c>docs/mechanics/scripttext.md</c>, on
/// <see cref="ScriptWorld"/>'s strip of ground, where each country's name is its tag.
/// </summary>
public class ScriptTextTests
{
	private static readonly Dictionary<string, string> Table = new()
	{
		["ATWAR"] = "At War.",
		["ATPEACE"] = "At Peace.",
		["IS_AI"] = "Is AI",
		["IS_NOT_AI"] = "Is NOT AI",
		["AND_TRIGGER_STARTS"] = "All of the below",
		["OR_TRIGGER_STARTS"] = "One of the below",
		["ADD_MONEY_EFFECT"] = "Gain §G$VALUE$§W money§W.\n",
		["REMOVE_MONEY_EFFECT"] = "Lose §R$VALUE$§W money§W.\n",
		["ADD_MANPOWER_EFFECT"] = "Gain §G$VALUE$§W manpower§W.\n",
		["NEW_CORE_EFFECT"] = "Gain core on §Y$NEW$§W.\n",
		["RANDOM_LIST_EFFECT"] = "One of §Y$SIZE$§W options will happen.\n",
		["RANDOM_EFFECT"] = "§Y$CHANCE$§W% chance of ",
		["NO_EFFECT"] = "No Effect",
	};

	private static CScriptWords Words(CGameState state)
	{
		return new CScriptWords(key => Table.GetValueOrDefault(key, key), state, ScriptWorld.Ger);
	}

	private static string Conditions(string text, bool atWar = false)
	{
		CGameState state = ScriptWorld.State(atWar);
		return CConditionText.For(Words(state), ScriptWorld.Condition(text), CEventScope.Start(ScriptWorld.Ger));
	}

	private static string Effects(string text)
	{
		CGameState state = ScriptWorld.State();
		return CEffectText.For(Words(state), ScriptWorld.Effects(text), CEventScope.Start(ScriptWorld.Ger));
	}

	[Fact]
	public void At_the_top_each_condition_is_a_line_with_its_asterisk()
	{
		Assert.Equal("(§G*§W)At War.\n(§R*§W)Is NOT AI\n", Conditions("war = yes ai = no", atWar: true));
	}

	[Fact]
	public void Inside_a_not_the_words_turn_round_and_red_is_where_a_condition_holds()
	{
		Assert.Equal("(§G*§W)At Peace.\n", Conditions("NOT = { war = yes }"));
		Assert.Equal("(§R*§W)At Peace.\n", Conditions("NOT = { war = yes }", atWar: true));
	}

	[Fact]
	public void A_block_below_the_top_has_its_header_and_its_conditions_a_level_deeper()
	{
		Assert.Equal("(§G*§W)One of the below:\n   (§G*§W)At War.\n   (§R*§W)Is NOT AI\n",
			Conditions("OR = { war = yes ai = no }", atWar: true));
	}

	[Fact]
	public void A_scope_of_one_line_puts_it_after_the_name_lowered_and_without_its_asterisk()
	{
		Assert.Equal("(§G*§W)POL at War.\n", Conditions("POL = { war = yes }", atWar: true));
	}

	[Fact]
	public void A_scope_of_two_lines_goes_under_its_name_with_their_asterisks()
	{
		Assert.Equal("(§G*§W)POL:\n   (§G*§W)At War.\n   (§G*§W)Is AI\n", Conditions("POL = { war = yes ai = yes }", atWar: true));
	}

	[Fact]
	public void A_block_inside_a_not_has_no_asterisk_of_its_own_beside_the_nots()
	{
		Assert.Equal("(§R*§W)POL:\n   (§G*§W)Is AI\n   (§R*§W)GER at Peace.\n",
			Conditions("POL = { ai = yes NOT = { GER = { war = yes } } }", atWar: true));
	}

	[Fact]
	public void A_key_no_table_has_is_shown_as_itself()
	{
		Assert.Equal("(§R*§W)HAS_FULL_FRONTAGE\n", Conditions("frontage_full = yes"));
	}

	[Fact]
	public void An_effects_line_and_a_scope_of_another_country_with_its_head()
	{
		Assert.Equal("Gain §G5§W money§W.\n(POL):    Gain §G§G2.0§W§W manpower§W.",
			Effects("money = 5 POL = { manpower = 2 }"));
	}

	[Fact]
	public void A_scope_of_the_players_own_country_has_no_head()
	{
		Assert.Equal("Gain §G§G2.0§W§W manpower§W.", Effects("GER = { manpower = 2 }"));
	}

	[Fact]
	public void A_negative_figure_keeps_its_minus_inside_lose()
	{
		Assert.Equal("Lose §R-5§W money§W.", Effects("money = -5"));
	}

	[Fact]
	public void A_run_of_add_core_is_one_line_naming_each_province()
	{
		Assert.Equal("Gain core on §Y§Yp2§W,§Y p4§W§W.", Effects("add_core = 2 add_core = 4"));
	}

	[Fact]
	public void Add_core_says_nothing_where_the_core_is_had_and_takes_the_run_before_it_with_it()
	{
		Assert.Equal("", Effects("add_core = 2 add_core = 3"));
	}

	[Fact]
	public void A_random_list_gives_each_entrys_chance_whole()
	{
		Assert.Equal("One of §Y2§W options will happen.\n§Y25§W% chance of Gain §G1§W money§W.\n\n§Y75§W% chance of No Effect",
			Effects("random_list = { 25 = { money = 1 } 75 = { } }"));
	}

	[Fact]
	public void An_enhanced_games_kill_leader_says_what_he_commands_and_where()
	{
		CGameState state = ScriptWorld.State();
		OpenHOI3.Sim.Countries.CCountry germany = state.Country(ScriptWorld.Ger)!;
		var leader = new OpenHOI3.Sim.Leaders.CLeader(7) { Name = "Rommel", Country = ScriptWorld.Ger, Rank = 1 };
		state.Leaders[7] = leader;
		germany.Leaders.Add(leader);
		germany.ActiveLeaders.Add(leader);
		var words = new CScriptWords(
			key => key == "KILL_LEADER_EFFECT" ? "Leader $NAME$$WHERE$ dies [$UNIT$|$LOCATION$]." : key, state, ScriptWorld.Ger);
		string Line() => CEffectText.For(words, ScriptWorld.Effects("kill_leader = 7"), CEventScope.Start(ScriptWorld.Ger));

		// A faithful game fills the name alone, as the original does.
		Assert.Equal("Leader §YRommel§W$WHERE$ dies [$UNIT$|$LOCATION$].", Line());

		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		// He commands nothing: the three are empty, and no bracket is left behind.
		Assert.Equal("Leader §YRommel§W dies [|].", Line());

		var unit = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Division, ScriptWorld.Ger) { Id = 900, Name = "7. Panzer", Location = 1 };
		germany.Units.Add(unit);
		unit.Leader = leader;
		leader.Unit = unit;
		Assert.Equal("Leader §YRommel§W (§R7. Panzer§W, §Rp1§W) dies [7. Panzer|p1].", Line());
		// A unit with no place has the unit alone in its bracket.
		unit.Location = 0;
		Assert.Equal("Leader §YRommel§W (§R7. Panzer§W) dies [7. Panzer|].", Line());
	}

	[Fact]
	public void Effects_that_say_nothing_write_nothing()
	{
		Assert.Equal("", Effects("set_country_flag = done"));
	}
}
