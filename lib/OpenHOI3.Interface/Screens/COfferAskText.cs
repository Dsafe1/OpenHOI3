using System;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Trade;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// An offer put to the player: its icon in the bar - the frame of its kind, its tooltip, when it blinks
/// and when it declines itself - and the dialog it opens, <c>DefaultDialog</c>, with its title and
/// description and the time it stays open. See <c>docs/mechanics/agreements.md</c>, section 10, and
/// <c>trade.md</c>, section 14.
/// </summary>
public static class COfferAskText
{
	/// <summary>How long an unopened icon waits before it declines its offer.</summary>
	public const long IconHours = 168;

	/// <summary>When an unopened icon starts to blink.</summary>
	public const long BlinkHours = 120;

	/// <summary>An alliance's dialog stays open a week; the others two months.</summary>
	public const long AllianceHours = 168;

	/// <summary>The kind's number, which is the icon's frame and picks its tooltip: 0 for a kind that asks nothing.</summary>
	public static int Kind(COffer offer) => offer.Kind switch
	{
		EOfferKind.Alliance => 2,
		EOfferKind.Trade => 3,
		EOfferKind.AskMilitaryAccess => 4,
		EOfferKind.OfferMilitaryAccess => 5,
		EOfferKind.NonAggressionPact => 7,
		EOfferKind.Licence => 10,
		EOfferKind.Debt => 11,
		EOfferKind.RequestLendLease => 12,
		EOfferKind.OfferLendLease => 13,
		_ => 0,
	};

	/// <summary>The kind's words in the icon's tooltip; peace and a NAP have a key no table holds, shown as itself.</summary>
	private static string KindKey(int kind) => kind switch
	{
		1 => "ICON_PEACE",
		2 => "ICON_ALLIANCE",
		3 => "ICON_TRADE",
		4 => "ICON_ASKMIL",
		5 => "ICON_OFFACC",
		6 => "ICON_CALLALLY",
		7 => "ICON_NAP",
		8 => "INVITEFACTIONTITLE",
		9 => "ICON_EXPEDITION",
		10 => "ICON_LICENCE",
		11 => "ALLOWDEBTTITLE",
		12 => "ICON_LEND_LEASE",
		_ => "ICON_LEND_LEASE_OFFER",
	};

	/// <summary>
	/// The icon's tooltip, <c>DIPLOICON_TIP</c>: the kind, the proposer, and the hour it declines itself;
	/// a trade's delayed half <c>TRADE_TOOL_REQDESC</c>, what the player would give for what he would get.
	/// </summary>
	public static (string Text, string Delayed) IconTooltip(Func<string, string> localise, CGameState state, COffer offer,
		long shownAt)
	{
		string text = CTextTemplate.Fill(localise("DIPLOICON_TIP"), ("TYPE", localise(KindKey(Kind(offer)))),
			("NATION", Name(state, offer.Actor)), ("DATE", CDiplomacyActions.HourDate(localise, shownAt + IconHours)));
		string delayed = "";
		if (offer.Deal is { } deal)
		{
			delayed = CTextTemplate.Fill(localise("TRADE_TOOL_REQDESC"), ("COUNTRY", Name(state, offer.Actor)),
				("TO", CTradeWindowText.Goods(localise, deal.Terms.To)), ("FROM", CTradeWindowText.Goods(localise, deal.Terms.From)));
		}
		return (text, delayed);
	}

	/// <summary>Whether an unopened icon blinks: from its 120th hour to its 168th.</summary>
	public static bool Blinks(long shownAt, long now) => shownAt + BlinkHours < now && now < shownAt + IconHours;

	/// <summary>
	/// Its alpha while it blinks: a tenth for a tenth of every half second of the screen's clock, else
	/// whole.
	/// </summary>
	public static float BlinkAlpha(double seconds) => (int)(seconds * 10) % 5 == 0 ? 0.1f : 1f;

	/// <summary>Whether an unopened icon has waited long enough to decline its offer.</summary>
	public static bool Lapsed(long shownAt, long now) => now > shownAt + IconHours;

	/// <summary>The dialog's title: the kind's own row title, the proposer's words - "Offer Trade Agreement".</summary>
	public static string Title(Func<string, string> localise, COffer offer) => CDiplomacyActions.Title(localise, offer);

