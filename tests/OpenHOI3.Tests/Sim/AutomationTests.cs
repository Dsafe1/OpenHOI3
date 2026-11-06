using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Settings;

namespace OpenHOI3.Tests.Sim;

/// <summary>Who does a country's work and what the difficulty does, as <c>docs/mechanics/frontend.md</c>, section 4, has them.</summary>
public class AutomationTests
{
	private static CGameState World(out CCountry played, out CCountry other)
	{
		played = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		other = new CCountry { Tag = new CCountryTag("ENG"), Name = "United Kingdom", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { played, other });
		state.Played.Add(played.Tag);
		return state;
	}

	[Fact]
	public void The_AI_runs_a_played_country_only_in_the_areas_handed_to_it()
	{
		CGameState state = World(out CCountry played, out CCountry other);
		state.Automation.Flags[(int)EAutomation.Diplomacy] = 1;

		Assert.True(CAutomation.AiRuns(state, played, EAutomation.Diplomacy));
		Assert.False(CAutomation.AiRuns(state, played, EAutomation.Production));
		Assert.True(CAutomation.AiRuns(state, other, EAutomation.Production));
	}

	[Fact]
	public void The_tech_minister_goes_by_the_Technology_setting_and_not_its_flag()
	{
		CGameState state = World(out CCountry played, out CCountry other);
		state.Automation.Flags[(int)EAutomation.Technology] = 1;

		Assert.False(CAutomation.AiRuns(state, played, EAutomation.Technology));
		Assert.Equal((false, false), CAutomation.TechHalves(state, played));

		state.Automation.Technology = 2;
		Assert.True(CAutomation.AiRuns(state, played, EAutomation.Technology));
		Assert.Equal((false, true), CAutomation.TechHalves(state, played));
		state.Automation.Technology = 3;
		Assert.Equal((true, false), CAutomation.TechHalves(state, played));
		state.Automation.Technology = 1;
		Assert.Equal((true, true), CAutomation.TechHalves(state, played));
		Assert.Equal((true, true), CAutomation.TechHalves(state, other));
	}

	[Fact]
	public void Ministers_are_made_for_the_areas_handed_over_and_dropped_for_those_taken_back()
	{
		CGameState state = World(out CCountry played, out CCountry other);
		played.ForeignCountdown = 0;
		played.PoliticsCountdown = 12;
		other.PoliticsCountdown = 5;
		state.Automation.Flags[(int)EAutomation.Diplomacy] = 1;

		CAutomation.SetMinisters(state);

		// A minister made has a new minister's draw of one to three hours, a foreign one its first work skipped; one no
		// longer the AI's is gone; a country nobody plays is left as it was.
		Assert.InRange(played.ForeignCountdown, 1, 3);
		Assert.True(played.ForeignMinister.SkipOnce);
		Assert.Equal(0, played.PoliticsCountdown);
		Assert.Equal(5, other.PoliticsCountdown);
	}

	[Fact]
	public void The_technology_screens_arrows_set_the_setting_with_its_flag_and_make_the_tech_minister()
	{
		CGameState state = World(out CCountry played, out CCountry _);

		new CSetAutomationCommand(EAutomation.Technology, 2).Execute(state);

		Assert.Equal(2, state.Automation.Technology);
		Assert.Equal(1, state.Automation.Flags[(int)EAutomation.Technology]);
		Assert.InRange(played.TechCountdown, 1, 3);

		new CSetAutomationCommand(EAutomation.Technology, 0).Execute(state);
		Assert.Equal(0, state.Automation.Flags[(int)EAutomation.Technology]);
		Assert.Equal(0, played.TechCountdown);
		// The Armies setting has no arrows, and a flag is 0 or 1.
		Assert.False(new CSetAutomationCommand(EAutomation.Armies, 1).MayExecute(state));
		Assert.False(new CSetAutomationCommand(EAutomation.Politics, 2).MayExecute(state));
	}

	/// <summary>Records each call of the daily sliders.</summary>
	private sealed class SliderScripts : IAiScripts
	{
		public List<(string Tag, int Level, double LendLease)> Calls { get; } = new();

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) => 0;

