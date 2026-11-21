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

	/// <summary>
	/// Every country's ministers made afresh as a saved game is loaded, as the original's load makes them: the list's
	/// placeholder country first, whose five draw too, then each country in the list's order, its foreign, production,
	/// intelligence, technology and politics minister in turn, each a draw on the AI's generator - a played country's only
	/// for the areas handed over - and a foreign minister's first work skipped. See <c>docs/mechanics/savegame.md</c>,
	/// section 2; <c>reversing/findings/FINDINGS-liveread.md</c>, section 106.
	/// </summary>
	public static void MakeMinistersOfLoad(CGameState state) => MakeMinisters(state);

	/// <summary>
	/// Every country's five ministers made, a new game's as a loaded game's: the original makes them before a game is
	/// entered either way, the same draws in the same order (<c>docs/mechanics/ministers.md</c>, section 1).
	/// </summary>
	public static void MakeMinisters(CGameState state)
	{
		for (int i = 0; i < PlaceholderMinisters; i++)
		{
			state.AiRandom.Next();
		}
		foreach (CCountry country in state.Countries)
		{
			country.ForeignCountdown = Made(state, country, EAutomation.Diplomacy, 0, 3);
			country.ForeignMinister.SkipOnce = country.ForeignCountdown > 0;
			country.ProductionCountdown = Made(state, country, EAutomation.Production, 0, 3);
			country.IntelligenceCountdown = Made(state, country, EAutomation.Intelligence, 0, 10);
			country.TechCountdown = Made(state, country, EAutomation.Technology, 0, 3);
			country.PoliticsCountdown = Made(state, country, EAutomation.Politics, 0, 3);
		}
	}

	/// <summary>The ministers of the original's placeholder country, which comes before every other in its list.</summary>
	private const int PlaceholderMinisters = 5;

	private static int Made(CGameState state, CCountry country, EAutomation area, int countdown, int hours)
	{
		if (!AiRuns(state, country, area))
		{
			return 0;
		}
		return countdown > 0 ? countdown : state.AiRandom.Next(country.Tag) % hours + 1;
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
