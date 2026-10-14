using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Politics;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on a country's government, its parties and its ministers: <c>docs/mechanics/politics.md</c>,
/// section 8. A name is matched case and all, as the original matches one.
/// </summary>
public static class CPoliticsConditions
{
	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		CCountry? country = state.Country(scope.Country);
		CScriptArgument value = trigger.Argument;
		CIdeology? ruling = country is null ? null : state.Ideology(country.RulingIdeology);
		switch (trigger.Keyword)
		{
			case "government":
				answer = country is not null && state.Governments.ContainsKey(value.Text)
					&& country.GovernmentType.Equals(value.Text, StringComparison.Ordinal);
				return true;
			case "ideology":
				answer = ruling is not null && ruling.Name.Equals(value.Text, StringComparison.Ordinal);
				return true;
			case "ideology_group":
				// The original faults on a country with no ruling party; here it is false.
				answer = ruling?.Group is { } group && group.Name.Equals(value.Text, StringComparison.Ordinal);
				return true;
			case "organisation":
				answer = ruling is not null && country!.Organisation[ruling.Index] >= value.Amount.Raw;
				return true;
			case "popularity":
				answer = ruling is not null && country!.Popularity[ruling.Index] >= value.Amount.Raw;
				return true;
			case "minister_alive":
				answer = FindMinister(state, country, (int)CConditions.WholeOf(value.Amount)) is { } minister && IsAlive(state, minister);
				return true;
			case "has_removable_minister":
				answer = CConditions.Is(trigger, country is not null && HasRemovable(state, country));
				return true;
			// The strategic-warfare impacts, a figure a month each (politics.md, section 6).
			case "strat_convoy_impact":
				answer = (country?.StrategicWarfare.ConvoyImpact ?? 0) >= value.Amount.Raw;
				return true;
			case "strat_allies_impact":
				answer = (country?.StrategicWarfare.AlliesImpact ?? 0) >= value.Amount.Raw;
				return true;
			case "strat_bomb_impact":
				answer = (country?.StrategicWarfare.BombingImpact ?? 0) >= value.Amount.Raw;
				return true;
			default:
				answer = false;
				return false;
		}
	}

	/// <summary>
	/// <c>&lt;ideology&gt; = { organisation = x popularity = y }</c>: that party's organisation at least x, or its
	/// popularity at least y - each a whole number, nought where it is not written, so a block naming one alone always
	/// holds.
	/// </summary>
	public static bool IdeologyBlock(CGameState state, CTrigger trigger, CEventScope scope)
	{
		if (state.Country(scope.Country) is not { } country || state.Ideology(trigger.Key) is not { } ideology)
		{
			return false;
		}
		long organisation = CConditions.WholeOf(CConditions.Field(trigger, "organisation")?.Amount ?? CFixedPoint.Zero) * 1000;
		long popularity = CConditions.WholeOf(CConditions.Field(trigger, "popularity")?.Amount ?? CFixedPoint.Zero) * 1000;
		return country.Organisation[ideology.Index] >= organisation || country.Popularity[ideology.Index] >= popularity;
	}

	/// <summary>
	/// Whether today is within his start and death dates, inclusive. Ours, following the start save: a minister
	/// with no death date is alive from his start (<c>politics.md</c>, section 7).
	/// </summary>
	public static bool IsAlive(CGameState state, CMinister minister)
	{
		return minister.StartDate <= state.Date && (minister.DeathDate == CDate.Zero || state.Date <= minister.DeathDate);
	}

	/// <summary>
	/// A minister by his id: the country's own, else any country's. Ours: the original's ids are the whole
	/// game's.
	/// </summary>
	public static CMinister? FindMinister(CGameState state, CCountry? country, int id)
	{
		if (country is not null && country.Ministers.TryGetValue(id, out CMinister? own))
		{
			return own;
		}
		foreach (CCountry other in state.Countries)
		{
			if (other.Ministers.TryGetValue(id, out CMinister? found))
			{
				return found;
			}
		}
		return null;
	}

	/// <summary>Whether a minister holds a changeable post and no fixed one.</summary>
	private static bool HasRemovable(CGameState state, CCountry country)
	{
		foreach ((string post, CMinister minister) in country.Government)
		{
			if (!IsChangeable(state, post))
			{
				continue;
			}
			bool holdsFixed = false;
			foreach ((string other, CMinister holder) in country.Government)
			{
				if (holder == minister && !IsChangeable(state, other))
				{
					holdsFixed = true;
					break;
				}
			}
			if (!holdsFixed)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsChangeable(CGameState state, string post)
	{
		foreach (CGovernmentPosition position in state.GovernmentPositions)
		{
			if (position.Name.Equals(post, StringComparison.OrdinalIgnoreCase))
			{
				return position.Changeable;
			}
		}
		return false;
	}
}
