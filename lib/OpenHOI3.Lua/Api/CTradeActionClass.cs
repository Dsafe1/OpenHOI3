using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CTradeAction</c>, an offer to trade a script makes with <c>CTradeAction(actor, recipient)</c>,
/// sets amounts on, tests and posts; and <c>CTradeRoute</c>, a route - a running one or one an action
/// carries. See <c>docs/mechanics/trade.md</c>, section 9.
/// </summary>
public static class CTradeActionClass
{
	private const string ActionName = "CTradeAction";
	private const string RouteName = "CTradeRoute";

	public static CLuaClass DefineAction(CGameApi api)
	{
		CLuaClass luaClass = new CLuaClass(ActionName)
			.Method("GetRoute", call => call.Return(new CLuaObject(api.TradeRoute, Of(call).Route)))
			.Method("SetRoute", call =>
			{
				CLuaTradeAction action = Of(call);
				CTradeRoute route = call.Object<CTradeRoute>(2, RouteName);
				action.Route = new CTradeRoute(route.From, route.To, route.Terms, action.Date);
				return 0;
			})
			.Method("SetTrading", call =>
			{
				CLuaTradeAction action = Of(call);
				long amount = call.Object<CFixedPoint>(2, "CFixedPoint").Raw;
				var goods = CGoodsPoolClass.Goods(call, 3);
				if (api.State.Country(action.Actor) is { } actor && api.State.Country(action.Recipient) is { } recipient)
				{
					CTradeTerms terms = CTradeRules.SetTrading(api.State, action.Route.Terms, actor, recipient, amount, goods);
					action.Route = new CTradeRoute(action.Route.From, action.Route.To, terms, action.Date);
				}
				return 0;
			})
			// What the asking side hands over: the actor's block where it is the actor, else the recipient's.
			.Method("GetTrading", call =>
			{
				CLuaTradeAction action = Of(call);
				var goods = CGoodsPoolClass.Goods(call, 2);
				CCountryTag tag = call.Object<CCountryTag>(3, "CCountryTag");
				long amount = tag == action.Actor ? action.Route.Terms.From(goods) : action.Route.Terms.To(goods);
				return call.Return(api.Fixed(new CFixedPoint(amount)));
			})
			.Method("SetValue", call =>
			{
				Of(call).Value = call.Boolean(2);
				return 0;
			})
			.Method("IsValid", call => call.Return(COfferRules.IsValid(api.State, Of(call).Offer)))
			.Method("IsSelectable", call => call.Return(COfferRules.IsSelectable(api.State, Of(call).Offer)))
			.Method("GetAIAcceptance", call => call.Return(CPactPasses.Score(api.State, Of(call).Offer)))
			.Method("GetActor", call => call.Return(api.Tag(Of(call).Actor)))
			.Method("GetRecipient", call => call.Return(api.Tag(Of(call).Recipient)))
			.Method("GetValue", call => call.Return(Of(call).Value));
		return luaClass.Constructs(call =>
		{
			CCountryTag actor = call.Object<CCountryTag>(2, "CCountryTag");
			CCountryTag recipient = call.Object<CCountryTag>(3, "CCountryTag");
			return call.Return(new CLuaObject(luaClass, new CLuaTradeAction(actor, recipient, api.State.TotalHours)));
		});
	}

	public static CLuaClass DefineRoute(CGameApi api)
	{
		return new CLuaClass(RouteName)
			.Method("GetFrom", call => call.Return(api.Tag(Route(call).From)))
			.Method("GetTo", call => call.Return(api.Tag(Route(call).To)))
			.Method("GetTradedFromOf", call => call.Return(api.Fixed(new CFixedPoint(Route(call).Terms.From(CGoodsPoolClass.Goods(call, 2))))))
			.Method("GetTradedToOf", call => call.Return(api.Fixed(new CFixedPoint(Route(call).Terms.To(CGoodsPoolClass.Goods(call, 2))))))
			.Method("IsInactive", call => call.Return(Route(call).Inactive));
	}

	private static CLuaTradeAction Of(CLuaCall call) => call.Object<CLuaTradeAction>(1, ActionName);

	private static CTradeRoute Route(CLuaCall call) => call.Object<CTradeRoute>(1, RouteName);
}
