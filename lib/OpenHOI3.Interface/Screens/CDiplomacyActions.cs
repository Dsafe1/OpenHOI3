using System;
using System.Collections.Generic;
using OpenHOI3.Interface.Messages;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Trade;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The diplomacy screen's actions towards the chosen country: which rows it lists and in what order,
/// which are greyed, each row's text and tooltip, and the request dialog's description and chance.
/// Only the kinds built are listed. See <c>docs/mechanics/agreements.md</c>, section 7.
/// </summary>
public static class CDiplomacyActions
{
	private const string NewLine = "\n";

	/// <summary>
	/// The rows the player may make towards the chosen country, in the original's order: alliance, NAP,
	/// guarantee, the two kinds of transit rights, alignment, embargo, and at peace influence. Each is
	/// an offer proposed now.
	/// </summary>
	public static List<COffer> Rows(CGameState state, CCountry player, CCountry chosen)
	{
		var rows = new List<COffer>();
		COffer Row(EOfferKind kind, bool value) =>
			new(kind, player.Tag, chosen.Tag, EOfferStage.Proposed, value, state.TotalHours);
		bool atWar = CCountryLists.AtWar(state, player.Tag, chosen.Tag);

		// At peace, the declarations; at war, the call to arms (war.md, section 10).
		rows.AddRange(CWarActions.DeclarationRows(state, player, chosen));
		if (CWarActions.CallRow(state, player, chosen) is { } call)
		{
			rows.Add(call);
		}

		// An expeditionary force, where allied or in one real faction: send, and at most one of each way to take back.
		if (CCountryLists.HasAlliance(state, player.Tag, chosen.Tag) || COfferRules.OneRealFaction(player, chosen))
		{
			rows.Add(Row(EOfferKind.Expedition, true));
			if (CExpeditionText.HoldsLentBy(player, chosen))
			{
				rows.Add(Row(EOfferKind.Expedition, false));
			}
			if (CExpeditionText.HoldsLentBy(chosen, player))
			{
				rows.Add(Row(EOfferKind.Expedition, false) with { Recall = true });
			}
		}
		// Technology, where allied or in one real faction: stop where sharing, else share where a theory puts
		// them below the player (aid.md, section 2).
		if (CCountryLists.HasAlliance(state, player.Tag, chosen.Tag) || COfferRules.OneRealFaction(player, chosen))
		{
			if (Sim.Aid.CTechnologySharing.IsSharing(player, chosen))
			{
				rows.Add(Row(EOfferKind.ShareTechnology, false));
			}
			else if (ShareableTheories(state, player, chosen).Count > 0)
			{
				rows.Add(Row(EOfferKind.ShareTechnology, true));
			}
		}
		// At war with them: Peace and Add war goal.
		if (CWarActions.PeaceRow(state, player, chosen) is { } peace)
		{
			rows.Add(peace);
		}
		if (CWarActions.GoalRow(state, player, chosen) is { } goal)
		{
			rows.Add(goal);
		}

		if (CCountryLists.HasAlliance(state, player.Tag, chosen.Tag))
		{
			rows.Add(Row(EOfferKind.Alliance, false));
		}
		else if (!(COfferRules.RealFaction(player) && COfferRules.RealFaction(chosen)) && !COfferRules.IsPuppet(player)
			&& !player.InExile && COfferRules.RecipientFree(player, chosen))
		{
			rows.Add(Row(EOfferKind.Alliance, true));
		}
		rows.Add(Row(EOfferKind.NonAggressionPact, COfferRules.Nap(state, player.Tag, chosen.Tag) is null));
		if (COfferRules.Guarantee(state, player.Tag, chosen.Tag) is not { } guarantee || guarantee.Guarantor == chosen.Tag)
		{
			rows.Add(Row(EOfferKind.Guarantee, true));
		}
		if (!COfferRules.OneRealFaction(player, chosen))
		{
			rows.Add(Row(EOfferKind.AskMilitaryAccess, !COfferRules.HasAccess(player, chosen)));
			rows.Add(Row(EOfferKind.OfferMilitaryAccess, !COfferRules.HasAccess(chosen, player)));
		}
		// Invite where the player leads a real faction and they are in none; join where the reverse (factions.md, section 3).
		if (CFactionOffers.Leads(state, player) && !COfferRules.RealFaction(chosen))
		{
			rows.Add(Row(EOfferKind.Faction, false));
		}
		else if (!COfferRules.RealFaction(player) && CFactionOffers.Leads(state, chosen))
		{
			rows.Add(Row(EOfferKind.Faction, true));
		}
		if (COfferRules.Align(state, player.Tag, chosen.Tag) is not null)
		{
			rows.Add(Row(EOfferKind.Align, false));
		}
		else if (!COfferRules.RealFaction(player) && !atWar && !COfferRules.IsPuppet(player))
		{
			rows.Add(Row(EOfferKind.Align, true));
		}
		if (COfferRules.Embargo(state, player.Tag, chosen.Tag) is not { } embargo)
		{
			rows.Add(Row(EOfferKind.Embargo, true));
		}
		else if (embargo.First == player.Tag)
		{
			rows.Add(Row(EOfferKind.Embargo, false));
		}
		if (!atWar)
		{
			// Buy Production Licence, always (aid.md, section 4).
			rows.Add(Row(EOfferKind.Licence, true));
			// Allow or revoke debt: revoking where they may run a debt with the player (aid.md, section 1).
			if (!player.InExile && !chosen.InExile && !COfferRules.IsPuppet(player))
			{
				rows.Add(Row(EOfferKind.Debt, !CGameState.Status(chosen, player).AllowDebts));
			}
			bool running = Influencing(state, player.Tag, chosen.Tag);
			if (!COfferRules.RealFaction(chosen) && !COfferRules.IsPuppet(chosen) && (running || COfferRules.IsMajor(player)))
			{
				// Listed only where it would do something.
				COffer influence = Row(EOfferKind.Influence, !running);
				if (COfferRules.IsValid(state, influence))
				{
					rows.Add(influence);
				}
			}
			// Offer Trade Agreement, listed only where it would do something (trade.md, section 7).
			COffer trade = Row(EOfferKind.Trade, true) with { Deal = new CTradeDeal(player.Tag, chosen.Tag, CTradeTerms.None) };
			if (COfferRules.IsValid(state, trade))
			{
				rows.Add(trade);
			}
			// Lend-lease, asked for and offered, listed only where valid (aid.md, section 3).
			foreach (COffer lendLease in new[]
			{
				Row(EOfferKind.RequestLendLease, !Sim.Aid.CLendLease.Gives(chosen, player)),
				Row(EOfferKind.OfferLendLease, !Sim.Aid.CLendLease.Gives(player, chosen)),
			})
			{
				if (COfferRules.IsValid(state, lendLease))
				{
					rows.Add(lendLease);
				}
			}
		}
		return rows;
	}

