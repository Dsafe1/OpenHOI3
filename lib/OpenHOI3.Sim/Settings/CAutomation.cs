using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Settings;

/// <summary>
/// Who does a country's work in an area: the AI for a country nobody plays, and for a played one the area the player has
/// handed to it - its minister then made for that country and working as any other's. See
/// <c>docs/mechanics/frontend.md</c>, section 4; <c>reversing/findings/FINDINGS-frontend.md</c>, D.3.
/// </summary>
public static class CAutomation
{
	/// <summary>Whether the AI does the country's work in the area.</summary>
	public static bool AiRuns(CGameState state, CCountry country, EAutomation area)
	{
		return !state.Played.Contains(country.Tag) || HandedOver(state, area);
	}

	/// <summary>Whether the player has handed the area to the AI; the tech minister by the Technology setting.</summary>
	public static bool HandedOver(CGameState state, EAutomation area)
	{
		return area == EAutomation.Technology ? state.Automation.Technology != 0 : state.Automation.Has(area);
	}

	/// <summary>
	/// Each played country's five ministers made or dropped again by what the player has handed over, as the original's
	/// delegation switch does when an area is handed over or taken back - the screens' arrows, and here a loaded save
	/// played as another country or set otherwise in the lobby. One the AI now runs and that had none is made with the
	/// draw a new minister makes on the game's generator, its first work one to three hours on (the intelligence minister's
	/// one to ten, and a foreign minister's first work skipped, as at the start), in the order foreign, production,
	/// intelligence, technology, politics; one it no longer runs is dropped; the rest are left as they were. A country
	/// nobody plays keeps all five. See <c>docs/mechanics/frontend.md</c>, section 4.3.
	/// </summary>
	public static void SetMinisters(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (!state.Played.Contains(country.Tag))
			{
				continue;
			}
			int foreign = country.ForeignCountdown;
			country.ForeignCountdown = Made(state, country, EAutomation.Diplomacy, foreign, 3);
			if (foreign <= 0 && country.ForeignCountdown > 0)
			{
				country.ForeignMinister.SkipOnce = true;
			}
			country.ProductionCountdown = Made(state, country, EAutomation.Production, country.ProductionCountdown, 3);
			country.IntelligenceCountdown = Made(state, country, EAutomation.Intelligence, country.IntelligenceCountdown, 10);
			country.TechCountdown = Made(state, country, EAutomation.Technology, country.TechCountdown, 3);
			country.PoliticsCountdown = Made(state, country, EAutomation.Politics, country.PoliticsCountdown, 3);
		}
	}

	private static int Made(CGameState state, CCountry country, EAutomation area, int countdown, int hours)
	{
		if (!AiRuns(state, country, area))
		{
			return 0;
		}
		return countdown > 0 ? countdown : state.AiRandom.Next() % hours + 1;
	}

	/// <summary>The tech minister's two halves for the country: the leadership sliders and the research.</summary>
	public static (bool Sliders, bool Research) TechHalves(CGameState state, CCountry country)
	{
		if (!state.Played.Contains(country.Tag))
		{
			return (true, true);
		}
		return state.Automation.Technology switch
		{
			1 => (true, true),
			2 => (false, true),
			3 => (true, false),
			_ => (false, false),
		};
	}
}