	/// <summary>
	/// The dialog's text, every <c>§W</c> turned to the dialog's black. A trade's is <c>TRADEREQDESC</c> -
	/// what the player gives as TO and gets as FROM - then, where the two need a convoy,
	/// <c>CONV_AT_TRADE</c> naming the buyer, and where the proposer may run a debt with the player and is
	/// at war, a space and <c>IS_BOUGHT_WITH_DEBT</c>. The others' are their kind's key with the
	/// proposer's name.
	/// </summary>
	public static string Description(Func<string, string> localise, CGameState state, COffer offer)
	{
		string proposer = Name(state, offer.Actor);
		string text;
		if (offer.Deal is { } deal && state.Country(offer.Actor) is { } actor && state.Country(offer.Recipient) is { } player)
		{
			text = CTextTemplate.Fill(localise("TRADEREQDESC"), ("COUNTRY", proposer),
				("FROM", CTradeWindowText.Goods(localise, deal.Terms.From)), ("TO", CTradeWindowText.Goods(localise, deal.Terms.To)));
			if (CTradeRules.NeedsConvoy(state, Sim.Supply.CSupplyNetworks.Refresh(state), actor, player))
			{
				CCountryTag buyer = deal.Terms.To(EGoods.Money) != 0 ? deal.To : deal.From;
				text += CTextTemplate.Fill(localise("CONV_AT_TRADE"), ("CONV", Name(state, buyer)));
			}
			if (CGameState.Status(actor, player).AllowDebts && state.IsAtWar(actor.Tag))
			{
				text += " " + localise("IS_BOUGHT_WITH_DEBT");
			}
		}
		else if (offer.Licence is { } licence)
		{
			// Ours: the variables CAskBuyLicence fills were not read; these are what the text asks for.
			text = CTextTemplate.Fill(localise("ASKBUYLICENCEREQDESC"), ("ACTOR", proposer),
				("NUM", ((long)licence.Serial * licence.Parallel).ToString(System.Globalization.CultureInfo.InvariantCulture)),
				("TYPE", localise(licence.UnitType)), ("CASH", CNumberText.Format(new CFixedPoint(licence.Fee), 2)));
		}
		else
		{
			string key = offer.Kind switch
			{
				EOfferKind.Alliance => "ALLIANCEREQDESC",
				EOfferKind.NonAggressionPact => "NAPREQDESC",
				EOfferKind.AskMilitaryAccess => "MILACCREQDESC",
				EOfferKind.Debt => "ASKDEBTREQDESC",
				EOfferKind.RequestLendLease => "ASK_LEND_LEASE_DESC",
				EOfferKind.OfferLendLease => "OFFER_LEND_LEASE_DESC",
				_ => "MILACCOFFDESC",
			};
			text = CTextTemplate.Fill(localise(key), ("ACTOR", proposer), ("COUNTRY", proposer));
		}
		return text.Replace("§W", "§b");
	}

	/// <summary>
	/// Whether an opened dialog stays open: two months from the offer and while it is valid - an alliance a
	/// week, and with neither an alliance nor a war between the two; a NAP with neither; a trade with no
	/// war; asked transit rights on the months alone.
	/// </summary>
	public static bool StaysOpen(CGameState state, COffer offer)
	{
		long now = state.TotalHours;
		bool war = CCountryLists.AtWar(state, offer.Actor, offer.Recipient);
		bool allied = CCountryLists.HasAlliance(state, offer.Actor, offer.Recipient);
		bool inMonths = COfferRules.AddMonths(offer.Date, 2) >= now;
		return offer.Kind switch
		{
			EOfferKind.Alliance => offer.Date + AllianceHours >= now && !allied && !war,
			EOfferKind.NonAggressionPact => inMonths && !allied && !war,
			EOfferKind.AskMilitaryAccess => inMonths,
			EOfferKind.Trade => inMonths && !war,
			_ => inMonths && COfferRules.IsValid(state, offer),
		};
	}

	private static string Name(CGameState state, CCountryTag tag) => state.Country(tag)?.Name ?? tag.Value;
}
