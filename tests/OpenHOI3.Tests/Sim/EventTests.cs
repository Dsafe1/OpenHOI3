using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Events;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// An event firing and a decision taken, against <c>docs/mechanics/events.md</c>, on
/// <see cref="ScriptWorld"/>'s strip of ground.
/// </summary>
public class EventTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;

	private static CEvent Event(int id, string trigger = "", int days = 30, params string[] options)
	{
		var made = new CEvent(isProvinceEvent: false, "events/test.txt", id) { Id = id };
		made.Trigger.Children.AddRange(ScriptWorld.Condition(trigger).Children);
		made.MeanTimeToHappen.Days = days;
		foreach (string effects in options.Length == 0 ? new[] { "set_country_flag = answered" } : options)
		{
			var option = new CEventOption { Name = $"EVT_{id}_OPTION" };
			option.Effects.AddRange(ScriptWorld.Effects(effects));
			made.Options.Add(option);
		}
		return made;
	}

	private static CEventOption Weighted(string factor)
	{
		var option = new CEventOption();
		option.AiChance.AiFactor = CFixedPoint.Parse(factor);
		return option;
	}

	private static bool Flag(CGameState state, CCountryTag tag, string flag) => state.Country(tag)!.Flags.IsSet(flag);

	[Fact]
	public void The_chance_of_a_day_makes_the_days_a_median()
	{
		CGameState state = ScriptWorld.State();
		var thirty = new CMeanTimeToHappen { Days = 30 };
		var none = new CMeanTimeToHappen { Days = 0 };
		var halved = new CMeanTimeToHappen { Days = 60 };
		halved.Modifiers.Add(new CMeanTimeToHappenModifier(CFixedPoint.Parse("0.5"), ScriptWorld.Condition("tag = GER")));
		CEventScope scope = CEventScope.Start(Ger);

		Assert.Equal(0.0228, CEventChance.OfDay(state, thirty, scope), 4);
		Assert.Equal(1f, CEventChance.OfDay(state, none, scope));
		Assert.Equal(CEventChance.OfDay(state, thirty, scope), CEventChance.OfDay(state, halved, scope));
	}

	[Fact]
	public void The_AI_takes_the_first_option_whose_share_the_roll_does_not_pass()
	{
		CGameState state = ScriptWorld.State();
		CEvent choice = Event(1);
		choice.Options.Clear();
		choice.Options.Add(Weighted("1"));
		choice.Options.Add(Weighted("3"));
		for (uint seed = 1; seed < 30; seed++)
		{
			state.Random.Seed(seed);
			var copy = new CRandom(seed);
			int roll = copy.Next() % 100;

			int chosen = CEventFiring.ChooseOption(state, choice, CEventScope.Start(Ger));

			Assert.Equal(roll <= 25 ? 0 : 1, chosen);
		}
	}

	[Fact]
	public void Weights_of_nothing_and_shares_cut_short_fall_to_the_first_option()
	{
		CGameState state = ScriptWorld.State();
		CEvent nothing = Event(1);
		nothing.Options.Clear();
		nothing.Options.Add(Weighted("0"));
		nothing.Options.Add(Weighted("0"));
		// Six even options have shares of 16, running to 96: a roll of 97 to 99 passes them all.
		CEvent sixths = Event(2);
		sixths.Options.Clear();
		for (int i = 0; i < 6; i++)
		{
			sixths.Options.Add(Weighted("1"));
		}

		Assert.Equal(0, CEventFiring.ChooseOption(state, nothing, CEventScope.Start(Ger)));
		var seen = new HashSet<int>();
		for (uint seed = 1; seed < 400; seed++)
		{
			state.Random.Seed(seed);
			int roll = new CRandom(seed).Next() % 100;
			int expected = roll > 96 ? 0 : roll / 16 - (roll % 16 == 0 && roll > 0 ? 1 : 0);
			Assert.Equal(expected, CEventFiring.ChooseOption(state, sixths, CEventScope.Start(Ger)));
			seen.Add(roll);
		}
		Assert.Contains(seen, roll => roll > 96);
	}

	[Fact]
	public void Candidates_are_events_with_options_not_triggered_only_whose_trigger_holds_a_month_ahead()
	{
		CGameState state = ScriptWorld.State();
		CEvent german = Event(1, "tag = GER");
		CEvent triggered = Event(2);
		triggered.IsTriggeredOnly = true;
		CEvent optionless = Event(3);
		optionless.Options.Clear();
		CEvent dated = Event(4, "date = 1936.6.20");
		state.Events = new[] { german, triggered, optionless, dated };

		CEventPass.RebuildCandidates(state);

		Assert.Equal(new[] { german, dated }, state.Country(Ger)!.EventCandidates);
		Assert.Equal(new[] { dated }, state.Country(Pol)!.EventCandidates);
	}

	[Fact]
	public void An_AI_country_posts_its_answer_and_its_immediate_block_runs_first()
	{
		CGameState state = ScriptWorld.State();
		CEvent fired = Event(1, options: "set_country_flag = option_ran");
		fired.Immediate.Effects.AddRange(ScriptWorld.Effects("set_country_flag = immediate_ran"));
		state.Events = new[] { fired };

		CEventFiring.Fire(state, fired, CEventScope.Start(Pol));

		Assert.True(Flag(state, Pol, "immediate_ran"));
		Assert.False(Flag(state, Pol, "option_ran"));
		var answer = Assert.IsType<CSelectEventOptionCommand>(Assert.Single(state.Outbox));
		Assert.True(answer.MayExecute(state));
		answer.Execute(state);
		Assert.True(Flag(state, Pol, "option_ran"));
	}

	[Fact]
	public void A_persons_event_is_opened_once_and_its_immediate_block_runs_each_time()
	{
		CGameState state = ScriptWorld.State();
		state.Played.Add(Ger);
		CEvent fired = Event(1);
		fired.Immediate.Effects.AddRange(ScriptWorld.Effects("manpower = 1"));
		state.Events = new[] { fired };

		CEventFiring.Fire(state, fired, CEventScope.Start(Ger));
		CEventFiring.Fire(state, fired, CEventScope.Start(Ger));

		Assert.Single(state.OpenEvents);
		Assert.Empty(state.Outbox);
		Assert.Equal(CFixedPoint.FromInt(52), state.Country(Ger)!.Manpower);
	}

	[Fact]
	public void An_unanswered_event_takes_its_first_option_after_four_months()
	{
		CGameState state = ScriptWorld.State();
		state.Played.Add(Ger);
		CEvent fired = Event(1, options: new[] { "set_country_flag = first", "set_country_flag = second" });
		state.Events = new[] { fired };
		CEventFiring.Fire(state, fired, CEventScope.Start(Ger));

		long deadline = state.OpenEvents[0].DeadlineHour;
		Assert.Equal(new CDate(1936, 10, 1).DayNumber * 24L, deadline);
		while (state.TotalHours <= deadline)
		{
			CEventPass.RunHourly(state);
			Assert.Empty(state.Outbox);
			state.AdvanceOneHour();
		}
		CEventPass.RunHourly(state);

		Assert.Empty(state.OpenEvents);
		var answer = Assert.IsType<CSelectEventOptionCommand>(Assert.Single(state.Outbox));
		Assert.Equal(0, answer.Option);
	}

	[Fact]
	public void A_fire_only_once_event_fires_once_a_game_for_whoever_gets_it_first()
	{
		CGameState state = ScriptWorld.State();
		CEvent once = Event(1);
		once.FireOnlyOnce = true;
		once.Immediate.Effects.AddRange(ScriptWorld.Effects("set_country_flag = fired"));
		state.Events = new[] { once };

		CEventFiring.Fire(state, once, CEventScope.Start(Pol));
		CEventFiring.Fire(state, once, CEventScope.Start(ScriptWorld.Ita));
		CEventPass.RebuildCandidates(state);

		Assert.True(Flag(state, Pol, "fired"));
		Assert.False(Flag(state, ScriptWorld.Ita, "fired"));
		Assert.Empty(state.Country(Ger)!.EventCandidates);
	}

	[Fact]
	public void A_certain_event_fires_every_day_its_trigger_holds()
	{
		CGameState state = ScriptWorld.State();
		CEvent certain = Event(1, "tag = POL", days: 0);
		certain.Immediate.Effects.AddRange(ScriptWorld.Effects("manpower = 1"));
		state.Events = new[] { certain };
		CEventPass.RebuildCandidates(state);

		CEventPass.RunDaily(state);
		CEventPass.RunDaily(state);

		Assert.Equal(CFixedPoint.FromInt(2), state.Country(Pol)!.Manpower);
		Assert.Equal(2, state.Outbox.Count);
	}

	[Fact]
	public void An_answer_sets_the_scope_back_to_its_seed_and_is_dropped_for_an_event_nobody_has()
	{
		CGameState state = ScriptWorld.State();
		CEvent fired = Event(1);
		state.Events = new[] { fired };
		CEventScope scope = CEventScope.Start(Ger, randomState: 5);
		scope.Draw(10);

		var answer = new CSelectEventOptionCommand(fired, scope, 0);
		answer.Execute(state);

		Assert.Equal(5u, scope.RandomState);
		Assert.False(new CSelectEventOptionCommand(Event(2), scope, 0).MayExecute(state));
	}

	[Fact]
	public void The_AIs_answer_runs_at_the_next_turn_of_the_queue()
	{
		CGameState state = ScriptWorld.State();
		CEvent fired = Event(1, options: "set_country_flag = option_ran");
		state.Events = new[] { fired };
		var simulation = new CSimulation(state);

		simulation.Post(new CFireForTest(fired, CEventScope.Start(Pol)));
		simulation.Pump();
		Assert.False(Flag(state, Pol, "option_ran"));
		simulation.Pump();

		Assert.True(Flag(state, Pol, "option_ran"));
	}

	[Fact]
	public void Decisions_offered_are_the_diplomatic_ones_that_may_be_taken_first_and_backwards()
	{
		CGameState state = ScriptWorld.State();
		var first = new CDecision("first", EDecisionGroup.Diplomatic, "decisions/test.txt", 1);
		var second = new CDecision("second", EDecisionGroup.Diplomatic, "decisions/test.txt", 2);
		var refused = new CDecision("refused", EDecisionGroup.Diplomatic, "decisions/test.txt", 3);
		refused.Allow.Children.AddRange(ScriptWorld.Condition("tag = POL").Children);
		var hidden = new CDecision("hidden", EDecisionGroup.Diplomatic, "decisions/test.txt", 4);
		hidden.Potential.Children.AddRange(ScriptWorld.Condition("tag = POL").Children);
		var political = new CDecision("political", EDecisionGroup.Political, "decisions/test.txt", 5);
		state.Decisions = new[] { first, refused, second, hidden, political };

		var offered = CDecisions.Offered(state, Ger);

		Assert.Equal(new[] { "second", "first", "refused" }, offered.Select(o => o.Decision.Name));
		Assert.Equal(new[] { true, true, false }, offered.Select(o => o.MayTake));
		Assert.Same(political, CDecisions.Find(state, "political"));
	}

	[Fact]
	public void A_decision_is_asked_again_when_it_runs_and_may_be_taken_again()
	{
		CGameState state = ScriptWorld.State();
		var decision = new CDecision("raise", EDecisionGroup.Diplomatic, "decisions/test.txt", 1);
		decision.Allow.Children.AddRange(ScriptWorld.Condition("tag = GER").Children);
		decision.Effects.AddRange(ScriptWorld.Effects("manpower = 10"));
		state.Decisions = new[] { decision };

		foreach (int _ in new[] { 1, 2 })
		{
			var take = new CExecuteDecisionCommand("raise", CDecisions.ScopeOf(Ger));
			Assert.True(take.MayExecute(state));
			take.Execute(state);
		}

		Assert.Equal(CFixedPoint.FromInt(70), state.Country(Ger)!.Manpower);
		Assert.False(new CExecuteDecisionCommand("raise", CDecisions.ScopeOf(Pol)).MayExecute(state));
		Assert.False(new CExecuteDecisionCommand("nothing", CDecisions.ScopeOf(Ger)).MayExecute(state));
	}

	/// <summary>A command that fires an event, as the day's pass does inside the hour's command.</summary>
	private sealed class CFireForTest : CCommand
	{
		private readonly CEvent _event;
		private readonly CEventScope _scope;

		public CFireForTest(CEvent fired, CEventScope scope)
		{
			_event = fired;
			_scope = scope;
		}

		public override void Execute(CGameState state)
		{
			CEventFiring.Fire(state, _event, _scope);
		}
	}
}
