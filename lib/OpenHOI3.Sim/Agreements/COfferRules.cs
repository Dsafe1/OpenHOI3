using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;
using OpenHOI3.Sim.Modifiers;
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
					? !allied && !atWar && !IsPuppet(actor) && !InExile() && RecipientFree(actor, recipient)
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
			default:
				return false;
		}
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
					? !allied && !atWar && !IsPuppet(actor) && RecipientFree(actor, recipient) && !InExile()
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
			_ => null,
		};
		return define is null ? CFixedPoint.Zero : Define(state, define);
	}

	/// <summary>Whether an AI answers the offer: what the original's per-kind answer test says.</summary>
	public static bool AiAnswers(COffer offer)
	{
		return offer.Kind switch
		{
			EOfferKind.Alliance or EOfferKind.NonAggressionPact => offer.Value,
			EOfferKind.AskMilitaryAccess or EOfferKind.OfferMilitaryAccess => offer.Stage == EOfferStage.Proposed && offer.Value,
			_ => false,
		};
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
			_ => null,
		};
	}

	/// <summary>
	/// Whether the breaker may break its NAP with the other: not for <c>NAP_UNBREAKABLE_MONTHS</c>, then
	/// in each of three windows of <c>NAP_FORCE_BALANCE_RULE_MONTHS</c> only on the border's balance of
	/// brigades, which is not built - so not - and after them always.
	/// </summary>
	public static bool CanBreakNap(CGameState state, CCountry breaker, CCountry other)
	{
		if (Nap(state, breaker.Tag, other.Tag) is not { } nap)
		{
			return false;
		}
		int unbreakable = (int)(Define(state, "NAP_UNBREAKABLE_MONTHS").Raw / 1000);
		int window = (int)(Define(state, "NAP_FORCE_BALANCE_RULE_MONTHS").Raw / 1000);
		return state.TotalHours >= AddMonths(nap.Start, unbreakable + 3 * window);
	}

	/// <summary>An hour some months on, the hour of the day kept.</summary>
	public static long AddMonths(long hour, int months)
	{
		CDate date = CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay)).AddMonths(months);
		return date.DayNumber * (long)CGameState.HoursPerDay + hour % CGameState.HoursPerDay;
	}

	public static CFixedPoint Define(CGameState state, string name) => state.Defines[Section, name];

	public static bool Afford(CGameState state, CCountry actor, COffer offer) => actor.DiplomaticInfluence >= Cost(state, offer);

	public static bool DiplomatEnRoute(CGameState state, CCountry actor, CCountry recipient) =>
		state.TotalHours <= CGameState.Status(actor, recipient).DiplomatUntil;

	public static bool IsPuppet(CCountry country) => country.Overlord is not null;

	/// <summary>The recipient is no puppet, or the actor's own.</summary>
	public static bool RecipientFree(CCountry actor, CCountry recipient) => recipient.Overlord is null || recipient.Overlord == actor.Tag;

	/// <summary>A government in exile, which the simulation has none of.</summary>
	public static bool InExile() => false;

	public static bool RealFaction(CCountry country) => country.Faction.Length > 0;

	public static bool OneRealFaction(CCountry a, CCountry b) => RealFaction(a) && string.Equals(a.Faction, b.Faction, StringComparison.Ordinal);

	public static bool IsMajor(CCountry country) => country.IsMajor || country.BaseIc > MajorIc;

	private static bool Owns(CGameState state, CCountry country) => CCountryLists.OwnedCount(state, country.Tag) > 0;

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
			if (string.Equals(given.Modifier.Name, name, StringComparison.OrdinalIgnoreCase) && !given.HasEnded(state))
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
