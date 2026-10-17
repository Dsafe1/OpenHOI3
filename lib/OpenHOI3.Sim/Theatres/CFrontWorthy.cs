using System;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Theatres;

/// <summary>
/// Whether a country is an enemy another draws fronts against: the original's <c>IsFrontWorthyEnemy</c>, with the
/// compiled-in table of who is whose by tag. See <c>docs/mechanics/theatres.md</c>, section 4.
/// </summary>
public static class CFrontWorthy
{
	/// <summary>The alignment distance from which a country in no faction counts another as an enemy, in thousandths.</summary>
	private const long FarEnough = 180000;

	private const string Democracy = "democracy";

	private static readonly string[] SovietTargets =
		{ "FIN", "ROM", "POL", "EST", "LAT", "LIT", "SWE", "HUN", "BUL", "TUR", "YUG", "SLO", "CZE", "AUS", "MAN", "JAP" };

	private static readonly string[] GermanTargets = { "HOL", "BEL", "LUX", "POL", "DEN" };

	private static readonly string[] JapaneseTargets =
		{ "CHC", "CHI", "CGX", "CSX", "KOR", "MAN", "MEN", "SIK", "CXB", "CYN" };

	public static bool Is(CGameState state, CCountry country, CCountryTag other)
	{
		if (other == country.Tag)
		{
			return false;
		}
		CCountry? them = state.Country(other);
		if (them?.Overlord is { } overlord)
		{
			return Is(state, country, overlord);
		}
		if (IsWarCandidate(state, country, other))
		{
			return true;
		}
		if (CCountryLists.IsFriend(state, country, other, withAccess: true))
		{
			return false;
		}
		if (country.Overlord is { } ours && state.Country(ours) is { } master && CCountryLists.HasAlliance(state, master.Tag, other))
		{
			return false;
		}
		if (CWarTargets.IsPreparingWarWith(state, country, other))
		{
			return true;
		}
		string theirFaction = them?.Faction ?? "";
		if (country.Faction.Length == 0)
		{
			return InNoFaction(state, country, other, them, theirFaction);
		}
		if (theirFaction.Length > 0 && theirFaction != country.Faction)
		{
			return true;
		}
		bool oneFaction = theirFaction == country.Faction;
		switch (country.Tag.Value)
		{
			case "SOV":
				return !oneFaction && Array.IndexOf(SovietTargets, other.Value) >= 0;
			case "GER":
				return !oneFaction && Array.IndexOf(GermanTargets, other.Value) >= 0;
			case "FRA":
				if (!oneFaction && other.Value == "ITA")
				{
					return true;
				}
				return other.Value is "BEL" or "LUX" && GermanyLeavesIt(state, country, other, them);
			case "BEL":
				return other.Value == "LUX" && GermanyLeavesIt(state, country, other, them);
			case "ENG":
				return !oneFaction && other.Value == "ITA";
			case "JAP":
				return !oneFaction && Array.IndexOf(JapaneseTargets, other.Value) >= 0;
			default:
				return false;
		}
	}

	/// <summary>
	/// The original's <c>IsWarCandidate</c>: an enemy; or not a friend (without military access) and either at war with a
	/// country of the other's faction or the target of a prepared war.
	/// </summary>
	public static bool IsWarCandidate(CGameState state, CCountry country, CCountryTag other)
	{
		if (CCountryLists.IsEnemy(state, country, other))
		{
			return true;
		}
		if (CCountryLists.IsFriend(state, country, other, withAccess: false))
		{
			return false;
		}
		if (state.Country(other) is { Faction.Length: > 0 } them)
		{
			foreach (CCountry enemy in CCountryLists.Enemies(state, country))
			{
				if (enemy.Faction == them.Faction)
				{
					return true;
				}
			}
		}
		return CWarTargets.IsPreparingWarWith(state, country, other);
	}

	private static bool InNoFaction(CGameState state, CCountry country, CCountryTag other, CCountry? them, string theirFaction)
	{
		if (country.Tag.Value == "FIN")
		{
			switch (other.Value)
			{
				case "NOR":
				case "GER":
					return false;
				case "SOV":
					return true;
			}
		}
		if (country.Tag.Value == "JAP" && Array.IndexOf(JapaneseTargets, other.Value) >= 0)
		{
			return true;
		}
		foreach (CCountry enemy in CCountryLists.Enemies(state, country))
		{
			if (enemy.Faction.Length > 0)
			{
				return false;
			}
		}
		if (theirFaction.Length > 0 && CStrategyFigures.GroupOfFaction(state, theirFaction) is { } group
			&& !group.Name.Equals(Democracy, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (other.Value is "JAP" or "ITA" && them is { Overlord: null })
		{
			return true;
		}
		return AlignmentDistance(country, them) >= FarEnough;
	}

	/// <summary>
	/// The original's distance on the triangle as this test works it out - with the x difference twice and the y never, in
	/// single precision: <c>floor((sqrt((d*d/1000 + d*d/1000) / 1000) + 0.0005) * 1000)</c>.
	/// </summary>
	public static long AlignmentDistance(CCountry country, CCountry? them)
	{
		long d = (int)(country.AlignmentX - (them?.AlignmentX ?? 0));
		int sum = (int)(d * d / 1000) + (int)(d * d / 1000);
		float root = MathF.Sqrt((float)((double)(float)sum / 1000.0));
		float lifted = (float)(root + 0.0005);
		float scaled = (float)(lifted * 1000.0);
		return (long)MathF.Floor(scaled);
	}

	/// <summary>
	/// France's Belgium and Luxembourg, and Belgium's Luxembourg: not the country's subject, and Germany owning land, outside
	/// its faction and not at war with it.
	/// </summary>
	private static bool GermanyLeavesIt(CGameState state, CCountry country, CCountryTag other, CCountry? them)
	{
		if (them?.Overlord == country.Tag)
		{
			return false;
		}
		CCountry? germany = state.Country(new CCountryTag("GER"));
		if (germany is null || COwnedCounts.Of(state).Count(germany.Tag) < 1 || germany.Faction == country.Faction)
		{
			return false;
		}
		return !CCountryLists.IsEnemy(state, germany, other);
	}
}
