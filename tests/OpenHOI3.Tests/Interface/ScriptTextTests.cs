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

	private static readonly Dictionary<string, string> OobWords = new()
	{
		[CLoadOobText.MissingKey] = "§RNot in the mod or the game.§W",
		[CLoadOobText.UnitsKey] = "§Y$COUNT$§W units in $PLACES$",
		[CLoadOobText.PlaceKey] = "§Y$NAME$§W",
		[CLoadOobText.PlaceManyKey] = "§Y$NAME$§W ($COUNT$)",
		[CLoadOobText.PlacesMoreKey] = " and $COUNT$ more",
		[CLoadOobText.BrigadesKey] = "$COUNT$ brigades",
		[CLoadOobText.ShipsKey] = "$COUNT$ ships",
		[CLoadOobText.WingsKey] = "$COUNT$ wings",
		[CLoadOobText.ProductionKey] = "§Y$COUNT$§W added to production",
		[CLoadOobText.LeadersKey] = "§Y$COUNT$§W leaders take command:",
		[CLoadOobText.LeaderKey] = "  §Y$NAME$§W to $KIND$ - $WHERE$",
		[CLoadOobText.LeaderUnknownKey] = "  §R$ID$ - not one of this country's leaders§W",
		[CLoadOobText.LeadersMoreKey] = "  and $COUNT$ more",
		[CLoadOobText.NoCommandKey] = "no command",
		[CLoadOobText.CommandKey] = "§R$UNIT$§W",
		[CLoadOobText.CommandAtKey] = "§R$UNIT$, $PROVINCE$§W",
		[CLoadOobText.RankMadeKey] = "    rank $RANK$, made $WANTED$ for $KIND$",
		[CLoadOobText.RankWrongKey] = "    §Rrank $RANK$, and $KIND$ wants $WANTED$§W",
		[CLoadOobText.HoldAltKey] = "§Ghold Alt for the rest§W",
		[CLoadOobText.KindKeyStem + "CORPS"] = "a corps",
		[CLoadOobText.KindKeyStem + "ARMY"] = "an army",
		[CLoadOobText.KindKeyStem + "NAVY"] = "a fleet",
	};

	[Fact]
	public void An_enhanced_games_load_oob_says_what_its_file_would_do()
	{
		CGameState state = ScriptWorld.State();
		OpenHOI3.Sim.Countries.CCountry germany = state.Country(ScriptWorld.Ger)!;
		var summary = new OpenHOI3.Sim.Units.COrderOfBattleSummary { Found = true, Units = 7, Brigades = 9, Wings = 2, Constructions = 3 };
		foreach ((int province, int units) in new[] { (1, 3), (2, 1), (3, 1), (4, 1), (5, 1) })
		{
			summary.Places.Add(new OpenHOI3.Sim.Units.COrderOfBattlePlace(province, units));
		}
		OpenHOI3.Sim.Leaders.CLeader Leader(int id, string name, int rank, OpenHOI3.Sim.Leaders.ELeaderType type = OpenHOI3.Sim.Leaders.ELeaderType.Land)
		{
			var leader = new OpenHOI3.Sim.Leaders.CLeader(id) { Name = name, Country = ScriptWorld.Ger, Rank = rank, Type = type };
			state.Leaders[id] = leader;
			germany.Leaders.Add(leader);
			return leader;
		}
		OpenHOI3.Sim.Leaders.CLeader guderian = Leader(1, "Guderian", 2);
		OpenHOI3.Sim.Leaders.CLeader rommel = Leader(2, "Rommel", 1);
		Leader(3, "Raeder", 1, OpenHOI3.Sim.Leaders.ELeaderType.Sea);
		var division = new OpenHOI3.Sim.Units.CUnit(OpenHOI3.Sim.Units.EUnitKind.Division, ScriptWorld.Ger) { Id = 900, Name = "7. Panzer", Location = 1 };
		rommel.Unit = division;
		summary.Leaders.Add(new OpenHOI3.Sim.Units.COrderOfBattleLeader(1, OpenHOI3.Sim.Units.EUnitKind.Corps));
		summary.Leaders.Add(new OpenHOI3.Sim.Units.COrderOfBattleLeader(2, OpenHOI3.Sim.Units.EUnitKind.Army));
		summary.Leaders.Add(new OpenHOI3.Sim.Units.COrderOfBattleLeader(3, OpenHOI3.Sim.Units.EUnitKind.Navy));
		summary.Leaders.Add(new OpenHOI3.Sim.Units.COrderOfBattleLeader(99, OpenHOI3.Sim.Units.EUnitKind.Corps));
		state.SummariseOrderOfBattle = file => file == "GER/new.txt" ? summary : new OpenHOI3.Sim.Units.COrderOfBattleSummary();
		bool alt = false;
		var words = new CScriptWords(key => OobWords.GetValueOrDefault(key, key), state, ScriptWorld.Ger) { ShowAll = () => alt };
		string Line(string file) => CLoadOobText.For(words, file, germany);

		// A faithful game's is the path, bare.
		Assert.Equal("GER/new.txt", Line("GER/new.txt"));

		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		Assert.Equal("GER/none.txt\n§RNot in the mod or the game.§W\n", Line("GER/none.txt"));
		string head = "GER/new.txt\n§Y7§W units in §Yp1§W (3), §Yp2§W, §Yp3§W, §Yp4§W";
		string rest = "\n9 brigades, 2 wings\n§Y3§W added to production\n§Y4§W leaders take command:"
			+ "\n  §YGuderian§W to a corps - no command"
			// He commands a division now, and an army wants rank 3: the game has the fix, so the file makes it so.
			+ "\n  §YRommel§W to an army - §R7. Panzer, p1§W\n    rank 1, made 3 for an army"
			+ "\n  §YRaeder§W to a fleet - no command"
			+ "\n  §R99 - not one of this country's leaders§W";
		// Four places and the rest counted, with the hint; Alt names them all.
		Assert.Equal(head + " and 1 more" + rest + "\n§Ghold Alt for the rest§W\n\n", Line("GER/new.txt"));
		alt = true;
		Assert.Equal(head + ", §YSea§W" + rest + "\n", Line("GER/new.txt"));

		// A game without the rank fix says he keeps a rank the unit does not take.
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(new[] { OpenHOI3.Sim.Rules.EFix.OobLeaderRanks }));
		Assert.Contains("\n    §Rrank 1, and an army wants 3§W", Line("GER/new.txt"));
		Assert.DoesNotContain("rank 2", Line("GER/new.txt"));      // Guderian's fits a corps
		// A localisation without the lines leaves the path.
		Assert.Equal("GER/new.txt", CLoadOobText.For(new CScriptWords(key => key, state, ScriptWorld.Ger), "GER/new.txt", germany));
	}

	[Fact]
	public void Effects_that_say_nothing_write_nothing()
	{
		Assert.Equal("", Effects("set_country_flag = done"));
	}
}
