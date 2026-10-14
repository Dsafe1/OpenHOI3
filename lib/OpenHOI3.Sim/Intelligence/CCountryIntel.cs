using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Espionage;

namespace OpenHOI3.Sim.Intelligence;

/// <summary>
/// How well a country knows another: its spies there over the most, less the other's <c>counter_intelligence</c>, and
/// nothing else - no floor and no other term. See <c>docs/mechanics/intelligence.md</c>, section 1.
/// </summary>
public static class CCountryIntel
{
	public const string CounterIntelligence = "counter_intelligence";

	/// <summary>The figure, in thousandths: one spy 100, ten 1000, less the other's counter-intelligence.</summary>
	public static long Factor(CCountry us, CCountry them)
	{
		return CGameState.Presence(us, them).Level * 1000 / CSpyPresence.MostLevel - them.Modifier[CounterIntelligence].Raw;
	}

	/// <summary>Whether we hold less than one whole spy there.</summary>
	public static bool HasNone(CCountry us, CCountry them) => CGameState.Presence(us, them).Level < 1000;
}
