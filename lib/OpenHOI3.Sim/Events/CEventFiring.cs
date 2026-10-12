using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Sim.Events;

/// <summary>
/// An event firing: who answers it, how the AI chooses, the <c>immediate</c> block, and
/// <c>fire_only_once</c>. See <c>docs/mechanics/events.md</c>, sections 4 to 6.
/// </summary>
public static class CEventFiring
{
	/// <summary>How long an open event waits for a person before it answers itself.</summary>
	public const int MonthsToAnswer = 4;

	/// <summary>
	/// Fires an event whose trigger has been asked. A country nobody plays has its answer posted;
	/// a person's has the event opened, unless that event is open already; and the <c>immediate</c>
	/// block runs either way.
	/// </summary>
	public static void Fire(CGameState state, CEvent fired, CEventScope scope, CScriptReport? report = null)
	{
		if (fired.FireOnlyOnce && state.FiredOnce.Contains(fired))
		{
			return;
		}
		state.RaiseEventFired(fired, scope);
		CCountryTag? answers = scope.Country ?? state.Province(scope.Province).Owner;
		if (answers is { } person && state.Played.Contains(person))
		{
			if (!state.OpenEvents.Exists(open => open.Event == fired))
			{
				long deadline = state.Date.AddMonths(MonthsToAnswer).DayNumber * (long)CGameState.HoursPerDay + state.Hour;
				state.OpenEvents.Add(new COpenEvent(fired, scope.Detached(), person, state.TotalHours, deadline));
			}
		}
		else if (state.Country(answers) is not null)
		{
			int option = ChooseOption(state, fired, scope, report);
			state.Outbox.Add(new CSelectEventOptionCommand(fired, scope.Detached(), option));
		}

		CEffects.Run(state, fired.Immediate.Effects, scope, report);
		if (fired.FireOnlyOnce)
		{
			state.FiredOnce.Add(fired);
		}
	}

	/// <summary>
	/// The AI's option: each option's share of a hundred by its weight, cut to whole numbers, and the
	/// first whose running sum a roll of 0 to 99 does not pass - the first option where the weights
	/// come to nothing or the roll passes them all. The roll is the game's generator's
	/// (<c>docs/compatibility.md</c>, entry 11).
	/// </summary>
	public static int ChooseOption(CGameState state, CEvent fired, CEventScope scope, CScriptReport? report = null)
	{
		float sum = 0f;
		foreach (CEventOption option in fired.Options)
		{
			sum += CEventChance.Weight(state, option.AiChance, scope, report);
		}
		if (!(sum > 0f))
		{
			return 0;
		}
		float share = (float)(sum / 100.0);
		int roll = state.Random.Next() % 100;
		long running = 0;
		for (int i = 0; i < fired.Options.Count; i++)
		{
			running += (long)(CEventChance.Weight(state, fired.Options[i].AiChance, scope, report) / share);
			if (roll <= running)
			{
				return i;
			}
		}
		return 0;
	}
}
