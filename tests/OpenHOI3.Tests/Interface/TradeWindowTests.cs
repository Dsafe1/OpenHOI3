using System.Collections.Generic;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Interface;

/// <summary>The trade window's texts, against <c>docs/mechanics/trade.md</c>, sections 7 and 10.</summary>
public class TradeWindowTests
{
	private static readonly Dictionary<string, string> Table = new()
	{
		["TRADE_DAY"] = "$VAL$/Day",
		["TRADE_NOTHING"] = "Nothing",
		["METAL_IRO"] = "Metal",
		["MONEY_IRO"] = "Money",
		["ENERGY_IRO"] = "Energy",
		["AND"] = "and ",
		["TRADE_BUY"] = "Buy",
		["TRADE_SELL"] = "Sell",
		["TRADENEWDESCTWO"] = "We offer $COUNTRY$ to $TYPE$ $SELL$ per day for $BUY$ per day.",
		["NO_TRADE_RULE"] = "Only with $RULE$.",
		["RULE_FREE_RESOURCE_GIFTS"] = "Free resources gifts",
		["TRADEDESC"] = "This will allow us to negotiate.",
	};

	private static string Localise(string key) => Table.GetValueOrDefault(key, key);

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set("economy", "MAX_DAILY_TRADE", CFixedPoint.FromInt(100));
		state.Defines.Set(CTradeRules.GoodsCostSection, "METAL", CFixedPoint.Parse("0.1"));
		state.Defines.Set(CTradeRules.GoodsCostSection, "ENERGY", CFixedPoint.Parse("0.05"));
		return state;
	}

	[Fact]
	public void A_slider_sells_below_its_middle_and_buys_from_it_fifty_a_day_at_the_ends()
	{
		CGameState state = World();
		Assert.Equal(-50000, CTradeWindowText.Amount(state, 0));
		Assert.Equal(0, CTradeWindowText.Amount(state, 500));
		Assert.Equal(100, CTradeWindowText.Amount(state, 501));
		Assert.Equal(50000, CTradeWindowText.Amount(state, 1000));
		Assert.Equal("12.50/Day", CTradeWindowText.Value(Localise, 12500));
	}

	[Fact]
	public void The_offer_says_what_the_recipient_does_and_lists_each_good_in_yellow()
	{
		CGameState state = World();
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		CCountry poland = state.Country(ScriptWorld.Pol)!;
		CTradeTerms terms = CTradeRules.SetTrading(state, CTradeTerms.None, germany, poland, 50000, EGoods.Metal);

		Assert.Equal("We offer POL to Sell §Y50.00 Metal§W  per day for §Y3.75 Money§W  per day.",
			CTradeWindowText.Description(Localise, terms, poland));
		Assert.Equal("Nothing", CTradeWindowText.Goods(Localise, CTradeTerms.None.From));

		CTradeTerms two = CTradeRules.SetTrading(state, terms, germany, poland, 20000, EGoods.Energy);
		Assert.Equal("§Y50.00 Metal§W  and §Y20.00 Energy§W ", CTradeWindowText.Goods(Localise, two.To));
	}

	[Fact]
	public void Accept_says_why_not_for_a_barter_and_nothing_for_terms_without_a_price()
	{
		CGameState state = World();
		CTradeTerms barter = CTradeTerms.None.With(fromBlock: true, EGoods.Metal, 5000).With(fromBlock: false, EGoods.Energy, 5000);
		COffer offer = new(EOfferKind.Trade, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Proposed, true, 0)
		{
			Deal = new CTradeDeal(ScriptWorld.Ger, ScriptWorld.Pol, barter),
		};
		Assert.Equal("Only with Free resources gifts.", CTradeWindowText.AcceptTooltip(Localise, state, offer));
		Assert.False(CTradeWindowText.MayAccept(state, offer.Deal!));

		COffer unpriced = offer with { Deal = new CTradeDeal(ScriptWorld.Ger, ScriptWorld.Pol, CTradeTerms.None) };
		Assert.Equal("", CTradeWindowText.AcceptTooltip(Localise, state, unpriced));
	}

	/// <summary>Poland's route selling Germany 10 metal a day for 0.75 money, Poland its from.</summary>
	private static CTradeRoute PolandSells(CGameState state)
	{
		CTradeTerms terms = CTradeTerms.None.With(fromBlock: true, EGoods.Metal, 10000).With(fromBlock: false, EGoods.Money, 750);
		var route = new CTradeRoute(ScriptWorld.Pol, ScriptWorld.Ger, terms, 0);
		state.TradeRoutes.Add(route);
		return route;
	}

	[Fact]
	public void A_trade_line_is_the_partners_flag_and_name_and_an_icon_a_good_in_the_column_of_the_players_side()
	{
		CGameState state = World();
		CTradeRoute route = PolandSells(state);

		Assert.Equal(route, Assert.Single(CTradeListText.Routes(state, ScriptWorld.Ger)));
		Assert.Equal("@POL POL", CTradeListText.Name(state, route, ScriptWorld.Ger));
		// Germany gives money (Export) and gets metal (Import); Poland the other way round.
		Assert.Equal(new[] { new CTradeListText.CTradeIcon(EGoods.Money, true), new CTradeListText.CTradeIcon(EGoods.Metal, false) },
			CTradeListText.Icons(route, ScriptWorld.Ger));
		Assert.Equal(new[] { new CTradeListText.CTradeIcon(EGoods.Money, false), new CTradeListText.CTradeIcon(EGoods.Metal, true) },
			CTradeListText.Icons(route, ScriptWorld.Pol));
		Assert.Equal("money_trade_icon", CTradeListText.IconName(EGoods.Money));
	}

	[Fact]
	public void An_icon_says_what_moves_each_day_and_the_inactive_marker_why_the_route_stopped()
	{
		CGameState state = World();
		var table = new Dictionary<string, string>(Table)
		{
			["PW_EXPORTING"] = "Exporting §Y$NUM$ $TYPE$§W to §Y$COUNTRY$§W",
			["PW_IMPORTING"] = "Importing §Y$NUM$ $TYPE$§W from §Y$COUNTRY$§W",
			["TRADE_INACTIVE_NO_RES"] = "§Y$COUNTRY$§W does not have enough §Y$RES$§Y.",
		};
		string Words(string key) => table.GetValueOrDefault(key, key);
		CTradeRoute route = PolandSells(state);

		Assert.Equal("Exporting §Y0.75 Money§W to §YPOL§W",
			CTradeListText.IconTooltip(Words, state, route, ScriptWorld.Ger, new CTradeListText.CTradeIcon(EGoods.Money, true)));
		Assert.Equal("Importing §Y10.00 Metal§W from §YPOL§W",
			CTradeListText.IconTooltip(Words, state, route, ScriptWorld.Ger, new CTradeListText.CTradeIcon(EGoods.Metal, false)));

		route.Inactive = true;
		route.InactiveKey = "TRADE_INACTIVE_NO_RES";
		route.InactiveCountry = ScriptWorld.Pol;
		route.InactiveGoods = EGoods.Metal;
		Assert.Equal("§YPOL§W does not have enough §YMetal§Y.", CTradeListText.InactiveReason(Words, state, route));
		Assert.False(CTradeListText.InactiveOnImport(route, ScriptWorld.Ger));
		Assert.True(CTradeListText.InactiveOnImport(route, ScriptWorld.Pol));
	}

	[Fact]
	public void The_cancel_dialog_says_the_routes_from_goods_for_its_to_goods_in_black_whichever_side_the_player_is()
	{
		CGameState state = World();
		var table = new Dictionary<string, string>(Table)
		{
			["CANCELTRADENEWDESC"] = "Do you wish to cancel our agreement with $COUNTRY$?\nThis will stop the transfer of $SELL$ for $BUY$.",
		};
		string Words(string key) => table.GetValueOrDefault(key, key);
		CTradeRoute route = PolandSells(state);
		Assert.Equal("Do you wish to cancel our agreement with POL?\nThis will stop the transfer of §Y10.00 Metal§b  for §Y0.75 Money§b .",
			CTradeListText.CancelDescription(Words, state, route, ScriptWorld.Ger));
	}

	[Fact]
	public void An_offer_to_the_player_says_who_offers_what_until_when_and_its_dialog_the_trade_in_black()
	{
		CGameState state = World();
		var table = new Dictionary<string, string>(Table)
		{
			["DIPLOICON_TIP"] = "$TYPE$ from §Y$NATION$§W. \\n If we do not respond before §Y$DATE$§W we will autodecline.",
			["ICON_TRADE"] = "A §YTrade Offer§W",
			["TRADE_TOOL_REQDESC"] = "$COUNTRY$ would like $TO$ each day in exchange for $FROM$ each day.",
			["TRADEREQDESC"] = "$COUNTRY$ is offering us a Trade Agreement. We would give them $TO$ each day in exchange for $FROM$ each day.",
			["June"] = "June",
		};
		string Words(string key) => table.GetValueOrDefault(key, key);
		CTradeTerms terms = CTradeTerms.None.With(fromBlock: true, EGoods.Metal, 10000).With(fromBlock: false, EGoods.Money, 750);
		var offer = new COffer(EOfferKind.Trade, ScriptWorld.Pol, ScriptWorld.Ger, EOfferStage.Proposed, true, state.TotalHours)
		{
			Deal = new CTradeDeal(ScriptWorld.Pol, ScriptWorld.Ger, terms),
		};

		Assert.Equal(3, COfferAskText.Kind(offer));
		(string text, string delayed) = COfferAskText.IconTooltip(Words, state, offer, state.TotalHours);
		Assert.Equal("A §YTrade Offer§W from §YPOL§W. \\n If we do not respond before §Y 0:00, 8 June, 1936§W we will autodecline.", text);
		Assert.Equal("POL would like §Y0.75 Money§W  each day in exchange for §Y10.00 Metal§W  each day.", delayed);
		Assert.Equal("POL is offering us a Trade Agreement. We would give them §Y0.75 Money§b  each day in exchange for §Y10.00 Metal§b  each day.",
			COfferAskText.Description(Words, state, offer));

		// It blinks from the 120th hour and declines at the 168th.
		Assert.False(COfferAskText.Blinks(0, 120));
		Assert.True(COfferAskText.Blinks(0, 121));
		Assert.False(COfferAskText.Lapsed(0, 168));
		Assert.True(COfferAskText.Lapsed(0, 169));
		Assert.True(COfferAskText.StaysOpen(state, offer));
		new OpenHOI3.Sim.Commands.CStartWarCommand(ScriptWorld.Ger, ScriptWorld.Pol).Execute(state);
		Assert.False(COfferAskText.StaysOpen(state, offer));
	}
}
