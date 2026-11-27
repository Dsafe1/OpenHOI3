using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Aid against <c>docs/mechanics/aid.md</c>, on <see cref="ScriptWorld"/>: Germany and Poland at peace.
/// </summary>
public class AidTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State();
		void Set(string name, string value) => state.Defines.Set(COfferRules.Section, name, CFixedPoint.Parse(value));
		Set("ALLOW_DEBT_INFLUENCE_COST", "1");
		Set("REVOKE_DEBT_INFLUENCE_COST", "1");
		Set(CDebts.YearsDefine, "50");
		foreach (CCountry country in state.Countries)
		{
			country.DiplomaticInfluence = CFixedPoint.FromInt(10);
		}
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	private static void Run(CGameState state, COffer offer) => new COfferCommand(offer).Execute(state);

	private static COffer Debt(CGameState state, CCountryTag actor, CCountryTag recipient, bool value, EOfferStage stage) =>
		new(EOfferKind.Debt, actor, recipient, stage, value, state.TotalHours);

	[Fact]
	public void Allowed_debt_is_the_askers_leave_and_revoking_it_charges_the_debtor_and_leaves_the_debt()
	{
		CGameState state = World();
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;

		// Germany asks to run a debt with Poland: a point of influence; accepted, Germany may.
		Run(state, Debt(state, Ger, Pol, true, EOfferStage.Proposed));
		Assert.Equal(CFixedPoint.FromInt(9), C(state, Ger).DiplomaticInfluence);
		Run(state, Debt(state, Ger, Pol, true, EOfferStage.Accepted));
		Assert.True(CGameState.Status(C(state, Ger), C(state, Pol)).AllowDebts);
		Assert.False(CGameState.Status(C(state, Pol), C(state, Ger)).AllowDebts);
		Assert.Equal("DEBTALLOWACCEPT", told[^1].We);

		// Poland revokes it: Germany, the debtor, pays; the debt stands.
		CGameState.Status(C(state, Ger), C(state, Pol)).Debt = 36500;
		Run(state, Debt(state, Pol, Ger, false, EOfferStage.Proposed));
		Assert.False(CGameState.Status(C(state, Ger), C(state, Pol)).AllowDebts);
		Assert.Equal(CFixedPoint.FromInt(8), C(state, Ger).DiplomaticInfluence);
		Assert.Equal(CFixedPoint.FromInt(10), C(state, Pol).DiplomaticInfluence);
		Assert.Equal(36500, CGameState.Status(C(state, Ger), C(state, Pol)).Debt);
		Assert.Equal("DEBTREVOKE", told[^1].They);
	}

	[Fact]
	public void A_puppet_neither_allows_nor_revokes_a_debt_and_another_countrys_puppet_is_not_asked()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		CGameState.Status(poland, germany).AllowDebts = true;           // Poland may run a debt with Germany
		Assert.True(COfferRules.IsSelectable(state, Debt(state, Ger, Pol, false, EOfferStage.Proposed)));

		// Germany a puppet - of Italy: it may revoke nothing, nor ask anything.
		germany.Overlord = ScriptWorld.Ita;
		Assert.False(COfferRules.IsSelectable(state, Debt(state, Ger, Pol, false, EOfferStage.Proposed)));
		Assert.False(COfferRules.IsSelectable(state, Debt(state, Ger, Pol, true, EOfferStage.Proposed)));
		// Nor is Italy's puppet asked by Poland; Italy, its master, may ask it.
		Assert.False(COfferRules.IsSelectable(state, Debt(state, Pol, Ger, true, EOfferStage.Proposed)));
		Assert.True(COfferRules.IsSelectable(state, Debt(state, ScriptWorld.Ita, Ger, true, EOfferStage.Proposed)));

		// A diplomat on his way stops it as well.
		germany.Overlord = null;
		CGameState.Status(germany, poland).DiplomatUntil = state.TotalHours + 24;
		Assert.False(COfferRules.IsSelectable(state, Debt(state, Ger, Pol, false, EOfferStage.Proposed)));
	}

	[Fact]
	public void A_debt_is_repaid_an_18250th_a_peaceful_day_for_ever_and_nothing_at_war_or_without_the_money()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		CGameState.Status(germany, poland).Debt = 36500;                 // 36.500 money: 0.002 a day
		state.Stockpile(germany)![EGoods.Money] = CFixedPoint.FromInt(1);
		state.Stockpile(poland)![EGoods.Money] = CFixedPoint.Zero;

		CDebts.RepayDaily(state);
		Assert.Equal(new CFixedPoint(998), state.Stockpile(germany)![EGoods.Money]);
		Assert.Equal(new CFixedPoint(2), state.Stockpile(poland)![EGoods.Money]);
		Assert.Equal(new CFixedPoint(2), germany.RepaidAway);
		Assert.Equal(new CFixedPoint(2), poland.IncomeFromDebt);
		Assert.Equal(36500, CGameState.Status(germany, poland).Debt);   // never lowered

		// Money of no more than the day's figure pays nothing, and the figure is worked out again.
		state.Stockpile(germany)![EGoods.Money] = new CFixedPoint(2);
		CDebts.RepayDaily(state);
		Assert.Equal(new CFixedPoint(2), state.Stockpile(germany)![EGoods.Money]);
		Assert.Equal(0, CGameState.Status(germany, poland).DailyRepay);

		// A debt under 18.250 gives nought a day and is never repaid.
		CGameState.Status(germany, poland).Debt = 18249;
		Assert.Equal(0, CDebts.DailyRepay(state, CGameState.Status(germany, poland)));
	}

	[Fact]
	public void An_enhanced_games_debt_comes_down_with_each_payment_and_a_small_one_is_paid_too()
	{
		CGameState state = World();
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		CGameState.Status(germany, poland).Debt = 36500;                 // 0.002 a day
		state.Stockpile(germany)![EGoods.Money] = CFixedPoint.FromInt(1);
		state.Stockpile(poland)![EGoods.Money] = CFixedPoint.Zero;

		CDebts.RepayDaily(state);
		Assert.Equal(36498, CGameState.Status(germany, poland).Debt);
		Assert.Equal(new CFixedPoint(2), state.Stockpile(poland)![EGoods.Money]);

		// The last payment is what is left, and the debt and its figure are then gone.
		CGameState.Status(germany, poland).Debt = 1;
		CDebts.RepayDaily(state);
		Assert.Equal(0, CGameState.Status(germany, poland).Debt);
		Assert.Equal(0, CGameState.Status(germany, poland).DailyRepay);
		Assert.Equal(new CFixedPoint(3), state.Stockpile(poland)![EGoods.Money]);
		CDebts.RepayDaily(state);
		Assert.Equal(new CFixedPoint(3), state.Stockpile(poland)![EGoods.Money]);

		// A debt under 18.250 is paid a thousandth a day, where the original's is never paid.
		CGameState.Status(germany, poland).Debt = 18249;
		Assert.Equal(1, CDebts.DailyRepay(state, CGameState.Status(germany, poland)));

		// Without the fix an enhanced game's debt stands as the original's.
		CGameState other = World();
		other.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(new[] { OpenHOI3.Sim.Rules.EFix.DebtRepaid }));
		CGameState.Status(C(other, Ger), C(other, Pol)).Debt = 36500;
		other.Stockpile(C(other, Ger))![EGoods.Money] = CFixedPoint.FromInt(1);
		CDebts.RepayDaily(other);
		Assert.Equal(36500, CGameState.Status(C(other, Ger), C(other, Pol)).Debt);
	}

	private const string Theory = "infantry_theory";

	private static COffer Share(CGameState state, CCountryTag actor, CCountryTag recipient, string category, bool value = true) =>
		new(EOfferKind.ShareTechnology, actor, recipient, EOfferStage.Proposed, value, state.TotalHours) { Category = category };

	[Fact]
	public void A_shared_theory_is_read_at_the_higher_of_the_two_and_only_a_theory_above_theirs_may_be_shared()
	{
		CGameState state = World();
		state.Defines.Set(COfferRules.Section, "SHARE_TECH_INFLUENCE_COST", CFixedPoint.FromInt(1));
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		germany.Abilities[Theory] = CFixedPoint.FromInt(5);
		germany.Abilities["infantry_practical"] = CFixedPoint.FromInt(5);
		poland.Abilities[Theory] = CFixedPoint.FromInt(2);

		Assert.False(COfferRules.IsValid(state, Share(state, Ger, Pol, "infantry_practical")));
		Assert.False(COfferRules.IsValid(state, Share(state, Pol, Ger, Theory)));
		Run(state, Share(state, Ger, Pol, Theory));
		Assert.Equal(CFixedPoint.FromInt(9), germany.DiplomaticInfluence);
		Assert.Equal(CFixedPoint.FromInt(5), poland.Ability(Theory));
		Assert.Equal(CFixedPoint.FromInt(2), poland.OwnAbility(Theory));
		// It follows the sharer's level as it changes.
		germany.Abilities[Theory] = CFixedPoint.FromInt(6);
		Assert.Equal(CFixedPoint.FromInt(6), poland.Ability(Theory));
		// Germany's 6 is not above what Poland now reads, 6: nothing more to share.
		Assert.False(COfferRules.IsValid(state, Share(state, Ger, Pol, Theory)));

		Run(state, Share(state, Ger, Pol, "", value: false));
		Assert.Equal(CFixedPoint.FromInt(2), poland.Ability(Theory));
		Assert.Empty(germany.TechnologyShares);
	}

	[Fact]
	public void A_new_sharer_ends_every_share_of_the_old_and_the_day_drops_one_no_longer_above()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry italy = C(state, ScriptWorld.Ita);
		CCountry poland = C(state, Pol);
		germany.Abilities[Theory] = CFixedPoint.FromInt(5);
		italy.Abilities[Theory] = CFixedPoint.FromInt(4);
		CTechnologySharing.Start(germany, poland, Theory);
		CTechnologySharing.Start(germany, poland, "infantry_practical");
		CTechnologySharing.Start(italy, poland, Theory);
		Assert.Empty(germany.TechnologyShares);
		Assert.Equal(italy, poland.TechnologySharedFrom[Theory]);

		// Poland's own level reaches Italy's: the day drops the share.
		poland.Abilities[Theory] = CFixedPoint.FromInt(4);
		CTechnologySharing.CheckDaily(state);
		Assert.Empty(italy.TechnologyShares);
		Assert.False(poland.TechnologySharedFrom.ContainsKey(Theory));
	}

	[Fact]
	public void Each_share_costs_its_define_of_leadership_and_never_leaves_less_than_one()
	{
		CGameState state = World();
		state.Defines.Set(COfferRules.Section, CTechnologySharing.LeadershipCostDefine, CFixedPoint.FromInt(1));
		CCountry germany = C(state, Ger);
		CTechnologySharing.Start(germany, C(state, Pol), Theory);
		CTechnologySharing.Start(germany, C(state, ScriptWorld.Ita), Theory);

		Assert.Equal(1500, CTechnologySharing.DrainLeadership(state, germany, 3500));
		// 2.5 cannot pay two and keep one: the newest share, Italy's, ends.
		Assert.Equal(1500, CTechnologySharing.DrainLeadership(state, germany, 2500));
		Assert.Single(germany.TechnologyShares);
	}

	[Fact]
	public void Lend_lease_goes_to_a_receiver_at_war_from_a_giver_not_too_neutral_over_land_between_neighbours()
	{
		CGameState state = World();
		state.Defines.Set(COfferRules.Section, CLendLease.NeutralityLimitDefine, CFixedPoint.FromInt(60));
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);

		Assert.Equal(CLendLease.ERefusal.ReceiverAtPeace, CLendLease.Refusal(state, germany, poland));
		new CStartWarCommand(Pol, ScriptWorld.Ita).Execute(state);
		Assert.Equal(CLendLease.ERefusal.None, CLendLease.Refusal(state, germany, poland));
		Assert.Equal(ELendLeaseRoute.Neighbour, CLendLease.Way(state, germany, poland));
		germany.EffectiveNeutrality = CFixedPoint.FromInt(61);
		Assert.Equal(CLendLease.ERefusal.GiverNeutrality, CLendLease.Refusal(state, germany, poland));
	}

	[Fact]
	public void Asking_for_lend_lease_costs_the_debts_define_and_an_hour_later_it_arrives_whole_over_land()
	{
		CGameState state = World();
		state.Defines.Set(COfferRules.Section, CLendLease.NeutralityLimitDefine, CFixedPoint.FromInt(60));
		new CStartWarCommand(Pol, ScriptWorld.Ita).Execute(state);
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;

		COffer ask = new(EOfferKind.RequestLendLease, Pol, Ger, EOfferStage.Proposed, true, state.TotalHours);
		Run(state, ask);
		Assert.Equal(CFixedPoint.FromInt(9), poland.DiplomaticInfluence);
		Run(state, ask with { Stage = EOfferStage.Accepted });
		Assert.True(CLendLease.Gives(germany, poland));
		Assert.Equal(CLendLeaseGift.StartingWeight, germany.LendLeaseGiving[0].Weight);
		Assert.Equal(0, germany.LendLeaseGiving[0].Effectiveness);
		Assert.Equal("LENDACCEPT", told[^1].We);

		CLendLease.RunHourly(state);
		Assert.Equal(1000, germany.LendLeaseGiving[0].Effectiveness);

		// Poland cancels it: Germany is told, the names turned round.
		Run(state, ask with { Value = false });
		Assert.False(CLendLease.Gives(germany, poland));
		Assert.True(told[^1].TurnedRound);
		Assert.Equal("LENDOFFERREFUSED", told[^1].They);
	}

	[Fact]
	public void A_giving_the_rules_no_longer_allow_is_ended_by_a_command_the_hour_finds_it()
	{
		CGameState state = World();
		state.Defines.Set(COfferRules.Section, CLendLease.NeutralityLimitDefine, CFixedPoint.FromInt(60));
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		CLendLease.Add(germany, poland);
		// Poland is at peace with everybody.
		CLendLease.RunHourly(state);
		var remove = Assert.IsType<CRemoveLendLeaseCommand>(Assert.Single(state.Outbox));
		remove.Execute(state);
		Assert.Empty(germany.LendLeaseGiving);
		Assert.Empty(poland.LendLeaseReceived);
	}

	private static COffer Licence(CGameState state, EOfferStage stage) =>
		new(EOfferKind.Licence, Ger, Pol, stage, true, state.TotalHours)
		{
			Licence = new CLicenceTerms("infantry_brigade", 3, 2, CFixedPoint.Parse("1.5")),
		};

	[Fact]
	public void A_licence_costs_its_money_a_unit_times_the_runs_and_one_the_buyer_cannot_pay_is_declined()
	{
		CGameState state = World();
		state.Defines.Set(COfferRules.Section, "LICENCE_INFLUENCE_COST", CFixedPoint.FromInt(1));
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		var told = new List<CPactMessage>();
		state.PactMessaged += told.Add;
		Assert.Equal(9000, Licence(state, EOfferStage.Accepted).Licence!.Fee);   // 1.5 x 3 x 2

		Run(state, Licence(state, EOfferStage.Proposed));
		Assert.Equal(CFixedPoint.FromInt(9), germany.DiplomaticInfluence);

		state.Stockpile(germany)![EGoods.Money] = CFixedPoint.FromInt(5);
		Run(state, Licence(state, EOfferStage.Accepted));
		Assert.Equal("LICBUYDECLINE", told[^1].We);
		Assert.Equal(CFixedPoint.FromInt(5), state.Stockpile(germany)![EGoods.Money]);

		state.Stockpile(germany)![EGoods.Money] = CFixedPoint.FromInt(10);
		state.Stockpile(poland)![EGoods.Money] = CFixedPoint.Zero;
		Run(state, Licence(state, EOfferStage.Accepted));
		Assert.Equal("LICBUYACCEPT", told[^1].We);
		Assert.Equal(CFixedPoint.FromInt(1), state.Stockpile(germany)![EGoods.Money]);
		Assert.Equal(CFixedPoint.FromInt(9), state.Stockpile(poland)![EGoods.Money]);
	}

	[Fact]
	public void A_buyer_at_war_and_allowed_debt_puts_the_licence_on_its_debt_and_the_seller_is_paid_nothing()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		new CStartWarCommand(Ger, ScriptWorld.Ita).Execute(state);
		CGameState.Status(germany, poland).AllowDebts = true;
		state.Stockpile(germany)![EGoods.Money] = CFixedPoint.Zero;
		state.Stockpile(poland)![EGoods.Money] = CFixedPoint.Zero;

		Run(state, Licence(state, EOfferStage.Accepted));
		Assert.Equal(9000, CGameState.Status(germany, poland).Debt);
		Assert.Equal(CFixedPoint.Zero, state.Stockpile(poland)![EGoods.Money]);
	}

	[Fact]
	public void A_givers_day_of_lend_lease_is_its_weights_share_and_the_receiver_makes_whole_IC_of_it_keeping_the_fraction()
	{
		// FINDINGS-tradeui.md E.5: TotalIC 200, a share of 3276/32768, two receivers at 500, both over land.
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		CCountry italy = C(state, ScriptWorld.Ita);
		germany.TotalIc = 200;
		germany.ProductionShares[(int)EProductionCategory.LendLease] = new CFixed15(3276);
		CLendLease.Add(germany, poland);
		CLendLease.Add(germany, italy);
		germany.LendLeaseGiving[0].Effectiveness = 1000;
		germany.LendLeaseGiving[1].Effectiveness = 600;
		CGoodsPool pool = state.Stockpile(germany)!;
		pool[EGoods.Metal] = CFixedPoint.FromInt(1000);
		pool[EGoods.Energy] = CFixedPoint.FromInt(1000);
		pool[EGoods.RareMaterials] = CFixedPoint.FromInt(1000);
		var charged = new List<CFixedPoint>();

		CLendLeaseIc.Give(state, germany, pool, charged.Add);
		Assert.Equal(new[] { new CFixedPoint(9997), new CFixedPoint(5998) }, charged);
		Assert.Equal(9997, poland.LendLeaseIncome);

		poland.TotalIc = 50;
		CLendLeaseIc.Receive(poland);
		Assert.Equal(59, poland.TotalIc);
		Assert.Equal(9, poland.LendLeaseIc);
		Assert.Equal(997, poland.LendLeaseIncome);
		Assert.Equal(997, poland.LendLeaseReceived[0].Amount);
	}

	[Fact]
	public void A_receiver_the_givers_stockpile_cannot_pay_for_gets_nothing_that_day()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		germany.TotalIc = 200;
		germany.ProductionShares[(int)EProductionCategory.LendLease] = new CFixed15(3276);
		CLendLease.Add(germany, poland);
		germany.LendLeaseGiving[0].Effectiveness = 1000;
		CGoodsPool pool = state.Stockpile(germany)!;
		pool[EGoods.Metal] = CFixedPoint.FromInt(5);
		pool[EGoods.Energy] = CFixedPoint.FromInt(1000);
		pool[EGoods.RareMaterials] = CFixedPoint.FromInt(1000);

		CLendLeaseIc.Give(state, germany, pool, _ => { });
		Assert.Equal(0, poland.LendLeaseIncome);
	}

	[Fact]
	public void A_war_between_the_two_ends_the_leave_both_ways_and_not_the_debt()
	{
		CGameState state = World();
		CGameState.Status(C(state, Ger), C(state, Pol)).AllowDebts = true;
		CGameState.Status(C(state, Pol), C(state, Ger)).AllowDebts = true;
		CGameState.Status(C(state, Ger), C(state, Pol)).Debt = 5000;
		new CStartWarCommand(Ger, Pol).Execute(state);
		Assert.False(CGameState.Status(C(state, Ger), C(state, Pol)).AllowDebts);
		Assert.False(CGameState.Status(C(state, Pol), C(state, Ger)).AllowDebts);
		Assert.Equal(5000, CGameState.Status(C(state, Ger), C(state, Pol)).Debt);
	}
}
