using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's hour: its area set again when marked and weekly, its plan posted, its command province, its commands made
/// and filled at its hour, and with a unit its focus areas, front coverage, subordinates, aircraft, headquarters and its
/// fleets' and air groups' regrouping. The original's <c>CAIUnit::Tick</c>, as far as this piece goes. See
/// <c>docs/mechanics/agents.md</c>, section 3.
/// </summary>
public static class CAgentTick
{
	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		int hour = (int)(state.TotalHours % CGameState.HoursPerDay);
		CDate date = state.Date;
		int dayOfYear = date.DayNumber - new CDate(date.Year, 1, 1).DayNumber;
		int number = country.Index + 1;
		int a0 = number % CGameState.HoursPerDay;
		int a3 = (number + 3) % CGameState.HoursPerDay;
		int a6 = (number + 6) % CGameState.HoursPerDay;
		int a9 = (number + 9) % CGameState.HoursPerDay;
		bool full = hour == a6 || agent.FullPassPending;

		if (agent.AreaDirty || (hour == 1 && dayOfYear % 7 == 0))
		{
			CAgentArea.Set(state, country, agent);
		}
		if ((hour == a0 || !agent.PlanPosted) && agent.Unit is not null && country.UnitAi.RunsUnits)
		{
			agent.PlanPosted = true;
		}
		CCommandProvince.Choose(state, country, agent, force: hour == a0 && dayOfYear % 9 == 0);
		if (hour == a3)
		{
			CCommandLadder.Organise(state, country, agent);
		}
		if (agent.Unit is not { } unit)
		{
			return;
		}
		if (hour == a9 && CAgents.TheatreOf(state, country, agent) is { } theatre)
		{
			CFocusAreas.Rebuild(state, country, agent, theatre);
		}
		CFrontCoverage.Update(state, country, agent);
		if (agent.ReplanSubordinates || hour == a9 || full)
		{
			CSubordinates.Sort(state, country, agent);
		}
		if (full)
		{
			if (unit.Kind == EUnitKind.Theatre)
			{
				CAircraftWants.Update(state, country, agent);
			}
			agent.FullPassPending = false;
			CCommandProvince.PullIn(state, country, agent);
		}
		CAirRegroup.Run(state, country, agent, force: full);
		if (full || agent.ReplanNaval || state.IsAtWar(country.Tag))
		{
			CFleetRegroup.Run(state, country, agent);
		}
		CCommandProvince.March(state, country, agent);
	}
}
