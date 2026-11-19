using System.Collections.Generic;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Trade;

/// <summary>
/// A trade route's life: made where an offer is accepted, with the convoy it needs; ended by a
/// cancellation, a war, an embargo or the loss of its convoy; and the day's exchange. See
/// <c>docs/mechanics/trade.md</c>, sections 3 to 6.
/// </summary>
public static class CTradeRoutes
{
	public const string InactiveConvoy = "TRADE_INACTIVE_CONVOY";
	public const string InactiveNoResource = "TRADE_INACTIVE_NO_RES";

	/// <summary>
	/// An accepted offer's route: nothing where its terms are not good; otherwise the route, dated the
	/// offer's, and its convoy where the two need one - and the route stands even where none can be made.
	/// </summary>
	public static CTradeRoute? Make(CGameState state, CTradeDeal deal, long date)
	{
		if (state.Country(deal.From) is not { } from || state.Country(deal.To) is not { } to
			|| !CTradeRules.IsGood(state, deal.Terms, from, to))
		{
			return null;
		}
		var route = new CTradeRoute(deal.From, deal.To, deal.Terms, date) { Made = state.NextPact() };
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (CTradeRules.NeedsConvoy(state, network, from, to))
		{
			CCountry buyer = CTradeRules.Buyer(deal.Terms, from.Tag, to.Tag) == from.Tag ? from : to;
			if (CTradeRules.ConvoyEnds(state, network, buyer, buyer == from ? to : from) is { } ends)
			{
				var carries = new bool[CGoodsPool.Count];
				for (int i = 0; i < CGoodsPool.Count; i++)
				{
					carries[i] = deal.Terms.From((EGoods)i) != 0 || deal.Terms.To((EGoods)i) != 0;
				}
				route.Convoy = CConvoyMaker.CreateTrade(state, buyer, ends.Start, ends.End, carries);
			}
		}
		state.TradeRoutes.Add(route);
		return route;
	}

	/// <summary>Ends a route, silently; its convoy stops being a trade convoy and goes in the day's look.</summary>
	public static void End(CGameState state, CTradeRoute route)
	{
		state.TradeRoutes.Remove(route);
		if (route.Convoy is { } convoy)
		{
			convoy.IsTrade = false;
		}
	}

	/// <summary>Ends every route between two countries: a war begun between them, or an embargo.</summary>
	public static void EndPair(CGameState state, CCountryTag one, CCountryTag other)
	{
		foreach (CTradeRoute route in state.TradeRoutes.ToArray())
		{
			if (route.Joins(one, other))
			{
				End(state, route);
			}
		}
	}