	/// <summary>The theories in which the actor's own level is above the recipient's as it reads it, in the categories' order.</summary>
	public static List<Sim.Research.CTechnologyCategory> ShareableTheories(CGameState state, CCountry actor, CCountry recipient)
	{
		var theories = new List<Sim.Research.CTechnologyCategory>();
		foreach (Sim.Research.CTechnologyCategory category in state.TechnologyCategories)
		{
			if (category.IsTheory && actor.OwnAbility(category.Name) > recipient.Ability(category.Name))
			{
				theories.Add(category);
			}
		}
		return theories;
	}

	/// <summary>Whether a row's button is greyed: it may not be pressed, or a diplomat is on his way.</summary>
	public static bool IsGreyed(CGameState state, COffer offer)
	{
		if (!COfferRules.IsSelectable(state, offer))
		{
			return true;
		}
		// Cancelling a trade, or stopping lend-lease being given, never waits for a diplomat.
		if (!offer.Value && offer.Kind is EOfferKind.Trade or EOfferKind.OfferLendLease)
		{
			return false;
		}
		return state.Country(offer.Actor) is { } actor && state.Country(offer.Recipient) is { } recipient
			&& COfferRules.DiplomatEnRoute(state, actor, recipient);
	}

	/// <summary>The key a row's texts are made from, by its kind and value.</summary>
	public static string Prefix(COffer offer)
	{
		return offer.Kind switch
		{
			EOfferKind.Alliance => offer.Value ? "ALLIANCE" : "CANCELALIANCE",
			EOfferKind.NonAggressionPact => offer.Value ? "NAP" : "CANCELNAP",
			EOfferKind.Guarantee => "GUARANT",
			EOfferKind.AskMilitaryAccess => offer.Value ? "MILACC" : "CANCELMILACC",
			EOfferKind.OfferMilitaryAccess => offer.Value ? "OFFERMILACC" : "REVOKEMILACC",
			EOfferKind.Embargo => offer.Value ? "EMBARGO" : "CANCELEMBARGO",
			EOfferKind.Influence => offer.Value ? "INFL" : "STOPINFL",
			EOfferKind.Trade => offer.Value ? "TRADE" : "CANCELTRADE",
			EOfferKind.Debt => offer.Value ? "ALLOWDEBT" : "REVOKEDEBT",
			EOfferKind.ShareTechnology => offer.Value ? "SHARE_TECH" : "CANCEL_SHARE_TECH",
			EOfferKind.RequestLendLease => offer.Value ? "REQLEND" : "CANCELLEND",
			EOfferKind.OfferLendLease => offer.Value ? "OFFERLL" : "STOPLL",
			EOfferKind.Licence => offer.Value ? "BUYLICENCE" : "SELLLICENCE",
			EOfferKind.Expedition => offer.Value ? "SENDEXPED" : offer.Recall ? "RETRIEVEEXPED" : "RESENDEXPED",
			EOfferKind.DeclareWar or EOfferKind.CallAlly or EOfferKind.AddWarGoal or EOfferKind.Peace => CWarActions.Prefix(offer),
			EOfferKind.Faction => CFactionText.Prefix(offer),
			_ => offer.Value ? "ALIGN" : "STOPALIGN",
		};
	}

