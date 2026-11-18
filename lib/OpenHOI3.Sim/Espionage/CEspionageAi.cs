using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Espionage;

/// <summary>
/// The AI's spying, for the countries nobody plays: each is given an intelligence minister as a new game is made, which
/// first works after one to ten hours and then once a day, in an hour of its country's own, running the install's Lua
/// <c>IntelligenceMinister_Tick</c>. See <c>docs/mechanics/espionage.md</c>, section 15.
/// </summary>
public static class CEspionageAi
{
	/// <summary>Every country nobody plays gets a minister, its first work a draw of one to ten hours on the game's generator.</summary>
	public static void SetUpAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.IntelligenceCountdown = Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Intelligence)
				? state.AiRandom.Next(country.Tag) % 10 + 1 : 0;
		}
	}

	/// <summary>
	/// The hour's work of every intelligence minister, in the countries' order: one whose country owns land or is in exile
	/// counts down to its first work, and from then on works every hour - but only does anything in its hour of the day,
	/// <c>(number + 1) mod 24</c>. Nothing while a game is being made, and nothing for a country a person plays.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		var due = new System.Collections.Generic.List<CCountry>();
		foreach (CCountry country in state.Countries)
		{
			if (country.AiPaused || country.IntelligenceCountdown <= 0 || !Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Intelligence)
				|| (COwnedCounts.Of(state).Count(country.Tag) == 0 && !country.InExile))
			{
				continue;
			}
			if (--country.IntelligenceCountdown > 0)
			{
				continue;
			}
			country.IntelligenceCountdown = 1;
			if (Hour(country) == hour)
			{
				due.Add(country);
			}
		}
		// Side by side, as the original's AI pass runs them (ministers.md, *Ours*).
		state.AiScripts?.RunSideBySide(state, due, Salt, (scripts, country) => scripts.RunIntelligence(state, country));
	}

	/// <summary>Which minister a run of the intelligence minister is, for the seed of its own generator on a worker.</summary>
	private const uint Salt = 2;

	/// <summary>
	/// The hour of the day a country's minister works in: its number in the original's list - which counts its
	/// placeholder, so Germany is 2 - plus one, of 24: Germany's is 03:00.
	/// </summary>
	public static int Hour(CCountry country) => (country.Index + 1 + 1) % CGameState.HoursPerDay;
}
