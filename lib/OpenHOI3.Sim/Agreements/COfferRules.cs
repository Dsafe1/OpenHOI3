using System.Globalization;
using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Trade;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agreements;

/// <summary>
/// When an offer is valid - does anything when executed - and when its button may be pressed, the two
/// tests the original keeps apart and lets disagree; what it costs; and whether an AI answers it, and
/// by which of the install's Lua functions. See <c>docs/mechanics/agreements.md</c>, sections 2 and 4.
/// </summary>
public static class COfferRules
{
	public const string Section = "diplomacy";

	/// <summary>Base IC over which a country counts as a major whatever its file says.</summary>
	private const int MajorIc = 100;

	/// <summary>How long a diplomat is on his way after an offer: seven days.</summary>
	public const long DiplomatHours = 168;

	/// <summary>Whether the offer does anything when its command runs.</summary>
	public static bool IsValid(CGameState state, COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient)
		{
			return false;
		}
		bool atWar = CCountryLists.AtWar(state, actor.Tag, recipient.Tag);
		bool allied = CCountryLists.HasAlliance(state, actor.Tag, recipient.Tag);
		switch (offer.Kind)
		{
			case EOfferKind.Alliance:
				return offer.Value
					? !allied && !atWar && !IsPuppet(actor) && !actor.InExile && RecipientFree(actor, recipient)
						&& !OneRealFaction(actor, recipient)
					: allied && !atWar && !state.IsAtWar(actor.Tag) && !state.IsAtWar(recipient.Tag) && !IsPuppet(actor)
						&& RecipientFree(actor, recipient);
			case EOfferKind.NonAggressionPact:
				return offer.Value
					? !allied && !atWar && !IsPuppet(actor) && RecipientFree(actor, recipient)
						&& Afford(state, actor, offer)
					: Nap(state, actor.Tag, recipient.Tag) is not null && !atWar && Afford(state, actor, offer);
			case EOfferKind.Guarantee:
				return !atWar && Guarantee(state, actor.Tag, recipient.Tag) is null;
			case EOfferKind.AskMilitaryAccess:
				return !OneRealFaction(actor, recipient) && HasAccess(actor, recipient) != offer.Value;
			case EOfferKind.OfferMilitaryAccess:
				return HasAccess(recipient, actor) != offer.Value;
			case EOfferKind.Embargo:
				return offer.Value
					? !allied && !atWar && !IsPuppet(actor) && (!IsPuppet(recipient) || recipient.Overlord == actor.Tag)
					: Embargo(state, actor.Tag, recipient.Tag) is not null;
			case EOfferKind.Influence:
			{
				bool common = !atWar && RealFaction(actor) && (recipient.Overlord is null || recipient.Overlord == actor.Tag)
					&& IsMajor(actor) && Owns(state, actor) && Owns(state, recipient);
				return common && (offer.Value || HasInfluenceModifier(state, recipient, actor.Faction));
			}
			case EOfferKind.Align:
				return !(atWar && offer.Value) && !RealFaction(actor) && !IsPuppet(actor) && !IsPuppet(recipient)
					&& Owns(state, actor) && Owns(state, recipient);
			case EOfferKind.Trade:
				// An exile trades only while it owns its seat.
				if (!Owns(state, actor) || !Owns(state, recipient) || (actor.InExile && state.Province(actor.ActingCapital).Owner != actor.Tag)
					|| (recipient.InExile && state.Province(recipient.ActingCapital).Owner != recipient.Tag))
				{
					return false;
				}
				// A puppet trades with its own master or nobody.
				return offer.Value
					? !atWar && (actor.Overlord is null || actor.Overlord == recipient.Tag)
						&& (recipient.Overlord is null || recipient.Overlord == actor.Tag)
					: offer.Deal is { } deal && CTradeRules.Matching(state, deal) is not null;
			case EOfferKind.Debt:
				// Neither in exile.
				return !actor.InExile && !recipient.InExile;
			case EOfferKind.ShareTechnology:
				// Sharing a theory the actor is above the recipient in, as it reads it, and the recipient owns land.
				return !offer.Value || (IsTheory(state, offer.Category) && Owns(state, recipient)
					&& actor.OwnAbility(offer.Category) > recipient.Ability(offer.Category));
			case EOfferKind.RequestLendLease:
			case EOfferKind.OfferLendLease:
				return actor != recipient && Owns(state, recipient);
			case EOfferKind.Licence:
				return !atWar;
			case EOfferKind.Expedition:
				return CExpeditions.IsValid(state, offer, actor, recipient);
			case EOfferKind.DeclareWar:
				return Wars.CDeclarations.IsValid(state, actor, recipient, offer.WarGoal);
			case EOfferKind.CallAlly:
				return Wars.CCallsToArms.IsValid(state, offer, actor, recipient);
			case EOfferKind.AddWarGoal:
				return Wars.CWarSides.Between(state, actor.Tag, recipient.Tag) is { } war && Wars.CWarGoals.CooledDown(state, actor, recipient)
					&& offer.WarGoal is { } goal && Wars.CWarGoals.IsValidForWar(state, goal, war);
			case EOfferKind.Peace:
				return Wars.CPeace.IsValid(state, actor, recipient);
			case EOfferKind.Faction:
				return Wars.CFactionOffers.IsValid(state, offer, actor, recipient);
			default:
				return false;
		}
	}

	/// <summary>Whether a technology category is a theory - the only kind that may be shared.</summary>
	public static bool IsTheory(CGameState state, string category)
	{
		foreach (Research.CTechnologyCategory each in state.TechnologyCategories)
		{
			if (each.Name.Equals(category, StringComparison.OrdinalIgnoreCase))
			{
				return each.IsTheory;
			}
		}
		return false;
	}

	/// <summary>Whether its button may be pressed; a button that may not is greyed.</summary>
	public static bool IsSelectable(CGameState state, COffer offer)
	{
		if (state.Country(offer.Actor) is not { } actor || state.Country(offer.Recipient) is not { } recipient
			|| actor == recipient)
		{
			return false;
		}
		bool atWar = CCountryLists.AtWar(state, actor.Tag, recipient.Tag);
		bool allied = CCountryLists.HasAlliance(state, actor.Tag, recipient.Tag);
		bool enRoute = DiplomatEnRoute(state, actor, recipient);
		switch (offer.Kind)
		{
			case EOfferKind.Alliance:
				if (enRoute)
				{
					return false;
				}
				return offer.Value
					? !allied && !atWar && !IsPuppet(actor) && RecipientFree(actor, recipient) && !recipient.InExile
						&& actor.EffectiveNeutrality <= Define(state, "ALLIANCE_NEUTRALITY_LIMIT")
						&& recipient.EffectiveNeutrality <= Define(state, "ALLIANCE_NEUTRALITY_LIMIT")
						&& Afford(state, actor, offer)
					: allied && !atWar && !state.IsAtWar(actor.Tag) && !state.IsAtWar(recipient.Tag)
						&& !IsPuppet(actor) && !IsPuppet(recipient) && Afford(state, actor, offer);
			case EOfferKind.NonAggressionPact:
				return !enRoute && IsValid(state, offer) && (offer.Value || CanBreakNap(state, actor, recipient));
			case EOfferKind.Guarantee:
				return Afford(state, actor, offer)
					&& actor.EffectiveNeutrality <= Define(state, "GUARANTEE_NEUTRALITY_LIMIT")
					&& !atWar && Guarantee(state, actor.Tag, recipient.Tag) is null;
			case EOfferKind.AskMilitaryAccess:
				return !atWar && Afford(state, actor, offer) && !OneRealFaction(actor, recipient)
					&& HasAccess(actor, recipient) != offer.Value && (offer.Value || !HasUnitsIn(state, actor, recipient));
			case EOfferKind.OfferMilitaryAccess:
				return !atWar && Afford(state, actor, offer) && HasAccess(recipient, actor) != offer.Value;
			case EOfferKind.Embargo:
				if (enRoute)
				{
					return false;
				}
				if (offer.Value)
				{
					return !OneRealFaction(actor, recipient) && !allied && !atWar && !IsPuppet(actor)
						&& recipient.Overlord != actor.Tag
						&& actor.EffectiveNeutrality <= Define(state, "EMBARGO_NETRALITY")
						&& Afford(state, actor, offer);
				}
				CEmbargo? standing = Embargo(state, actor.Tag, recipient.Tag);
				return (standing is null || standing.First == actor.Tag) && !atWar;
			case EOfferKind.Influence:
			{
				if (!Owns(state, recipient) || IsPuppet(actor) || !IsMajor(actor) || atWar)
				{
					return false;
				}
				bool has = HasInfluenceModifier(state, recipient, actor.Faction);
				if (offer.Value ? has || !Afford(state, actor, offer) : !has)
				{
					return false;
				}
				return !state.IsAtWar(recipient.Tag) || !AtWarWithFaction(state, recipient, actor.Faction);
			}
			case EOfferKind.Align:
			{
				if ((atWar && offer.Value) || !Afford(state, actor, offer))
				{
					return false;
				}
				bool has = HasInfluenceModifier(state, actor, recipient.Faction);
				CAlign? running = Align(state, actor.Tag, recipient.Tag);
				if (!offer.Value)
				{
					return has && running is not null;
				}
				return !has && RealFaction(recipient) && Leader(state, recipient.Faction) == recipient.Tag && running is null
					&& (!state.IsAtWar(actor.Tag) || !AtWarWithFaction(state, actor, recipient.Faction));
			}
			case EOfferKind.Trade:
				if (!offer.Value)
				{
					return true;
				}
				return !atWar && !IsPuppet(actor) && RecipientFree(actor, recipient) && Afford(state, actor, offer)
					&& (!CTradeRules.NeedsConvoy(state, Supply.CSupplyNetworks.Refresh(state), actor, recipient)
						|| (CTradeRules.HasPort(state, actor) && CTradeRules.HasPort(state, recipient)))
					&& Embargo(state, actor.Tag, recipient.Tag) is null;
			case EOfferKind.Debt:
				// Allowing asks whether the actor may not yet run a debt with them; revoking whether they may
				// with the actor. Both costs are asked of the actor's influence whichever it is. A puppet does neither, and
				// the recipient is no puppet but the actor's own, so a puppet never revokes its master's leave.
				return !enRoute && !actor.InExile && !recipient.InExile && !IsPuppet(actor) && RecipientFree(actor, recipient)
					&& (offer.Value ? !CGameState.Status(actor, recipient).AllowDebts : CGameState.Status(recipient, actor).AllowDebts)
					&& actor.DiplomaticInfluence >= Define(state, "ALLOW_DEBT_INFLUENCE_COST")
					&& actor.DiplomaticInfluence >= Define(state, "REVOKE_DEBT_INFLUENCE_COST")
					&& !atWar;
			case EOfferKind.ShareTechnology:
				return !enRoute && !atWar && (!offer.Value || Afford(state, actor, offer));
			case EOfferKind.RequestLendLease:
				// Asking passes the rules, the actor receiving; cancelling needs the recipient to be giving.
				return offer.Value
					? !enRoute && Aid.CLendLease.Refusal(state, recipient, actor) == Aid.CLendLease.ERefusal.None
					: Aid.CLendLease.Gives(recipient, actor);
			case EOfferKind.OfferLendLease:
				return offer.Value
					? !enRoute && !Aid.CLendLease.Gives(actor, recipient)
						&& Aid.CLendLease.Refusal(state, actor, recipient) == Aid.CLendLease.ERefusal.None
					: Aid.CLendLease.Gives(actor, recipient);
			case EOfferKind.Licence:
				return !enRoute && !atWar && (!offer.Value || Afford(state, actor, offer));
			case EOfferKind.Expedition:
				return CExpeditions.IsSelectable(state, offer, actor, recipient);
			case EOfferKind.DeclareWar:
				return Wars.CDeclarations.IsSelectable(state, actor, recipient);
			case EOfferKind.CallAlly:
				return Wars.CCallsToArms.IsSelectable(state, offer, actor, recipient);
			case EOfferKind.AddWarGoal:
				return atWar && Wars.CWarGoals.CooledDown(state, actor, recipient);
			case EOfferKind.Peace:
				return Wars.CPeace.IsSelectable(state, actor, recipient);
			case EOfferKind.Faction:
				return Wars.CFactionOffers.Refusal(state, offer, actor, recipient) == Wars.EFactionRefusal.None;
			default:
				return false;
		}
	}

	/// <summary>What the offer costs its actor in influence, which its button asks it to have.</summary>
	public static CFixedPoint Cost(CGameState state, COffer offer)
	{
		string? define = offer.Kind switch
		{
			EOfferKind.Alliance => offer.Value ? "JOIN_ALLIANCE_INFLUENCE_COST" : "LEAVE_ALLIANCE_INFLUENCE_COST",
			EOfferKind.NonAggressionPact => offer.Value ? "NAP_JOIN_INFLUENCE_COST" : "LEAVE_NAP_INFLUENCE_COST",
			EOfferKind.Guarantee => "GUARANTEE_INFLUENCE_COST",
			EOfferKind.AskMilitaryAccess or EOfferKind.OfferMilitaryAccess => "MILACCESS_INFLUENCE_COST",
			EOfferKind.Embargo => offer.Value ? "EMBARGO_INFLUENCE_COST" : null,
			EOfferKind.Influence => offer.Value ? "INFLUENCE_INFLUENCE_COST" : null,
			EOfferKind.Align => "ALIGN_INFLUENCE_COST",
			// What a cancellation is said to cost; it is never charged (trade.md, section 3).
			EOfferKind.Trade => offer.Value ? "TRADE_INFLUENCE_COST" : "TRADE_CANCEL_INFLUENCE_COST",
			EOfferKind.Debt => offer.Value ? "ALLOW_DEBT_INFLUENCE_COST" : "REVOKE_DEBT_INFLUENCE_COST",
			EOfferKind.ShareTechnology => offer.Value ? "SHARE_TECH_INFLUENCE_COST" : null,
			// Asking for lend-lease is charged the debt's define; offering it costs nothing (aid.md, section 5).
			EOfferKind.RequestLendLease => offer.Value ? "ALLOW_DEBT_INFLUENCE_COST" : null,
			EOfferKind.Licence => offer.Value ? "LICENCE_INFLUENCE_COST" : null,
			EOfferKind.Expedition => offer.Value ? CExpeditions.CostDefine : null,
			EOfferKind.DeclareWar => Wars.CDeclarations.InfluenceCost,
			EOfferKind.CallAlly => Wars.CCallsToArms.InfluenceCost,
			EOfferKind.Faction => Wars.CFactionOffers.CostDefine(offer.Value),
			_ => null,
		};
		return define is null ? CFixedPoint.Zero : Define(state, define);
	}

	/// <summary>Whether an AI answers the offer: what the original's per-kind answer test says.</summary>
	public static bool AiAnswers(COffer offer)
	{
		return offer.Kind switch
		{
			EOfferKind.Alliance or EOfferKind.NonAggressionPact or EOfferKind.Trade or EOfferKind.Debt
				or EOfferKind.RequestLendLease or EOfferKind.OfferLendLease => offer.Value,
			// A licence is always answered.
			EOfferKind.Licence => true,
			EOfferKind.Expedition => offer.Value && offer.Stage == EOfferStage.Proposed,
			EOfferKind.AskMilitaryAccess or EOfferKind.OfferMilitaryAccess => offer.Stage == EOfferStage.Proposed && offer.Value,
			// Both kinds of call are answered (war.md, section 6).
			EOfferKind.CallAlly => offer.Stage == EOfferStage.Proposed,
			// Peace is answered as it is proposed (war.md, section 11).
			EOfferKind.Peace => offer.Stage == EOfferStage.Proposed && offer.Value,
			// Either direction is answered as it is proposed (factions.md, section 3).
			EOfferKind.Faction => offer.Stage == EOfferStage.Proposed,
			_ => false,
		};
	}

	/// <summary>
	/// Whether an offer on a played country's list is put to its player: a proposal that makes something,
	/// of a kind with a dialog of its own. A guarantee, an embargo, an influence and an alignment took
	/// effect when proposed and ask nothing; an answer asks nothing. See <c>agreements.md</c>, section 10.
	/// </summary>
	public static bool AsksPlayer(COffer offer)
	{
		// A licence is put whatever its value; a call to arms only where it asks (war.md, section 6).
		return offer.Stage == EOfferStage.Proposed && (offer.Kind == EOfferKind.Licence || (offer.Value && offer.Kind is EOfferKind.Alliance
			or EOfferKind.NonAggressionPact or EOfferKind.AskMilitaryAccess or EOfferKind.OfferMilitaryAccess
			or EOfferKind.Trade or EOfferKind.Debt or EOfferKind.RequestLendLease or EOfferKind.OfferLendLease
			or EOfferKind.CallAlly)) || (offer.Stage == EOfferStage.Proposed && offer.Kind == EOfferKind.Faction);
	}

	/// <summary>The install's Lua function the AI's answer is scored by; none where the AI does not answer.</summary>
	public static string? ScoreFunction(EOfferKind kind)
	{
		return kind switch
		{
			EOfferKind.Alliance => "DiploScore_Alliance",
			EOfferKind.NonAggressionPact => "DiploScore_NonAgression",
			EOfferKind.AskMilitaryAccess => "DiploScore_DemandMilitaryAccess",
			EOfferKind.OfferMilitaryAccess => "DiploScore_OfferMilitaryAccess",
			EOfferKind.Trade => "DiploScore_OfferTrade",
			EOfferKind.Debt => "DiploScore_Debt",
			EOfferKind.RequestLendLease => "DiploScore_RequestLendLease",
			EOfferKind.OfferLendLease => "DiploScore_OfferLendLease",
			EOfferKind.Licence => "DiploScore_LicenceTechnology",
			EOfferKind.Expedition => "DiploScore_SendExpeditionaryForce",
			EOfferKind.CallAlly => "DiploScore_CallAlly",
			EOfferKind.Peace => "DiploScore_PeaceAction",
			EOfferKind.Faction => "DiploScore_InviteToFaction",
			_ => null,
		};
	}

	/// <summary>
	/// Whether the breaker may break its NAP with the other: not for <c>NAP_UNBREAKABLE_MONTHS</c>, then
	/// in each of three windows of <c>NAP_FORCE_BALANCE_RULE_MONTHS</c> only on the border's balance of
	/// brigades, as the breaker sees it, and after them always.
	/// </summary>
	public static bool CanBreakNap(CGameState state, CCountry breaker, CCountry other)
	{
		if (Nap(state, breaker.Tag, other.Tag) is not { } nap)
		{
			return false;
		}
		int unbreakable = (int)(Define(state, "NAP_UNBREAKABLE_MONTHS").Raw / 1000);
		int window = (int)(Define(state, "NAP_FORCE_BALANCE_RULE_MONTHS").Raw / 1000);
		long date = AddMonths(nap.Start, unbreakable);
		if (date > state.TotalHours)
		{
			return false;
		}
		long balance = CNapBalance.As(nap, breaker.Tag);
		for (int step = 1; step <= 3; step++)
		{
			date = AddMonths(date, window);
			if (date > state.TotalHours)
			{
				return balance >= Define(state, "NAP_BREAK_FORCE_BALANCE_" + step.ToString(CultureInfo.InvariantCulture)).Raw;
			}
		}
		return true;
	}

	/// <summary>An hour some months on, the hour of the day kept.</summary>
	public static long AddMonths(long hour, int months)
	{
		CDate date = CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay)).AddMonths(months);
		return date.DayNumber * (long)CGameState.HoursPerDay + hour % CGameState.HoursPerDay;
	}

	public static CFixedPoint Define(CGameState state, string name) => state.Defines[Section, name];

	public static bool Afford(CGameState state, CCountry actor, COffer offer) => actor.DiplomaticInfluence >= Cost(state, offer);

	/// <summary>In one faction, the two with none counted as one, as the check when an offer runs has it.</summary>
	public static bool SameFactionEvenNone(CCountry one, CCountry other) => one.Faction == other.Faction;

	public static bool DiplomatEnRoute(CGameState state, CCountry actor, CCountry recipient) =>
		state.TotalHours <= CGameState.Status(actor, recipient).DiplomatUntil;

	/// <summary>
	/// Whether a diplomat on his way stops this offer - its row greyed, and as a proposal refused at its post: every kind
	/// but three. A trade and an expedition only while offering or sending; stopping lend-lease only where the actor is not
	/// giving the recipient any. See <c>docs/mechanics/agreements.md</c>, section 1.
	/// </summary>
	public static bool WaitsForDiplomat(CGameState state, COffer offer)
	{
		return offer.Kind switch
		{
			EOfferKind.Trade or EOfferKind.Expedition => offer.Value,
			EOfferKind.OfferLendLease => offer.Value || state.Country(offer.Actor) is not { } actor
				|| state.Country(offer.Recipient) is not { } recipient || !Aid.CLendLease.Gives(actor, recipient),
			_ => true,
		};
	}

	/// <summary>
	/// Whether a proposal is thrown away as it is posted, before it is ever executed: one a diplomat stops while the
	/// actor's diplomat is on his way to the recipient - a call to arms among them, which sends none itself. Answers and
	/// the offers a declaration makes and runs on the spot are not posted and are never asked. See
	/// <c>docs/mechanics/agreements.md</c>, section 1.
	/// </summary>
	public static bool RefusedAtPost(CGameState state, COffer offer)
	{
		return offer.Stage == EOfferStage.Proposed && WaitsForDiplomat(state, offer)
			&& state.Country(offer.Actor) is { } actor && state.Country(offer.Recipient) is { } recipient
			&& DiplomatEnRoute(state, actor, recipient);
	}

	public static bool IsPuppet(CCountry country) => country.Overlord is not null;

	/// <summary>The recipient is no puppet, or the actor's own.</summary>
	public static bool RecipientFree(CCountry actor, CCountry recipient) => recipient.Overlord is null || recipient.Overlord == actor.Tag;


	public static bool RealFaction(CCountry country) => country.Faction.Length > 0;

	public static bool OneRealFaction(CCountry a, CCountry b) => RealFaction(a) && string.Equals(a.Faction, b.Faction, StringComparison.Ordinal);

	public static bool IsMajor(CCountry country) => country.IsMajor || country.BaseIc > MajorIc;

	private static bool Owns(CGameState state, CCountry country) => CCountryFacts.Of(state).OwnedCount(country.Tag) > 0;

	public static bool HasAccess(CCountry holder, CCountry other) => CGameState.Status(holder, other).HasMilitaryAccess;

	/// <summary>Whether any unit of the country stands in a province the other owns.</summary>
	public static bool HasUnitsIn(CGameState state, CCountry country, CCountry owner)
	{
		foreach (CUnit unit in country.AllUnits())
		{
			if (state.Province(unit.Location).Owner == owner.Tag)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A faction's leader, its first member; none for a faction with no members.</summary>
	public static CCountryTag? Leader(CGameState state, string faction)
	{
		List<CCountry> members = CCountryLists.FactionMembers(state, faction);
		return members.Count > 0 ? members[0].Tag : null;
	}

	/// <summary>Whether the country is at war with any member of the faction.</summary>
	public static bool AtWarWithFaction(CGameState state, CCountry country, string faction)
	{
		foreach (CCountry member in CCountryLists.FactionMembers(state, faction))
		{
			if (CCountryLists.AtWar(state, country.Tag, member.Tag))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The static modifier a faction's influence puts on a country: its group's <c>influence</c> key.</summary>
	public static string InfluenceModifier(CGameState state, string faction)
	{
		foreach (CIdeologyGroup group in state.IdeologyGroups)
		{
			if (faction.Length > 0 && string.Equals(group.Faction, faction, StringComparison.Ordinal))
			{
				return group.Influence;
			}
		}
		return "";
	}

	/// <summary>Whether the country carries the faction's influence modifier, given by an influence or an alignment.</summary>
	public static bool HasInfluenceModifier(CGameState state, CCountry country, string faction)
	{
		string name = InfluenceModifier(state, faction);
		if (name.Length == 0)
		{
			return false;
		}
		foreach (CTimedModifier given in country.EventModifiers)
		{
			if (given.Modifier.Name == name && !given.HasEnded(state))
			{
				return true;
			}
		}
		return false;
	}

	public static CNap? Nap(CGameState state, CCountryTag a, CCountryTag b) => state.Naps.Find(n => n.Joins(a, b));

	public static CEmbargo? Embargo(CGameState state, CCountryTag a, CCountryTag b) => state.Embargoes.Find(e => e.Joins(a, b));

	public static CAlign? Align(CGameState state, CCountryTag aligner, CCountryTag leader) =>
		state.Aligns.Find(a => a.Aligner == aligner && a.Leader == leader);

	public static CGuarantee? Guarantee(CGameState state, CCountryTag a, CCountryTag b)
	{
		foreach (CGuarantee guarantee in state.Guarantees)
		{
			if (guarantee.Joins(a, b))
			{
				return guarantee;
			}
		}
		return null;
	}
}
