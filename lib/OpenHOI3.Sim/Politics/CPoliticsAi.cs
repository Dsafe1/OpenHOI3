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
	/// How many times a new game's laws are looked at, at most. The start save holds where repeating it stops - three
	/// passes for Germany - and what repeats it was not found; ours.
	/// </summary>
	public const int MostLawPasses = 10;

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
				? state.AiRandom.Next() % 3 + 1 : 0;
		}
	}

	/// <summary>
	/// The changes of law a new game's look posted, run once its two first days are, as the original's queue runs them
	/// after the days - so the days are run under the laws the new game brought forward, and each change then holds the
	/// country's brigades to the running game's ceilings. Then the look again, and its changes, until a look posts
	/// nothing (ours: what repeats it in the original was not found). Answers how many looks there were, the one
	/// <see cref="SetUpAtStart"/> made among them; none where nothing was held.
	/// </summary>
	public static int RunNewGameLaws(CGameState state)
	{
		if (state.NewGameLaws.Count == 0)
		{
			return 0;
		}
		var posted = new List<CCommand>(state.NewGameLaws);
		state.NewGameLaws.Clear();
		int passes = 1;
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
			if (posted.Count == 0 || passes == MostLawPasses || state.AiScripts is null)
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
		foreach (CCountry country in state.Countries)
		{
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
