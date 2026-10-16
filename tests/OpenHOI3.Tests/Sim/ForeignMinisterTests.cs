using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Script;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The foreign minister's decisions and prepared wars, against <c>docs/mechanics/foreignminister.md</c>: its first work
/// and hour, the decisions pass, the war targets and their pruning, the walk and what is ready. Germany holds Berlin (1)
/// and Kiel (2), Poland Posen (3) beside Berlin, Albania Tirana (4) and Italy Rome (5), each alone; Slovakia is
/// Germany's puppet holding Pressburg (6) beside Kiel. Germany and Italy are the axis.
/// </summary>
public class ForeignMinisterTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");
	private static readonly CCountryTag Alb = new("ALB");
	private static readonly CCountryTag Ita = new("ITA");
	private static readonly CCountryTag Slo = new("SLO");
	private static readonly CCountryTag Nul = new("---");

	/// <summary>Answers each decision by its name - a missing name answers 50 - and keeps what it was asked.</summary>
	private sealed class Scripts : IAiScripts
	{
		public Dictionary<string, int?> Answers { get; } = new();

		public List<(string Decision, uint Seed)> Asked { get; } = new();

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) => 0;

		public int? EvaluateDecision(CGameState state, CCountry country, CDecision decision, CEventScope scope, out string? error)
		{
			Asked.Add((decision.Name, scope.Seed));
			int? answer = Answers.TryGetValue(decision.Name, out int? given) ? given : 50;
			error = answer is null ? "broken " + decision.Name : null;
			return answer;
		}

		/// <summary>The days the tick was run for, by country.</summary>
		public List<CCountryTag> Ticks { get; } = new();

		public List<(CCountryTag Minister, CCountryTag Joiner, CCountryTag Enemy)> Told { get; } = new();

		public void RunForeign(CGameState state, CCountry country) => Ticks.Add(country.Tag);

		public void ForeignOnWar(CGameState state, CCountry country, CCountryTag joiner, CCountryTag enemy, CWar war) =>
			Told.Add((country.Tag, joiner, enemy));
	}

	private static CGameState World(Scripts? scripts = null)
	{
		CCountry Country(CCountryTag tag, int capital, bool major = false) =>
			new() { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital, IsMajor = major };
		CCountry germany = Country(Ger, 1, major: true);
		germany.Faction = "axis";
		CCountry italy = Country(Ita, 5, major: true);
		italy.Faction = "axis";
		CCountry slovakia = Country(Slo, 6);
		slovakia.Overlord = Ger;
		CProvince Land(int id, CCountryTag owner, params int[] beside) =>
			new(id, $"p{id}", isSea: false) { Owner = owner, Controller = owner, Neighbours = beside, Continent = "europe" };
		CProvince[] land =
		{
			Land(1, Ger, 2, 3), Land(2, Ger, 1, 6), Land(3, Pol, 1), Land(4, Alb), Land(5, Ita), Land(6, Slo, 2),
		};
		TestRoads.Pave(land);
		var countries = new[] { germany, Country(Pol, 3), Country(Alb, 4), italy, slovakia };
		return new CGameState(new CDate(1939, 3, 1), new[] { new CProvince(0, "", isSea: false) }.Concat(land).ToArray(),
			countries, buildings: new[] { TestRoads.Infra })
		{
			AiScripts = scripts,
		};
	}

	private static CDecision Decision(string name, EDecisionGroup group = EDecisionGroup.Diplomatic, string? allow = null)
	{
		var decision = new CDecision(name, group, "decisions/test.txt", 1);
		if (allow is not null)
		{
			decision.Allow.Children.AddRange(ScriptWorld.Condition(allow).Children);
		}
		return decision;
	}

	private static void Advance(CGameState state, int hours)
	{
		for (int i = 0; i < hours; i++)
		{
			state.AdvanceOneHour();
		}
	}

	private static IEnumerable<string> Posted(CGameState state) =>
		state.Outbox.OfType<CExecuteDecisionCommand>().Select(c => c.Decision);

	// ---- the minister's first work and hour

	[Fact]
	public void A_foreign_minister_first_works_one_to_three_hours_on_and_none_is_made_for_a_played_country()
	{
		CGameState state = World();
		state.Played.Add(Pol);

		CForeignAi.SetUpAtStart(state);

		Assert.InRange(state.Country(Ger)!.ForeignCountdown, 1, 3);
		Assert.Equal(0, state.Country(Pol)!.ForeignCountdown);
		// One draw for every country nobody plays.
		Assert.Equal(4, state.Random.Draws);
		CCountry germany = state.Country(Ger)!;
		Assert.Equal((germany.Index + 1 + 13) % 24, CResearchAi.Hour(germany, 13));
	}

	[Fact]
	public void The_first_work_only_clears_the_skip_and_the_tick_then_runs_in_its_hour_once_a_day()
	{
		var scripts = new Scripts();
		CGameState state = World(scripts);
		CCountry germany = state.Country(Ger)!;
		germany.ForeignCountdown = 1;
		germany.ForeignMinister.SkipOnce = true;
		foreach (CCountry other in state.Countries.Where(c => c != germany))
		{
			other.ForeignCountdown = 0;
		}

		int hour = CResearchAi.Hour(germany, 13);
		while (state.TotalHours % 24 != hour)
		{
			state.AdvanceOneHour();
		}
		CForeignAi.RunHourly(state);
		Assert.False(germany.ForeignMinister.SkipOnce);
		Assert.Empty(scripts.Ticks);

		for (int i = 0; i < 48; i++)
		{
			state.AdvanceOneHour();
			CForeignAi.RunHourly(state);
		}
		Assert.Equal(new[] { Ger, Ger }, scripts.Ticks);
	}

	// ---- the proposal and the proposed war

	[Fact]
	public void The_best_selectable_proposal_is_kept_and_posted_above_fifty_and_one_at_fifty_stays()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		COffer Nap(CCountryTag to) => new(EOfferKind.NonAggressionPact, Ger, to, EOfferStage.Proposed, true, 0);

		CForeignAi.Propose(state, germany, Nap(Pol), 50f);
		CForeignAi.Propose(state, germany, Nap(Alb), 40f);
		// Not selectable: an offer to itself.
		CForeignAi.Propose(state, germany, Nap(Ger), 90f);
		Assert.Equal(Pol, germany.ForeignMinister.Proposal!.Recipient);

		CForeignAi.PostProposal(state, germany);
		Assert.Empty(state.Outbox);
		Assert.NotNull(germany.ForeignMinister.Proposal);

		CForeignAi.Propose(state, germany, Nap(Alb), 60f);
		CForeignAi.PostProposal(state, germany);
		COfferCommand posted = Assert.IsType<COfferCommand>(Assert.Single(state.Outbox));
		Assert.Equal(Alb, posted.Offer.Recipient);
		Assert.Null(germany.ForeignMinister.Proposal);
		Assert.Equal(-1f, germany.ForeignMinister.ProposalScore);
	}

	[Fact]
	public void A_proposed_war_is_replaced_only_by_one_forty_better_and_occupation_is_in_hundredths()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;

		CForeignAi.ProposeWar(germany, Pol, 50f);
		CForeignAi.ProposeWar(germany, Alb, 90f);
		Assert.Equal(Pol, germany.ForeignMinister.ProposedWar);
		CForeignAi.ProposeWar(germany, Alb, 90.5f);
		Assert.Equal(Alb, germany.ForeignMinister.ProposedWar);
		CForeignAi.ClearWarProposal(germany);
		Assert.Null(germany.ForeignMinister.ProposedWar);

		state.Province(2).Controller = Pol;
		Assert.Equal(50, CForeignAi.PercOccupied(state, Ger));
		Assert.Equal(0, CForeignAi.PercOccupied(state, Pol));
		Assert.Equal(0, CForeignAi.PercOccupied(state, new CCountryTag("XXX")));
	}

	[Fact]
	public void A_ready_war_with_no_decision_is_declared_limited_where_it_was_prepared_so()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CWarTargets.Prepare(state, germany, Alb, 0, null, true);

		CForeignAi.WalkWarTargets(state, germany);

		COfferCommand declared = Assert.IsType<COfferCommand>(Assert.Single(state.Outbox));
		Assert.Equal((EOfferKind.DeclareWar, Alb, false), (declared.Offer.Kind, declared.Offer.Recipient, declared.Offer.Value));
		Assert.NotNull(declared.Offer.WarGoal);
	}

	[Fact]
	public void Every_foreign_minister_is_told_of_each_new_pair_at_war()
	{
		var scripts = new Scripts();
		CGameState state = World(scripts);
		foreach (CCountry country in state.Countries)
		{
			country.ForeignCountdown = country.Tag == Pol ? 0 : 1;
		}
		var war = new CWar("war");
		state.Wars.Add(war);

		CWarSides.Join(state, war, Ger, attacking: true);
		CWarSides.Join(state, war, Pol, attacking: false);
		CWarSides.Join(state, war, Alb, attacking: false);

		// Poland has no minister; each pair goes to the other four.
		Assert.Equal(8, scripts.Told.Count);
		Assert.Equal(new[] { (Pol, Ger), (Alb, Ger) }, scripts.Told.Where(t => t.Minister == Ger).Select(t => (t.Joiner, t.Enemy)));
	}

	// ---- the decisions pass

	[Fact]
	public void The_pass_asks_the_diplomatic_decisions_that_may_be_taken_in_order_and_posts_those_answered_above_nought()
	{
		var scripts = new Scripts();
		CGameState state = World(scripts);
		state.Decisions = new[]
		{
			Decision("first"), Decision("refused", allow: "tag = POL"), Decision("political", EDecisionGroup.Political),
			Decision("nought"), Decision("second"),
		};
		scripts.Answers["nought"] = 0;

		CForeignAi.ExecuteDiploDecisions(state, state.Country(Ger)!);

		Assert.Equal(new[] { "first", "nought", "second" }, scripts.Asked.Select(a => a.Decision));
		Assert.Equal(new[] { "first", "second" }, Posted(state));
		// One scope, one draw, for the whole pass.
		Assert.Single(scripts.Asked.Select(a => a.Seed).Distinct());
		Assert.Equal(1, state.Random.Draws);
		Assert.All(state.Outbox.OfType<CExecuteDecisionCommand>(), c => Assert.Equal(Ger, c.Scope.Country));
	}

	[Fact]
	public void A_failed_answer_ends_the_pass_there()
	{
		var scripts = new Scripts();
		CGameState state = World(scripts);
		state.Decisions = new[] { Decision("first"), Decision("broken"), Decision("after") };
		scripts.Answers["broken"] = null;

		string? error = CForeignAi.ExecuteDiploDecisions(state, state.Country(Ger)!);

		Assert.Equal("broken broken", error);
		Assert.Equal(new[] { "first", "broken" }, scripts.Asked.Select(a => a.Decision));
		Assert.Equal(new[] { "first" }, Posted(state));
	}

	// ---- prepared wars

	[Fact]
	public void A_war_is_not_prepared_on_nobody_a_country_with_no_land_a_puppet_or_a_faction_member()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		state.Province(4).Owner = null;

		foreach (CCountryTag target in new[] { Nul, Alb, Slo, Ita })
		{
			CWarTargets.Prepare(state, germany, target, 0, null, false);
		}

		Assert.Empty(germany.WarTargets);
	}

	[Fact]
	public void A_second_preparation_changes_only_the_value_and_keeps_the_first_decision()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CDecision danzig = Decision("danzig_or_war");

		CWarTargets.Prepare(state, germany, Pol, 1, danzig, false);
		Advance(state, 24);
		CWarTargets.Prepare(state, germany, Pol, 2, null, true);

		CWarTarget entry = Assert.Single(germany.WarTargets);
		Assert.Same(danzig, entry.Decision);
		Assert.False(entry.Limited);
		Assert.Equal(2, entry.ValueBits);
		Assert.Equal(state.TotalHours - 24, entry.Hour);
	}

	[Fact]
	public void Preparing_war_counts_a_listed_target_until_the_war_begins()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		Assert.False(CWarTargets.IsPreparingWar(state, germany));

		CWarTargets.Prepare(state, germany, Pol, 0, null, false);

		Assert.True(CWarTargets.IsPreparingWar(state, germany));
		Assert.True(CWarTargets.IsPreparingWarWith(state, germany, Pol));
		Assert.False(CWarTargets.IsPreparingWarWith(state, germany, Alb));
		var war = new CWar("war");
		war.Attackers.Add(Ger);
		war.Defenders.Add(Pol);
		state.Wars.Add(war);
		Assert.False(CWarTargets.IsPreparingWarWith(state, germany, Pol));
		// Still listed, and still counted as being prepared.
		Assert.True(CWarTargets.IsPreparingWar(state, germany));
		CWarTargets.Cancel(germany, Pol);
		Assert.Empty(germany.WarTargets);
	}

	[Fact]
	public void While_a_great_target_is_listed_a_small_one_older_than_three_months_or_a_month_before_it_is_dropped()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		state.Country(Pol)!.MaxIc = 50;

		CWarTargets.Prepare(state, germany, Pol, 0, null, false);
		Advance(state, 24 * 31 * 4);
		CWarTargets.Prune(state, germany);
		Assert.Single(germany.WarTargets);

		// A month and more on, Italy - a major - is prepared against too: Poland, four months old, goes.
		germany.Faction = "";
		CWarTargets.Prepare(state, germany, Ita, 0, null, false);
		CWarTargets.Prune(state, germany);
		Assert.Equal(new[] { Ita }, germany.WarTargets.Select(t => t.Target));
	}

	// ---- the walk

	[Fact]
	public void A_ready_target_has_its_decision_posted_without_the_lua_and_a_neighbour_is_not_ready()
	{
		var scripts = new Scripts();
		CGameState state = World(scripts);
		CCountry germany = state.Country(Ger)!;
		CDecision albania = Decision("annexation_of_albania");
		CDecision refused = Decision("refused", allow: "tag = POL");
		state.Decisions = new[] { albania, refused };

		Assert.True(CForeignAi.IsReady(state, germany, Alb));
		Assert.False(CForeignAi.IsReady(state, germany, Pol));

		CWarTargets.Prepare(state, germany, Alb, 0, albania, false);
		CWarTargets.Prepare(state, germany, Ita, 0, refused, false);
		germany.Faction = "";
		CWarTargets.Prepare(state, germany, Ita, 0, refused, false);
		CForeignAi.WalkWarTargets(state, germany);

		Assert.Equal(new[] { "annexation_of_albania" }, Posted(state));
		Assert.Empty(scripts.Asked);
		// A draw for each ready target with a decision.
		Assert.Equal(2, state.Random.Draws);

		// With Poland, a neighbour that is not ready, listed too, the day ends before anything is posted.
		state.Outbox.Clear();
		CWarTargets.Prepare(state, germany, Pol, 0, null, false);
		CForeignAi.WalkWarTargets(state, germany);
		Assert.Empty(state.Outbox);
	}

	[Fact]
	public void Transit_rights_into_a_target_are_given_up_and_end_the_day()
	{
		CGameState state = World();
		CCountry germany = state.Country(Ger)!;
		CWarTargets.Prepare(state, germany, Pol, 0, null, false);
		CWarTargets.Prepare(state, germany, Alb, 0, Decision("annexation_of_albania"), false);
		CGameState.Status(germany, state.Country(Pol)!).HasMilitaryAccess = true;

		CForeignAi.WalkWarTargets(state, germany);

		COfferCommand given = Assert.IsType<COfferCommand>(Assert.Single(state.Outbox));
		Assert.Equal(EOfferKind.AskMilitaryAccess, given.Offer.Kind);
		Assert.Equal((Ger, Pol, false), (given.Offer.Actor, given.Offer.Recipient, given.Offer.Value));
	}
}
