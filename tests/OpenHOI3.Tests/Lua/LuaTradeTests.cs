using System;
using System.Collections.Generic;
using System.IO;
using OpenHOI3.Content;
using OpenHOI3.Lua;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;
using OpenHOI3.Tests.Sim;

namespace OpenHOI3.Tests.Lua;

/// <summary>
/// What the trade scripts are handed, against <c>docs/mechanics/trade.md</c>, section 9, on
/// <see cref="ScriptWorld"/>: Germany and Poland land neighbours, metal at 0.1.
/// </summary>
public sealed class LuaTradeTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-luatrade-" + Guid.NewGuid().ToString("N"));
	private readonly List<string> _reports = new();

	public LuaTradeTests()
	{
		Directory.CreateDirectory(Path.Combine(_root, "script"));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private CAiScripts Load(string autoexec = "")
	{
		File.WriteAllText(Path.Combine(_root, "script", "autoexec.lua"), autoexec);
		return new CAiScripts(CDirectorySettings.At(_root), _reports.Add);
	}

	private static CGameState World()
	{
		CGameState state = ScriptWorld.State();
		state.Defines.Set(CTradeRules.GoodsCostSection, "METAL", CFixedPoint.Parse("0.1"));
		state.Defines.Set(COfferRules.Section, "TRADE_INFLUENCE_COST", CFixedPoint.Parse("3"));
		foreach (CCountry country in state.Countries)
		{
			country.DiplomaticInfluence = CFixedPoint.FromInt(10);
		}
		return state;
	}

	private const string Ger = "CCountryDataBase.GetTag('GER')";
	private const string Pol = "CCountryDataBase.GetTag('POL')";

	/// <summary>A Lua expression: Germany's action buying ten metal a day from Poland.</summary>
	private const string Buying = "(function() local a = CTradeAction(" + Ger + ", " + Pol + ") "
		+ "a:SetTrading(CFixedPoint(10), CGoodsPool._METAL_) return a end)()";

	[Fact]
	public void A_fixed_point_is_made_from_a_number_through_single_precision()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal(-2.5, scripts.Ask(state, "CFixedPoint(-2.5):Get()"));
		Assert.Equal(0.0, scripts.Ask(state, "CFixedPoint(0.0004):Get()"));
		Assert.Equal(0.0, scripts.Ask(state, "CFixedPoint(-0.0004):Get()"));
		Assert.Equal((double)-0.001f, scripts.Ask(state, "CFixedPoint(-0.0006):Get()"));
	}

	[Fact]
	public void An_action_buys_on_the_recipients_block_and_sells_on_its_own_and_prices_the_other()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal(10.0, scripts.Ask(state, Buying + ":GetRoute():GetTradedToOf(CGoodsPool._METAL_):Get()"));
		Assert.Equal((double)0.75f, scripts.Ask(state, Buying + $":GetTrading(CGoodsPool._MONEY_, {Ger}):Get()"));
		Assert.Equal(0.0, scripts.Ask(state, Buying + $":GetTrading(CGoodsPool._MONEY_, {Pol}):Get()"));
		Assert.Equal(4.0, scripts.Ask(state, $"(function() local a = CTradeAction({Ger}, {Pol}) "
			+ "a:SetTrading(CFixedPoint(-4), CGoodsPool._METAL_) return a:GetRoute():GetTradedFromOf(CGoodsPool._METAL_):Get() end)()"));
		Assert.Equal(true, scripts.Ask(state, Buying + ":IsValid()"));
		Assert.Equal(true, scripts.Ask(state, Buying + ":IsSelectable()"));
	}

	[Fact]
	public void A_score_is_handed_the_action_and_both_blocks_money_first_and_a_scripts_own_asking_reenters()
	{
		CGameState state = World();
		using CAiScripts scripts = Load(@"
function DiploScore_OfferTrade(ai, from, to, observer, action, tradedFrom, tradedTo)
	if not (to == observer) then return -1 end
	return tradedFrom.vMoney * 1000 + tradedTo.vMetal + action:GetRoute():GetTradedToOf(CGoodsPool._METAL_):Get()
end");
		state.AiScripts = scripts;
		CTradeTerms terms = CTradeRules.SetTrading(state, CTradeTerms.None, state.Country(ScriptWorld.Ger)!,
			state.Country(ScriptWorld.Pol)!, CFixedPoint.FromInt(10).Raw, EGoods.Metal);
		var offer = new COffer(EOfferKind.Trade, ScriptWorld.Ger, ScriptWorld.Pol, EOfferStage.Proposed, true, state.TotalHours)
		{
			Deal = new CTradeDeal(ScriptWorld.Ger, ScriptWorld.Pol, terms),
		};
		// 0.75 money given, 10 metal got twice over.
		Assert.Equal(770, scripts.OfferTradeScore(state, offer));
		Assert.Equal(770.0, scripts.Ask(state, Buying + ":GetAIAcceptance()"));
		Assert.Empty(_reports);
	}

	[Fact]
	public void A_scripts_posted_offer_is_an_offer_command_dated_when_it_was_made()
	{
		CGameState state = World();
		using CAiScripts scripts = Load($@"
MADE = nil
function ProposeTrades(ai, tag)
	local a = CTradeAction(tag, {Pol})
	a:SetTrading(CFixedPoint(10), CGoodsPool._METAL_)
	ai:PostAction(a)
end
function EvalutateExistingTrades(ai, tag)
	for route in ai:GetCountry():AIGetTradeRoutes() do
		local a = CTradeAction(tag, route:GetTo())
		a:SetRoute(route)
		a:SetValue(false)
		ai:PostAction(a)
	end
end");
		CCountry germany = state.Country(ScriptWorld.Ger)!;
		scripts.RunTrade(state, germany, propose: true);
		var posted = Assert.IsType<COfferCommand>(Assert.Single(state.Outbox));
		Assert.Equal(EOfferKind.Trade, posted.Offer.Kind);
		Assert.Equal(10000, posted.Offer.Deal!.Terms.To(EGoods.Metal));
		Assert.Equal(state.TotalHours, posted.Offer.Date);

		state.Outbox.Clear();
		CTradeRoutes.Make(state, posted.Offer.Deal, state.TotalHours);
		scripts.RunTrade(state, germany, propose: false);
		var cancel = Assert.IsType<COfferCommand>(Assert.Single(state.Outbox));
		Assert.False(cancel.Offer.Value);
		Assert.True(COfferRules.IsValid(state, cancel.Offer));
		Assert.Empty(_reports);
	}

	[Fact]
	public void Rebels_are_the_only_unreal_tag_and_a_route_goes_stale_after_a_fortnight_failing()
	{
		CGameState state = World();
		using CAiScripts scripts = Load();
		Assert.Equal(false, scripts.Ask(state, "CCountryDataBase.GetTag('REB'):IsReal()"));
		Assert.Equal(true, scripts.Ask(state, Ger + ":IsReal()"));

		CTradeRoute route = CTradeRoutes.Make(state, new CTradeDeal(ScriptWorld.Ger, ScriptWorld.Pol,
			CTradeRules.SetTrading(state, CTradeTerms.None, state.Country(ScriptWorld.Ger)!, state.Country(ScriptWorld.Pol)!,
				10000, EGoods.Metal)), state.TotalHours)!;
		route.Inactive = true;
		route.LastInactive = state.TotalHours - 337;
		Assert.Equal(true, scripts.Ask(state,
			$"(function() for r in {Ger}:GetCountry():AIGetTradeRoutes() do return r:IsInactive() end end)()"));
		Assert.Equal(true, scripts.Ask(state, $"(function() local ai = nil for r in {Ger}:GetCountry():AIGetTradeRoutes() do "
			+ "return r end end)() ~= nil"));
	}
}