	/// <summary>A row's button text and the dialog's title: <c>&lt;prefix&gt;TITLE</c>.</summary>
	public static string Title(Func<string, string> localise, COffer offer) => localise(Prefix(offer) + "TITLE");

	/// <summary>A row's tooltip: the first of the kind's reasons that holds, else what the action does.</summary>
	public static string Tooltip(Func<string, string> localise, CGameState state, COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient)
		{
			return "";
		}
		string? said = offer.Kind switch
		{
			EOfferKind.Alliance => Alliance(localise, state, offer, actor, recipient),
			EOfferKind.NonAggressionPact => Nap(localise, state, offer, actor, recipient),
			EOfferKind.Guarantee => Guarantee(localise, state, offer, actor, recipient),
			EOfferKind.AskMilitaryAccess => AskAccess(localise, state, offer, actor, recipient),
			EOfferKind.OfferMilitaryAccess => OfferAccess(localise, state, offer, actor, recipient),
			EOfferKind.Embargo => Embargo(localise, state, offer, actor, recipient),
			EOfferKind.Influence => Influence(localise, state, offer, actor, recipient),
			EOfferKind.Trade => TradeTooltip(localise, state, offer, actor, recipient),
			EOfferKind.Debt => DebtTooltip(localise, state, offer, actor, recipient),
			EOfferKind.ShareTechnology => ShareTooltip(localise, state, offer, actor, recipient),
			EOfferKind.RequestLendLease => RequestLendLeaseTooltip(localise, state, offer, actor, recipient),
			EOfferKind.OfferLendLease => OfferLendLeaseTooltip(localise, state, offer, actor, recipient),
			EOfferKind.Licence => LicenceTooltip(localise, state, offer, actor, recipient),
			EOfferKind.Expedition => ExpeditionTooltip(localise, state, offer, actor, recipient),
			EOfferKind.Faction => CFactionText.Tooltip(localise, state, offer, actor, recipient, (a, r) => EnRoute(localise, state, a, r)),
			EOfferKind.DeclareWar or EOfferKind.CallAlly or EOfferKind.AddWarGoal or EOfferKind.Peace =>
				CWarActions.Tooltip(localise, state, offer, actor, recipient, (a, r) => EnRoute(localise, state, a, r)),
			_ => Align(localise, state, offer, actor, recipient),
		};
		return said ?? "";
	}

	/// <summary>
	/// A licence's row: itself, a diplomat, too little influence to buy - whole - then what it does. There is
	/// no arm for war, though war greys it (aid.md, section 5).
	/// </summary>
	private static string LicenceTooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		if (!offer.Value)
		{
			return localise("SELLLICENCEDESC");
		}
		return NoInfluence(localise, state, offer, actor, 0) ?? localise("BUYLICENCEDESC");
	}

	/// <summary>
	/// Asking for lend-lease: a diplomat, or why it may not be - alone - or <c>LEND_LEASE_DESC</c> and the
	/// way it would come; cancelling it, <c>LEND_LEASE_CANCEL_DESC</c> where they give, else nothing. See
	/// <c>aid.md</c>, section 6.
	/// </summary>
	private static string RequestLendLeaseTooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor,
		CCountry recipient)
	{
		if (!offer.Value)
		{
			return Sim.Aid.CLendLease.Gives(recipient, actor) ? localise("LEND_LEASE_CANCEL_DESC") : "";
		}
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		return LendLeaseWay(localise, state, giver: recipient, receiver: actor, giverIsActor: false, out bool may) is var way && may
			? localise("LEND_LEASE_DESC") + NewLine + way
			: way;
	}

	/// <summary>
	/// Offering lend-lease: a diplomat; nothing where already giving; else <c>OFFERLL_DESCRIPTION</c> and the
	/// way it would go, or why it may not - prefixed either way. Stopping, <c>STOPLL_DESCRIPTION</c>.
	/// </summary>
	private static string OfferLendLeaseTooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor,
		CCountry recipient)
	{
		if (!offer.Value)
		{
			return localise("STOPLL_DESCRIPTION");
		}
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		if (Sim.Aid.CLendLease.Gives(actor, recipient))
		{
			return "";
		}
		return localise("OFFERLL_DESCRIPTION") + NewLine
			+ LendLeaseWay(localise, state, giver: actor, receiver: recipient, giverIsActor: true, out _);
	}

	/// <summary>
	/// The way lend-lease would go, or why it may not, in the words of the side that asks: our neutrality or
	/// theirs, we or they at war.
	/// </summary>
	private static string LendLeaseWay(Func<string, string> localise, CGameState state, CCountry giver, CCountry receiver,
		bool giverIsActor, out bool may)
	{
		may = false;
		switch (Sim.Aid.CLendLease.Refusal(state, giver, receiver))
		{
			case Sim.Aid.CLendLease.ERefusal.InExile:
				return CTextTemplate.Fill(localise("ACT_LENDLEASE_ANY_GOI"), ("ACTOR", giver.Name), ("RECIPIENT", receiver.Name));
			case Sim.Aid.CLendLease.ERefusal.GiverNeutrality:
				return CTextTemplate.Fill(localise(giverIsActor ? "ACT_LENDLEASE_OUR_NEUTRALITY" : "ACT_LENDLEASE_NEUTRALITY"),
					("LIMIT", CNumberText.Trimmed(COfferRules.Define(state, Sim.Aid.CLendLease.NeutralityLimitDefine))),
					("VAL", CNumberText.Format(giver.EffectiveNeutrality, 2)));
			case Sim.Aid.CLendLease.ERefusal.ReceiverAtPeace:
				return localise(giverIsActor ? "ACT_LENDLEASE_THEM_ONLY_AT_WAR" : "ACT_LENDLEASE_ONLY_AT_WAR");
			case Sim.Aid.CLendLease.ERefusal.AtWarWithEachOther:
				return localise("WEWAR");
			case Sim.Aid.CLendLease.ERefusal.NoWay:
				return localise("LEND_LEASE_NOT_ALLOWED");
		}
		may = true;
		return Sim.Aid.CLendLease.Way(state, giver, receiver) switch
		{
			Sim.Aid.ELendLeaseRoute.Neighbour => localise("LEND_LEASE_ALLOWED_NEIGHBOUR"),
			Sim.Aid.ELendLeaseRoute.Teleport => localise("LEND_LEASE_ALLOWED_TELEPORT"),
			_ => CTextTemplate.Fill(localise("LEND_LEASE_ALLOWED_CONVOY"), ("COUNTRY", receiver.Name)),
		};
	}

	/// <summary>
	/// Sharing technology's row: itself, a diplomat, then for sharing too little influence - its cost whole -
	/// or what it does; for stopping what it does. See <c>aid.md</c>, section 2.
	/// </summary>
	private static string ShareTooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		if (!offer.Value)
		{
			return localise("CANCEL_SHARE_TECH_DESC");
		}
		return NoInfluence(localise, state, offer, actor, 0) ?? localise("SHARE_TECH_DESC");
	}

	/// <summary>Debt's row: itself, war, a diplomat, too little influence (whole), then what it does - aid.md, section 1.</summary>
	private static string DebtTooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		return EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 0)
			?? localise(offer.Value ? "ALLOWDEBTDESC" : "REVOKEDEBTDESC");
	}

	private static string? TradeTooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((Common(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 0)) is { } said)
		{
			return said;
		}
		if (!offer.Value)
		{
			return localise("CANCELTRADEDESC");
		}
		if (COfferRules.Embargo(state, actor.Tag, recipient.Tag) is not null)
		{
			return CTextTemplate.Fill(localise("CDIP_EMBARGO"), ("LIST", recipient.Name));
		}
		if (CTradeRules.NeedsConvoy(state, Sim.Supply.CSupplyNetworks.Refresh(state), actor, recipient)
			&& (!CTradeRules.HasPort(state, actor) || !CTradeRules.HasPort(state, recipient)))
		{
			return localise("TRADENO_PORTS");
		}
		return localise("TRADEDESC");
	}

	/// <summary>The request dialog's description: what the action is, and what it costs where it does.</summary>
	public static string Description(Func<string, string> localise, CGameState state, COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient)
		{
			return "";
		}
		string Country(string key) => CTextTemplate.Fill(localise(key), ("COUNTRY", recipient.Name));
		string Recipient(string key) => CTextTemplate.Fill(localise(key), ("RECIPIENT", recipient.Name));
		string Cost() => CTextTemplate.Fill(localise("DIP_INFLUENCECOST"),
			("COST", CNumberText.Format(COfferRules.Cost(state, offer), 2)));
		switch (offer.Kind)
		{
			case EOfferKind.Alliance:
				return Country(offer.Value ? "ALLIANCENEWDESC" : "BREAKALLIANCENEWDESC") + NewLine + Cost();
			case EOfferKind.NonAggressionPact:
				if (offer.Value)
				{
					return Country("NAPNEWDESC") + NewLine + Cost();
				}
				string threat = CTextTemplate.Fill(localise("DIP_THREAT"),
					("AMOUNT", CNumberText.Format(COfferRules.Define(state, "LEAVE_NAP_THREAT_COST"), 2)),
					("PROV", state.Province(recipient.Capital).Name));
				return Country("BREAKNAPNEWDESC") + NewLine + threat + NewLine + Cost();
			case EOfferKind.Guarantee:
				// ALLIES_GUARANT follows where the actor's faction makes its members guarantee too (factions.md, section 5).
				return Country("GUARANTNEWDESC")
					+ (CFactions.HasRule(state, actor, CFactions.GuaranteeRule) ? NewLine + localise("ALLIES_GUARANT") : "")
					+ NewLine + Cost();
			case EOfferKind.AskMilitaryAccess:
				return Country(offer.Value ? "MILACCNEWDESC" : "CANCELMILACCNEWDESC");
			case EOfferKind.OfferMilitaryAccess:
				return Country(offer.Value ? "OFFERMILACCNEWDESC" : "REVOKEMILACCNEWDESC");
			case EOfferKind.Embargo:
				return offer.Value ? Recipient("EMBARGONEWDESC") + NewLine + Cost() : Recipient("CANCELEMBARGONEWDESC");
			case EOfferKind.Influence:
				return offer.Value ? Drift(localise, state, "INFLNEWDESC", recipient, actor.Faction) : Recipient("STOPINFLNEWDESC");
			case EOfferKind.Debt:
			{
				// Allowing names the recipient's faction, not the recipient (aid.md, section 5).
				string cost = CNumberText.Format(COfferRules.Cost(state, offer), 0);
				return offer.Value
					? CTextTemplate.Fill(localise("ALLOWDEBTNEWDESC"), ("NAME", localise(recipient.Faction)),
						("YEAR", CNumberText.Format(COfferRules.Define(state, Sim.Aid.CDebts.YearsDefine), 0)), ("COST", cost))
					: CTextTemplate.Fill(localise("REVOKEDEBTNEWDESC"), ("NAME", recipient.Name), ("COST", cost));
			}
			case EOfferKind.RequestLendLease:
				return localise(offer.Value ? "LEND_LEASE_DESCRIPTION" : "LEND_LEASE_BREAK_DESCRIPTION");
			case EOfferKind.OfferLendLease:
				return localise(offer.Value ? "OFFERLL_DESCRIPTION" : "STOPLL_DESCRIPTION");
			case EOfferKind.CallAlly:
				return CWarActions.CallDescription(localise, state, offer, recipient);
			case EOfferKind.Faction:
				return CFactionText.Description(localise, state, offer, actor, recipient);
			case EOfferKind.Peace:
				return CPeaceText.Description(localise, state, actor, recipient);
			case EOfferKind.ShareTechnology:
			{
				// Stopping only; sharing has a dialog of its own.
				string first = Sim.Aid.CTechnologySharing.FirstShared(actor, recipient);
				return first.Length > 0
					? CTextTemplate.Fill(localise("CANCEL_SHARE_TECH_INFO"), ("TECH", localise(first)), ("COUNTRY", recipient.Name))
					: Country("CANCEL_SHARE_TECH_INFO_NO");
			}
			default:
				return offer.Value ? Drift(localise, state, "ALIGNNEWDESC", recipient, recipient.Faction) : Recipient("STOPALIGNNEWDESC");
		}
	}

	/// <summary>
	/// The dialog's word for the recipient's AI's likely answer, from its score; nothing where the AI does
	/// not answer the kind.
	/// </summary>
	public static string Chance(Func<string, string> localise, CGameState state, COffer offer)
	{
		if (!COfferRules.AiAnswers(offer))
		{
			return "";
		}
		// The AI's answer to a peace rolls the game's generator; the original's dialog rolls it too, this one puts
		// it back, so that what a screen shows never moves a game's rolls (war.md, section 11).
		var saved = state.Random.Snapshot();
		int score = CPactPasses.Score(state, offer);
		state.Random.Restore(saved);
		string key = score switch
		{
			< 1 => "DIPCHANCE_IMPOSSIBLE",
			< 10 => "DIPCHANCE_VUNLIKELY",
			< 40 => "DIPCHANCE_UNLIKELY",
			< 60 => "DIPCHANCE_MAYBE",
			< 80 => "DIPCHANCE_LIKELY",
			_ => "DIPCHANCE_VLIKELY",
		};
		return localise(key);
	}

	private static string? Alliance(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (Common(localise, state, actor, recipient) is { } common)
		{
			return common;
		}
		if (offer.Value)
		{
			CFixedPoint limit = COfferRules.Define(state, "ALLIANCE_NEUTRALITY_LIMIT");
			if (actor.EffectiveNeutrality > limit)
			{
				return Neutrality(localise, "DIP_TO_NEUTR_THREAT", actor, limit);
			}
			if (recipient.EffectiveNeutrality > limit)
			{
				return Neutrality(localise, "DIP_THEY_TO_NEUTR_THREAT", recipient, limit);
			}
		}
		return NoInfluence(localise, state, offer, actor, 0)
			?? localise(offer.Value ? "OFFERALLIANCEDESC" : "CANCELALLIANCEDESC");
	}

	private static string? Nap(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((Common(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 0)) is { } said)
		{
			return said;
		}
		if (offer.Value)
		{
			return localise("NAPEDESC");
		}
		if (COfferRules.Nap(state, actor.Tag, recipient.Tag) is { } nap)
		{
			int unbreakable = (int)(COfferRules.Define(state, "NAP_UNBREAKABLE_MONTHS").Raw / 1000);
			int window = (int)(COfferRules.Define(state, "NAP_FORCE_BALANCE_RULE_MONTHS").Raw / 1000);
			if (state.TotalHours < COfferRules.AddMonths(nap.Start, unbreakable))
			{
				return CTextTemplate.Fill(localise("CANCELNAP_TOO_SOON_DESC"),
					("MONTHS", unbreakable.ToString(System.Globalization.CultureInfo.InvariantCulture)));
			}
			for (int step = 1; step <= 3; step++)
			{
				if (state.TotalHours >= COfferRules.AddMonths(nap.Start, unbreakable + step * window))
				{
					continue;
				}
				// The balance of brigades along the border is not built, so it is nought - below any limit.
				CFixedPoint limit = COfferRules.Define(state, "NAP_BREAK_FORCE_BALANCE_" + step);
				return CTextTemplate.Fill(localise("CANCELNAP_TOO_LOW_BALANCE"),
					("LIMIT_PERC", CNumberText.Trimmed(limit * 100)),
					("CURR_PERC", CNumberText.Trimmed(CFixedPoint.Zero)));
			}
		}
		return localise("CANCELNAPDESC");
	}

	private static string? Guarantee(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)) is { } said)
		{
			return said;
		}
		if (COfferRules.Guarantee(state, actor.Tag, recipient.Tag) is { } standing)
		{
			return localise(standing.Guarantor != recipient.Tag ? "HASGUARANT" : "THEYGUARANT");
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		CFixedPoint limit = COfferRules.Define(state, "GUARANTEE_NEUTRALITY_LIMIT");
		if (actor.EffectiveNeutrality > limit)
		{
			return CTextTemplate.Fill(localise("DIP_TO_NEUTR"),
				("NOW", CNumberText.Format(actor.EffectiveNeutrality, 2)), ("LIM", CNumberText.Trimmed(limit)));
		}
		return localise("GUARANTDESC");
	}

	private static string? AskAccess(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)) is { } said)
		{
			return said;
		}
		if (!offer.Value && COfferRules.HasAccess(actor, recipient))
		{
			return localise(COfferRules.HasUnitsIn(state, actor, recipient) ? "CANCELMILACCDESC_NOPES_TROOPS" : "CANCELMILACCDESC");
		}
		return localise(CCountryLists.AtWar(state, actor.Tag, recipient.Tag) ? "ISATWAR" : "MILACCDESC");
	}

	private static string? OfferAccess(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		// Revoking says its own title.
		return EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)
			?? localise(offer.Value ? "OFFERMILACCDESC" : "REVOKEMILACCTITLE");
	}

	private static string? Embargo(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (Common(localise, state, actor, recipient) is { } common)
		{
			return common;
		}
		if (!offer.Value)
		{
			return localise("CANCELEMBARGODESC");
		}
		if (NoInfluence(localise, state, offer, actor, 0) is { } lacking)
		{
			return lacking;
		}
		CFixedPoint limit = COfferRules.Define(state, "EMBARGO_NETRALITY");
		return actor.EffectiveNeutrality > limit
			? Neutrality(localise, "DIP_TO_NEUTR_THREAT", actor, limit)
			: localise("EMBARGODESC");
	}

	private static string? Influence(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		if (!offer.Value)
		{
			return localise("STOPNFLDESC");
		}
		if (NoInfluence(localise, state, offer, actor, 2) is { } said)
		{
			return said;
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		if (COfferRules.AtWarWithFaction(state, recipient, actor.Faction))
		{
			return localise("THEY_AT_WAR_WITH_FACTION");
		}
		if (COfferRules.HasInfluenceModifier(state, recipient, actor.Faction))
		{
			return Already(localise, state, actor, recipient);
		}
		return localise("INFLDESC");
	}

	/// <summary>
	/// "Already being influenced by": the last member of the actor's faction influencing the recipient, or
	/// aligning with it either way, with the day its influence ends; <c>ERROR</c>, which has no text, where none is.
	/// </summary>
	private static string Already(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		CCountry? nation = null;
		long ends = 0;
		long days = COfferRules.Define(state, "DAYS_OF_INFLUENCE_RELATION_CHANGE").Raw / 1000;
		foreach (CCountry member in CCountryLists.FactionMembers(state, actor.Faction))
		{
			CAlign? align = COfferRules.Align(state, recipient.Tag, member.Tag) ?? COfferRules.Align(state, member.Tag, recipient.Tag);
			if (state.Influences.Find(i => i.Influencer == member.Tag && i.Influenced == recipient.Tag) is { } influence)
			{
				nation = member;
				ends = influence.Start + days * CGameState.HoursPerDay;
			}
			else if (align is not null)
			{
				nation = member;
				ends = AlignEnds(state, align);
			}
		}
		if (nation is null)
		{
			return localise("ERROR");
		}
		return CTextTemplate.Fill(localise("DIP_ALLREADY_INF"), ("RECIPIENT", recipient.Name), ("NATION", nation.Name),
			("DATE", HourDate(localise, ends)));
	}

	private static string? Align(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if ((EnRoute(localise, state, actor, recipient) ?? NoInfluence(localise, state, offer, actor, 2)) is { } said)
		{
			return said;
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		if (!COfferRules.RealFaction(recipient))
		{
			return localise("ALIGN_NO_FACTION");
		}
		string faction = localise(recipient.Faction);
		if (offer.Value && COfferRules.Leader(state, recipient.Faction) is { } leader && leader != recipient.Tag)
		{
			return CTextTemplate.Fill(localise("ALIGN_NOT_LEADER"), ("FACTION", faction),
				("LEADER", state.Country(leader)?.Name ?? leader.Value));
		}
		if (offer.Value && COfferRules.Align(state, actor.Tag, recipient.Tag) is { } running)
		{
			return CTextTemplate.Fill(localise("ALIGN_ALLREADY_ALL"), ("FACTION", faction),
				("DATE", HourDate(localise, AlignEnds(state, running))));
		}
		// Stopping says what starting does.
		return localise(COfferRules.AtWarWithFaction(state, actor, recipient.Faction) ? "AT_WAR_WITH_FACTION" : "ALIGNDESC");
	}

	/// <summary>
	/// What every agreement but the guarantee, the transit rights, influence and alignment tests first:
	/// itself, a diplomat on his way, war between them, the actor a puppet.
	/// </summary>
	private static string? Common(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (EnRoute(localise, state, actor, recipient) is { } enRoute)
		{
			return enRoute;
		}
		if (CCountryLists.AtWar(state, actor.Tag, recipient.Tag))
		{
			return localise("WEWAR");
		}
		return COfferRules.IsPuppet(actor) ? localise("SUBJECT") : null;
	}

	/// <summary>A diplomat on his way, with the hour he arrives.</summary>
	private static string? EnRoute(Func<string, string> localise, CGameState state, CCountry actor, CCountry recipient)
	{
		if (!COfferRules.DiplomatEnRoute(state, actor, recipient))
		{
			return null;
		}
		return CTextTemplate.Fill(localise("ENROUTE"),
			("DATE", HourDate(localise, CGameState.Status(actor, recipient).DiplomatUntil)));
	}

	/// <summary>Too little influence for the offer, its cost to as many places as the kind writes it.</summary>
	private static string? NoInfluence(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, int decimals)
	{
		CFixedPoint cost = COfferRules.Cost(state, offer);
		if (actor.DiplomaticInfluence >= cost)
		{
			return null;
		}
		return CTextTemplate.Fill(localise("DIP_NO_INFL"), ("INF", CNumberText.Format(cost, decimals)));
	}

	private static string Neutrality(Func<string, string> localise, string key, CCountry country, CFixedPoint limit)
	{
		return CTextTemplate.Fill(localise(key), ("VAL", CNumberText.Format(country.EffectiveNeutrality, 2)),
			("LIM", CNumberText.Trimmed(limit)));
	}

	/// <summary>
	/// An influence's or an alignment's dialog: how far the faction's influence modifier moves the recipient
	/// each day, and for how many days - the influence's define for both.
	/// </summary>
	private static string Drift(Func<string, string> localise, CGameState state, string key, CCountry recipient, string faction)
	{
		long days = COfferRules.Define(state, "DAYS_OF_INFLUENCE_RELATION_CHANGE").Raw / 1000;
		string time = days < 0 ? localise("UNLIMITED") : days.ToString(System.Globalization.CultureInfo.InvariantCulture);
		CFixedPoint value = state.FindStaticModifier(COfferRules.InfluenceModifier(state, faction)) is { } modifier
			? modifier[CAlignment.AlignTowards]
			: CFixedPoint.Zero;
		return CTextTemplate.Fill(localise(key), ("RECIPIENT", recipient.Name), ("TIME", time),
			("VAL", CNumberText.Format(value, 2)), ("FACTION", localise(faction)));
	}

	/// <summary>The hour an alignment's days run out.</summary>
	private static long AlignEnds(CGameState state, CAlign align)
	{
		long days = COfferRules.Define(state, "DAYS_OF_ALIGN_RELATION_CHANGE").Raw / 1000;
		return align.Start + days * CGameState.HoursPerDay;
	}

	private static bool Influencing(CGameState state, CCountryTag influencer, CCountryTag influenced) =>
		state.Influences.Exists(i => i.Influencer == influencer && i.Influenced == influenced);

	/// <summary>An hour as the original writes it: " 13:00, 1 January, 1936", a space first.</summary>
	private static string ExpeditionTooltip(Func<string, string> localise, CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (actor == recipient)
		{
			return localise("ACT_NO_SELF");
		}
		if (offer.Value)
		{
			if (COfferRules.DiplomatEnRoute(state, actor, recipient))
			{
				return EnRoute(localise, state, actor, recipient);
			}
			CFixedPoint cost = COfferRules.Define(state, CExpeditions.CostDefine);
			return actor.DiplomaticInfluence < cost
				? Text.CTextTemplate.Fill(localise("DIP_NO_INFL"), ("INF", Text.CNumberText.Format(cost, 0)))
				: localise("SENDEXPEDDESC");
		}
		return localise(offer.Recall ? "RETRIEVEEXPEDDESC" : "RESENDEXPEDDESC");
	}

	public static string HourDateOf(Func<string, string> localise, long hour) => HourDate(localise, hour);

	internal static string HourDate(Func<string, string> localise, long hour)
	{
		long day = Math.Max(hour, 0) / CGameState.HoursPerDay;
		return CMessageText.Date(localise, CDate.FromDayNumber((int)day), (int)(Math.Max(hour, 0) % CGameState.HoursPerDay));
	}
}
