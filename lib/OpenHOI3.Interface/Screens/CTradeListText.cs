using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the production screen's list of trades says: which routes it holds and in what order, a line's
/// name and its goods icons, which column each icon is in, where the inactive marker goes, the tooltips,
/// and the cancel dialog's texts. See <c>docs/mechanics/trade.md</c>, section 13.
/// </summary>
public static class CTradeListText
{
	/// <summary>A line's goods icon: the good, and whether it is one the player hands over - the Export column.</summary>
	public readonly record struct CTradeIcon(EGoods Goods, bool PlayerGives);

	/// <summary>How far right of a column's start the inactive marker stands: on its second icon's place.</summary>
	public const float InactiveOffset = 32f;

	/// <summary>
	/// The player's routes in the list's order: the other countries in theirs, and each one's routes in the
	/// order they were made.
	/// </summary>
	public static List<CTradeRoute> Routes(CGameState state, CCountryTag player)
	{
		var routes = new List<CTradeRoute>();
		foreach (CCountry other in state.Countries)
		{
			if (other.Tag == player)
			{
				continue;
			}
			foreach (CTradeRoute route in state.TradeRoutes)
			{
				if (route.Joins(player, other.Tag))
				{
					routes.Add(route);
				}
			}
		}
		return routes;
	}

	/// <summary>The other country of a route.</summary>
	public static CCountryTag Partner(CTradeRoute route, CCountryTag player) => route.From == player ? route.To : route.From;

	/// <summary>A line's name: the partner's flag in the text, then his name.</summary>
	public static string Name(CGameState state, CTradeRoute route, CCountryTag player)
	{
		CCountryTag partner = Partner(route, player);
		return "@" + partner.Value + " " + (state.Country(partner)?.Name ?? partner.Value);
	}

	/// <summary>
	/// A line's icons, a good at most once, in the goods' order: a good the route's <c>from</c> hands over,
	/// else one its <c>to</c> does, in the column of the side the player is on.
	/// </summary>
	public static List<CTradeIcon> Icons(CTradeRoute route, CCountryTag player)
	{
		var icons = new List<CTradeIcon>();
		for (int i = 0; i < CGoodsPool.Count; i++)
		{
			var goods = (EGoods)i;
			if (route.Terms.From(goods) > 0)
			{
				icons.Add(new CTradeIcon(goods, route.To != player));
			}
			else if (route.Terms.To(goods) > 0)
			{
				icons.Add(new CTradeIcon(goods, route.To == player));
			}
		}
		return icons;
	}

	/// <summary>The interface's icon type for a good: <c>supplies_trade_icon</c> and the rest.</summary>
	public static string IconName(EGoods goods) => goods switch
	{
		EGoods.Supplies => "supplies_trade_icon",
		EGoods.Fuel => "fuel_trade_icon",
		EGoods.Money => "money_trade_icon",
		EGoods.CrudeOil => "oil_trade_icon",
		EGoods.Metal => "metal_trade_icon",
		EGoods.Energy => "energy_trade_icon",
		_ => "raremat_trade_icon",
	};

	/// <summary>
	/// Whether the inactive marker stands in the Import column - where the player is the one the reason
	/// names - rather than the Export column. Shown only while the route is inactive.
	/// </summary>
	public static bool InactiveOnImport(CTradeRoute route, CCountryTag player) => route.InactiveCountry == player;

	/// <summary>
	/// An icon's tooltip: <c>PW_EXPORTING</c> or <c>PW_IMPORTING</c>, the good's amount a day to two
	/// places, its name and the partner's.
	/// </summary>
	public static string IconTooltip(Func<string, string> localise, CGameState state, CTradeRoute route, CCountryTag player,
		CTradeIcon icon)
	{
		bool fromBlock = route.Terms.From(icon.Goods) > 0;
		long amount = fromBlock ? route.Terms.From(icon.Goods) : route.Terms.To(icon.Goods);
		CCountryTag partner = Partner(route, player);
		return CTextTemplate.Fill(localise(icon.PlayerGives ? "PW_EXPORTING" : "PW_IMPORTING"),
			("NUM", CNumberText.Format(new CFixedPoint(amount), 2)),
			("TYPE", localise(CTradeWindowText.GoodsName(icon.Goods))),
			("COUNTRY", state.Country(partner)?.Name ?? partner.Value));
	}

	/// <summary>The inactive marker's tooltip, <c>TRADE_IS_INACTIVE</c>.</summary>
	public static string InactiveTooltip(Func<string, string> localise) => localise("TRADE_IS_INACTIVE");

	/// <summary>Its delayed half: why the route did not run, as its day wrote it.</summary>
	public static string InactiveReason(Func<string, string> localise, CGameState state, CTradeRoute route)
	{
		if (route.InactiveKey.Length == 0)
		{
			return "";
		}
		CCountryTag? named = route.InactiveCountry;
		string country = named is { } tag ? state.Country(tag)?.Name ?? tag.Value : "";
		return CTextTemplate.Fill(localise(route.InactiveKey), ("COUNTRY", country),
			("RES", localise(CTradeWindowText.GoodsName(route.InactiveGoods))));
	}

	/// <summary>The cancel dialog's title, <c>CANCELTRADETITLE</c>.</summary>
	public static string CancelTitle(Func<string, string> localise) => localise("CANCELTRADETITLE");

	/// <summary>
	/// The cancel dialog's text, <c>CANCELTRADENEWDESC</c>: the partner, SELL the route's <c>from</c>'s goods
	/// and BUY its <c>to</c>'s whichever side the player is on - so it reads backwards for a route's
	/// <c>to</c>, as the original's does - with every <c>§W</c> turned to the dialog's black.
	/// </summary>
	public static string CancelDescription(Func<string, string> localise, CGameState state, CTradeRoute route, CCountryTag player)
	{
		CCountryTag partner = Partner(route, player);
		string text = CTextTemplate.Fill(localise("CANCELTRADENEWDESC"), ("COUNTRY", state.Country(partner)?.Name ?? partner.Value),
			("SELL", CTradeWindowText.Goods(localise, route.Terms.From)), ("BUY", CTradeWindowText.Goods(localise, route.Terms.To)));
		return text.Replace("§W", "§b");
	}
}