	/// <summary>
	/// Every country's trade for the day, each owning a province in its turn: for each other country
	/// and each route between them, in the order they were made, the convoy looked at and, in the
	/// route's <c>from</c>'s day, the exchange. A route whose needed convoy is missing ends after the walk.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool[] owns = Diplomacy.CRelations.Owning(state);
		foreach (CCountry country in state.Countries)
		{
			if (owns[country.Index])
			{
				RunCountry(state, network, country);
			}
		}
	}

	/// <summary>One country's trade, in its own day (<c>docs/mechanics/tick.md</c>, section 3), for a country owning a province.</summary>
	public static void RunCountry(CGameState state, CSupplyNetwork network, CCountry country)
	{
		var lost = new List<CTradeRoute>();
		foreach (CCountry other in state.Countries)
		{
			if (other == country || other.Tag.Value == "REB")
			{
				continue;
			}
			foreach (CTradeRoute route in state.TradeRoutes.ToArray())
			{
				if (!route.Joins(country.Tag, other.Tag))
				{
					continue;
				}
				CConvoy? convoy = route.Convoy is { } held && IsKept(state, held) ? held : null;
				if (convoy is null && CTradeRules.NeedsConvoy(state, network, country, other))
				{
					if (!lost.Contains(route))
					{
						lost.Add(route);
					}
					continue;
				}
				if (convoy is not null && (convoy.Path.Count < 2 || convoy.Transports == 0
					|| !CConvoyRules.IsRouteUsable(state, network, convoy)))
				{
					Fail(state, route, InactiveConvoy, CTradeRules.Buyer(route.Terms, route.From, route.To), EGoods.Supplies);
					continue;
				}
				if (route.From == country.Tag && state.Country(route.To) is { } to)
				{
					Exchange(state, route, country, to, convoy);
				}
			}
		}
		foreach (CTradeRoute route in lost)
		{
			state.TradeRoutes.Remove(route);
		}
	}

	/// <summary>Whether a route's convoy is still among its owner's.</summary>
	private static bool IsKept(CGameState state, CConvoy convoy)
	{
		return state.Country(convoy.Owner) is { } owner && owner.Convoys.Contains(convoy);
	}

	/// <summary>
	/// The day's exchange, good by good: each side gives its block, scaled by the convoy's efficiency,
	/// out of a copy of its stockpile, and gets the other's; money owed on debt is not taken. Where a
	/// stockpile would be below nought - money too - the route fails and nothing of the day stands.
	/// </summary>
	private static void Exchange(CGameState state, CTradeRoute route, CCountry from, CCountry to, CConvoy? convoy)
	{
		if (state.Stockpile(from) is not { } fromPool || state.Stockpile(to) is not { } toPool)
		{
			return;
		}
		long efficiency = convoy is null ? CFixedPoint.Scale : CConvoyRules.Efficiency(state, convoy);
		var a = new long[CGoodsPool.Count];
		var b = new long[CGoodsPool.Count];
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			a[i] = fromPool[(EGoods)i].Raw;
			b[i] = toPool[(EGoods)i].Raw;
		}
		bool onDebt = false;
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			var goods = (EGoods)i;
			long give = route.Terms.From(goods) * efficiency / CFixedPoint.Scale;
			long get = route.Terms.To(goods) * efficiency / CFixedPoint.Scale;
			bool fromOnDebt = false;
			bool toOnDebt = false;
			if (goods == EGoods.Money)
			{
				// Debt is not built: nobody allows himself one (docs/mechanics/aid.md).
				fromOnDebt = state.IsAtWar(from.Tag) && CGameState.Status(from, to).AllowDebts;
				toOnDebt = state.IsAtWar(to.Tag) && CGameState.Status(to, from).AllowDebts;
				onDebt |= fromOnDebt || toOnDebt;
			}
			if (fromOnDebt)
			{
				CGameState.Status(from, to).Debt += give;
			}
			else
			{
				a[i] -= give;
			}
			if (toOnDebt)
			{
				CGameState.Status(to, from).Debt += get;
			}
			else
			{
				b[i] -= get;
			}
			a[i] += get;
			b[i] += give;
			if (!onDebt && a[i] < 0)
			{
				Fail(state, route, InactiveNoResource, from.Tag, goods);
				return;
			}
			if (!onDebt && b[i] < 0)
			{
				Fail(state, route, InactiveNoResource, to.Tag, goods);
				return;
			}
			if (!(goods == EGoods.Money && onDebt))
			{
				Credit(from.TradedAway, from.TradedAwaySansAlliedSupply, goods, give);
				Credit(from.TradedFor, from.TradedForSansAlliedSupply, goods, get);
				Credit(to.TradedAway, to.TradedAwaySansAlliedSupply, goods, get);
				Credit(to.TradedFor, to.TradedForSansAlliedSupply, goods, give);
				if (convoy is not null)
				{
					convoy.Daily[goods] = new CFixedPoint(give + get);
				}
			}
		}
		CTradeRules.SetInactive(state, route, false);
		route.InactiveKey = "";
		route.InactiveCountry = null;
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			fromPool[(EGoods)i] = new CFixedPoint(a[i]);
			toPool[(EGoods)i] = new CFixedPoint(b[i]);
		}
	}

	private static void Credit(CGoodsPool pool, CGoodsPool sansAlliedSupply, EGoods goods, long amount)
	{
		pool[goods] += new CFixedPoint(amount);
		sansAlliedSupply[goods] += new CFixedPoint(amount);
	}

	private static void Fail(CGameState state, CTradeRoute route, string key, CCountryTag country, EGoods goods)
	{
		CTradeRules.SetInactive(state, route, true);
		route.InactiveKey = key;
		route.InactiveCountry = country;
		route.InactiveGoods = goods;
	}
}
