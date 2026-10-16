using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Research;

/// <summary>
/// The AI's research, for the countries nobody plays: each is given a tech minister as a new game is made, which first
/// works after one to three hours and then once a day, in an hour of its country's own, running the install's Lua
/// <c>TechMinister_Tick</c> - which sets the leadership shares and fills the free research slots. And a technology of
/// several levels whose level is researched is taken off such a country's queue again. See
/// <c>docs/mechanics/ministers.md</c>, sections 1 to 3.
/// </summary>
public static class CResearchAi
{
	/// <summary>The tech minister's place in the day: its country's number plus three.</summary>
	private const int HourOffset = 3;

	/// <summary>
	/// The production and tech ministers of every country nobody plays, the production minister's first: a draw of one
	/// to three hours each on the game's generator, as the original's are made in that order.
	/// </summary>
	public static void SetUpAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			bool played = state.Played.Contains(country.Tag);
			country.ProductionCountdown = played ? 0 : state.Random.Next() % 3 + 1;
			country.TechCountdown = played ? 0 : state.Random.Next() % 3 + 1;
		}
	}

	/// <summary>
	/// The hour's work of every tech minister, in the countries' order: one whose country owns land or is in exile counts
	/// down to its first work, and from then on works every hour - but only does anything in its hour of the day,
	/// <c>(number + 3) mod 24</c>. Nothing while a game is being made, and nothing for a country a person plays.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		foreach (CCountry country in state.Countries)
		{
			if (!Works(state, country, country.TechCountdown))
			{
				continue;
			}
			if (--country.TechCountdown > 0)
			{
				continue;
			}
			country.TechCountdown = 1;
			if (Hour(country, HourOffset) == hour)
			{
				state.AiScripts?.RunTech(state, country);
			}
		}
	}

	/// <summary>
	/// What a tech minister does on hearing that a level of a technology of several levels was researched: it posts the
	/// technology's removal from the queue, which the research put it back on.
	/// </summary>
	public static void LevelResearched(CGameState state, CCountry country, IEnumerable<CTechnology> technologies)
	{
		if (country.TechCountdown <= 0)
		{
			return;
		}
		foreach (CTechnology technology in technologies)
		{
			state.Outbox.Add(new Commands.CStopResearchCommand(country.Tag, technology.Name));
		}
	}

	/// <summary>Whether a minister of a country works this hour at all: it has one, nobody plays it, and it owns land or is in exile.</summary>
	public static bool Works(CGameState state, CCountry country, int countdown)
	{
		return countdown > 0 && !state.Played.Contains(country.Tag)
			&& (COwnedCounts.Of(state).Count(country.Tag) > 0 || country.InExile);
	}

	/// <summary>
	/// The hour of the day a country's minister works in: its number in the original's list - which counts its
	/// placeholder, so Germany is 2 - plus the minister's offset, of 24.
	/// </summary>
	public static int Hour(CCountry country, int offset) => (country.Index + 1 + offset) % CGameState.HoursPerDay;
}
