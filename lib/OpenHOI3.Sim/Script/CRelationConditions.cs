using System;
using System.Linq;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on the scope's country among the others - who plays it, its wars, its
/// alliances, its overlord, its faction, its neighbours, the nations it could release:
/// <c>docs/mechanics/triggers.md</c>, sections 5 and 6.
/// </summary>
public static class CRelationConditions
{
	/// <summary>The rebels' tag, an enemy of every country but itself.</summary>
	public const string Rebels = "REB";

	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		CCountry? country = state.Country(scope.Country);
		CScriptArgument value = trigger.Argument;
		long whole = CConditions.WholeOf(value.Amount);
		switch (trigger.Keyword)
		{
			case "ai":
				answer = CConditions.Is(trigger, scope.Country is not { } played || !state.Played.Contains(played));
				return true;
			case "war":
				answer = CConditions.Is(trigger, scope.Country is { } us && state.IsAtWar(us));
				return true;
			case "war_with":
				answer = IsEnemy(state, scope.Country, CConditions.TagByOwner(state, trigger, scope));
				return true;
			case "truce_with":
				answer = country is not null && state.Country(CConditions.TagByOwner(state, trigger, scope)) is { } trucer
					&& Wars.CTruces.Has(state, country, trucer);
				return true;
			case "casus_belli":
				// Some type it may use on the other, THIS read straight off the root (war.md, section 2).
				answer = country is not null && state.Country(CConditions.TagDirect(trigger, scope)) is { } on
					&& Wars.CCasusBellis.HasUsable(state, country, on);
				return true;
			case "has_wargoal":
				// At war with the other and holding a goal against it in that war, whatever its type.
				answer = country is not null && CConditions.TagDirect(trigger, scope) is { } against
					&& Wars.CWarSides.Between(state, country.Tag, against) is { } fought
					&& fought.GoalsOf(country.Tag).Any(g => g.Recipient == against);
				return true;
			case "has_cb":
				answer = country is not null && HasCb(state, trigger, scope, country);
				return true;
			case "enemy_ic_ratio":
				answer = EnemyIcRatio(state, scope) is { } ratio && ratio <= value.Amount.Raw;
				return true;
			case "alliance_with":
				answer = scope.Country is { } one && CConditions.TagByOwner(state, trigger, scope) is { } other
					&& CCountryLists.HasAlliance(state, one, other);
				return true;
			case "guarantee":
				// The scope guarantees the other.
				answer = scope.Country is { } guarantor && CConditions.TagByOwner(state, trigger, scope) is { } guaranteed
					&& state.Guarantees.Exists(g => g.Guarantor == guarantor && g.Guaranteed == guaranteed);
				return true;
			case "non_aggression_pact":
				answer = scope.Country is { } side && CConditions.TagByOwner(state, trigger, scope) is { } pact
					&& COfferRules.Nap(state, side, pact) is not null;
				return true;
			case "military_access":
				// The scope may pass through the other's land.
				answer = country is not null && state.Country(CConditions.TagByOwner(state, trigger, scope)) is { } through
					&& COfferRules.HasAccess(country, through);
				return true;
			case "is_subject":
				answer = CConditions.Is(trigger, country?.Overlord is not null);
				return true;
			case "vassal_of":
				answer = country?.Overlord is { } overlord && overlord == CConditions.TagByOwner(state, trigger, scope);
				return true;
			case "undeclared_war_with":
				answer = scope.Country is { } undeclaredOne && CConditions.TagDirect(trigger, scope) is { } undeclaredOther
					&& Wars.CUndeclaredWars.Between(state, undeclaredOne, undeclaredOther) is not null;
				return true;
			case "num_of_vassals":
				answer = (country is null ? 0 : CCountryLists.VassalCount(state, country)) >= Written(value, whole);
				return true;
			case "num_of_allies":
				answer = (country is null ? 0 : CCountryLists.Allies(state, country).Count) >= Written(value, whole);
				return true;
			case "is_in_any_faction":
				answer = CConditions.Is(trigger, (country?.Faction ?? "").Length > 0);
				return true;
			case "num_in_faction":
				answer = CCountryLists.FactionMembers(state, country?.Faction ?? "").Count >= Written(value, whole);
				return true;
			// The faction's share of the world's victory points at least the value, both thousandths of a percent; a country
			// in no faction - or no country, a province's scope - asks the null faction's (factions.md, section 7).
			case "faction_progress":
				answer = Wars.CFactionProgress.Share(state, country?.Faction ?? "") >= value.Amount.Raw;
				return true;
			case "faction":
				answer = InFaction(state, trigger, scope, country);
				return true;
			case "relation":
				answer = Felt(state, country, RelationTarget(state, trigger, scope), threat: false)
					>= CFixedPoint.FromInt(CConditions.WholeOf(CConditions.Field(trigger, "value")?.Amount ?? CFixedPoint.Zero));
				return true;
			case "threat":
				answer = Felt(state, country, ThreatTarget(state, trigger, scope), threat: true)
					>= CFixedPoint.FromInt(CConditions.WholeOf(CConditions.Field(trigger, "value")?.Amount ?? CFixedPoint.Zero));
				return true;
			case "is_threatend":
				answer = country is not null && IsThreatened(state, country, value.Amount);
				return true;
			case "neutrality":
				answer = (country?.EffectiveNeutrality ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "base_neutrality":
				answer = (country?.Neutrality ?? CFixedPoint.Zero) >= value.Amount;
				return true;
			case "neighbour":
				answer = state.Country(CConditions.TagDirect(trigger, scope)) is { } named && country is not null
					&& CCountryLists.Neighbours(state, named).Contains(country);
				return true;
			case "can_create_vassals":
				// The value is not read.
				answer = country is not null && CCountryLists.PossibleVassals(state, country).Count > 0;
				return true;
			case "is_possible_vassal":
				// This and from are not read, and ask about none.
				answer = country is not null && CEventScope.Known(value.Tag) is { } vassal
					&& CCountryLists.PossibleVassals(state, country).Exists(c => c.Tag == vassal);
				return true;
			default:
				answer = false;
				return false;
		}
	}

