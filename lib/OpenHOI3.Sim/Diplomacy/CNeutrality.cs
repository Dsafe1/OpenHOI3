using System;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Diplomacy;

/// <summary>
/// A country's neutrality, and its effective neutrality - less the threat it feels from its
/// most-threatening country. See <c>docs/mechanics/diplomacy.md</c>, section 6.
/// </summary>
public static class CNeutrality
{
	public const string ChangeModifier = "neutrality_change";

	private static readonly CFixedPoint Most = CFixedPoint.FromInt(100);

	/// <summary>
	/// Each country's day: neutrality held between 0 and 100, effective neutrality made, and then
	/// neutrality moved by the country's <c>neutrality_change</c>.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.Neutrality = CFixedPoint.Clamp(country.Neutrality, CFixedPoint.Zero, Most);
			UpdateEffective(state, country);
			Change(state, country, country.Modifier[ChangeModifier]);
		}
	}

	/// <summary>Adds to neutrality, floored at 0 and not capped, and makes effective neutrality again.</summary>
	public static void Change(CGameState state, CCountry country, CFixedPoint change)
	{
		country.Neutrality = CFixedPoint.Max(country.Neutrality + change, CFixedPoint.Zero);
		UpdateEffective(state, country);
	}

	/// <summary>
	/// The most effective neutrality a country may have and still declare war on the other: the larger
	/// threat between them, less <c>NEUT_INCREASE_DIFF_CONTINENT</c> where their capitals are on
	/// different continents and the other is no neighbour, and its faction's neutrality, floored at 0
	/// (section 6). <c>NEUT_REDUCTION_AT_CLAIMS</c> is added where the country may use the casus belli for
	/// its cores, <c>aquire_all_cores</c>, on the other and its goal is to be had (<c>war.md</c>, section 5).
	/// </summary>
	public static CFixedPoint WarCeiling(CGameState state, CCountry country, CCountry other)
	{
		CFixedPoint ceiling = CFixedPoint.Max(CFixedPoint.Max(CGameState.Status(country, other).Threat,
			CGameState.Status(other, country).Threat), CFixedPoint.Zero);
		Wars.CCasusBelliType claims = Wars.CCasusBellis.Find(state, ClaimsType);
		if (claims.IsReal && Wars.CCasusBellis.CanUse(state, country, other, claims)
			&& Wars.CWarGoals.IsAvailable(state, new Wars.CWarGoal(claims, country.Tag, other.Tag), null))
		{
			ceiling += state.Defines[Section, AtClaims];
		}
		if (OnOtherContinents(state, country, other) && !CCountryLists.Neighbours(state, country).Contains(other))
		{
			ceiling -= state.Defines[Section, DifferentContinent];
		}
		ceiling += CAlignment.FactionGroup(state, country)?.FactionNeutrality ?? CFixedPoint.Zero;
		return CFixedPoint.Max(ceiling, CFixedPoint.Zero);
	}

	public const string Section = "diplomacy";
	public const string DifferentContinent = "NEUT_INCREASE_DIFF_CONTINENT";
	public const string AtClaims = "NEUT_REDUCTION_AT_CLAIMS";

	/// <summary>The casus belli type whose use raises the ceiling - so spelled in the original's code.</summary>
	public const string ClaimsType = "aquire_all_cores";

	/// <summary>Whether the two capitals are on different continents.</summary>
	public static bool OnOtherContinents(CGameState state, CCountry country, CCountry other)
	{
		return !string.Equals(state.Province(country.Capital).Continent, state.Province(other.Capital).Continent,
			StringComparison.Ordinal);
	}

	/// <summary>Neutrality less the threat felt from the most-threatening country, held between 0 and 100.</summary>
	public static void UpdateEffective(CGameState state, CCountry country)
	{
		CFixedPoint threat = state.Country(country.MostThreatening) is { } feared
			? CGameState.Status(country, feared).Threat
			: CFixedPoint.Zero;
		country.EffectiveNeutrality = CFixedPoint.Clamp(country.Neutrality - threat, CFixedPoint.Zero, Most);
	}
}
