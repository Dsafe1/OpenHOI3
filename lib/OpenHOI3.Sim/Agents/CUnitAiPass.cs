using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Each country's AI pass over its units, every hour once a game runs: the gate that waits for its theatres'
/// headquarters, its agents' hours, and for a country the AI runs, agents made and bound, the orphans attached, the
/// powers and objectives at its hour and the stances. The unit half of the original's <c>ProcessAI</c>. See
/// <c>docs/mechanics/agents.md</c>, section 2.
/// </summary>
public static class CUnitAiPass
{
	public static void RunHourly(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		foreach (CCountry country in state.Countries)
		{
			if (country.Tag.Value == Revolts.CRebelFactions.Rebels || country.Strategy.TheatresDirty
				|| (Countries.COwnedCounts.Of(state).Count(country.Tag) == 0 && !country.InExile))
			{
				continue;
			}
			Run(state, country, hour);
		}
	}

	private static void Run(CGameState state, CCountry country, int hour)
	{
		CUnitAi ai = country.UnitAi;
		if (state.Played.Contains(country.Tag))
		{
			ai.RunsUnits = false;
		}
		CAgents.Prune(state, country);
		if (ai.Waiting)
		{
			bool ready = true;
			foreach (Theatres.CTheatre theatre in country.Strategy.Theatres)
			{
				if (Theatres.CTheatres.Headquarters(state, theatre) is null)
				{
					ready = false;
					break;
				}
			}
			if (ready)
			{
				// Every unit put under the theatre it stands in, and the pass ends (agents.md, section 2.1).
				CTheatreSort.Sort(state, country);
				ai.Waiting = false;
				return;
			}
		}
		foreach (CAgent agent in ai.Agents.ToArray())
		{
			CAgentTick.Run(state, country, agent);
		}
		if (!ai.RunsUnits)
		{
			return;
		}
		CAgents.CreateForTheatres(state, country);
		CAgents.BindAndAttachOrphans(state, country);
		int number = country.Index + 1;
		if (hour == number % CGameState.HoursPerDay)
		{
			// The day's powers are worked out in the balancing, which waits for the pass to have run once.
			if (ai.UnitPassDone)
			{
				CTheatreBalance.Run(state, country);
			}
			CPlanObjectives.RebuildPriorityProvinces(state, country);
			foreach (CAgent agent in ai.Agents)
			{
				CPlanObjectives.Generate(state, country, agent);
			}
		}
		if (hour == (number + 16) % CGameState.HoursPerDay)
		{
			CAirDistribution.Run(state, country);
		}
		CPlanStances.Update(state, country, force: hour == (number + 16) % CGameState.HoursPerDay);
		ai.UnitPassDone = true;
	}
}
