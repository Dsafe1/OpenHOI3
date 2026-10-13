using System;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Trade;

/// <summary>
/// What a trade costs and when it may run: the price of an amount of a good, setting an amount in an
/// offer's terms, whether a route is good, whether two countries need a convoy to trade and where it
/// would run. See <c>docs/mechanics/trade.md</c>, sections 1, 5 and 8.
/// </summary>
public static class CTradeRules
{
	public const string GoodsCostSection = "goods_cost";

	/// <summary>The faction rule under which goods may be traded for goods, or for nothing.</summary>
	public const string FreeGiftsRule = "free_resource_gifts";

	/// <summary>The relation's whole range, -200 to +200, in thousandths.</summary>
	private const long RelationRange = 400000;

	/// <summary>How much of a good's cost the best relation takes off: a half, in thousandths, and no define.</summary>
	private const long MostOff = 500;

	/// <summary>One thousandth: the least a block's figure has to be to count.</summary>
	private const long Least = 1;

	/// <summary>The key each good's cost has in the block <c>goods_cost</c>.</summary>
	public static string CostKey(EGoods goods) => goods switch
	{
		EGoods.Supplies => "SUPPLIES",
		EGoods.Fuel => "FUEL",
		EGoods.Money => "MONEY",
		EGoods.CrudeOil => "CRUDE_OIL",
		EGoods.Metal => "METAL",
		EGoods.Energy => "ENERGY",
		_ => "RARE_MATERIALS",
	};

	/// <summary>
	/// What an amount of a good costs between a route's two ends: its <c>goods_cost</c>, less up to a
	/// half by the relation <paramref name="to"/> holds towards <paramref name="from"/>. Each division cuts.
	/// </summary>
	public static long Cost(CGameState state, EGoods goods, long amount, CCountry from, CCountry to)
	{
		long relation = 200000 + CGameState.Status(to, from).Relation.Raw;
		long share = relation * CFixedPoint.Scale / RelationRange;
		long off = share * MostOff / CFixedPoint.Scale;
		long cost = state.Defines[GoodsCostSection, CostKey(goods)].Raw;
		return cost * amount / CFixedPoint.Scale * (CFixedPoint.Scale - off) / CFixedPoint.Scale;
	}

	/// <summary>Whether a country's faction gives it the rule <c>free_resource_gifts</c>.</summary>
	public static bool HasFreeGifts(CGameState state, CCountry country)
	{
		return country.Faction.Length > 0 && CAlignment.FactionGroup(state, country) is { } group
			&& group.FactionRules.Contains(FreeGiftsRule);
	}

