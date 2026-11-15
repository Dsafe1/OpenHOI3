using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// The AI's politics. A new game has every country's laws looked at by the install's Lua, the player's too, the changes
/// made once the game's two first days are run, and gives each country nobody plays a politics minister. The minister works once a day, in an hour of its country's own: on the 1st, 8th, 15th, 22nd and
/// 29th it fills the empty posts, and then the Lua's <c>PoliticsMinister_Tick</c> runs. See
/// <c>docs/mechanics/politics.md</c>, section 12.
/// </summary>
public static class CPoliticsAi
{
	/// <summary>
	/// How many times a new game's laws are looked at: once, as the original asks the script once in each country's
	/// set-up. Measured on the 1939 bookmark's start save, whose Soviet Union the look takes one step, from
	/// <c>volunteer_army</c> to <c>one_year_draft</c>, where a second would take it on; until 2026-11-15 the look was
	/// repeated until it posted nothing, at most ten times, which gives the 1936 start save's laws the same, 707 of 707
	/// either way (politics.md, section 12).
	/// </summary>
	public const int MostLawPasses = 1;

	/// <summary>
	/// A new game's politics: <c>Laws(country)</c> for every country, the player's too, in the countries' order - the
	/// changes it posts made free and silent, the game being made, and <b>held</b> in <see cref="CGameState.NewGameLaws"/>
	/// until the game's two first days are run (<see cref="RunNewGameLaws"/>); then each country nobody plays is given a
	/// politics minister, which first works after one to three hours, a draw on the game's generator each.
	/// </summary>
	public static void SetUpAtStart(CGameState state)
	{
		state.NewGameLaws.Clear();
		if (state.AiScripts is not null)
		{
			state.NewGameLaws.AddRange(LookAtLaws(state));
		}
		foreach (CCountry country in state.Countries)
		{
			country.PoliticsCountdown = Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Politics)
				? state.AiRandom.Next(country.Tag) % 3 + 1 : 0;
		}
	}

	/// <summary>
	/// The changes of law a new game's look posted, run once its two first days are, as the original's queue runs them
	/// after the days - so the days are run under the laws the new game brought forward, and each change then holds the
	/// country's brigades to the running game's ceilings - and after them, in the order they were posted, <b>everything
	/// else the entry posted</b>, the days' among it: the original's queue holds them behind the look's, and its drain
	/// asks each whether it may still run before running it. So a country leaving a war in the two days gives up the
	/// laws only a war allowed (China at the 1939 bookmark), and the AI's sliders set in the days stand on the first
	/// screen. Then the look again, and its changes, while a look posts something and <see cref="MostLawPasses"/>
	/// allows - which is once. Answers how many looks there were, the one <see cref="SetUpAtStart"/> made among them;
	/// none where nothing was held.
	/// </summary>
	public static int RunNewGameLaws(CGameState state)
	{
		var posted = new List<CCommand>(state.NewGameLaws);
		int passes = posted.Count > 0 ? 1 : 0;
		state.NewGameLaws.Clear();
		posted.AddRange(state.Outbox);
		state.Outbox.Clear();
		while (true)
		{
			foreach (CCommand command in posted)
			{
				if (command.MayExecute(state))
				{
					command.Execute(state);
				}
			}
			state.Outbox.Clear();
			if (passes == 0 || posted.Count == 0 || passes == MostLawPasses || state.AiScripts is null)
			{
				return passes;
			}
			passes++;
			posted = LookAtLaws(state);
		}
	}

	/// <summary>
	/// One look at every country's laws by the Lua's <c>Laws(country)</c>, the player's too, as the original asks the
	/// script for every country it sets up for a new game (<c>docs/compatibility.md</c>, entry 6, reversed on 2026-11-02):
	/// the changes it posts, made while the game counts as being made, so free and silent.
	/// </summary>
	private static List<CCommand> LookAtLaws(CGameState state)
	{
		var posted = new List<CCommand>();
		if (state.AiScripts is not { } scripts)
		{
			return posted;
		}
		bool was = state.IsBeingMade;
		state.IsBeingMade = true;
		// Each country is asked in its turn of the original's set-up, when a country whose turn is still to come has no IC
		// worked out - so a law's enemy_ic_ratio the script asks is false while any enemy comes later (section 13).
		int[] worked = new int[state.Countries.Count];
		foreach (CCountry country in state.Countries)
		{
			worked[country.Index] = country.MaxIc;
			country.MaxIc = 0;
		}
		foreach (CCountry country in state.Countries)
		{
			country.MaxIc = worked[country.Index];
			scripts.RunLaws(state, country);
			posted.AddRange(state.Outbox);
			state.Outbox.Clear();
		}
		state.IsBeingMade = was;
		return posted;
	}

	/// <summary>
	/// The hour's work of every politics minister, in the countries' order: one whose country owns land or is in exile
	/// counts down to its first work, and from then on works every hour it is asked - but only does anything in its
	/// hour of the day, <c>(number + 17) mod 24</c>. Nothing while a game is being made, and nothing for a country a
	/// person plays.
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
			if (country.AiPaused || country.PoliticsCountdown <= 0 || !Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Politics)
				|| (COwnedCounts.Of(state).Count(country.Tag) == 0 && !country.InExile))
			{
				continue;
			}
			if (--country.PoliticsCountdown > 0)
			{
				continue;
			}
			country.PoliticsCountdown = 1;
			if (Hour(country) != hour)
			{
				continue;
			}
			// The day of the month counted from nought: the 1st, 8th, 15th, 22nd and 29th.
			if ((state.Date.Day - 1) % 7 == 0)
			{
				CMinisterPosts.FillVacant(state, country);
			}
			state.AiScripts?.RunPolitics(state, country);
		}
	}

	/// <summary>
	/// The hour of the day a country's minister works in: its number plus 17, of 24. *Ours*: the number is its place
	/// among the countries plus one, the original's null country being 0 - the original's own numbering was not read.
	/// </summary>
	public static int Hour(CCountry country) => (country.Index + 1 + 17) % CGameState.HoursPerDay;
}
