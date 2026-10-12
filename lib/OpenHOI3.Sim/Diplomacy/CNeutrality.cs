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

	/// <summary>Neutrality less the threat felt from the most-threatening country, held between 0 and 100.</summary>
	public static void UpdateEffective(CGameState state, CCountry country)
	{
		CFixedPoint threat = state.Country(country.MostThreatening) is { } feared
			? CGameState.Status(country, feared).Threat
			: CFixedPoint.Zero;
		country.EffectiveNeutrality = CFixedPoint.Clamp(country.Neutrality - threat, CFixedPoint.Zero, Most);
	}
}
