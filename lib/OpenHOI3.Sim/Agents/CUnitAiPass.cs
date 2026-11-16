using System.Collections.Generic;
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
		var revolting = new HashSet<CCountryTag>();
		foreach (Provinces.CProvince province in state.Provinces)
		{
			if (province.Owner is { } owner && province.Controller?.Value == Revolts.CRebelFactions.Rebels)
			{
				revolting.Add(owner);
			}
		}
		foreach (CCountry country in state.Countries)
		{
			country.UnitAi.InRevolt = revolting.Contains(country.Tag);
			if (country.AiPaused || country.Tag.Value == Revolts.CRebelFactions.Rebels || country.Strategy.TheatresDirty
				|| (Countries.COwnedCounts.Of(state).Count(country.Tag) == 0 && !country.InExile))
			{
				continue;
			}
			Run(state, country, hour);
		}
	}

	/// <summary>
	/// Every country the AI runs given its agents, before a loaded game's first pass: in the running original they are
	/// there when that pass begins (<c>docs/mechanics/savegame.md</c>, section 2).
	/// </summary>
	public static void MakeAgentsOfLoad(CGameState state)
	{
		// The theatres' fronts are made again first: a save written straight after entering has them in another order, which
		// is the order the positions of one score come in (FINDINGS-liveread.md, section 18).
		foreach (CCountry country in state.Countries)
		{
			if (country.Strategy.Theatres.Count > 0)
			{
				Theatres.CTheatreRefresh.FrontsOfLoad(state, country);
			}
		}
		foreach (CCountry country in state.Countries)
		{
			if (state.Played.Contains(country.Tag))
			{
				country.UnitAi.RunsUnits = false;
			}
			if (!country.UnitAi.RunsUnits || country.AiPaused || country.Tag.Value == Revolts.CRebelFactions.Rebels
				|| (Countries.COwnedCounts.Of(state).Count(country.Tag) == 0 && !country.InExile))
			{
				continue;
			}
			// Made and not bound: the first pass ticks them with no unit and binds them after, so each runs its land block in
			// the second (FINDINGS-liveread.md, section 39).
			CAgents.CreateForTheatres(state, country, bind: false);
		}
	}

	/// <summary>
	/// Every country's pass forgotten, for a loaded game's second pass before its first hour: the running original makes
	/// two, at the one hour (<c>CTick.RunSecondOfLoad</c>).
	/// </summary>
	public static void ForgetLastPass(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.UnitAi.LastPassHour = -1;
		}
	}

	private static void Run(CGameState state, CCountry country, int hour)
	{
		CUnitAi ai = country.UnitAi;
		if (ai.LastPassHour == state.TotalHours)
		{
			return;
		}
		ai.LastPassHour = state.TotalHours;
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
		foreach (Units.CUnit unit in country.AllUnits())
		{
			unit.AiBound = -1;
		}
		int number = country.Index + 1;
		if (hour == number % CGameState.HoursPerDay)
		{
			CAgentProfile.Time(state, "the sea targets", () => CSeaTargets.Assign(state, country));
		}
		foreach (CAgent agent in ai.Agents.ToArray())
		{
			CAgentTick.Run(state, country, agent);
		}
		foreach (CInvasion invasion in ai.Invasions.ToArray())
		{
			CAgentProfile.Time(state, "the invasions", () => CInvasionTick.Run(state, country, invasion));
		}
		CInvasionPlanner.Prune(state, country);
		if (hour == 1 && state.Date.Day == 1)
		{
			ai.FailedAreas.Clear();
		}
		if (!ai.RunsUnits)
		{
			return;
		}
		CAgents.CreateForTheatres(state, country);
		CAgents.BindAndAttachOrphans(state, country);
		if (hour == number % CGameState.HoursPerDay)
		{
			// The day's powers are worked out in the balancing, which waits for the pass to have run once.
			if (ai.UnitPassDone)
			{
				CAgentProfile.Time(state, "the balancing", () => CTheatreBalance.Run(state, country));
			}
			CPlanObjectives.RebuildPriorityProvinces(state, country);
			foreach (CAgent agent in ai.Agents)
			{
				CPlanObjectives.Generate(state, country, agent);
			}
			CAgentProfile.Time(state, "the invasion plans", () =>
			{
				CInvasionPlanner.Rank(state, country);
				CInvasionPlanner.Plan(state, country);
			});
		}
		if (hour == (number + 8) % CGameState.HoursPerDay)
		{
			CAgentProfile.Time(state, "the transports", () => CFleetMovers.Gather(state, country));
			CAgentProfile.Time(state, "the leaders", () => CLeaderPass.Run(state, country));
		}
		if (!ai.UnitPassDone || hour == (number + 16) % CGameState.HoursPerDay)
		{
			CAgentProfile.Time(state, "the recall", () => CStrandedRecall.Run(state, country));
		}
		if (hour == (number + 16) % CGameState.HoursPerDay)
		{
			CAgentProfile.Time(state, "the promotions", () => CLeaderPromotion.Run(state, country));
			CAgentProfile.Time(state, "the fleet movers", () => CFleetMovers.Distribute(state, country));
			CAirDistribution.Run(state, country);
		}
		CAgentProfile.Time(state, "the stances", () => CPlanStances.Update(state, country, force: hour == (number + 16) % CGameState.HoursPerDay));
		ai.UnitPassDone = true;
	}
}