	/// <summary>
	/// <c>has_cb = { type = T actor = A }</c>: whether a country at war with the scope's holds a goal of type T
	/// against it - the actor's war first, then every enemy owning a province, so the actor restricts nothing.
	/// An unknown type is false (<c>war.md</c>, section 9).
	/// </summary>
	private static bool HasCb(CGameState state, CTrigger trigger, CEventScope scope, CCountry us)
	{
		Wars.CCasusBelliType type = Wars.CCasusBellis.Find(state, CConditions.Field(trigger, "type")?.Text);
		if (!type.IsReal)
		{
			return false;
		}
		bool Holds(CCountryTag? holder)
		{
			return holder is { } enemy && Wars.CWarSides.Between(state, us.Tag, enemy) is { } war
				&& war.GoalsOf(enemy).Any(g => g.Recipient == us.Tag && g.Type == type);
		}
		CScriptArgument? actor = CConditions.Field(trigger, "actor");
		CCountryTag? named = actor is null ? null
			: actor.IsThis ? scope.Root.Country : actor.IsFrom ? scope.FromCountry : CEventScope.Known(actor.Tag);
		if (Holds(named))
		{
			return true;
		}
		foreach (CCountry enemy in CCountryLists.Enemies(state, us))
		{
			if (CCountryFacts.Of(state).OwnedCount(enemy.Tag) > 0 && Holds(enemy.Tag))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether the two are enemies: never a country and itself; at war; or either is the rebels.
	/// </summary>
	public static bool IsEnemy(CGameState state, CCountryTag? us, CCountryTag? them)
	{
		if (us == them)
		{
			return false;
		}
		if (us?.Value == Rebels || them?.Value == Rebels)
		{
			return true;
		}
		return us is { } one && them is { } other && CCountryLists.AtWar(state, one, other);
	}

	/// <summary>
	/// The scope's relation with, or the threat it feels from, another country: a scope with no
	/// country, or another that is no country, holds nothing (<c>docs/mechanics/diplomacy.md</c>,
	/// section 7).
	/// </summary>
	private static CFixedPoint Felt(CGameState state, CCountry? us, CCountryTag? them, bool threat)
	{
		if (us is null || state.Country(them) is not { } other)
		{
			return CFixedPoint.Zero;
		}
		CDiplomacyStatus status = CGameState.Status(us, other);
		return threat ? status.Threat : status.Relation;
	}

	/// <summary><c>relation</c>'s <c>who</c>: THIS and FROM fall back to a province's controller.</summary>
	internal static CCountryTag? RelationTarget(CGameState state, CTrigger trigger, CEventScope scope)
	{
		if (CConditions.Field(trigger, "who") is not { } who)
		{
			return null;
		}
		if (who.IsThis)
		{
			return scope.Root.Country ?? state.Province(scope.Root.Province).Controller;
		}
		if (who.IsFrom)
		{
			return scope.FromCountry ?? state.Province(scope.Root.FromProvince).Controller;
		}
		return CEventScope.Known(who.Tag);
	}

	/// <summary><c>threat</c>'s <c>which</c>: THIS by owner, FROM the scope's own.</summary>
	private static CCountryTag? ThreatTarget(CGameState state, CTrigger trigger, CEventScope scope)
	{
		if (CConditions.Field(trigger, "which") is not { } which)
		{
			return null;
		}
		return which.IsThis ? CConditions.ThisByOwner(state, scope) : which.IsFrom ? scope.FromCountry : CEventScope.Known(which.Tag);
	}

	/// <summary>Whether a country on the scope's neighbour list poses it a threat of at least <paramref name="least"/>.</summary>
	private static bool IsThreatened(CGameState state, CCountry country, CFixedPoint least)
	{
		foreach (CCountry neighbour in CCountryLists.Neighbours(state, country))
		{
			if (CGameState.Status(country, neighbour).Threat >= least)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A count's bar: the number, or nought where <c>this</c> or <c>from</c> was written.</summary>
	private static long Written(CScriptArgument value, long whole)
	{
		return value.IsThis || value.IsFrom ? 0 : whole;
	}

	/// <summary>
	/// The IC the country would have with resources enough over its enemies' in thousandths, or
	/// null where the answer is false outright. The country is the scope's, or else FROM's.
	/// </summary>
	private static long? EnemyIcRatio(CGameState state, CEventScope scope)
	{
		if (state.Country(scope.Country ?? scope.FromCountry) is not { } country || !state.IsAtWar(country.Tag))
		{
			return null;
		}
		long sum = 0;
		foreach (CCountry enemy in CCountryLists.Enemies(state, country))
		{
			long theirs = enemy.MaxIc * (long)CFixedPoint.Scale;
			if (theirs == 0)
			{
				return null;
			}
			sum += theirs;
		}
		if (sum <= 0)
		{
			return 0;
		}
		return country.MaxIc * (long)CFixedPoint.Scale * CFixedPoint.Scale / sum;
	}

	/// <summary><c>faction</c>: we are a country, and in that faction - or in THIS's.</summary>
	private static bool InFaction(CGameState state, CTrigger trigger, CEventScope scope, CCountry? country)
	{
		if (country is null)
		{
			return false;
		}
		string faction = trigger.Argument.IsThis
			? state.Country(CConditions.ThisByOwner(state, scope))?.Faction ?? ""
			: trigger.Argument.Text;
		return faction.Length > 0 && !trigger.Argument.IsWord("none")
			&& country.Faction.Equals(faction, StringComparison.Ordinal);
	}
}
