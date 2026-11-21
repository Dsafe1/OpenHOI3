using System.Collections.Generic;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Wars;

/// <summary>
/// The victory conditions: which are chosen for a new game, the day's test of them, and the game finished by them.
/// See <c>docs/mechanics/factions.md</c>, sections 8 and 9.
/// </summary>
public static class CVictoryConditions
{
	/// <summary>The defines' block the two counts are in.</summary>
	public const string Section = "country";

	/// <summary>How many conditions each faction picks: <c>NUMBER_OF_VICTORY_CONDITIONS</c>, whole.</summary>
	public static int Number(CGameState state) => (int)(state.Defines[Section, "NUMBER_OF_VICTORY_CONDITIONS"].Raw / 1000);

	/// <summary>How many met conditions win: <c>VICTORY_CONDITIONS_TO_WIN</c>, whole.</summary>
	public static int ToWin(CGameState state) => (int)(state.Defines[Section, "VICTORY_CONDITIONS_TO_WIN"].Raw / 1000);

	/// <summary>
	/// A new game's choice: each faction's list walked in order, each condition chosen where it is primary and each
	/// primary spending one of the count, the walk stopping when none is left. A faction left short of the count is
	/// filled as the original fills it: once for each one missing, while any is unchosen, a draw's signed remainder
	/// by the number unchosen picks that many back from the last unchosen condition - and a remainder below nought
	/// takes the list's last condition, chosen or not. The original draws on a generator of its own seeded from the
	/// wall clock; here it is one seeded from the game's seed, so that every machine makes the same game. Answers the
	/// factions so filled.
	/// </summary>
	public static List<string> ChooseAtStart(CGameState state)
	{
		var shortOf = new List<string>();
		int number = Number(state);
		CRandom? random = null;
		foreach ((string faction, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			if (state.VictoryChoicesMade.Contains(faction))
			{
				continue;
			}
			int budget = number;
			for (int i = 0; i < conditions.Count && budget > 0; i++)
			{
				conditions[i].Chosen = conditions[i].Primary;
				if (conditions[i].Primary)
				{
					budget--;
				}
			}
			if (budget > 0 && conditions.Exists(c => !c.Chosen))
			{
				shortOf.Add(faction);
				random ??= new CRandom(state.Seed);
				Fill(conditions, budget, random);
			}
		}
		state.GameFinished = false;
		return shortOf;
	}

	private static void Fill(List<CVictoryCondition> conditions, int missing, CRandom random)
	{
		for (int i = 0; i < missing; i++)
		{
			int unchosen = conditions.FindAll(c => !c.Chosen).Count;
			if (unchosen == 0)
			{
				return;
			}
			int pick = random.Next() % unchosen;
			if (pick < 0)
			{
				conditions[^1].Chosen = true;
				continue;
			}
			for (int at = conditions.Count - 1; at >= 0; at--)
			{
				if (conditions[at].Chosen)
				{
					continue;
				}
				if (pick-- == 0)
				{
					conditions[at].Chosen = true;
					break;
				}
			}
		}
	}

	/// <summary>A faction's choice made in the lobby: these conditions chosen and no others. A choice also clears a finished game, as the original's does.</summary>
	public static void SetChosen(CGameState state, string faction, ICollection<string> names)
	{
		foreach ((string name, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			if (name != faction)
			{
				continue;
			}
			foreach (CVictoryCondition condition in conditions)
			{
				condition.Chosen = names.Contains(condition.Name);
			}
		}
		state.VictoryChoicesMade.Add(faction);
		state.GameFinished = false;
	}

	/// <summary>
	/// The last thing of the day: each chosen condition is met while its trigger holds, asked with no country in scope,
	/// and dated the day it starts to; then, unless the game is finished, the first faction with
	/// <c>VICTORY_CONDITIONS_TO_WIN</c> met finishes it. Nobody is kept as its winner.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		CEventScope scope = CEventScope.Start(null);
		var counts = new List<int>();
		foreach ((string _, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			int met = 0;
			foreach (CVictoryCondition condition in conditions)
			{
				if (!condition.Chosen)
				{
					continue;
				}
				bool held = CConditions.Holds(state, condition.Trigger, scope);
				if (held)
				{
					met++;
					if (!condition.Met)
					{
						condition.Date = state.TotalHours;
					}
				}
				condition.Met = held;
			}
			counts.Add(met);
		}
		if (state.GameFinished)
		{
			return;
		}
		int toWin = ToWin(state);
		foreach (int met in counts)
		{
			if (met >= toWin)
			{
				state.GameFinished = true;
				return;
			}
		}
	}

	/// <summary>How many of the faction's chosen conditions are met.</summary>
	public static int Met(CGameState state, string faction) => Count(state, faction, met: true);

	/// <summary>How many conditions the faction has chosen.</summary>
	public static int Chosen(CGameState state, string faction) => Count(state, faction, met: false);

	private static int Count(CGameState state, string faction, bool met)
	{
		foreach ((string name, List<CVictoryCondition> conditions) in state.VictoryConditions)
		{
			if (name == faction)
			{
				return conditions.FindAll(c => c.Chosen && (!met || c.Met)).Count;
			}
		}
		return 0;
	}
}
