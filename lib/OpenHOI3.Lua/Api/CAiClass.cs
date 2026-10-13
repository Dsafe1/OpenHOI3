using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAI</c>, a country's AI, which the AI's functions are handed first; and <c>CAIStrategy</c>, what
/// its strategy makes of each other country - not built, so nought. See <c>docs/mechanics/lua.md</c>,
/// sections 4 and 4.3.
/// </summary>
public static class CAiClass
{
	private const string AiName = "CAI";
	private const string StrategyName = "CAIStrategy";

	/// <summary>What a strategy's figures would come from, which no file of the base game declares.</summary>
	private const string StrategyUnbuilt = "the AI's strategy (add_ai_strategy): friendliness, antagonism and threat are 0";

	public static CLuaClass DefineAi(CGameApi api)
	{
		return new CLuaClass(AiName)
			.Method("GetCountry", call => call.Return(api.Of(call.Object<CLuaAi>(1, AiName).Country)))
			.Method("GetCurrentDate", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				return call.Return(new CLuaObject(api.Date, api.State.Date));
			})
			.Method("GetRelation", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				CCountry? holder = api.CountryOf(call, 2);
				CCountry? other = api.CountryOf(call, 3);
				return call.Return(holder is not null && other is not null
					? new CLuaObject(api.DiplomacyStatus, CGameState.Status(holder, other))
					: null);
			})
			.Method("GetNumberOfOwnedProvinces", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				return call.Return(CCountryFacts.Of(api.State).OwnedCount(call.Object<CCountryTag>(2, "CCountryTag")));
			})
			.Method("GetCountryAlignmentDistance", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				CCountry a = call.Object<CCountry>(2, "CCountry");
				CCountry b = call.Object<CCountry>(3, "CCountry");
				return call.Return(api.Fixed(new CFixedPoint(
					CLuaFloats.AlignmentDistance(a.AlignmentX, a.AlignmentY, b.AlignmentX, b.AlignmentY))));
			})
			// Both countries have the faction rule free_resource_gifts (docs/mechanics/trade.md, section 8).
			.Method("CanTradeFreeResources", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				CCountry? a = api.CountryOf(call, 2);
				CCountry? b = api.CountryOf(call, 3);
				return call.Return(a is not null && b is not null
					&& Sim.Trade.CTradeRules.HasFreeGifts(api.State, a) && Sim.Trade.CTradeRules.HasFreeGifts(api.State, b));
			})
			// Trade (docs/mechanics/trade.md, section 9).
			.Method("AlreadyTradingDisabledResource", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				Sim.Trade.CTradeRoute route = call.Object<Sim.Trade.CTradeRoute>(2, "CTradeRoute");
				return call.Return(Sim.Trade.CTradeRules.AlreadyTradingDisabledResource(api.State,
					new Sim.Trade.CTradeDeal(route.From, route.To, route.Terms)));
			})
			.Method("HasTradeGoneStale", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				Sim.Trade.CTradeRoute route = call.Object<Sim.Trade.CTradeRoute>(2, "CTradeRoute");
				return call.Return(route.Inactive && route.LastInactive != long.MinValue
					&& route.LastInactive + StaleHours < api.State.TotalHours);
			})
			.Method("GetSpamPenalty", call => call.Return(SpamPenalty(api, call.Object<CLuaAi>(1, AiName).Country,
				call.Object<CCountryTag>(2, "CCountryTag"))))
			// Posted as an offer from the screen is, keeping the hour the action was made in.
			.Method("PostAction", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				CLuaTradeAction action = call.Object<CLuaTradeAction>(2, "CTradeAction");
				api.Post(new Sim.Commands.COfferCommand(action.Offer));
				return 0;
			})
			.Constant("_DIPLOMACY_", 0)
			.Constant("_PRODUCTION_", 1)
			.Constant("_TECHNOLOGY_", 2)
			.Constant("_POLITICS_", 3)
			.Constant("_INTELLIGENCE_", 4);
	}

	/// <summary>How long a route may stay failing before an AI calls it stale: a fortnight.</summary>
	private const long StaleHours = 336;

	/// <summary>
	/// What an AI holds against offering a country more: nothing unless a person plays it; otherwise ten
	/// for each day from now to 24 days past its last diplomat's arrival there, never below nought.
	/// </summary>
	private static float SpamPenalty(CGameApi api, CCountry country, CCountryTag tag)
	{
		if (!api.State.Played.Contains(tag) || api.State.Country(tag) is not { } other)
		{
			return 0f;
		}
		long until = CGameState.Status(country, other).DiplomatUntil;
		if (until == long.MinValue)
		{
			return 0f;
		}
		long days = until / CGameState.HoursPerDay - api.State.TotalHours / CGameState.HoursPerDay + 24;
		return 10f * System.Math.Max(0, days);
	}

	public static CLuaClass DefineStrategy(CGameApi api)
	{
		return new CLuaClass(StrategyName)
			.Method("GetCountry", call => call.Return(api.Of(call.Object<CCountry>(1, StrategyName))))
			.Method("GetCountryTag", call => call.Return(api.Tag(call.Object<CCountry>(1, StrategyName).Tag)))
			.Method("GetFriendliness", call => Unbuilt(api, call))
			.Method("GetAntagonism", call => Unbuilt(api, call))
			.Method("GetThreat", call => Unbuilt(api, call))
			.Constant("_AI_UNDEFINED_", 0)
			.Constant("_AI_MILITARIST_", 1)
			.Constant("_AI_INDUSTRIALIST_", 2)
			.Constant("_AI_DIPLOMAT_", 3)
			.Constant("_AI_BALANCED_", 4);
	}

	private static int Unbuilt(CGameApi api, CLuaCall call)
	{
		call.Object<CCountry>(1, StrategyName);
		call.Object<CCountryTag>(2, "CCountryTag");
		api.ReportUnbuilt(StrategyUnbuilt);
		return call.Return(0);
	}
}
