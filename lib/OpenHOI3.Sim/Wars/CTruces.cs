using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A truce, the original's <c>CDiplomacyStatus::HasTruce</c>: not at war, and the holder's <c>last_war</c>
/// towards the other within the last 2400 hours - a figure in the code, not a define. See
/// <c>docs/mechanics/war.md</c>, section 4.
/// </summary>
public static class CTruces
{
	/// <summary>A hundred days.</summary>
	public const long Hours = 2400;

	public static bool Has(CGameState state, CCountry holder, CCountry other)
	{
		if (CCountryLists.AtWar(state, holder.Tag, other.Tag) || CGameState.Status(holder, other).LastWar is not { } last)
		{
			return false;
		}
		return last <= state.TotalHours && last + Hours > state.TotalHours;
	}
}
