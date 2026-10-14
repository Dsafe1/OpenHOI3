using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Politics;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Spies and their day, against <c>docs/mechanics/espionage.md</c>, sections 1 to 13, on <see cref="ScriptWorld"/>'s
/// strip: Germany, Poland, Italy, Slovakia (which owns nothing) and REB, in that order; Germany and Italy are the axis.
/// </summary>
public class EspionageTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static CSpyPresence P(CGameState state, CCountryTag holder, CCountryTag other) =>
		CGameState.Presence(C(state, holder), C(state, other));

	private static CGameState State()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set("country", CSpyPlacing.MostSpies, CFixedPoint.FromInt(10));
		state.Defines.Set("country", "SPY_DETECTION_CHANCE", CFixedPoint.Parse("0.1"));
		state.Defines.Set("country", "NO_MISSION_SPY_DETECTION_MULTIPLIER", CFixedPoint.Parse("0.1"));
		state.Defines.Set("country", "SPY_COVERT_OPS_GAIN", CFixedPoint.Parse("0.5"));
		state.Defines.Set("country", "SPY_COVERT_OPS_MAX", CFixedPoint.FromInt(50));
		state.Defines.Set("country", "SPY_PARTY_ORGANIZATION_BOOST", CFixedPoint.Parse("0.01"));
		state.Defines.Set("country", "COUP_COVERT_OPS_COST", CFixedPoint.FromInt(20));
		state.Defines.Set("country", "COUP_CHANCE_PER_POSITION", CFixedPoint.Parse("0.2"));
		return state;
	}

	[Fact]
	public void A_country_starts_with_priority_1_and_counter_espionage_at_home_and_nothing_anywhere_else()
	{
		CGameState state = State();

		CSpyPresence home = P(state, Ger, Ger);
		Assert.Equal(1, home.Priority);
		Assert.Equal(1, home.Weight(ESpyMission.CounterEspionage));
		Assert.Equal(1000, home.Share(ESpyMission.CounterEspionage));
		Assert.Equal(0, P(state, Ger, Pol).Priority);
		Assert.False(P(state, Ger, Pol).HasAnyMission());
	}

	[Fact]
	public void A_mission_s_share_is_its_weight_over_the_sum_truncated()
	{
		var presence = new CSpyPresence();
		presence.SetMission(1, 1);
		presence.SetMission(2, 1);
		presence.SetMission(3, 1);

		Assert.Equal(333, presence.Shares[1]);
		Assert.Equal(333, presence.Shares[3]);

		presence.SetMission(1, 0);
		presence.SetMission(2, 0);
		presence.SetMission(3, 0);
		Assert.Equal(0, presence.Shares[3]);
	}

	[Fact]
	public void Whole_spies_go_to_the_highest_band_first_and_what_is_over_stays_free()
	{
		CGameState state = State();
		P(state, Ger, Pol).Priority = 3;
		C(state, Ger).FreeSpies = CFixedPoint.Parse("2.5");

		CSpyPlacing.Run(state, C(state, Ger), CFixedPoint.Zero);

		// Band 3 holds Poland alone, so it takes both; the half left is no whole spy for the home in band 1.
		Assert.Equal(2000, P(state, Ger, Pol).Level);
		Assert.Equal(0, P(state, Ger, Ger).Level);
		Assert.Equal(CFixedPoint.Parse("0.5"), C(state, Ger).FreeSpies);
	}

	[Fact]
	public void Priority_1_stops_at_seven_spies_and_priority_0_never_gains_one()
	{
		CGameState state = State();
		P(state, Ger, Ger).Level = 7000;
		C(state, Ger).FreeSpies = CFixedPoint.FromInt(5);

		CSpyPlacing.Run(state, C(state, Ger), CFixedPoint.Zero);

		Assert.Equal(7000, P(state, Ger, Ger).Level);
		Assert.Equal(0, P(state, Ger, Pol).Level);
		Assert.Equal(CFixedPoint.FromInt(5), C(state, Ger).FreeSpies);
	}

	[Fact]
	public void The_cursor_does_not_wrap_within_a_day_so_a_band_can_sit_a_day_out()
	{
		CGameState state = State();
		P(state, Ger, Pol).Priority = 3;
		P(state, Ger, Ita).Priority = 3;
		CCountry germany = C(state, Ger);

		var given = new List<string>();
		for (int day = 0; day < 4; day++)
		{
			long poland = P(state, Ger, Pol).Level, italy = P(state, Ger, Ita).Level;
			CSpyPlacing.Run(state, germany, CFixedPoint.One);
			given.Add(P(state, Ger, Pol).Level > poland ? "POL" : P(state, Ger, Ita).Level > italy ? "ITA" : "none");
		}

		// Past Italy nobody owns a province, so the third day's scan places nothing and starts again on the fourth.
		Assert.Equal(new[] { "POL", "ITA", "none", "POL" }, given);
	}

	[Fact]
	public void A_friend_s_benign_spies_at_home_are_never_caught()
	{
		CGameState state = State();
		C(state, Ger).Modifier.Add("counter_espionage", CFixedPoint.FromInt(100));
		P(state, Ger, Ger).Level = 10000;
		CSpyPresence italians = P(state, Ita, Ger);
		italians.Level = 10000;
		italians.SetMission((int)ESpyMission.BoostRulingParty, 1);

		for (int day = 0; day < 200; day++)
		{
			CCounterEspionage.Run(state, P(state, Ger, Ger), C(state, Ger), C(state, Ger));
		}

		Assert.Equal(10000, italians.Level);
	}

	[Fact]
	public void A_certain_first_roll_still_catches_about_one_time_in_ten_and_counts_it_at_home()
	{
		CGameState state = State();
		C(state, Ger).Modifier.Add("counter_espionage", CFixedPoint.FromInt(100));
		P(state, Ger, Ger).Level = 10000;
		CSpyPresence poles = P(state, Pol, Ger);
		poles.SetMission((int)ESpyMission.Military, 1);

		int caught = 0;
		for (int day = 0; day < 300; day++)
		{
			poles.Level = 10000;
			CCounterEspionage.Run(state, P(state, Ger, Ger), C(state, Ger), C(state, Ger));
			caught += poles.Level < 10000 ? 1 : 0;
		}

		Assert.InRange(caught, 15, 45);
		Assert.Equal(caught, C(state, Ger).SpiesCaught);
	}

	[Fact]
	public void A_catch_tells_the_played_catcher_where_the_spy_came_from()
	{
		CGameState state = State();
		state.Played.Add(Ger);
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;

		CSpyMessages.Caught(state, C(state, Ger), C(state, Pol), C(state, Ger));

		CPactMessage message = Assert.Single(messages);
		Assert.Equal(CSpyMessages.WeCaught, message.We);
		Assert.Equal(Ger, message.Told);
		Assert.Contains(("WHERE", "POL"), message.Values);
	}

	[Fact]
	public void Hostile_is_any_of_six_missions_or_counter_espionage_in_a_country_that_is_no_friend()
	{
		CGameState state = State();
		var presence = new CSpyPresence();
		presence.SetMission((int)ESpyMission.RaiseNationalUnity, 1);
		Assert.False(CCounterEspionage.IsHostile(state, presence, C(state, Pol), C(state, Ger)));

		presence.SetMission((int)ESpyMission.CounterEspionage, 1);
		Assert.True(CCounterEspionage.IsHostile(state, presence, C(state, Pol), C(state, Ger)));
		Assert.False(CCounterEspionage.IsHostile(state, presence, C(state, Ita), C(state, Ger)));   // the axis

		presence.SetMission((int)ESpyMission.IncreaseThreat, 1);
		Assert.True(CCounterEspionage.IsHostile(state, presence, C(state, Ita), C(state, Ger)));
	}

	[Fact]
	public void Covert_ops_earn_a_thirtieth_of_the_monthly_figure_a_day_up_to_the_most()
	{
		CGameState state = State();
		CSpyPresence presence = P(state, Ger, Pol);
		presence.Level = 10000;
		presence.SetMission((int)ESpyMission.CovertOps, 1);

		CSpyMissions.Run(state, presence, C(state, Ger), C(state, Pol));
		Assert.Equal(166, presence.CovertOpsPoints);

		presence.CovertOpsPoints = 49950;
		CSpyMissions.Run(state, presence, C(state, Ger), C(state, Pol));
		Assert.Equal(50000, presence.CovertOpsPoints);
	}

	[Fact]
	public void Boosting_the_ruling_party_raises_the_target_s_organisation_and_the_spies_tally()
	{
		CGameState state = ScriptWorld.State(groups: AlignmentTests.Groups());
		state.Defines.Set("country", "SPY_PARTY_ORGANIZATION_BOOST", CFixedPoint.Parse("0.01"));
		CCountry germany = C(state, Ger);
		germany.Organisation = new CPartyFigures(state.Ideologies.Count);
		germany.RulingIdeology = "national_socialist";
		int party = state.Ideology("national_socialist")!.Index;
		germany.Organisation.Set(party, 20000, CPartyFigures.HistorySlot);
		CSpyPresence poles = P(state, Pol, Ger);
		poles.Level = 10000;
		poles.SetMission((int)ESpyMission.BoostRulingParty, 1);

		CSpyMissions.Run(state, poles, C(state, Pol), germany);

		Assert.Equal(20100, germany.Organisation[party]);
		Assert.Equal(20100, germany.Organisation.Tallies[party, CPartyFigures.SpySlot]);
	}

	[Fact]
	public void Military_espionage_runs_where_the_day_and_the_target_s_number_make_eight()
	{
		CGameState state = State();
		// Germany is first here, so its number in the original's list would be 1: the 8th, 16th and 24th of a month;
		// Poland's is 2: the 7th, 15th and 23rd.
		Assert.False(CSpyMissions.IsMilitaryDay(state, C(state, Ger)));   // 1 June
		for (int hour = 0; hour < 6 * CGameState.HoursPerDay; hour++)
		{
			state.AdvanceOneHour();
		}
		Assert.True(CSpyMissions.IsMilitaryDay(state, C(state, Pol)));    // 7 June
		Assert.False(CSpyMissions.IsMilitaryDay(state, C(state, Ger)));
	}

	[Fact]
	public void A_new_grant_replaces_the_old_and_each_goes_when_its_days_are_up()
	{
		var unit = new CUnit(EUnitKind.Division, Pol);
		CIntelGrants.Grant(unit, Ger, 32, 9);
		CIntelGrants.Grant(unit, Ger, 2, 2);

		CIntelGrant grant = Assert.Single(unit.IntelGrants);
		Assert.Equal(2, grant.Level);
		Assert.Equal(2, grant.Days);
	}

	[Fact]
	public void The_pick_is_the_first_whose_running_total_of_weights_passes_the_draw()
	{
		long[] weights = { 0, 3, 0, 5 };

		Assert.Equal(1, CTechnologyEspionage.Pick(weights, 2));
		Assert.Equal(3, CTechnologyEspionage.Pick(weights, 3));
		Assert.Equal(1, CTechnologyEspionage.Pick(weights, 8));   // 8 % 8 is 0
		Assert.Equal(-1, CTechnologyEspionage.Pick(new long[] { 0, 0 }, 5));
	}

	[Fact]
	public void Lowering_unity_is_a_modifier_scaled_by_the_spies_and_the_share()
	{
		var lower = new CModifier(CSpyModifiers.Lower);
		lower.Add("national_unity", CFixedPoint.Parse("-0.1"));
		var germany = new CCountry { Tag = Ger, Name = "GER", DefinitionFile = "" };
		var italy = new CCountry { Tag = Ita, Name = "ITA", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false), new CProvince(1, "p1", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(2, "p2", isSea: false) { Owner = Ita, Controller = Ita },
			},
			new[] { germany, italy }, staticModifiers: new[] { lower });
		CSpyPresence italians = CGameState.Presence(italy, germany);
		italians.Level = 5000;
		italians.SetMission((int)ESpyMission.LowerNationalUnity, 1);
		italians.SetMission((int)ESpyMission.Technology, 1);

		CCountryModifiers.Rebuild(state);

		// Five spies, half of them on it: -0.1 x 5 x 0.5.
		Assert.Equal(CFixedPoint.Parse("-0.25"), germany.Modifier["national_unity"]);
	}

	[Fact]
	public void The_priority_command_stores_anything_and_the_mission_command_always_stamps_the_hour()
	{
		CGameState state = State();

		new CChangeSpyPriorityCommand(Ger, Pol, 7).Execute(state);
		Assert.Equal(7, P(state, Ger, Pol).Priority);

		new CChangeSpyMissionCommand(Ger, Pol, 9, 3).Execute(state);
		Assert.False(P(state, Ger, Pol).HasAnyMission());
		Assert.Equal(state.TotalHours, P(state, Ger, Pol).LastMissionChange);

		new CChangeSpyMissionCommand(Ger, Pol, (int)ESpyMission.Technology, 2).Execute(state);
		Assert.Equal(1000, P(state, Ger, Pol).Share(ESpyMission.Technology));
	}

	[Fact]
	public void Spies_and_spy_mission_count_ask_the_scope_country_s_presence_in_this()
	{
		CGameState state = State();
		CSpyPresence presence = P(state, Ger, Pol);
		presence.Level = 3000;
		presence.SetMission((int)ESpyMission.Technology, 1);
		presence.SetMission((int)ESpyMission.Military, 1);
		CEventScope inPoland = CEventScope.Start(Pol).With(Ger, 0);

		Assert.True(CConditions.Holds(state, ScriptWorld.Condition("spies = 3"), inPoland));
		Assert.False(CConditions.Holds(state, ScriptWorld.Condition("spies = 4"), inPoland));
		Assert.True(CConditions.Holds(state, ScriptWorld.Condition("spy_mission_count = { type = 2 value = 1.5 }"), inPoland));
		Assert.False(CConditions.Holds(state, ScriptWorld.Condition("spy_mission_count = { type = 2 value = 1.6 }"), inPoland));
		// THIS is the scope itself: its own presence at home, where it has nobody yet.
		Assert.False(CConditions.Holds(state, ScriptWorld.Condition("spies = 1"), CEventScope.Start(Ger)));
	}

	[Fact]
	public void A_new_game_s_spies_score_the_pair_and_halve_it_for_a_minor()
	{
		CGameState state = State();

		// Poland: a relation under -100 (2), a neighbour (1), one continent (1) - halved for a minor.
		Assert.Equal(2, CSpyStart.Score(state, C(state, Ger), C(state, Pol), -150));
		// Its own: one continent and itself (3), halved.
		Assert.Equal(2, CSpyStart.Score(state, C(state, Ger), C(state, Ger), 25));
		// Italy, in Africa here: of one faction, so nothing.
		Assert.Equal(0, CSpyStart.Score(state, C(state, Ger), C(state, Ita), 75));
	}

	[Fact]
	public void A_coup_spends_the_spies_and_twenty_points_held_at_nought()
	{
		CGameState state = State();
		CSpyPresence presence = P(state, Pol, Ger);
		presence.Level = 4000;
		presence.CovertOpsPoints = 15000;

		CCoup.Perform(state, C(state, Pol), C(state, Ger));

		Assert.Equal(0, presence.Level);
		Assert.Equal(0, presence.CovertOpsPoints);
		// Poland's party group holds no seat in Germany's cabinet here, so it could not have succeeded.
		Assert.Equal(0, CCoup.Chance(state, C(state, Pol), C(state, Ger)));
	}

	[Fact]
	public void A_covert_operation_takes_its_cost_about_the_controller_with_no_floor()
	{
		CGameState state = State();
		var operation = new CCovertOperation("scout_area") { Cost = 15 };
		P(state, Ger, Pol).CovertOpsPoints = 20000;

		Assert.True(CCovertOps.MaySelect(state, C(state, Ger), state.Province(4), operation));
		CCovertOps.Send(state, C(state, Ger), state.Province(4), operation);
		Assert.Equal(5000, P(state, Ger, Pol).CovertOpsPoints);
		Assert.False(CCovertOps.MaySelect(state, C(state, Ger), state.Province(4), operation));

		CCovertOps.Send(state, C(state, Ger), state.Province(4), operation);
		Assert.Equal(-10000, P(state, Ger, Pol).CovertOpsPoints);
	}

	[Fact]
	public void A_minister_waits_one_to_ten_hours_and_then_works_in_its_country_s_hour_and_the_player_has_none()
	{
		CGameState state = State();
		state.Played.Add(Ger);

		CEspionageAi.SetUpAtStart(state);

		Assert.Equal(0, C(state, Ger).IntelligenceCountdown);
		Assert.InRange(C(state, Pol).IntelligenceCountdown, 1, 10);
		// Poland is second here: number 2 in the original's list, so its hour is 03:00.
		Assert.Equal(3, CEspionageAi.Hour(C(state, Pol)));
	}

	[Fact]
	public void An_annexation_takes_every_country_s_spies_in_the_annexed()
	{
		CGameState state = State();
		P(state, Ita, Pol).Level = 3000;

		CAnnexation.Annex(state, C(state, Ger), C(state, Pol));

		Assert.Equal(0, P(state, Ita, Pol).Level);
	}
}
