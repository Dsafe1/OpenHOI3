using System.Collections.Generic;
using OpenHOI3.Content.Script;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;
using OpenHOI3.Tests.Content;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A faction's progress and the victory conditions, against <c>docs/mechanics/factions.md</c>, sections 7 and 8, on
/// <see cref="FactionTests.World"/>: Germany and Italy in the axis, the rest in none; Berlin is province 1, Warsaw 2.
/// </summary>
public class VictoryTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CGameState World()
	{
		CGameState state = FactionTests.World();
		// 1172 points in the world, 115 of them in Berlin.
		state.Province(1).Points = 115;
		state.Province(2).Points = 1057;
		return state;
	}

	private static CVictoryCondition Condition(string name, string trigger, bool primary)
	{
		CScriptContext context = ScriptFixture.Context();
		CTrigger block = CTrigger.All();
		CScriptNode node = ScriptFixture.Parse("trigger = { " + trigger + " }", context);
		CTriggerReader.ReadInto(block, node.Children[0], context);
		return new CVictoryCondition(name, block, primary);
	}

	[Fact]
	public void Progress_is_the_points_a_factions_members_hold_and_the_null_factions_is_never_set_back()
	{
		CGameState state = World();
		CFactionProgress.RunHourly(state);
		Assert.Equal(115000, CFactionProgress.Progress(state, "axis"));
		Assert.Equal(1057000, CFactionProgress.Progress(state, ""));

		// Held, not owned: Germany holding Warsaw counts it for the axis.
		state.Province(2).Controller = Ger;
		CFactionProgress.RunHourly(state);
		Assert.Equal(1172000, CFactionProgress.Progress(state, "axis"));
		// The null faction's keeps what it had: no group without a faction sets it back.
		Assert.Equal(1057000, CFactionProgress.Progress(state, ""));
		state.Province(2).Controller = new CCountryTag("POL");
		CFactionProgress.RunHourly(state);
		Assert.Equal(2114000, CFactionProgress.Progress(state, ""));
	}

	[Fact]
	public void The_share_is_cut_to_a_tenth_of_a_percent_and_faction_progress_asks_it()
	{
		CGameState state = World();
		CFactionProgress.RunHourly(state);
		Assert.Equal(9800, CFactionProgress.Share(state, "axis"));

		CEventScope scope = CEventScope.Start(Ger);
		Assert.True(CConditions.Holds(state, Condition("", "faction_progress = 9.8", false).Trigger, scope));
		Assert.False(CConditions.Holds(state, Condition("", "faction_progress = 9.81", false).Trigger, scope));
	}

	[Fact]
	public void A_new_game_chooses_the_primaries_until_the_count_is_spent()
	{
		CGameState state = World();
		state.Defines.Set(CVictoryConditions.Section, "NUMBER_OF_VICTORY_CONDITIONS", CFixedPoint.FromInt(2));
		var axis = new List<CVictoryCondition>
		{
			Condition("a", "always = yes", true), Condition("b", "always = yes", false),
			Condition("c", "always = yes", true), Condition("d", "always = yes", true),
		};
		var allies = new List<CVictoryCondition> { Condition("e", "always = yes", true), Condition("f", "always = yes", false) };
		state.VictoryConditions = new() { ("axis", axis), ("allies", allies) };

		List<string> shortOf = CVictoryConditions.ChooseAtStart(state);

		Assert.Equal(new[] { true, false, true, false }, axis.ConvertAll(c => c.Chosen));
		Assert.Equal(new[] { "allies" }, shortOf);
		Assert.Equal(2, CVictoryConditions.Chosen(state, "axis"));
		// The allies have one primary of the two: the one left is drawn, there being no other.
		Assert.Equal(new[] { true, true }, allies.ConvertAll(c => c.Chosen));
	}

	[Fact]
	public void A_faction_short_of_primaries_is_filled_by_draws_counted_back_from_its_last_unchosen_condition()
	{
		CGameState state = World();
		state.Defines.Set(CVictoryConditions.Section, "NUMBER_OF_VICTORY_CONDITIONS", CFixedPoint.FromInt(3));
		var axis = new List<CVictoryCondition>
		{
			Condition("a", "always = yes", true), Condition("b", "always = yes", false), Condition("c", "always = yes", false),
			Condition("d", "always = yes", false), Condition("e", "always = yes", false),
		};
		state.VictoryConditions = new() { ("axis", axis) };

		CVictoryConditions.ChooseAtStart(state);

		// The same draws by hand, on a generator seeded as the game is.
		var expected = new List<bool> { true, false, false, false, false };
		var random = new CRandom(state.Seed);
		for (int i = 0; i < 2; i++)
		{
			int pick = random.Next() % expected.FindAll(chosen => !chosen).Count;
			if (pick < 0)
			{
				expected[^1] = true;
				continue;
			}
			for (int at = expected.Count - 1; at >= 0; at--)
			{
				if (!expected[at] && pick-- == 0)
				{
					expected[at] = true;
					break;
				}
			}
		}
		Assert.Equal(expected, axis.ConvertAll(c => c.Chosen));
		Assert.True(axis[0].Chosen);
		Assert.InRange(CVictoryConditions.Chosen(state, "axis"), 2, 3);
	}

	[Fact]
	public void A_lobby_choice_is_left_alone_by_a_new_games_own()
	{
		CGameState state = World();
		state.Defines.Set(CVictoryConditions.Section, "NUMBER_OF_VICTORY_CONDITIONS", CFixedPoint.FromInt(1));
		var axis = new List<CVictoryCondition> { Condition("a", "always = yes", true), Condition("b", "always = yes", false) };
		state.VictoryConditions = new() { ("axis", axis) };
		state.GameFinished = true;

		CVictoryConditions.SetChosen(state, "axis", new[] { "b" });
		CVictoryConditions.ChooseAtStart(state);

		Assert.Equal(new[] { false, true }, axis.ConvertAll(c => c.Chosen));
		Assert.False(state.GameFinished);
	}

	[Fact]
	public void A_condition_is_met_while_it_holds_and_the_first_faction_with_enough_finishes_the_game()
	{
		CGameState state = World();
		state.Defines.Set(CVictoryConditions.Section, "VICTORY_CONDITIONS_TO_WIN", CFixedPoint.One);
		CVictoryCondition warsaw = Condition("warsaw", "2 = { controller = { faction = axis } }", true);
		warsaw.Chosen = true;
		state.VictoryConditions = new() { ("axis", new List<CVictoryCondition> { warsaw }) };

		CVictoryConditions.RunDaily(state);
		Assert.False(warsaw.Met);
		Assert.False(state.GameFinished);

		state.Province(2).Controller = Ger;
		CVictoryConditions.RunDaily(state);
		Assert.True(warsaw.Met);
		Assert.Equal(state.TotalHours, warsaw.Date);
		Assert.True(state.GameFinished);
		Assert.Equal(1, CVictoryConditions.Met(state, "axis"));

		// Lost again, it no longer counts, and keeps its date; the game stays finished.
		long date = state.TotalHours;
		state.AdvanceOneHour();
		state.Province(2).Controller = new CCountryTag("POL");
		CVictoryConditions.RunDaily(state);
		Assert.False(warsaw.Met);
		Assert.Equal(date, warsaw.Date);
		Assert.True(state.GameFinished);
	}
}
