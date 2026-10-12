using System;
using OpenHOI3.Sim.Countries;

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
			case "enemy_ic_ratio":
				answer = EnemyIcRatio(state, scope) is { } ratio && ratio <= value.Amount.Raw;
				return true;
			case "alliance_with":
				answer = scope.Country is { } one && CConditions.TagByOwner(state, trigger, scope) is { } other
					&& CCountryLists.HasAlliance(state, one, other);
				return true;
			case "is_subject":
				answer = CConditions.Is(trigger, country?.Overlord is not null);
				return true;
			case "vassal_of":
				answer = country?.Overlord is { } overlord && overlord == CConditions.TagByOwner(state, trigger, scope);
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
			case "faction":
				answer = InFaction(state, trigger, scope, country);
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
