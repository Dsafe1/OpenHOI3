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

		if (agent.AreaDirty || (hour == 1 && dayOfYear % 7 == 0))
		{
			CAgentProfile.Time(state, "the area", () => CAgentArea.Set(state, country, agent));
		}
		if ((hour == a0 || !agent.PlanPosted) && agent.Unit is not null && country.UnitAi.RunsUnits)
		{
			CPlanAxis.Update(state, country, agent, agent.Unit.Plan.Stance);
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
		// The full pass is asked for here, after the area was set: an area set this hour makes this hour's pass full
		// (FINDINGS-liveread.md, section 83).
		bool full = hour == a6 || agent.FullPassPending;
		CAgentProfile.Time(state, "the coverage", () => CFrontCoverage.Update(state, country, agent));
		if (agent.ReplanSubordinates || hour == a9 || full)
		{
			CAgentProfile.Time(state, "the subordinates", () => CSubordinates.Sort(state, country, agent));
		}
		CAgentProfile.Time(state, "the threatened", () => CThreatenedProvinces.Update(state, country, agent));
		if (full)
		{
			CAiExpeditions.Run(state, country, agent);
		}
		if (CHoldProvinces.IsDue(state, country, agent, full))
		{
			CAgentProfile.Time(state, "the hold list", () => CHoldProvinces.Rebuild(state, country, agent));
		}
		if (full)
		{
			if (unit.Kind == EUnitKind.Theatre)
			{
				CAgentProfile.Time(state, "the aircraft", () => CAircraftWants.Update(state, country, agent));
			}
		}
		// At war, in a war nobody declared, or with provinces in revolt (FINDINGS-liveread.md, section 25).
		bool fighting = state.IsAtWar(country.Tag) || Wars.CUndeclaredWars.Involves(state, country.Tag);
		bool landHour = full || (hour % 3 == 0 && (fighting || country.UnitAi.InRevolt));
		if (landHour)
		{
			CAgentProfile.Time(state, "the land block", () => CLandBlock.Run(state, country, agent));
		}
		if (full)
		{
			agent.FullPassPending = false;
			CCommandProvince.PullIn(state, country, agent);
		}
		// Daily, at once while its flag is set, or every hour for a country the AI does not run (FINDINGS-moving.md, section D;
		// FINDINGS-liveread.md, section 43).
		if (hour == a6 || agent.ReassignAreasNow || !country.UnitAi.RunsUnits)
		{
			CAgentProfile.Time(state, "the sealift", () => CSealift.Run(state, country, agent));
		}
		CAgentProfile.Time(state, "the air bases", () => CAirBaseLists.Build(state, country, agent, full));
		CAgentProfile.Time(state, "the air", () => CAirBody.Run(state, country, agent, full));
		bool forced = full || agent.ReplanNaval;
		bool regroup = forced || fighting;
		if (country.UnitAi.RunsUnits || unit.Agent is not null)
		{
			int before = state.Outbox.Count;
			CAgentProfile.Time(state, "the fleets", () => CNavalBody.Run(state, country, agent, regroup, forced));
			if (state.AiTrace is { } trace && trace.Country == country.Tag)
			{
				for (int index = before; index < state.Outbox.Count; index++)
				{
					trace.Write($"    fleets ({agent.Unit?.Name}): {state.Outbox[index]}");
				}
			}
		}
		else if (regroup)
		{
			CAgentProfile.Time(state, "the fleets", () => CFleetRegroup.Run(state, country, agent));
		}
		CCommandProvince.March(state, country, agent);
		if (landHour)
		{
			CAgentProfile.Time(state, "the attack", () => CLandAttack.Run(state, country, agent));
		}
	}

	/// <summary>A province the country owns held by the rebels: the original's <c>provinces_in_revolt</c> above nought.</summary>
	public static bool InRevolt(CGameState state, CCountry country)
	{
		foreach (Provinces.CProvince province in state.Provinces)
		{
			if (province.Owner == country.Tag && province.Controller?.Value == Revolts.CRebelFactions.Rebels)
			{
				return true;
			}
		}
		return false;
	}
}
