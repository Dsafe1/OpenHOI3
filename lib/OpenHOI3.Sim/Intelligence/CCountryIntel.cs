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

	/// <summary>The minister's key the original reads nowhere: "Intel during Peace".</summary>
	public const string PeaceIntel = "peace_offmap_intel";

	/// <summary>The minister's key the original reads nowhere: "Political Intel".</summary>
	public const string PoliticalIntel = "offmap_political_intel";

	/// <summary>The figure, in thousandths: one spy 100, ten 1000, less the other's counter-intelligence.</summary>
	public static long Factor(CCountry us, CCountry them)
	{
		return CGameState.Presence(us, them).Level * 1000 / CSpyPresence.MostLevel - them.Modifier[CounterIntelligence].Raw;
	}

	/// <summary>
	/// The figure as a game reads it: the original's, and under an enhanced game's fix <c>minister_intel</c> with the
	/// two modifiers the original reads nowhere added - <c>offmap_political_intel</c> towards every country, and
	/// <c>peace_offmap_intel</c> towards a country ours is not at war with. The rule is ours, what they were meant to do
	/// being nowhere in the original: each is worth what that much of a spy's share is, 0.1 one spy
	/// (<c>docs/mechanics/intelligence.md</c>, section 1).
	/// </summary>
	public static long Factor(CGameState state, CCountry us, CCountry them)
	{
		return Factor(us, them) + Bonus(state, us, them);
	}

	/// <summary>What the fix <c>minister_intel</c> adds to the figure, in thousandths; nothing without it.</summary>
	public static long Bonus(CGameState state, CCountry us, CCountry them)
	{
		if (!state.Rules.Has(Rules.EFix.MinisterIntel) || us == them)
		{
			return 0;
		}
		long bonus = us.Modifier[PoliticalIntel].Raw;
		if (!CCountryLists.IsEnemy(state, us, them.Tag))
		{
			bonus += us.Modifier[PeaceIntel].Raw;
		}
		return bonus;
	}

	/// <summary>Whether we hold less than one whole spy there.</summary>
	public static bool HasNone(CCountry us, CCountry them) => CGameState.Presence(us, them).Level < 1000;
}
