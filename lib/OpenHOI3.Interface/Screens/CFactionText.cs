using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What Join Faction and Invite to Faction say: the row's tooltip, the request dialog's text, and the dialog put
/// to a player asked. See <c>docs/mechanics/factions.md</c>, section 3.
/// </summary>
public static class CFactionText
{
	/// <summary>The rows' key prefix: <c>JOINFACTION</c> or <c>INVITEFACTION</c>.</summary>
	public static string Prefix(COffer offer) => offer.Value ? "JOINFACTION" : "INVITEFACTION";

	/// <summary>
	/// The row's tooltip: the first reason it is greyed - the neutrality's limit the other direction's, and nothing of
	/// its own for a war with a member - else what it does.
	/// </summary>
	public static string Tooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor,
		CCountry recipient, Func<CCountry, CCountry, string?> enRoute)
	{
		CCountry joiner = CFactionOffers.Joiner(offer, actor, recipient);
		CCountry owner = CFactionOffers.Owner(offer, actor, recipient);
		switch (CFactionOffers.Refusal(state, offer, actor, recipient))
		{
			case EFactionRefusal.Self:
				return localise("ACT_NO_SELF");
			case EFactionRefusal.EnRoute:
				return enRoute(actor, recipient) ?? "";
			case EFactionRefusal.Puppet:
				return CTextTemplate.Fill(localise("DIP_THEY_PUPPET"),
					("OVERLORD", state.Country(recipient.Overlord)?.Name ?? recipient.Overlord?.Value ?? ""));
			case EFactionRefusal.Influence:
				return CTextTemplate.Fill(localise("DIP_NO_INFL"),
					("INF", CNumberText.Format(COfferRules.Define(state, CFactionOffers.CostDefine(offer.Value)), 0)));
			case EFactionRefusal.Neutrality:
			{
				// Each direction prints the other's limit.
				string limit = offer.Value ? "INVITE_FACTION_NEUTRALITY" : "JOIN_FACTION_NEUTRALITY";
				return CTextTemplate.Fill(localise(offer.Value ? "DIP_TO_NEUTR_THREAT" : "DIP_THEY_TO_NEUTR_THREAT"),
					("VAL", CNumberText.Format(joiner.EffectiveNeutrality, 2)),
					("LIM", CNumberText.Trimmed(COfferRules.Define(state, limit))));
			}
			case EFactionRefusal.Distance:
			{
				float distance = CFactions.Group(state, owner.Faction) is { } group ? CFactionOffers.Distance(state, joiner, group) : 0f;
				CFixedPoint most = state.Defines[Sim.Diplomacy.CAlignment.Section, CFactionOffers.JoinDistance];
				return CTextTemplate.Fill(localise(offer.Value ? "FACTION_DISTANCE" : "FACTION_DISTANCE_2"),
					("VAL", CNumberText.Format(most, 2)), ("FACTION", localise(owner.Faction)),
					("VALC", distance.ToString("F2", CultureInfo.InvariantCulture)));
			}
			default:
				return localise(offer.Value ? "JOINFACTIONDESC" : "INVITEFACTIONDESC");
		}
	}

	/// <summary>
	/// The request dialog's text: <c>JOINFACTIONNEWDESC</c> or <c>INVITEFACTIONNEWDESC</c>, then the cost line with
	/// the alliance's costs to two places, as the original prints them.
	/// </summary>
	public static string Description(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		string faction = localise(CFactionOffers.Owner(offer, actor, recipient).Faction);
		string text = offer.Value
			? CTextTemplate.Fill(localise("JOINFACTIONNEWDESC"), ("FACTION", faction))
			: CTextTemplate.Fill(localise("INVITEFACTIONNEWDESC"), ("COUNTRY", recipient.Name), ("FACTION", faction));
		CFixedPoint cost = COfferRules.Define(state, offer.Value ? "JOIN_ALLIANCE_INFLUENCE_COST" : "LEAVE_ALLIANCE_INFLUENCE_COST");
		return text + "\n" + CTextTemplate.Fill(localise("DIP_INFLUENCECOST"), ("COST", CNumberText.Format(cost, 2)));
	}

	/// <summary>
	/// The dialog put to a player asked: <c>ASKFACTIONREQDESC</c> for a request to join the player's faction,
	/// <c>INVITEFACTIONREQDESC</c> for an invitation to the proposer's.
	/// </summary>
	public static string AskDescription(Func<string, string> localise, CGameState state, COffer offer)
	{
		string proposer = state.Country(offer.Actor)?.Name ?? offer.Actor.Value;
		string faction = (offer.Value ? state.Country(offer.Recipient) : state.Country(offer.Actor))?.Faction ?? "";
		return CTextTemplate.Fill(localise(offer.Value ? "ASKFACTIONREQDESC" : "INVITEFACTIONREQDESC"),
			("ACTOR", proposer), ("FACTION", localise(faction)));
	}
}