		public bool BalanceProductionSliders(CGameState state, CCountry country, int level = 0, double lendLease = 0)
		{
			Calls.Add((country.Tag.Value, level, lendLease));
			return true;
		}
	}

	private static CGameState Owning(out CCountry played, out SliderScripts scripts)
	{
		played = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		var other = new CCountry { Tag = new CCountryTag("ENG"), Name = "United Kingdom", DefinitionFile = "" };
		var provinces = new[]
		{
			new CProvince(0, "", isSea: false), new CProvince(1, "", isSea: false) { Owner = played.Tag },
			new CProvince(2, "", isSea: false) { Owner = other.Tag },
		};
		scripts = new SliderScripts();
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { played, other }) { AiScripts = scripts };
		state.Played.Add(played.Tag);
		return state;
	}

	[Fact]
	public void The_players_sliders_are_set_each_day_at_his_level_with_his_lend_lease_share()
	{
		CGameState state = Owning(out CCountry played, out SliderScripts scripts);
		played.ProductionShares[(int)EProductionCategory.LendLease] = new CFixed15(CFixed15.Scale / 4);

		CProductionAi.BalanceDaily(state);
		// Manual Control: the AI's sliders alone.
		Assert.Equal(new[] { ("ENG", 0, 0.0) }, scripts.Calls);

		scripts.Calls.Clear();
		state.Automation.Sliders = 3;
		CProductionAi.BalanceDaily(state);
		Assert.Equal(new[] { ("GER", 3, 0.25), ("ENG", 0, 0.0) }, scripts.Calls);

		// Production handed over: the AI's sliders for the player's country too, at level 0.
		scripts.Calls.Clear();
		state.Automation.Flags[(int)EAutomation.Production] = 1;
		CProductionAi.BalanceDaily(state);
		Assert.Equal(new[] { ("GER", 0, 0.0), ("ENG", 0, 0.0) }, scripts.Calls);
	}

	[Fact]
	public void Production_handed_to_the_AI_puts_the_slider_automation_back_to_Manual_Control()
	{
		CGameState state = World(out CCountry _, out CCountry _);
		new CSetSliderAutomationCommand(1).Execute(state);

		new CSetAutomationCommand(EAutomation.Politics, 1).Execute(state);
		Assert.Equal(1, state.Automation.Sliders);

		new CSetAutomationCommand(EAutomation.Production, 1).Execute(state);
		Assert.Equal(0, state.Automation.Sliders);
		Assert.False(state.Automation.SlidersDue);
	}

	[Fact]
	public void A_slider_automation_raised_sets_the_sliders_at_the_next_hour_and_once()
	{
		CGameState state = Owning(out CCountry _, out SliderScripts scripts);

		new CSetSliderAutomationCommand(1).Execute(state);
		CProductionAi.RunHourly(state);
		CProductionAi.RunHourly(state);

		Assert.Equal(new[] { ("GER", 1, 0.0) }, scripts.Calls);
		new CSetSliderAutomationCommand(0).Execute(state);
		Assert.False(state.Automation.SlidersDue);
		Assert.False(new CSetSliderAutomationCommand(4).MayExecute(state));
	}

	[Fact]
	public void The_difficulty_names_a_played_countrys_player_block_and_every_others_ai_block()
	{
		CGameState state = World(out CCountry played, out CCountry other);

		Assert.Null(CDifficulty.BlockName(state, played));
		state.GamePlay.Difficulty = 0;
		Assert.Equal("very_easy_player", CDifficulty.BlockName(state, played));
		Assert.Equal("very_easy_ai", CDifficulty.BlockName(state, other));
		state.GamePlay.Difficulty = 3;
		Assert.Equal("hard_player", CDifficulty.BlockName(state, played));
	}

	[Fact]
	public void The_difficultys_combat_modifier_is_its_step_times_the_impact_for_a_played_country_alone()
	{
		CGameState state = World(out CCountry played, out CCountry other);
		state.Defines.Set(CManpower.MilitarySection, CDifficulty.CombatImpact, CFixedPoint.Parse("0.2"));

		state.GamePlay.Difficulty = 4;
		Assert.Equal(-400, CDifficulty.Combat(state, played));
		Assert.Equal(0, CDifficulty.Combat(state, other));
		state.GamePlay.Difficulty = 1;
		Assert.Equal(200, CDifficulty.Combat(state, played));
		state.GamePlay.Difficulty = CGamePlaySettings.NormalDifficulty;
		Assert.Equal(0, CDifficulty.Combat(state, played));
	}
}
