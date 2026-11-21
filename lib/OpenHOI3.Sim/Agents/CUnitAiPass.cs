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
		// The original runs the countries' passes side by side, and a small country's is done before most of a great power's
		// orders are given; so what one pass reads of another country's units is where the AI had them bound as the hour
		// began - but for Germany's, given before the worker threads' countries count (ShowOrdersOfTheHour).
		foreach (CCountry country in state.Countries)
		{
			foreach (Units.CUnit unit in country.AllUnits())
			{
				unit.AiBoundAtHour = unit.AiBound;
			}
		}
		var sideBySide = new List<Ai.CAiRun>();
		foreach (CCountry country in state.Countries)
		{
			country.UnitAi.InRevolt = revolting.Contains(country.Tag);
			if (country.AiPaused || country.Tag.Value == Revolts.CRebelFactions.Rebels || country.Strategy.TheatresDirty
				|| (Countries.COwnedCounts.Of(state).Count(country.Tag) == 0 && !country.InExile))
			{
				continue;
			}
			var run = new Ai.CAiRun(state, country, Salt);
			if (country.Index > FirstOnMainThread)
			{
				sideBySide.Add(run);
				continue;
			}
			run.Run((s, c) => Run(s, c, hour));
			run.Finish();
			ShowOrdersOfTheHour(country);
		}
		RunSideBySide(state, sideBySide, hour);
	}

	/// <summary>The salt of the unit pass's runs' generators (<see cref="Ai.CAiRun"/>).</summary>
	private const uint Salt = 0x554E4954;

	/// <summary>How many threads at most run the countries side by side: as many as are worth it (section 2, *Ours*).</summary>
	private const int Workers = 8;

	/// <summary>
	/// The passes of every country after the main thread's first, side by side on worker threads, as the original's: each
	/// reads the world as the main thread's countries left it, and what each posts is put after the rest in the countries'
	/// order once all are done - so it comes out as their passes in turn would. What a pass asks for that is made on first
	/// use, and would be made by the first to ask, is made before (<c>docs/mechanics/agents.md</c>, section 2, *Ours*).
	/// In turn instead where the game asks for it, and where a tool traces the AI or plays back the original's draws.
	/// </summary>
	private static void RunSideBySide(CGameState state, List<Ai.CAiRun> runs, int hour)
	{
		if (runs.Count == 0)
		{
			return;
		}
		_ = state.Positions;
		Supply.CSupplyNetworks.Refresh(state);
		if (state.AiInTurn || state.AiTrace is not null || state.SharedAiRandom.Feed is not null || runs.Count == 1)
		{
			foreach (Ai.CAiRun run in runs)
			{
				run.Run((s, c) => Run(s, c, hour));
			}
		}
		else
		{
			var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Workers };
			try
			{
				System.Threading.Tasks.Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, runs.Count, 1), options,
					range =>
					{
						for (int i = range.Item1; i < range.Item2; i++)
						{
							runs[i].Run((s, c) => Run(s, c, hour));
						}
					});
			}
			catch (System.AggregateException failed) when (failed.InnerExceptions.Count >= 1)
			{
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failed.InnerExceptions[0]).Throw();
			}
		}
		foreach (Ai.CAiRun run in runs)
		{
			run.Finish();
		}
	}

	/// <summary>
	/// The list's first countries, REB and Germany, the original's main thread runs before the worker threads are under way;
	/// the main thread's other three, Britain, the Soviet Union and the United States, begin after nearly every worker's
	/// country (<c>reversing/findings/FINDINGS-liveread.md</c>, sections 111 and 143).
	/// </summary>
	private const int FirstOnMainThread = 1;

	/// <summary>
	/// After a country's pass: whether the later countries' passes read the orders it gave this hour, or where its units
	/// were bound as the hour began. The first countries of the original's main thread give theirs while the worker
	/// threads' countries are still to count, so those are read as given; every other country's are read as the hour began,
	/// its pass running side by side with the rest (<c>docs/mechanics/moving.md</c>, section 7.3).
	/// </summary>
	public static void ShowOrdersOfTheHour(CCountry country)
	{
		if (country.Index > FirstOnMainThread)
		{
			return;
		}
		foreach (Units.CUnit unit in country.AllUnits())
		{
			unit.AiBoundAtHour = unit.AiBound;
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
			// A headquarters its player has handed to the AI since the last pass is taken up (agents.md, section 1).
			CAgents.Adopt(state, country);
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
			CWeakEnemyPorts.Rebuild(state, country);
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
