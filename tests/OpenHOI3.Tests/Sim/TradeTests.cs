using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Trade routes against <c>docs/mechanics/trade.md</c>, on <see cref="ScriptWorld"/>: Germany and Poland
/// land neighbours, who need no convoy; Italy alone in Africa with no harbour, whom Germany could trade
/// with only by a convoy that cannot be made.
/// </summary>
public class TradeTests
{
	private static readonly CCountryTag Ger = ScriptWorld.Ger;
	private static readonly CCountryTag Pol = ScriptWorld.Pol;
	private static readonly CCountryTag Ita = ScriptWorld.Ita;

	private sealed class TradeScripts : IAiScripts
	{
		public int Score { get; init; } = 100;

		public int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other) => null;

		public int DiploScore(CGameState state, string function, CCountry actor, CCountry recipient, CCountry observer) => Score;

		public int OfferTradeScore(CGameState state, COffer offer) => Score;
	}

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State();
		void Set(string section, string name, string value) => state.Defines.Set(section, name, CFixedPoint.Parse(value));
		Set(COfferRules.Section, "TRADE_INFLUENCE_COST", "3");
		Set(COfferRules.Section, "TRADE_RELATION_CHANGES", "15");
		Set(COfferRules.Section, "TRADE_CANCEL_RELATION_COST", "15");
		Set(COfferRules.Section, "TRADE_CANCEL_INFLUENCE_COST", "1");
		Set(CTradeRules.GoodsCostSection, "METAL", "0.1");
		Set(CTradeRules.GoodsCostSection, "ENERGY", "0.05");
		Set(CTradeRules.GoodsCostSection, "FUEL", "0.75");
		foreach (CCountry country in state.Countries)
		{
			country.DiplomaticInfluence = CFixedPoint.FromInt(10);
		}
		state.Stockpile(C(state, Ger))![EGoods.Money] = CFixedPoint.FromInt(100);
		state.Stockpile(C(state, Pol))![EGoods.Metal] = CFixedPoint.FromInt(100);
		return state;
	}

	private static CCountry C(CGameState state, CCountryTag tag) => state.Country(tag)!;

	/// <summary>Germany's offer to buy an amount of metal a day from another country.</summary>
	private static COffer BuyMetal(CGameState state, CCountryTag seller, string amount)
	{
		CTradeTerms terms = CTradeRules.SetTrading(state, CTradeTerms.None, C(state, Ger), C(state, seller),
			CFixedPoint.Parse(amount).Raw, EGoods.Metal);
		return new COffer(EOfferKind.Trade, Ger, seller, EOfferStage.Proposed, true, state.TotalHours)
		{
			Deal = new CTradeDeal(Ger, seller, terms),
		};
	}

	private static void Run(CGameState state, COffer offer) => new COfferCommand(offer).Execute(state);

	[Fact]
	public void A_goods_price_is_its_cost_less_up_to_a_half_by_the_relation_each_division_cut()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);

		// 0.4 metal a day at 0.1: 0.040 at relation -200, 0.030 at nought, 0.020 at +200.
		CGameState.Status(poland, germany).Relation = CFixedPoint.FromInt(-200);
		Assert.Equal(40, CTradeRules.Cost(state, EGoods.Metal, 400, germany, poland));
		CGameState.Status(poland, germany).Relation = CFixedPoint.Zero;
		Assert.Equal(30, CTradeRules.Cost(state, EGoods.Metal, 400, germany, poland));
		CGameState.Status(poland, germany).Relation = CFixedPoint.FromInt(200);
		Assert.Equal(20, CTradeRules.Cost(state, EGoods.Metal, 400, germany, poland));
		// Sweden's 0.4 metal for Finland's 0.026 money is relation +80.
		CGameState.Status(poland, germany).Relation = CFixedPoint.FromInt(80);
		Assert.Equal(26, CTradeRules.Cost(state, EGoods.Metal, 400, germany, poland));
	}

	[Fact]
	public void Buying_puts_the_good_in_the_sellers_block_and_its_price_in_the_buyers_and_a_good_never_flows_both_ways()
	{
		CGameState state = World();
		CTradeTerms terms = BuyMetal(state, Pol, "10").Deal!.Terms;

		Assert.Equal(10000, terms.To(EGoods.Metal));
		Assert.Equal(750, terms.From(EGoods.Money));
		Assert.Equal(0, terms.To(EGoods.Money));

		// Selling the same good instead clears the purchase and prices the other way.
		terms = CTradeRules.SetTrading(state, terms, C(state, Ger), C(state, Pol), -CFixedPoint.FromInt(4).Raw, EGoods.Metal);
		Assert.Equal(0, terms.To(EGoods.Metal));
		Assert.Equal(4000, terms.From(EGoods.Metal));
		Assert.Equal(300, terms.To(EGoods.Money));
		Assert.Equal(0, terms.From(EGoods.Money));
	}

	[Fact]
	public void Terms_are_good_with_a_price_and_no_barter()
	{
		CGameState state = World();
		CCountry germany = C(state, Ger);
		CCountry poland = C(state, Pol);
		CTradeTerms terms = BuyMetal(state, Pol, "10").Deal!.Terms;
		Assert.True(CTradeRules.IsGood(state, terms, germany, poland));

		CTradeTerms barter = terms.With(fromBlock: true, EGoods.Energy, 5000);
		Assert.False(CTradeRules.IsGood(state, barter, germany, poland));
		CTradeTerms unpaid = terms.With(fromBlock: true, EGoods.Money, 0);
		Assert.False(CTradeRules.IsGood(state, unpaid, germany, poland));
	}

	[Fact]
	public void An_accepted_offer_makes_a_route_whose_day_moves_the_goods_and_the_money()
	{
		CGameState state = World();
		state.AiScripts = new TradeScripts();
		COffer offer = BuyMetal(state, Pol, "10");
		Run(state, offer);
		Assert.Equal(CFixedPoint.FromInt(7), C(state, Ger).DiplomaticInfluence);

		CPactPasses.AnswerOffers(state);
		foreach (CCommand command in state.Outbox.ToArray())
		{
			command.Execute(state);
		}
		CTradeRoute route = Assert.Single(state.TradeRoutes);
		Assert.Null(route.Convoy);
		Assert.Equal(CFixedPoint.FromInt(15), CGameState.Status(C(state, Ger), C(state, Pol)).Relation);

		CTradeRoutes.RunDaily(state);
		CGoodsPool germany = state.Stockpile(C(state, Ger))!;
		CGoodsPool poland = state.Stockpile(C(state, Pol))!;
		Assert.Equal(CFixedPoint.FromInt(10), germany[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(90), poland[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Parse("99.25"), germany[EGoods.Money]);
		Assert.Equal(CFixedPoint.Parse("0.75"), poland[EGoods.Money]);
		Assert.Equal(CFixedPoint.FromInt(10), C(state, Ger).TradedForSansAlliedSupply[EGoods.Metal]);
		Assert.False(route.Inactive);
	}

	[Fact]
	public void A_seller_without_the_good_or_a_buyer_without_the_money_stops_the_route_and_nothing_moves()
	{
		CGameState state = World();
		CTradeRoute route = CTradeRoutes.Make(state, BuyMetal(state, Pol, "10").Deal!, state.TotalHours)!;
		state.Stockpile(C(state, Pol))![EGoods.Metal] = CFixedPoint.FromInt(5);

		CTradeRoutes.RunDaily(state);
		Assert.True(route.Inactive);
		Assert.Equal(CTradeRoutes.InactiveNoResource, route.InactiveKey);
		Assert.Equal(Pol, route.InactiveCountry);
		Assert.Equal(CFixedPoint.FromInt(5), state.Stockpile(C(state, Pol))![EGoods.Metal]);
		long stamped = route.LastInactive;

		state.Stockpile(C(state, Pol))![EGoods.Metal] = CFixedPoint.FromInt(50);
		state.Stockpile(C(state, Ger))![EGoods.Money] = CFixedPoint.Zero;
		CTradeRoutes.RunDaily(state);
		Assert.Equal(Ger, route.InactiveCountry);
		Assert.Equal(EGoods.Money, route.InactiveGoods);
		Assert.Equal(stamped, route.LastInactive);
	}

	[Fact]
	public void A_cancellation_ends_the_matching_route_costs_relation_and_no_influence_and_is_not_answered()
	{
		CGameState state = World();
		CTradeDeal deal = BuyMetal(state, Pol, "10").Deal!;
		CTradeRoutes.Make(state, deal, state.TotalHours);
		var cancel = new COffer(EOfferKind.Trade, Pol, Ger, EOfferStage.Proposed, false, state.TotalHours) { Deal = deal };
		Assert.True(COfferRules.IsValid(state, cancel));
		Assert.False(COfferRules.AiAnswers(cancel));

		Run(state, cancel);
		Assert.Empty(state.TradeRoutes);
		Assert.Equal(CFixedPoint.FromInt(-15), CGameState.Status(C(state, Ger), C(state, Pol)).Relation);
		Assert.Equal(CFixedPoint.FromInt(10), C(state, Pol).DiplomaticInfluence);
		Assert.False(COfferRules.IsValid(state, cancel));
	}

	[Fact]
	public void An_AI_accepts_one_trade_an_hour_and_keeps_the_next_for_the_one_after()
	{
		CGameState state = World();
		state.AiScripts = new TradeScripts();
		COffer first = BuyMetal(state, Pol, "10");
		COffer second = BuyMetal(state, Pol, "5");
		C(state, Pol).PendingOffers.Add(first);
		C(state, Pol).PendingOffers.Add(second);

		CPactPasses.AnswerOffers(state);
		Assert.Single(state.Outbox);
		Assert.Equal(second, Assert.Single(C(state, Pol).PendingOffers));
	}

	[Fact]
	public void The_runtimes_rand_seeded_to_one_rolls_47_0_56_24_68_25_87_51()
	{
		var rand = new CRuntimeRand(1);
		var rolls = new List<int>();
		for (int i = 0; i < 8; i++)
		{
			rolls.Add(rand.Next() * rand.Next() % 100);
		}
		Assert.Equal(new[] { 47, 0, 56, 24, 68, 25, 87, 51 }, rolls);
	}

	[Fact]
	public void A_players_offers_are_put_to_him_once_and_under_automate_trade_rolled_each_hour_from_47()
	{
		CGameState state = World();
		state.AiScripts = new TradeScripts { Score = 50 };
		state.Played.Add(Pol);
		var asked = new List<COffer>();
		state.OfferAsked += asked.Add;
		C(state, Pol).PendingOffers.Add(BuyMetal(state, Pol, "10"));
		C(state, Pol).PendingOffers.Add(new COffer(EOfferKind.Guarantee, Ger, Pol, EOfferStage.Proposed, true, 0));

		CPactPasses.AnswerOffers(state);
		Assert.Equal(EOfferKind.Trade, Assert.Single(asked).Kind);
		Assert.Empty(C(state, Pol).PendingOffers);

		// Automated, the trade stays on the list and the hour's first roll, 47, is under 50: accepted.
		state.AutomateTrade.Add(Pol);
		C(state, Pol).PendingOffers.Add(BuyMetal(state, Pol, "10"));
		C(state, Pol).PendingOffers.Add(BuyMetal(state, Pol, "5"));
		CPactPasses.AnswerOffers(state);
		Assert.Single(asked);
		Assert.Equal(2, C(state, Pol).PendingOffers.Count);
		CAutomatedTrades.Answer(state, C(state, Pol));
		// The second roll, 0, would accept too: it is kept for the next hour.
		Assert.Equal(EOfferStage.Accepted, ((COfferCommand)Assert.Single(state.Outbox)).Offer.Stage);
		Assert.Single(C(state, Pol).PendingOffers);
	}

	[Fact]
	public void A_war_ends_the_pairs_routes_and_a_needed_convoy_that_cannot_be_made_ends_one_the_next_day()
	{
		CGameState state = World();
		CTradeRoutes.Make(state, BuyMetal(state, Pol, "10").Deal!, state.TotalHours);
		new CStartWarCommand(Ger, Pol).Execute(state);
		Assert.Empty(state.TradeRoutes);

		// Italy is across the sea with no harbour: the route stands, with no convoy, until the day.
		state.Stockpile(C(state, Ita))![EGoods.Metal] = CFixedPoint.FromInt(100);
		CTradeRoute? route = CTradeRoutes.Make(state, BuyMetal(state, Ita, "10").Deal!, state.TotalHours);
		Assert.NotNull(route);
		Assert.Null(route!.Convoy);
		CTradeRoutes.RunDaily(state);
		Assert.Empty(state.TradeRoutes);
	}

	[Fact]
	public void A_route_is_paid_with_the_money_the_countries_before_its_day_made_that_day_and_not_its_own()
	{
		// Germany's day comes before Poland's. With 10 base IC at 0.1 money an IC it makes 1.000 money a day, and has none.
		CGameState state = World();
		state.Defines.Set(CResources.EconomySection, CResources.IcToMoney, CFixedPoint.Parse("0.1"));
		state.Province(1).Modifier.Set(CIndustrialCapacity.ProvinceIc, CFixedPoint.FromInt(10));
		CGoodsPool germany = state.Stockpile(C(state, Ger))!;
		germany[EGoods.Money] = CFixedPoint.Zero;
		germany[EGoods.Metal] = CFixedPoint.FromInt(100);                // enough that no shortage cuts its IC
		germany[EGoods.Energy] = CFixedPoint.FromInt(100);
		germany[EGoods.RareMaterials] = CFixedPoint.FromInt(100);

		// Poland sells Germany 10 metal for 0.750 in Poland's day, after Germany's: Germany pays with today's money.
		CTradeTerms sale = CTradeRules.SetTrading(state, CTradeTerms.None, C(state, Pol), C(state, Ger),
			-CFixedPoint.FromInt(10).Raw, EGoods.Metal);
		CTradeRoute later = CTradeRoutes.Make(state, new CTradeDeal(Pol, Ger, sale), state.TotalHours)!;
		// Germany buys 10 more in its own day, before its money is made: nothing to pay with.
		CTradeRoute own = CTradeRoutes.Make(state, BuyMetal(state, Pol, "10").Deal!, state.TotalHours)!;

		CTick.RunEconomy(state);
		Assert.False(later.Inactive);
		Assert.True(own.Inactive);
		Assert.Equal(EGoods.Money, own.InactiveGoods);
		// Poland, with no energy, has no IC to make money of: what it holds is Germany's payment alone.
		Assert.Equal(CFixedPoint.Parse("0.75"), state.Stockpile(C(state, Pol))![EGoods.Money]);
	}
}
