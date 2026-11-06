using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Politics;

/// <summary>
/// The AI's politics, for the countries nobody plays - never the player's (<c>docs/compatibility.md</c>, entry 6). A
/// new game has every such country's laws looked at by the install's Lua until they stop moving, and gives each a
/// politics minister. The minister works once a day, in an hour of its country's own: on the 1st, 8th, 15th, 22nd and
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
	/// A new game's politics for the countries nobody plays: <c>Laws(country)</c> for each, in the countries' order,
	/// the changes it posts made at once - free and silent while the game is being made - and again until a pass
	/// posts nothing; then each is given a politics minister, which first works after one to three hours, a draw on
	/// the game's generator each. Answers how many passes were run.
	/// </summary>
	public static int SetUpAtStart(CGameState state)
	{
		int passes = 0;
		if (state.AiScripts is { } scripts)
		{
			bool was = state.IsBeingMade;
			state.IsBeingMade = true;
			while (passes < MostLawPasses)
			{
				passes++;
				var posted = new List<CCommand>();
				// Every country, the player's too, as the original asks the script for every country it sets up for a new
				// game (docs/compatibility.md, entry 6, reversed on 2026-11-02).
				foreach (CCountry country in state.Countries)
				{
					scripts.RunLaws(state, country);
					posted.AddRange(state.Outbox);
					state.Outbox.Clear();
				}
				foreach (CCommand command in posted)
				{
					if (command.MayExecute(state))
					{
						command.Execute(state);
					}
				}
				state.Outbox.Clear();
				if (posted.Count == 0)
				{
					break;
				}
			}
			state.IsBeingMade = was;
		}
		foreach (CCountry country in state.Countries)
		{
			country.PoliticsCountdown = Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Politics)
				? state.Random.Next() % 3 + 1 : 0;
		}
		return passes;
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
			if (country.PoliticsCountdown <= 0 || !Settings.CAutomation.AiRuns(state, country, Settings.EAutomation.Politics)
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