	/// <summary>
	/// An offer's terms with an amount of a good set, between its <c>from</c> - the actor - and its
	/// <c>to</c>: above nought the actor buys it, below nought it sells it, nought clears it; a good
	/// never flows both ways. Then, unless both have <c>free_resource_gifts</c>, each side's money is
	/// worked out again as the price of what it receives.
	/// </summary>
	public static CTradeTerms SetTrading(CGameState state, CTradeTerms terms, CCountry from, CCountry to, long amount,
		EGoods goods)
	{
		if (amount > 0)
		{
			terms = terms.With(fromBlock: false, goods, amount).With(fromBlock: true, goods, 0);
		}
		else if (amount < 0)
		{
			terms = terms.With(fromBlock: true, goods, -amount).With(fromBlock: false, goods, 0);
		}
		else
		{
			terms = terms.With(fromBlock: true, goods, 0).With(fromBlock: false, goods, 0);
		}
		if (HasFreeGifts(state, from) && HasFreeGifts(state, to))
		{
			return terms;
		}
		long toPays = 0;
		long fromPays = 0;
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			var each = (EGoods)i;
			if (each == EGoods.Money)
			{
				continue;
			}
			toPays += Cost(state, each, terms.From(each), from, to);
			fromPays += Cost(state, each, terms.To(each), from, to);
		}
		return terms.With(fromBlock: false, EGoods.Money, toPays).With(fromBlock: true, EGoods.Money, fromPays);
	}

	/// <summary>
	/// Whether terms may make a route: always between two with <c>free_resource_gifts</c>; otherwise
	/// no barter, a price, and a convoy - where it has one - starting and ending in land one of the two
	/// holds.
	/// </summary>
	public static bool IsGood(CGameState state, CTradeTerms terms, CCountry from, CCountry to, CConvoy? convoy = null)
	{
		if (HasFreeGifts(state, from) && HasFreeGifts(state, to))
		{
			return true;
		}
		bool fromGives = false;
		bool toGives = false;
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			var goods = (EGoods)i;
			if (goods == EGoods.Money)
			{
				continue;
			}
			fromGives |= terms.From(goods) > Least;
			toGives |= terms.To(goods) > Least;
		}
		if (fromGives && toGives)
		{
			return false;
		}
		if (terms.From(EGoods.Money) < Least && terms.To(EGoods.Money) < Least)
		{
			return false;
		}
		if (convoy is not null)
		{
			CCountryTag? start = state.Province(convoy.Start).Controller;
			CCountryTag? end = state.Province(convoy.End).Controller;
			if ((start != from.Tag && start != to.Tag) || (end != from.Tag && end != to.Tag))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>The buyer, who runs the convoy: <c>to</c> where it pays money, else <c>from</c>.</summary>
	public static CCountryTag Buyer(CTradeTerms terms, CCountryTag from, CCountryTag to)
	{
		return terms.To(EGoods.Money) != 0 ? to : from;
	}

	/// <summary>
	/// Whether two countries need a convoy to trade: not where they are land neighbours, their capitals
	/// are on one continent, or the areas of their capitals touch; otherwise - and where either capital
	/// has no area - they do.
	/// </summary>
	public static bool NeedsConvoy(CGameState state, CSupplyNetwork network, CCountry a, CCountry b)
	{
		if (CCountryFacts.Of(state).AreNeighbours(a.Tag, b.Tag))
		{
			return false;
		}
		if (network.AreaOf(a.Capital) is not { } first || network.AreaOf(b.Capital) is not { } second)
		{
			return true;
		}
		string continent = state.Province(a.Capital).Continent;
		if (continent.Length > 0 && string.Equals(continent, state.Province(b.Capital).Continent, StringComparison.Ordinal))
		{
			return false;
		}
		return !first.Neighbours.Contains(second);
	}

	/// <summary>
	/// The two harbours a trade convoy of the buyer's would run between: from the harbour of the buyer's
	/// capital area nearest the other's, to the harbour of the other's capital area nearest the buyer's
	/// first harbour. None where either is no harbour with a naval base.
	/// </summary>
	public static (int Start, int End)? ConvoyEnds(CGameState state, CSupplyNetwork network, CCountry buyer, CCountry other)
	{
		if (network.AreaOf(buyer.Capital) is not { } home || home.Ports.Count == 0
			|| network.AreaOf(other.Capital) is not { } theirs)
		{
			return null;
		}
		int end = CConvoyRules.NearestPort(state, theirs, home.Ports[0]);
		int start = CConvoyRules.NearestPort(state, home, end);
		if (end == 0 || start == 0 || !CConvoyRules.IsHarbour(state, state.Province(end))
			|| !CConvoyRules.IsHarbour(state, state.Province(start)))
		{
			return null;
		}
		return (start, end);
	}

	/// <summary>Whether a route of these terms could have the convoy it needs: always where it needs none.</summary>
	public static bool IsConvoyPossible(CGameState state, CSupplyNetwork network, CTradeDeal deal)
	{
		if (state.Country(deal.From) is not { } from || state.Country(deal.To) is not { } to
			|| !NeedsConvoy(state, network, from, to))
		{
			return true;
		}
		CCountry buyer = Buyer(deal.Terms, deal.From, deal.To) == from.Tag ? from : to;
		return ConvoyEnds(state, network, buyer, buyer == from ? to : from) is not null;
	}

	/// <summary>Whether a country has a harbour of its own: a province with a port and a naval base.</summary>
	public static bool HasPort(CGameState state, CCountry country) => CCountryFacts.Of(state).HasHarbour(country.Tag);

	/// <summary>The route of the pair matching a deal in both countries and both blocks exactly; the first such.</summary>
	public static CTradeRoute? Matching(CGameState state, CTradeDeal deal)
	{
		return state.TradeRoutes.Find(r => r.From == deal.From && r.To == deal.To && r.Terms.Equals(deal.Terms));
	}

	/// <summary>
	/// Whether the pair already trades a good of these terms on a route that is not running: for some
	/// good but money, more than a thousandth of it in the same block of both - a block compared by its
	/// place, so a route made the other way round is matched from-block to from-block.
	/// </summary>
	public static bool AlreadyTradingDisabledResource(CGameState state, CTradeDeal deal)
	{
		foreach (CTradeRoute route in state.TradeRoutes)
		{
			if (!route.Inactive || !route.Joins(deal.From, deal.To))
			{
				continue;
			}
			for (int i = 0; i < CGoodsPool.Count; i++)
			{
				var goods = (EGoods)i;
				if (goods == EGoods.Money)
				{
					continue;
				}
				if ((deal.Terms.From(goods) > Least && route.Terms.From(goods) > Least)
					|| (deal.Terms.To(goods) > Least && route.Terms.To(goods) > Least))
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// Marks a route as running or not. A running route that fails is stamped with the hour, unless it
	/// was last found failing within 23 days.
	/// </summary>
	public static void SetInactive(CGameState state, CTradeRoute route, bool inactive)
	{
		if (!route.Inactive && inactive
			&& (route.LastInactive == long.MinValue || route.LastInactive + StickyHours < state.TotalHours))
		{
			route.LastInactive = state.TotalHours;
		}
		route.Inactive = inactive;
	}

	/// <summary>How long a route's last failure is kept before a new one is stamped: 23 days.</summary>
	public const long StickyHours = 552;
}
