using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Offers and pacts against <c>docs/mechanics/agreements.md</c>, on <see cref="ScriptWorld"/>: Germany
/// and Italy the Axis, Poland and Slovakia in no faction, with Their Finest Hour's defines.
/// </summary>
public class AgreementsTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;

	/// <summary>Answers every offer with one score.</summary>
	private sealed class FixedScripts : IAiScripts
	{
		public FixedScripts(int score)
		{
			Score = score;
		}

		public int Score { get; }

		public List<string> Asked { get; } = new();

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer)
		{
			Asked.Add($"{function} {actor.Tag} {recipient.Tag} {observer.Tag}");
			return Score;
		}
	}

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State();
		void Set(string name, string value) => state.Defines.Set(COfferRules.Section, name, CFixedPoint.Parse(value));
		Set("JOIN_ALLIANCE_INFLUENCE_COST", "5");
		Set("LEAVE_ALLIANCE_INFLUENCE_COST", "1");
		Set("GUARANTEE_INFLUENCE_COST", "5");
		Set("REVOKE_GUARANTEE_INFLUENCE_COST", "1");
		Set("EMBARGO_INFLUENCE_COST", "1");
		Set("MILACCESS_INFLUENCE_COST", "1");
		Set("INFLUENCE_INFLUENCE_COST", "3");
		Set("ALLIANCE_NEUTRALITY_LIMIT", "25");
		Set("ALLIANCE_RELATION_CHANGE", "15");
		Set("ALLIANCE_REJECT_RELATION_CHANGE", "-50");
		Set("GUARANTEE_NEUTRALITY_LIMIT", "60");
		Set("NAP_RELATION_CHANGE", "50");
		Set("LEAVE_NAP_RELATION_CHANGE", "-30");
		Set("NAP_JOIN_INFLUENCE_COST", "5");
		Set("NAP_REJECT_RELATION_CHANGE", "-20");
		Set("LEAVE_NAP_INFLUENCE_COST", "1");
		Set("MILACC_ACCEPT_RELATION_CHANGE", "10");
		Set("MILACC_DECLINE_RELATION_CHANGE", "-10");
		Set("REVOKE_GUARANTEE_RELATION_CHANGE", "-20");
		Set("DAYS_OF_INFLUENCE_RELATION_CHANGE", "-1");
		Set("DAYS_OF_ALIGN_RELATION_CHANGE", "-1");
		Set("ALIGN_INFLUENCE_COST", "1");
		Set("EMBARGO_RELATION_CHANGE", "-30");
		Set("EMBARGO_NETRALITY", "65");
		Set("INFLUENCE_UPKEEP", "1");
		Set("NAP_EXPIRY_MONTHS", "45");
		Set("NAP_UNBREAKABLE_MONTHS", "6");
		Set("NAP_FORCE_BALANCE_RULE_MONTHS", "6");
		foreach (CCountry country in state.Countries)
		{
			country.DiplomaticInfluence = CFixedPoint.FromInt(10);
		}
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static COffer Offer(CGameState state, EOfferKind kind, CCountryTag actor, CCountryTag recipient, bool value = true,
		EOfferStage stage = EOfferStage.Proposed) => new(kind, actor, recipient, stage, value, state.TotalHours);

	private static void Run(CGameState state, COffer offer) => new COfferCommand(offer).Execute(state);

	/// <summary>Runs what the game posted, as the queue's next turn would.</summary>
	private static void RunPosted(CGameState state)
	{
		var posted = new List<CCommand>(state.Outbox);
		state.Outbox.Clear();
		foreach (CCommand command in posted)
		{
			command.Execute(state);
		}
	}

	private static CFixedPoint Relation(CGameState state, CCountryTag a, CCountryTag b) => CGameState.Status(C(state, a), C(state, b)).Relation;

	[Fact]
	public void A_proposal_charges_influence_sends_a_diplomat_for_seven_days_and_waits_on_the_recipient()
	{
		CGameState state = World();
		Run(state, Offer(state, EOfferKind.Alliance, Pol, ScriptWorld.Slo));

		Assert.Equal(CFixedPoint.FromInt(5), C(state, Pol).DiplomaticInfluence);
		Assert.True(COfferRules.DiplomatEnRoute(state, C(state, Pol), C(state, ScriptWorld.Slo)));
		Assert.Equal(state.TotalHours + 168, CGameState.Status(C(state, Pol), C(state, ScriptWorld.Slo)).DiplomatUntil);
		Assert.Single(C(state, ScriptWorld.Slo).PendingOffers);
		// The diplomat greys the next offer's button, not its validity.
		COffer next = Offer(state, EOfferKind.NonAggressionPact, Pol, ScriptWorld.Slo);
		Assert.True(COfferRules.IsValid(state, next));
		Assert.False(COfferRules.IsSelectable(state, next));
	}

	[Fact]
	public void A_proposal_a_diplomat_waits_for_is_thrown_away_at_its_post_while_one_is_on_his_way()
	{
		CGameState state = World();
		CCountryTag slo = ScriptWorld.Slo;
		Run(state, Offer(state, EOfferKind.Alliance, Pol, slo));

		// A guarantee and a call to arms send no diplomat, and still wait for one already sent (FINDINGS-liveread.md, section 151).
		Assert.False(new COfferCommand(Offer(state, EOfferKind.Guarantee, Pol, slo)).MayPost(state));
		Assert.False(new COfferCommand(Offer(state, EOfferKind.CallAlly, Pol, slo) with { Versus = Ger }).MayPost(state));
		// An answer, a cancelled trade and a withdrawn expedition are never stopped; the other way round is another pair.
		Assert.True(new COfferCommand(Offer(state, EOfferKind.Alliance, Pol, slo, stage: EOfferStage.Accepted)).MayPost(state));
		Assert.True(new COfferCommand(Offer(state, EOfferKind.Trade, Pol, slo, value: false)).MayPost(state));
		Assert.True(new COfferCommand(Offer(state, EOfferKind.Expedition, Pol, slo, value: false)).MayPost(state));
		Assert.False(new COfferCommand(Offer(state, EOfferKind.Trade, Pol, slo)).MayPost(state));
		Assert.True(new COfferCommand(Offer(state, EOfferKind.Guarantee, slo, Pol)).MayPost(state));
		// Stopping lend-lease waits only where none is being given.
		COffer stop = Offer(state, EOfferKind.OfferLendLease, Pol, slo, value: false);
		Assert.False(new COfferCommand(stop).MayPost(state));
		C(state, Pol).LendLeaseGiving.Add(new OpenHOI3.Sim.Aid.CLendLeaseGift(slo));
		Assert.True(new COfferCommand(stop).MayPost(state));
		// The hour the diplomat arrives still counts; the next does not.
		CDiplomacyStatus status = CGameState.Status(C(state, Pol), C(state, slo));
		status.DiplomatUntil = state.TotalHours;
		Assert.False(new COfferCommand(Offer(state, EOfferKind.Guarantee, Pol, slo)).MayPost(state));
		status.DiplomatUntil = state.TotalHours - 1;
		Assert.True(new COfferCommand(Offer(state, EOfferKind.Guarantee, Pol, slo)).MayPost(state));
	}

	[Fact]
	public void The_queue_takes_no_proposal_its_post_throws_away()
	{
		CGameState state = World();
		var simulation = new CSimulation(state);
		CCountryTag slo = ScriptWorld.Slo;
		simulation.Post(new COfferCommand(Offer(state, EOfferKind.Alliance, Pol, slo)));
		simulation.Pump();

		simulation.Post(new COfferCommand(Offer(state, EOfferKind.Guarantee, Pol, slo)));
		Assert.Equal(0, simulation.Pump());
		Assert.Single(C(state, slo).PendingOffers);
	}

	[Fact]
	public void An_AI_accepts_over_thirty_on_a_draw_below_its_score_and_the_answer_comes_back_as_a_command()
	{
		CGameState state = World();
		var scripts = new FixedScripts(100);
		state.AiScripts = scripts;
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;
		// Germany answers, owning land; Slovakia, owning none, would not.
		Run(state, Offer(state, EOfferKind.Alliance, Pol, Ger));
		CFixedPoint before = Relation(state, Pol, Ger);

		CPactPasses.AnswerOffers(state);
		Assert.Equal(new[] { "DiploScore_Alliance POL GER GER" }, scripts.Asked);
		Assert.Empty(C(state, Ger).PendingOffers);
		RunPosted(state);

		Assert.True(CCountryLists.HasAlliance(state, Pol, Ger));
		Assert.Equal(before + CFixedPoint.FromInt(15), Relation(state, Pol, Ger));
		Assert.Equal("MILALLACCEPT", messages[0].We);
		// The answer waits on the actor's list, where nothing answers it.
		Assert.Single(C(state, Pol).PendingOffers);
	}

	[Fact]
	public void A_declined_pact_tells_its_proposer_they_rejected_it_and_costs_twenty()
	{
		CGameState state = World();
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;
		CFixedPoint before = Relation(state, Pol, Ger);

		Run(state, Offer(state, EOfferKind.NonAggressionPact, Pol, Ger, stage: EOfferStage.Declined));

		Assert.Equal(before - CFixedPoint.FromInt(20), Relation(state, Pol, Ger));
		Assert.Equal(("THEYREJECTNAP", "WEREJECTNAP"), (messages[0].We, messages[0].They));
	}

	[Fact]
	public void A_score_of_thirty_or_less_is_always_declined()
	{
		CGameState state = World();
		state.AiScripts = new FixedScripts(30);
		COffer offer = Offer(state, EOfferKind.NonAggressionPact, Pol, ScriptWorld.Slo);
		Assert.Equal(EOfferStage.Declined, CPactPasses.Answer(state, offer));
		state.AiScripts = new FixedScripts(31);
		var answers = new HashSet<EOfferStage>();
		for (int i = 0; i < 200; i++)
		{
			answers.Add(CPactPasses.Answer(state, offer));
		}
		Assert.Equal(2, answers.Count);
	}

	[Fact]
	public void A_guarantee_takes_effect_when_offered_no_button_revokes_it_and_revoking_it_gives_influence_back()
	{
		CGameState state = World();
		state.AiScripts = new FixedScripts(100);
		Run(state, Offer(state, EOfferKind.Guarantee, Pol, ScriptWorld.Slo));
		Assert.Single(state.Guarantees);
		Assert.Equal(CFixedPoint.FromInt(5), C(state, Pol).DiplomaticInfluence);
		// Nobody answers a guarantee.
		CPactPasses.AnswerOffers(state);
		Assert.Empty(state.Outbox);

		// The offer to revoke is never valid, so only what the arm does can be tried.
		COffer revoke = Offer(state, EOfferKind.Guarantee, Pol, ScriptWorld.Slo, value: false);
		Assert.False(COfferRules.IsValid(state, revoke));
		CFixedPoint before = Relation(state, Pol, ScriptWorld.Slo);
		CPacts.Apply(state, revoke);
		Assert.Empty(state.Guarantees);
		Assert.Equal(CFixedPoint.FromInt(6), C(state, Pol).DiplomaticInfluence);
		Assert.Equal(before - CFixedPoint.FromInt(20), Relation(state, Pol, ScriptWorld.Slo));
	}

	[Fact]
	public void Asked_access_is_one_way_offered_access_withdrawn_ends_both_and_access_adds_to_the_months_drift()
	{
		CGameState state = World();
		Run(state, Offer(state, EOfferKind.AskMilitaryAccess, Pol, ScriptWorld.Slo, stage: EOfferStage.Accepted));
		Assert.True(COfferRules.HasAccess(C(state, Pol), C(state, ScriptWorld.Slo)));
		Assert.False(COfferRules.HasAccess(C(state, ScriptWorld.Slo), C(state, Pol)));

		long withAccess = CRelations.Drift(state, C(state, Pol), C(state, ScriptWorld.Slo), new bool[state.Countries.Count], new HashSet<CCountry>());
		long without = CRelations.Drift(state, C(state, ScriptWorld.Slo), C(state, Pol), new bool[state.Countries.Count], new HashSet<CCountry>());
		Assert.Equal(25, withAccess - without);

		Run(state, Offer(state, EOfferKind.OfferMilitaryAccess, ScriptWorld.Slo, Pol, stage: EOfferStage.Accepted));
		Run(state, Offer(state, EOfferKind.OfferMilitaryAccess, ScriptWorld.Slo, Pol, value: false));
		Assert.False(COfferRules.HasAccess(C(state, Pol), C(state, ScriptWorld.Slo)));
		Assert.False(COfferRules.HasAccess(C(state, ScriptWorld.Slo), C(state, Pol)));
	}

	[Fact]
	public void A_pact_may_not_be_revoked_before_its_three_windows_and_revoking_moves_relation()
	{
		CGameState state = World();
		Run(state, Offer(state, EOfferKind.NonAggressionPact, Pol, ScriptWorld.Slo, stage: EOfferStage.Accepted));
		Assert.Single(state.Naps);
		COffer revoke = Offer(state, EOfferKind.NonAggressionPact, Pol, ScriptWorld.Slo, value: false);
		Assert.True(COfferRules.IsValid(state, revoke));
		Assert.False(COfferRules.IsSelectable(state, revoke));

		state.Naps[0] = state.Naps[0] with { Start = COfferRules.AddMonths(state.TotalHours, -24) };
		Assert.True(COfferRules.IsSelectable(state, revoke));
		CFixedPoint before = Relation(state, Pol, ScriptWorld.Slo);
		Run(state, revoke);
		Assert.Empty(state.Naps);
		Assert.Equal(before - CFixedPoint.FromInt(30), Relation(state, Pol, ScriptWorld.Slo));
	}

	[Fact]
	public void An_embargo_stands_when_imposed_and_goes_when_lifted()
	{
		CGameState state = World();
		CFixedPoint before = Relation(state, Pol, ScriptWorld.Slo);
		Run(state, Offer(state, EOfferKind.Embargo, Pol, ScriptWorld.Slo));
		Assert.Single(state.Embargoes);
		Assert.Equal(before - CFixedPoint.FromInt(30), Relation(state, Pol, ScriptWorld.Slo));
		Run(state, Offer(state, EOfferKind.Embargo, Pol, ScriptWorld.Slo, value: false));
		Assert.Empty(state.Embargoes);
	}

	[Fact]
	public void An_alliance_needs_the_two_not_both_in_factions_and_dissolving_costs_thirty_more()
	{
		CGameState state = World();
		Assert.False(COfferRules.IsValid(state, Offer(state, EOfferKind.Alliance, Ger, Ita)));
		Assert.True(COfferRules.IsValid(state, Offer(state, EOfferKind.Alliance, Ger, Pol)));

		state.Alliances.Add(new CAlliance(Pol, ScriptWorld.Slo));
		CFixedPoint before = Relation(state, Pol, ScriptWorld.Slo);
		Run(state, Offer(state, EOfferKind.Alliance, Pol, ScriptWorld.Slo, value: false));
		Assert.False(CCountryLists.HasAlliance(state, Pol, ScriptWorld.Slo));
		Assert.Equal(before - CFixedPoint.FromInt(80), Relation(state, Pol, ScriptWorld.Slo));
	}

	[Fact]
	public void Pacts_end_by_themselves_a_NAP_after_45_months_a_guarantee_after_twenty_years_an_alliance_in_one_faction()
	{
		CGameState state = World();
		var messages = new List<CPactMessage>();
		state.PactMessaged += messages.Add;
		long now = state.TotalHours;
		// A pact ends on the first hour after its months, not on the last of them.
		state.Naps.Add(new CNap(Ger, Pol, COfferRules.AddMonths(now, -45) - 1));
		state.Naps.Add(new CNap(Pol, Ita, COfferRules.AddMonths(now, -45)));
		state.Guarantees.Add(new CGuarantee(Ger, Pol, now - CPactPasses.GuaranteeHours - 1));
		state.Guarantees.Add(new CGuarantee(Ita, Pol, now - CPactPasses.GuaranteeHours + 1));
		state.Alliances.Add(new CAlliance(Ger, Ita));

		CPactPasses.RunDaily(state);

		Assert.Equal(new[] { new CNap(Pol, Ita, COfferRules.AddMonths(now, -45)) }, state.Naps);
		Assert.Equal(new[] { new CGuarantee(Ita, Pol, now - CPactPasses.GuaranteeHours + 1) }, state.Guarantees);
		Assert.Empty(state.Alliances);
		Assert.Contains(messages, m => m.We == "MILALLWEBAN");
	}

	[Fact]
	public void An_influence_costs_upkeep_each_day_and_ends_when_it_cannot_be_paid_or_a_war_begins()
	{
		CGameState state = World();
		// A major, or a country of more than 100 base IC, may influence.
		C(state, Ger).BaseIc = 101;
		Run(state, Offer(state, EOfferKind.Influence, Ger, Pol));
		Assert.Single(state.Influences);
		Assert.Equal(CFixedPoint.FromInt(7), C(state, Ger).DiplomaticInfluence);
		CPactPasses.RunDaily(state);
		Assert.Equal(CFixedPoint.FromInt(6), C(state, Ger).DiplomaticInfluence);

		C(state, Ger).DiplomaticInfluence = CFixedPoint.Parse("0.5");
		CPactPasses.RunDaily(state);
		Assert.Empty(state.Influences);
		Assert.Equal(CFixedPoint.Zero, C(state, Ger).DiplomaticInfluence);

		C(state, Ger).DiplomaticInfluence = CFixedPoint.FromInt(10);
		Run(state, Offer(state, EOfferKind.Influence, Ger, Pol));
		new CStartWarCommand(Ger, Pol).Execute(state);
		Assert.Empty(state.Influences);
	}

	[Fact]
	public void An_offer_waiting_more_than_96_hours_is_dropped_and_a_played_countrys_list_is_emptied_each_hour()
	{
		CGameState state = World();
		C(state, Pol).PendingOffers.Add(Offer(state, EOfferKind.Alliance, ScriptWorld.Slo, Pol) with { Queued = state.TotalHours - 97 });
		C(state, Pol).PendingOffers.Add(Offer(state, EOfferKind.Alliance, ScriptWorld.Slo, Pol) with { Queued = state.TotalHours - 96 });
		CPactPasses.RunDaily(state);
		Assert.Single(C(state, Pol).PendingOffers);

		state.Played.Add(Pol);
		state.AiScripts = new FixedScripts(100);
		CPactPasses.AskPlayers(state);
		Assert.Empty(C(state, Pol).PendingOffers);
		Assert.Empty(state.Outbox);
	}
}
