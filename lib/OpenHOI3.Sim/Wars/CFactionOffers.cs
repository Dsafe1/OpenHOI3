using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// Join Faction and Invite to Faction, one offer whose value is the direction: set, the actor asks to join the
/// recipient's faction; clear, the actor asks the recipient to join its own. The country that moves is the joiner,
/// the other the owner. See <c>docs/mechanics/factions.md</c>, section 3.
/// </summary>
public static class CFactionOffers
{
	/// <summary>The define a faction's join distance is read from, in the alignment block.</summary>
	public const string JoinDistance = "FACTION_JOIN_DIST";

	public static CCountry Joiner(COffer offer, CCountry actor, CCountry recipient) => offer.Value ? actor : recipient;

	public static CCountry Owner(COffer offer, CCountry actor, CCountry recipient) => offer.Value ? recipient : actor;

	/// <summary>The influence the offer costs its actor, and is charged on acceptance: the faction's, nothing in Their Finest Hour.</summary>
	public static string CostDefine(bool join) => join ? "JOIN_FACTION_INFLUENCE_COST" : "INVITE_FACTION_INFLUENCE_COST";

	/// <summary>Whether a country leads a real faction: it is its first member.</summary>
	public static bool Leads(CGameState state, CCountry country) =>
		COfferRules.RealFaction(country) && COfferRules.Leader(state, country.Faction) == country.Tag;

	/// <summary>
	/// Valid: neither in exile; the owner leads a real faction and the joiner is in none. Asked when the offer is
	/// made, at the answer, and while the player's dialog is open.
	/// </summary>
	public static bool IsValid(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		CCountry joiner = Joiner(offer, actor, recipient);
		return !actor.InExile && !recipient.InExile && Leads(state, Owner(offer, actor, recipient)) && !COfferRules.RealFaction(joiner);
	}

	/// <summary>The first reason the row is greyed, in the original's order, or none.</summary>
	public static EFactionRefusal Refusal(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (actor == recipient)
		{
			return EFactionRefusal.Self;
		}
		if (COfferRules.DiplomatEnRoute(state, actor, recipient))
		{
			return EFactionRefusal.EnRoute;
		}
		if (COfferRules.IsPuppet(recipient))
		{
			return EFactionRefusal.Puppet;
		}
		if (actor.DiplomaticInfluence < COfferRules.Define(state, CostDefine(offer.Value)))
		{
			return EFactionRefusal.Influence;
		}
		CCountry joiner = Joiner(offer, actor, recipient);
		CCountry owner = Owner(offer, actor, recipient);
		string limit = offer.Value ? "JOIN_FACTION_NEUTRALITY" : "INVITE_FACTION_NEUTRALITY";
		if (joiner.EffectiveNeutrality > COfferRules.Define(state, limit))
		{
			return EFactionRefusal.Neutrality;
		}
		if (CFactions.Group(state, owner.Faction) is not { } group || !CFactions.Within(state, joiner, group, JoinDistance))
		{
			return EFactionRefusal.Distance;
		}
		if (state.IsAtWar(joiner.Tag) && COfferRules.AtWarWithFaction(state, joiner, owner.Faction))
		{
			return EFactionRefusal.War;
		}
		return EFactionRefusal.None;
	}

	/// <summary>The joiner's place on the triangle from the owner's faction's corner, as the tooltip writes it: in single precision.</summary>
	public static float Distance(CGameState state, CCountry joiner, CIdeologyGroup group)
	{
		float squared = CAlignment.Squared(group.X - joiner.AlignmentX, group.Y - joiner.AlignmentY);
		return (float)System.Math.Sqrt((float)(squared / 1000.0f));
	}

	/// <summary>
	/// The offer applied, for a proposal and an answer alike. Nothing at all where the joiner is at war with a member of
	/// the owner's faction. A proposal does nothing more. Accepted: the actor pays, the joiner comes in with its wars and
	/// so does every country whose overlord it is, the joiner's alliances with the members end, and the players are
	/// told. Declined: the actor is told, where it is played.
	/// </summary>
	public static void Apply(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		CCountry joiner = Joiner(offer, actor, recipient);
		string faction = Owner(offer, actor, recipient).Faction;
		if (state.IsAtWar(joiner.Tag) && COfferRules.AtWarWithFaction(state, joiner, faction))
		{
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Accepted:
				Accept(state, offer, actor, recipient, joiner, faction);
				break;
			case EOfferStage.Declined:
				if (state.Played.Contains(actor.Tag))
				{
					string comment = "FACTIONCOMMENTBAD_" + faction;
					state.RaisePactMessage(new CPactMessage(offer.Value ? "FACTIONJOINDECLINCE" : "FACTIONINVITEDECLINCE", "", "",
						actor.Tag, recipient.Tag)
					{
						Told = actor.Tag,
						Values = new[] { ("FACTION", faction), ("BAD_COMMENT", comment), ("COMMENT_BAD", comment) },
					});
				}
				break;
		}
	}

	private static void Accept(CGameState state, COffer offer, CCountry actor, CCountry recipient, CCountry joiner, string faction)
	{
		CFixedPoint left = actor.DiplomaticInfluence - COfferRules.Define(state, CostDefine(offer.Value));
		actor.DiplomaticInfluence = left < CFixedPoint.Zero ? CFixedPoint.Zero : left;
		CFactions.AddCountry(state, joiner, faction, joinWars: true);
		foreach (CCountry subject in state.Countries)
		{
			if (subject.Overlord == joiner.Tag)
			{
				CFactions.AddCountry(state, subject, faction, joinWars: true);
			}
		}
		List<CCountry> members = CCountryLists.FactionMembers(state, faction);
		foreach (CCountry member in members)
		{
			state.Alliances.RemoveAll(alliance => alliance.Joins(joiner.Tag, member.Tag));
		}
		var values = new[] { ("JOINER", joiner.Name), ("FACTION", faction), ("COMMENT", "FACTIONCOMMENT_" + faction) };
		foreach (CCountryTag played in state.Played)
		{
			string key = !offer.Value && played == actor.Tag ? "FACTIONINVITEACCEPT"
				: offer.Value && members.Exists(member => member.Tag == played) ? "FACTIONJOINACCEPT"
				: played != recipient.Tag ? "FACTIONJOINACCEPTOTHER" : "";
			if (key.Length > 0)
			{
				state.RaisePactMessage(new CPactMessage(key, "", "", actor.Tag, recipient.Tag) { Told = played, Values = values });
			}
		}
	}
}
