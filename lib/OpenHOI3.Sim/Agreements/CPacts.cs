using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// What an offer does when its command runs, for its kind, stage and value, arm by arm as the original
/// has them, and the ending of each pact. See <c>docs/mechanics/agreements.md</c>, section 5.
/// </summary>
public static class CPacts
{
	/// <summary>What dissolving an alliance costs in relation after its define: a constant.</summary>
	private static readonly CFixedPoint AllianceLeftOver = CFixedPoint.FromInt(-30);

	public static void Apply(CGameState state, COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient)
		{
			return;
		}
		switch (offer.Kind)
		{
			case EOfferKind.Alliance:
				Alliance(state, offer, actor, recipient);
				break;
			case EOfferKind.NonAggressionPact:
				Nap(state, offer, actor, recipient);
				break;
			case EOfferKind.Guarantee:
				Guarantee(state, offer, actor, recipient);
				break;
			case EOfferKind.AskMilitaryAccess:
				AskAccess(state, offer, actor, recipient);
				break;
			case EOfferKind.OfferMilitaryAccess:
				OfferAccess(state, offer, actor, recipient);
				break;
			case EOfferKind.Embargo:
				Embargo(state, offer, actor, recipient);
				break;
			case EOfferKind.Influence:
				Influence(state, offer, actor, recipient);
				break;
			case EOfferKind.Align:
				Align(state, offer, actor, recipient);
				break;
			case EOfferKind.Trade:
				TradeOffer(state, offer, actor, recipient);
				break;
			case EOfferKind.Debt:
				Debt(state, offer, actor, recipient);
				break;
			case EOfferKind.ShareTechnology:
				ShareTechnology(state, offer, actor, recipient);
				break;
			case EOfferKind.RequestLendLease:
				RequestLendLease(state, offer, actor, recipient);
				break;
			case EOfferKind.OfferLendLease:
				OfferLendLease(state, offer, actor, recipient);
				break;
			case EOfferKind.Licence:
				Licence(state, offer, actor, recipient);
				break;
		}
	}

	/// <summary>
	/// Buying a licence - the actor buys, the recipient sells - and its answer. An acceptance the buyer can
	/// pay neither in cash nor on debt becomes a decline; otherwise the buyer's runs are ordered at the
	/// seller's levels, a land type's as reserves, and the fee goes on its debt or from its money to the
	/// seller's. Only the buyer is told. See <c>aid.md</c>, section 4.
	/// </summary>
	private static void Licence(CGameState state, COffer offer, CCountry buyer, CCountry seller)
	{
		if (offer.Stage == EOfferStage.Proposed)
		{
			if (offer.Value)
			{
				Charge(state, buyer, "LICENCE_INFLUENCE_COST");
			}
			return;
		}
		Aid.CLicenceTerms? terms = offer.Licence;
		bool onDebt = CGameState.Status(buyer, seller).AllowDebts && state.IsAtWar(buyer.Tag);
		Economy.CGoodsPool? pool = state.Stockpile(buyer);
		bool accepted = offer.Stage == EOfferStage.Accepted && terms is not null
			&& (onDebt || (pool is not null && pool[Economy.EGoods.Money].Raw >= terms.Fee));
		string name = terms?.UnitType ?? "";
		string count = ((long)(terms?.Serial ?? 0) * (terms?.Parallel ?? 0)).ToString(System.Globalization.CultureInfo.InvariantCulture);
		if (!accepted || terms is null)
		{
			state.RaisePactMessage(new CPactMessage("LICBUYDECLINE", "", "", buyer.Tag, seller.Tag)
			{
				Values = new[] { ("DECLINED", "DECLINED"), ("NAME", name), ("NUM", count) },
			});
			return;
		}
		if (state.SubUnitTypes.TryGetValue(terms.UnitType, out Units.CSubUnitType? type))
		{
			for (int run = 0; run < terms.Parallel; run++)
			{
				state.Outbox.Add(new Commands.COrderUnitCommand(buyer.Tag, new[] { (type.Key, "") }, size: terms.Serial,
					isReserve: type.Branch == Units.EUnitBranch.Land, builder: seller.Tag));
			}
		}
		var fee = new CFixedPoint(terms.Fee);
		if (onDebt)
		{
			CGameState.Status(buyer, seller).Debt += terms.Fee;
		}
		else if (pool is not null)
		{
			pool[Economy.EGoods.Money] -= fee;
			if (state.Stockpile(seller) is { } theirs)
			{
				theirs[Economy.EGoods.Money] += fee;
			}
		}
		state.RaisePactMessage(new CPactMessage("LICBUYACCEPT", "", "", buyer.Tag, seller.Tag)
		{
			Values = new[] { ("ACCEPTED", "ACCEPTED"), ("NAME", name), ("NUM", count) },
		});
	}

	/// <summary>
	/// Asking for lend-lease - the recipient would give - its answer, or cancelling it; asking costs the debt's
	/// define. See <c>aid.md</c>, section 3.
	/// </summary>
	private static void RequestLendLease(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			if (offer.Stage == EOfferStage.Proposed)
			{
				Aid.CLendLease.Remove(recipient, actor);
				ToldTurnedRound(state, actor, recipient, "LENDOFFERREFUSED");
			}
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				Charge(state, actor, "ALLOW_DEBT_INFLUENCE_COST");
				break;
			case EOfferStage.Accepted:
				Aid.CLendLease.Add(recipient, actor);
				Message(state, "LENDACCEPT", "", "", actor, recipient);
				break;
			default:
				Message(state, "LENDDECLINE", "", "", actor, recipient);
				break;
		}
	}

	/// <summary>Offering lend-lease - free - its answer, or stopping it. See <c>aid.md</c>, section 3.</summary>
	private static void OfferLendLease(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			if (offer.Stage == EOfferStage.Proposed)
			{
				Aid.CLendLease.Remove(actor, recipient);
				ToldTurnedRound(state, actor, recipient, "LENDREVOKE");
			}
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Accepted:
				Aid.CLendLease.Add(actor, recipient);
				Message(state, "LENDOFFERACCEPT", "", "", actor, recipient);
				break;
			case EOfferStage.Declined:
				Message(state, "LENDOFFERREFUSED", "", "", actor, recipient);
				break;
		}
	}

	/// <summary>
	/// Sharing a theory, or stopping every share with the recipient - <c>aid.md</c>, section 2. It takes
	/// effect when proposed, is never answered and tells nobody.
	/// </summary>
	private static void ShareTechnology(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (offer.Stage != EOfferStage.Proposed)
		{
			return;
		}
		if (!offer.Value)
		{
			Aid.CTechnologySharing.Stop(actor, recipient);
			return;
		}
		if (offer.Category.Length == 0)
		{
			return;
		}
		Charge(state, actor, "SHARE_TECH_INFLUENCE_COST");
		Aid.CTechnologySharing.Start(actor, recipient, offer.Category);
	}

	/// <summary>
	/// Asking to run a debt, its answer, or revoking another's - <c>aid.md</c>, section 1. Revoking charges
	/// the recipient, the debtor; nothing touches the debt itself.
	/// </summary>
	private static void Debt(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			CGameState.Status(recipient, actor).AllowDebts = false;
			Charge(state, recipient, "REVOKE_DEBT_INFLUENCE_COST");
			Message(state, "", "DEBTREVOKE", "", actor, recipient);
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				Charge(state, actor, "ALLOW_DEBT_INFLUENCE_COST");
				break;
			case EOfferStage.Accepted:
				CGameState.Status(actor, recipient).AllowDebts = true;
				Message(state, "DEBTALLOWACCEPT", "", "", actor, recipient);
				break;
			default:
				Message(state, "DEBTALLOWDECLINE", "", "", actor, recipient);
				break;
		}
	}

	/// <summary>An offer to trade, or a cancellation: <c>trade.md</c>, section 3.</summary>
	private static void TradeOffer(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			// Never charged, though the texts show TRADE_CANCEL_INFLUENCE_COST.
			if (offer.Deal is { } ended && Trade.CTradeRules.Matching(state, ended) is { } route)
			{
				Trade.CTradeRoutes.End(state, route);
			}
			CRelations.Change(actor, recipient, -COfferRules.Define(state, "TRADE_CANCEL_RELATION_COST"));
			state.RaisePactMessage(new CPactMessage("WEBRKTRADE", "BRKTRADEUS", "BRKTRADEOTH", actor.Tag, recipient.Tag)
			{
				Deal = offer.Deal,
			});
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				Charge(state, actor, "TRADE_INFLUENCE_COST");
				break;
			case EOfferStage.Accepted:
				if (offer.Deal is not { } deal || Trade.CTradeRoutes.Make(state, deal, offer.Date) is null)
				{
					return;
				}
				Relation(state, actor, recipient, "TRADE_RELATION_CHANGES");
				state.RaisePactMessage(new CPactMessage("ACTRAWITHUS", "WETRADEAG", "OTHERTRADEAG", actor.Tag, recipient.Tag)
				{
					Deal = deal,
				});
				break;
			default:
				Message(state, "DETRAWITHUS", "WEDETRA", "DETRAWITHOTHER", actor, recipient);
				break;
		}
	}

	private static void Alliance(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			state.Alliances.RemoveAll(a => a.Joins(actor.Tag, recipient.Tag));
			Relation(state, actor, recipient, "ALLIANCE_REJECT_RELATION_CHANGE");
			Charge(state, actor, "LEAVE_ALLIANCE_INFLUENCE_COST");
			Message(state, "MILALLWEBAN", "MILALLBAN", "MILALLBANOTHER", actor, recipient);
			CRelations.Change(actor, recipient, AllianceLeftOver);
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				Charge(state, actor, "JOIN_ALLIANCE_INFLUENCE_COST");
				break;
			case EOfferStage.Accepted:
				state.Alliances.Add(new CAlliance(actor.Tag, recipient.Tag));
				Relation(state, actor, recipient, "ALLIANCE_RELATION_CHANGE");
				Message(state, "MILALLACCEPT", "MILALLWEACCEPT", "MILLALLINVACCEPTOTHER", actor, recipient);
				break;
			default:
				Message(state, "MILALLREJECT", "MILALLWEREJECT", "MILLALLJOINREJECTOTHER", actor, recipient);
				break;
		}
	}

	private static void Nap(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			state.Naps.RemoveAll(n => n.Joins(actor.Tag, recipient.Tag));
			CThreat.SpreadOverMap(state, recipient, COfferRules.Define(state, "LEAVE_NAP_THREAT_COST").Raw, 1000,
				state.Province(recipient.Capital));
			Relation(state, actor, recipient, "LEAVE_NAP_RELATION_CHANGE");
			Charge(state, actor, "LEAVE_NAP_INFLUENCE_COST");
			Message(state, "WECANCELNAP", "THEYCANCELNAP", "OTHERCANCELNAP", actor, recipient);
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				Charge(state, actor, "NAP_JOIN_INFLUENCE_COST");
				break;
			case EOfferStage.Accepted:
				state.Naps.Add(new CNap(actor.Tag, recipient.Tag, offer.Date));
				Relation(state, actor, recipient, "NAP_RELATION_CHANGE");
				Message(state, "WENAP", "THEYNAP", "OTHERNAP", actor, recipient);
				break;
			default:
				Relation(state, actor, recipient, "NAP_REJECT_RELATION_CHANGE");
				// The other way round from every other arm: the proposer is told "they", the one who declined "we".
				Message(state, "THEYREJECTNAP", "WEREJECTNAP", "OTHERREJECTNAP", actor, recipient);
				break;
		}
	}

	private static void Guarantee(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (offer.Value)
		{
			state.Guarantees.Add(new CGuarantee(actor.Tag, recipient.Tag, offer.Date));
			Charge(state, actor, "GUARANTEE_INFLUENCE_COST");
			Message(state, "WEGUARAT", "GUARATOUS", "GUARATOOTHER", actor, recipient);
			return;
		}
		// Whoever gave it, and the influence is given back.
		state.Guarantees.RemoveAll(g => g.Joins(actor.Tag, recipient.Tag));
		Relation(state, actor, recipient, "REVOKE_GUARANTEE_RELATION_CHANGE");
		actor.DiplomaticInfluence += COfferRules.Define(state, "REVOKE_GUARANTEE_INFLUENCE_COST");
		// Everybody else is told what the guarantor is.
		Message(state, "GUARACANCELTOTHEM", "GUARACANCELTOUS", "GUARACANCELTOTHEM", actor, recipient);
	}

	private static void AskAccess(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			Relation(state, actor, recipient, "MILACC_DECLINE_RELATION_CHANGE");
			CGameState.Status(actor, recipient).HasMilitaryAccess = false;
			Message(state, "WECANMIL", "CANCMILUS", "CANMILOTHE", actor, recipient);
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Proposed:
				Charge(state, actor, "MILACCESS_INFLUENCE_COST");
				break;
			case EOfferStage.Accepted:
				CGameState.Status(actor, recipient).HasMilitaryAccess = true;
				Relation(state, actor, recipient, "MILACC_ACCEPT_RELATION_CHANGE");
				Message(state, "ACCMILUS", "WEACCMIL", "ACCMILOTHER", actor, recipient);
				break;
			default:
				if (Played(state, actor, recipient))
				{
					Relation(state, actor, recipient, "MILACC_DECLINE_RELATION_CHANGE");
				}
				actor.DiplomaticInfluence += COfferRules.Define(state, "MILACCESS_INFLUENCE_COST");
				Message(state, "DECMILUS", "WEDECMIL", "DECMILOTHER", actor, recipient);
				break;
		}
	}

	private static void OfferAccess(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			if (Played(state, actor, recipient))
			{
				CRelations.Change(actor, recipient, CFixedPoint.FromInt(-20));
			}
			CGameState.Status(actor, recipient).HasMilitaryAccess = false;
			CGameState.Status(recipient, actor).HasMilitaryAccess = false;
			Message(state, "WECANMIL", "CANCMILUS", "CANMILOTHE", actor, recipient);
			return;
		}
		switch (offer.Stage)
		{
			case EOfferStage.Accepted:
				CGameState.Status(recipient, actor).HasMilitaryAccess = true;
				CRelations.Change(actor, recipient, CFixedPoint.FromInt(10));
				Message(state, "OFFACCMILUS", "WEOFFACCMIL", "OFFACCMILOTHER", actor, recipient);
				break;
			case EOfferStage.Declined:
				Message(state, "OFFDECMILUS", "WEDECOFFMIL", "DECOFFMILOTHER", actor, recipient);
				break;
		}
	}

	private static void Embargo(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		long threat = COfferRules.Define(state, "EMBARGO_THREAT_COST").Raw;
		if (offer.Value)
		{
			state.Embargoes.Add(new CEmbargo(actor.Tag, recipient.Tag));
			Trade.CTradeRoutes.EndPair(state, actor.Tag, recipient.Tag);
			Relation(state, actor, recipient, "EMBARGO_RELATION_CHANGE");
			Charge(state, actor, "EMBARGO_INFLUENCE_COST");
			CThreat.SpreadOverMap(state, recipient, threat, 1000, state.Province(recipient.Capital));
			Message(state, "WEEMBARGO", "THEYEMBARGO", "OTHEREMBARGO", actor, recipient);
			return;
		}
		CThreat.SpreadOverMap(state, recipient, threat, -1000, state.Province(recipient.Capital));
		state.Embargoes.RemoveAll(e => e.Joins(actor.Tag, recipient.Tag));
		Message(state, "WECANCELEMBARGO", "THEYCANCELEMBARGO", "OTHERCANCELEMBARGO", actor, recipient);
	}

	private static void Influence(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		if (!offer.Value)
		{
			StopInfluence(state, actor, recipient);
			return;
		}
		foreach (CInfluence running in state.Influences)
		{
			if (running.Influenced == recipient.Tag && state.Country(running.Influencer)?.Faction == actor.Faction)
			{
				return;
			}
		}
		state.Influences.Add(new CInfluence(actor.Tag, recipient.Tag, actor.Faction, offer.Date));
		Give(state, recipient, actor.Faction);
		Charge(state, actor, "INFLUENCE_INFLUENCE_COST");
		Message(state, "WEINFLUENCE", "THEYINFLUENCE", "OTHERINFLUENCE", actor, recipient, actor.Faction);
	}

	private static void Align(CGameState state, COffer offer, CCountry actor, CCountry recipient)
	{
		bool has = COfferRules.HasInfluenceModifier(state, actor, recipient.Faction);
		if (!offer.Value)
		{
			if (has)
			{
				StopAlign(state, actor, recipient);
			}
			return;
		}
		if (has)
		{
			return;
		}
		state.Aligns.Add(new CAlign(actor.Tag, recipient.Tag, recipient.Faction, offer.Date));
		Give(state, actor, recipient.Faction);
		Charge(state, actor, "ALIGN_INFLUENCE_COST");
		Message(state, "WEALIGN", "THEYALIGN", "OTHERALIGN", actor, recipient, recipient.Faction);
	}

	/// <summary>Ends the influence of the influencer on the influenced, taking its modifier off, with its message.</summary>
	public static void StopInfluence(CGameState state, CCountry influencer, CCountry influenced)
	{
		CInfluence? running = state.Influences.Find(i => i.Influencer == influencer.Tag && i.Influenced == influenced.Tag);
		if (running is null)
		{
			return;
		}
		EndInfluence(state, running);
		if (COfferRules.RealFaction(influencer))
		{
			Message(state, "WEINFLUENCESTOP", "THEYINFLUENCESTOP", "OTHERINFLUENCESTOP", influencer, influenced, running.Faction);
		}
	}

	/// <summary>Ends the aligner's alignment towards the leader, taking its modifier off, with its message.</summary>
	public static void StopAlign(CGameState state, CCountry aligner, CCountry leader)
	{
		if (state.Aligns.Find(a => a.Aligner == aligner.Tag && a.Leader == leader.Tag) is { } running)
		{
			EndAlign(state, running);
		}
		Message(state, "WEALIGNSTOP", "THEYALIGNSTOP", "OTHERALIGNSTOP", aligner, leader, leader.Faction);
	}

	/// <summary>Takes an influence off the list and its modifier off the influenced country, silently.</summary>
	public static void EndInfluence(CGameState state, CInfluence influence)
	{
		state.Influences.Remove(influence);
		if (state.Country(influence.Influenced) is { } influenced)
		{
			TakeOff(state, influenced, influence.Faction);
		}
	}

	/// <summary>Takes an alignment off the list and its modifier off the aligner, silently.</summary>
	public static void EndAlign(CGameState state, CAlign align)
	{
		state.Aligns.Remove(align);
		if (state.Country(align.Aligner) is { } aligner)
		{
			TakeOff(state, aligner, align.Faction);
		}
	}

	/// <summary>
	/// A war between two begins: it ends their influence on each other and their trade routes, and either's
	/// leave to run a debt with the other - section 6, <c>trade.md</c>, section 4, and <c>aid.md</c>,
	/// section 1.
	/// </summary>
	public static void WarBegun(CGameState state, CCountryTag one, CCountryTag other)
	{
		Trade.CTradeRoutes.EndPair(state, one, other);
		if (state.Country(one) is { } first && state.Country(other) is { } second)
		{
			CGameState.Status(first, second).AllowDebts = false;
			CGameState.Status(second, first).AllowDebts = false;
		}
		foreach (CInfluence influence in state.Influences.ToArray())
		{
			if ((influence.Influencer == one && influence.Influenced == other)
				|| (influence.Influencer == other && influence.Influenced == one))
			{
				EndInfluence(state, influence);
			}
		}
	}

	/// <summary>Gives the country the faction's influence modifier for the define's days, for ever where below nought.</summary>
	private static void Give(CGameState state, CCountry country, string faction)
	{
		string name = COfferRules.InfluenceModifier(state, faction);
		if (name.Length == 0 || state.FindStaticModifier(name) is not { } modifier)
		{
			return;
		}
		int days = (int)(COfferRules.Define(state, "DAYS_OF_INFLUENCE_RELATION_CHANGE").Raw / 1000);
		country.EventModifiers.Add(CTimedModifier.Given(state, modifier, days));
	}

	private static void TakeOff(CGameState state, CCountry country, string faction)
	{
		string name = COfferRules.InfluenceModifier(state, faction);
		country.EventModifiers.RemoveAll(m => string.Equals(m.Modifier.Name, name, StringComparison.OrdinalIgnoreCase));
	}

	private static bool Played(CGameState state, CCountry a, CCountry b) => state.Played.Contains(a.Tag) || state.Played.Contains(b.Tag);

	private static void Relation(CGameState state, CCountry actor, CCountry recipient, string define)
	{
		CRelations.Change(actor, recipient, COfferRules.Define(state, define));
	}

	/// <summary>Takes the define's influence from the country, never below nought.</summary>
	private static void Charge(CGameState state, CCountry country, string define)
	{
		CFixedPoint left = country.DiplomaticInfluence - COfferRules.Define(state, define);
		country.DiplomaticInfluence = left < CFixedPoint.Zero ? CFixedPoint.Zero : left;
	}

	/// <summary>Tells the recipient alone, with ACTOR and RECIPIENT written the other way round.</summary>
	public static void ToldTurnedRound(CGameState state, CCountry actor, CCountry recipient, string key)
	{
		state.RaisePactMessage(new CPactMessage("", key, "", actor.Tag, recipient.Tag) { TurnedRound = true });
	}

	public static void Message(CGameState state, string we, string they, string other, CCountry actor, CCountry recipient,
		string faction = "")
	{
		state.RaisePactMessage(new CPactMessage(we, they, other, actor.Tag, recipient.Tag) { Faction = faction });
	}
}
