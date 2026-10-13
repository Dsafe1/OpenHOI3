using System;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the trade window says: a slider's place as an amount a day, each good's line, the offer's text
/// and Accept's tooltip. See <c>docs/mechanics/trade.md</c>, sections 7 and 10.
/// </summary>
public static class CTradeWindowText
{
	/// <summary>The goods with a slider, by the slider's name: every good but money.</summary>
	public static IReadOnlyList<(EGoods Goods, string Slider, string Value, string Reset)> Sliders { get; } = new[]
	{
		(EGoods.CrudeOil, "crude_slider", "value_crude_oil", "reset_crude_oil"),
		(EGoods.Fuel, "fuel_slider", "value_fuel", "reset_fuel"),
		(EGoods.Supplies, "supplies_slider", "value_supplies", "reset_supplies"),
		(EGoods.Metal, "metal_slider", "value_metal", "reset_metal"),
		(EGoods.RareMaterials, "rare_met_slider", "value_rare_met", "reset_rare_met"),
		(EGoods.Energy, "energy_slider", "value_energy", "reset_energy"),
	};

	/// <summary>The key a good's name is written by (ours: the original's key was not read; these say the same).</summary>
	public static string GoodsName(EGoods goods) => goods switch
	{
		EGoods.Supplies => "SUPPLIES_IRO",
		EGoods.Fuel => "FUEL_IRO",
		EGoods.Money => "MONEY_IRO",
		EGoods.CrudeOil => "CRUDE_OIL_IRO",
		EGoods.Metal => "METAL_IRO",
		EGoods.Energy => "ENERGY_IRO",
		_ => "RAREMAT_IRO",
	};

	/// <summary>
	/// A slider's place, 0 to 1000, as an amount a day: below its middle a sale, from it a purchase, the
	/// ends <c>MAX_DAILY_TRADE</c> over two either way.
	/// </summary>
	public static long Amount(CGameState state, long place)
	{
		long most = state.Defines["economy", "MAX_DAILY_TRADE"].Raw;
		return place < 500 ? -most * (500 - place) / 1000 : most * (place - 500) / 1000;
	}

	/// <summary>A good's line: <c>TRADE_DAY</c>, the amount to two places.</summary>
	public static string Value(Func<string, string> localise, long amount)
	{
		return CTextTemplate.Fill(localise("TRADE_DAY"), ("VAL", CNumberText.Format(new CFixedPoint(amount), 2)));
	}

	/// <summary>
	/// A block as text: each good of a figure other than nought, "§Y0.40 Metal§W ", joined by ", " and the
	/// last two by " and "; <c>TRADE_NOTHING</c> where there is none. The joins count only the figures above
	/// nought, as the original's do.
	/// </summary>
	public static string Goods(Func<string, string> localise, Func<EGoods, long> block)
	{
		var items = new List<string>();
		int positive = 0;
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			long figure = block((EGoods)i);
			if (figure > 0)
			{
				positive++;
			}
			if (figure != 0)
			{
				items.Add("§Y" + CNumberText.Format(new CFixedPoint(figure), 2) + " " + localise(GoodsName((EGoods)i)) + "§W ");
			}
		}
		if (items.Count == 0)
		{
			return localise("TRADE_NOTHING");
		}
		var text = new StringBuilder();
		for (int i = 0; i < items.Count; i++)
		{
			if (i > 0)
			{
				text.Append(i == positive - 1 ? " " + localise("AND") : ", ");
			}
			text.Append(items[i]);
		}
		return text.ToString();
	}

	/// <summary>
	/// The offer's text, <c>TRADENEWDESCTWO</c>, COUNTRY the recipient: where the actor pays nothing, SELL
	/// what it gives, BUY what it gets and TYPE <c>TRADE_BUY</c>; where it pays, BUY what it gives, SELL
	/// what it gets, and TYPE <c>TRADE_TRADE</c> if the recipient pays too, else <c>TRADE_SELL</c>.
	/// </summary>
	public static string Description(Func<string, string> localise, CTradeTerms terms, CCountry recipient)
	{
		string gives = Goods(localise, terms.From);
		string gets = Goods(localise, terms.To);
		bool actorPays = terms.From(EGoods.Money) >= 1;
		string type = !actorPays ? "TRADE_BUY" : terms.To(EGoods.Money) >= 1 ? "TRADE_TRADE" : "TRADE_SELL";
		return CTextTemplate.Fill(localise("TRADENEWDESCTWO"), ("COUNTRY", recipient.Name), ("TYPE", localise(type)),
			("SELL", actorPays ? gets : gives), ("BUY", actorPays ? gives : gets));
	}

	/// <summary>Whether Accept may be pressed: the terms are good and their convoy could be made.</summary>
	public static bool MayAccept(CGameState state, CTradeDeal deal)
	{
		return state.Country(deal.From) is { } from && state.Country(deal.To) is { } to
			&& CTradeRules.IsGood(state, deal.Terms, from, to)
			&& CTradeRules.IsConvoyPossible(state, CSupplyNetworks.Refresh(state), deal);
	}

	/// <summary>
	/// Accept's tooltip: the row's, where it may be pressed; <c>TR_CANT_CONVOY</c> naming the buyer where
	/// only the convoy is wanting; <c>NO_TRADE_RULE</c> for a barter; nothing for terms with no price.
	/// </summary>
	public static string AcceptTooltip(Func<string, string> localise, CGameState state, Sim.Agreements.COffer offer)
	{
		if (offer.Deal is not { } deal || state.Country(deal.From) is not { } from || state.Country(deal.To) is not { } to)
		{
			return "";
		}
		if (CTradeRules.IsGood(state, deal.Terms, from, to))
		{
			if (CTradeRules.IsConvoyPossible(state, CSupplyNetworks.Refresh(state), deal))
			{
				return CDiplomacyActions.Tooltip(localise, state, offer);
			}
			CCountryTag buyer = CTradeRules.Buyer(deal.Terms, deal.From, deal.To);
			return CTextTemplate.Fill(localise("TR_CANT_CONVOY"), ("ACTOR", state.Country(buyer)?.Name ?? buyer.Value));
		}
		return IsBarter(deal.Terms)
			? CTextTemplate.Fill(localise("NO_TRADE_RULE"), ("RULE", localise("RULE_FREE_RESOURCE_GIFTS")))
			: "";
	}

	private static bool IsBarter(CTradeTerms terms)
	{
		bool from = false;
		bool to = false;
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			if ((EGoods)i == EGoods.Money)
			{
				continue;
			}
			from |= terms.From((EGoods)i) > 1;
			to |= terms.To((EGoods)i) > 1;
		}
		return from && to;
	}
}
