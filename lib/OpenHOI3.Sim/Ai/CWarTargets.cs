using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// A country's prepared wars, as the Lua's <c>CAIStrategy</c> methods make, drop and ask of them, and the pruning of the
/// small ones while a great one is prepared. See <c>docs/mechanics/foreignminister.md</c>, section 4.
/// </summary>
public static class CWarTargets
{
	/// <summary>More whole IC of its provinces than this makes a target great, as being a major does.</summary>
	private const int GreatIc = 100;

	/// <summary>
	/// <c>PrepareWarDecision</c>: nothing against the null tag, a country owning no province, the country's own puppet or a
	/// member of its faction; otherwise a new entry at the end where the target has none, and then the entry's value set -
	/// so a listed target keeps its first decision, limit and hour.
	/// </summary>
	public static void Prepare(CGameState state, CCountry country, CCountryTag target, int valueBits,
		Script.CDecision? decision, bool limited)
	{
		if (state.Country(target) is not { } other || COwnedCounts.Of(state).Count(target) <= 0
			|| other.Overlord == country.Tag || InItsFaction(country, other))
		{
			return;
		}
		CWarTarget? entry = Find(country, target);
		if (entry is null)
		{
			entry = new CWarTarget(target, decision, limited, state.TotalHours);
			country.WarTargets.Add(entry);
		}
		entry.ValueBits = valueBits;
	}

	/// <summary><c>CancelPrepareWar</c>: the target's first entry taken off.</summary>
	public static void Cancel(CCountry country, CCountryTag target)
	{
		if (Find(country, target) is { } entry)
		{
			country.WarTargets.Remove(entry);
		}
	}

	/// <summary>The target's first entry, or none.</summary>
	public static CWarTarget? Find(CCountry country, CCountryTag target)
	{
		foreach (CWarTarget entry in country.WarTargets)
		{
			if (entry.Target == target)
			{
				return entry;
			}
		}
		return null;
	}

	/// <summary>
	/// <c>IsPreparingWar</c>: an entry whose target owns a province or is in exile, is not the country's puppet and is not
	/// in its faction.
	/// </summary>
	public static bool IsPreparingWar(CGameState state, CCountry country)
	{
		foreach (CWarTarget entry in country.WarTargets)
		{
			if (state.Country(entry.Target) is { } other
				&& (COwnedCounts.Of(state).Count(entry.Target) > 0 || other.InExile)
				&& other.Overlord != country.Tag && !InItsFaction(country, other))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// <c>IsPreparingWarWith</c>: false at war with it, or where it owns no province or is in exile; else whether it is
	/// listed.
	/// </summary>
	public static bool IsPreparingWarWith(CGameState state, CCountry country, CCountryTag target)
	{
		if (CCountryLists.IsEnemy(state, country, target) || COwnedCounts.Of(state).Count(target) <= 0
			|| state.Country(target) is not { InExile: false })
		{
			return false;
		}
		return Find(country, target) is not null;
	}

	/// <summary>
	/// The pruning, for a country that is a major or has more than 100 IC: with a great target listed - the last one, a
	/// major or more than 100 IC - every small target prepared more than a month before it, or more than three months
	/// ago, is taken off. With none, nothing.
	/// </summary>
	public static void Prune(CGameState state, CCountry country)
	{
		CCountryTag? great = null;
		int greatMonth = 0;
		foreach (CWarTarget entry in country.WarTargets)
		{
			if (IsGreat(state, entry.Target))
			{
				great = entry.Target;
				greatMonth = Month(entry.Hour);
			}
		}
		if (great is null)
		{
			return;
		}
		int thisMonth = Month(state.TotalHours);
		country.WarTargets.RemoveAll(entry => !IsGreat(state, entry.Target) && entry.Target != great
			&& (greatMonth - Month(entry.Hour) > 1 || thisMonth - Month(entry.Hour) > 3));
	}

	/// <summary>Whether the pruning runs for a country: a major, or more than 100 whole IC of its provinces.</summary>
	public static bool Prunes(CCountry country) => country.IsMajor || country.MaxIc > GreatIc;

	private static bool IsGreat(CGameState state, CCountryTag tag) => state.Country(tag) is { } other && Prunes(other);

	/// <summary>A date's month counted from year 0: twelve a year and the month of this one.</summary>
	private static int Month(long hour)
	{
		CDate date = CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay));
		return date.Year * 12 + date.Month - 1;
	}

	/// <summary>
	/// The prepared wars checked as a strategy is made: each kept where its target owns a province or is in exile, is not
	/// the country's puppet and is not in its faction, and - with no decision - the country could declare war on it at all,
	/// or - with one - the decision's <c>potential</c> holds for it (aistrategy.md, section 9.4).
	/// </summary>
	public static void Recheck(CGameState state, CCountry country)
	{
		country.WarTargets.RemoveAll(entry => !Stands(state, country, entry));
	}

	private static bool Stands(CGameState state, CCountry country, CWarTarget entry)
	{
		if (state.Country(entry.Target) is not { } target
			|| (COwnedCounts.Of(state).Count(entry.Target) <= 0 && !target.InExile)
			|| target.Overlord == country.Tag || InItsFaction(country, target))
		{
			return false;
		}
		if (entry.Decision is { } decision)
		{
			return Script.CConditions.Holds(state, decision.Potential, Script.CEventScope.Start(country.Tag));
		}
		return Wars.CDeclarations.IsSelectable(state, country, target);
	}

	private static bool InItsFaction(CCountry country, CCountry other) =>
		country.Faction.Length > 0 && other.Faction == country.Faction;
}
