using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CAI</c>, a country's AI, which the AI's functions are handed first; and <c>CAIStrategy</c>, what
/// its strategy makes of each other country (<c>docs/mechanics/aistrategy.md</c>). See <c>docs/mechanics/lua.md</c>,
/// sections 4 and 4.3.
/// </summary>
public static class CAiClass
{
	private const string AiName = "CAI";
	private const string StrategyName = "CAIStrategy";

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
				CCountryTag holderTag = call.Object<CCountryTag>(2, "CCountryTag");
				CCountry? holder = api.State.Country(holderTag) ?? (holderTag.Value == CGameApi.NullTag.Value ? api.NullCountry : null);
				return call.Return(api.Status(holder, call, 3) is { } status ? new CLuaObject(api.DiplomacyStatus, status) : null);
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
			// Posted as an offer from the screen is, keeping the hour the action was made in - a trade, or a licence the
			// production script's licence check asks for (ministers.md, section 7).
			.Method("PostAction", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				api.Post(new Sim.Commands.COfferCommand(OfferOf(call, 2)));
				return 0;
			})
			// A command posted from the AI's country (docs/mechanics/politics.md, section 12).
			.Method("Post", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				return CPoliticsCommandClass.Post(api, call, 2);
			})
			.Method("CanDeclareWar", call =>
			{
				call.Object<CLuaAi>(1, AiName);
				CCountryTag actor = call.Object<CCountryTag>(2, "CCountryTag");
				CCountryTag target = call.Object<CCountryTag>(3, "CCountryTag");
				return call.Return(api.State.Country(actor) is { } from && api.State.Country(target) is { } at
					&& CCountryLists.OwnedCount(api.State, target) > 0 && OpenHOI3.Sim.Wars.CDeclarations.IsSelectable(api.State, from, at));
			})
			// The production script's (ministers.md, section 4): brigades of each type, by the type's index.
			.Method("GetProductionSubUnitCounts", call => call.Return(new CLuaObject(api.ArrayInt,
				CLicenceActionClass.ByTypeIndex(api, Sim.Economy.CProductionAi.InProduction(call.Object<CLuaAi>(1, AiName).Country)))))
			.Method("GetDeployedSubUnitCounts", call => call.Return(new CLuaObject(api.ArrayInt,
				CLicenceActionClass.ByTypeIndex(api, Sim.Economy.CProductionAi.Deployed(call.Object<CLuaAi>(1, AiName).Country)))))
			.Static("FastNormalizeByPriority", CLuaShares.FastNormalizeByPriority)
			.Constant("_DIPLOMACY_", 0)
			.Constant("_PRODUCTION_", 1)
			.Constant("_TECHNOLOGY_", 2)
			.Constant("_POLITICS_", 3)
			.Constant("_INTELLIGENCE_", 4);
	}

	/// <summary>The offer a diplomatic action handed to a method stands for - any kind a script makes.</summary>
	public static Sim.Agreements.COffer OfferOf(CLuaCall call, int argument)
	{
		return call.ObjectAt(argument, out _) switch
		{
			CLuaTradeAction trade => trade.Offer,
			CLuaLicenceAction licence => licence.Offer,
			CLuaDiplomaticAction action => action.Offer,
			_ => throw new CLuaError($"bad argument #{argument} (CDiplomaticAction expected)"),
		};
	}

	/// <summary>Whether the AI may declare a war, which was not read.</summary>
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
			// The weights the strategy's lists hold, nought where a country is not listed (aistrategy.md).
			.Method("GetFriendliness", call => Weight(call, strategy => strategy.Befriend))
			.Method("GetAntagonism", call => Weight(call, strategy => strategy.Antagonize))
			.Method("GetThreat", call => Weight(call, strategy => strategy.Threat))
			.Method("GetProtectionism", call => Weight(call, strategy => strategy.Protect))
			// The prepared wars (docs/mechanics/foreignminister.md, section 4).
			.Method("PrepareWarDecision", call =>
			{
				Sim.Script.CDecision? decision = call.TypeOf(4) == ELuaType.Nil
					? null
					: call.Object<Sim.Script.CDecision>(4, CDecisionClass.DecisionName);
				return PrepareWar(api, call, decision, call.Boolean(5));
			})
			.Method("PrepareWar", call => PrepareWar(api, call, null, false))
			.Method("PrepareLimitedWar", call => PrepareWar(api, call, null, true))
			.Method("CancelPrepareWar", call =>
			{
				Sim.Ai.CWarTargets.Cancel(call.Object<CCountry>(1, StrategyName), call.Object<CCountryTag>(2, "CCountryTag"));
				return 0;
			})
			.Method("IsPreparingWar", call => call.Return(
				Sim.Ai.CWarTargets.IsPreparingWar(api.State, call.Object<CCountry>(1, StrategyName))))
			.Method("IsPreparingWarWith", call => call.Return(Sim.Ai.CWarTargets.IsPreparingWarWith(api.State,
				call.Object<CCountry>(1, StrategyName), call.Object<CCountryTag>(2, "CCountryTag"))))
			.Method("IsMilitarist", call => call.Return(call.Object<CCountry>(1, StrategyName).Strategy.IsMilitarist))
			.Constant("_AI_UNDEFINED_", 0)
			.Constant("_AI_MILITARIST_", 1)
			.Constant("_AI_INDUSTRIALIST_", 2)
			.Constant("_AI_DIPLOMAT_", 3)
			.Constant("_AI_BALANCED_", 4);
	}

	/// <summary>
	/// A war prepared on the tag given second, the value third - a single-precision number, as the original takes it - with
	/// a decision and a limit or none.
	/// </summary>
	private static int PrepareWar(CGameApi api, CLuaCall call, Sim.Script.CDecision? decision, bool limited)
	{
		CCountry country = call.Object<CCountry>(1, StrategyName);
		CCountryTag target = call.Object<CCountryTag>(2, "CCountryTag");
		int valueBits = System.BitConverter.SingleToInt32Bits((float)call.Number(3));
		Sim.Ai.CWarTargets.Prepare(api.State, country, target, valueBits, decision, limited);
		return 0;
	}

	private static int Weight(CLuaCall call, System.Func<Sim.Ai.CAiStrategy, Sim.Ai.CStrategyList<CCountryTag>> list)
	{
		CCountry country = call.Object<CCountry>(1, StrategyName);
		CCountryTag other = call.Object<CCountryTag>(2, "CCountryTag");
		return call.Return(list(country.Strategy).WeightOf(other));
	}
}
