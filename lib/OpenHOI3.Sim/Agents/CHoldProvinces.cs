using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The provinces an agent wants held, best first: its plan's objectives on its side, the priority provinces it holds or
/// is at peace over, and - for a theatre - its puppets' unguarded focus areas and its theatre's provinces worth a tenth;
/// cut to its divisions. The original's <c>CAIUnit::RebuildHoldProvinces</c>. See
/// <c>docs/mechanics/moving.md</c>, section 5.
/// </summary>
public static class CHoldProvinces
{
	private const long Whole = 1000;

	private const long LeastScore = 100;

	public static bool IsDue(CGameState state, CCountry country, CAgent agent, bool full)
	{
		return !agent.HoldProvincesMade || agent.Threatened.Count > 0
			|| (full && (state.Date.Day % 7 == 0 || !country.UnitAi.RunsUnits));
	}

	public static void Rebuild(CGameState state, CCountry country, CAgent agent)
	{
		agent.HoldProvinces.Clear();
		var seen = new HashSet<int>();
		var context = CProvinceHoldValue.Context(state, country);
		int wanted = 0;
		if (agent.Unit is { } unit)
		{
			foreach (CObjective objective in unit.Plan.Objectives)
			{
				CProvince province = state.Province(objective.Province);
				if (!CApproaches.IsFriendly(state, country, province.Controller) || !seen.Add(province.Id))
				{
					continue;
				}
				Insert(agent, province.Id, CHoldScore.Of(state, country, agent, province, context) + objective.Priority * Whole);
				wanted++;
			}
		}
		foreach (int id in country.UnitAi.PriorityProvinces)
		{
			CProvince province = state.Province(id);
			if (province.Controller is not { } holder || holder.Value == CRebelFactions.Rebels
				|| (holder != country.Tag && CCountryLists.IsEnemy(state, country, holder)) || !seen.Add(id))
			{
				continue;
			}
			Insert(agent, id, CHoldScore.Of(state, country, agent, province, context) + 200 * Whole);
			wanted++;
		}
		if ((agent.Unit is null || agent.Unit.Kind == EUnitKind.Theatre) && country.Overlord is null)
		{
			Puppets(state, country, agent, seen, context);
			if (CAgents.TheatreOf(state, country, agent) is { } theatre)
			{
				foreach (int id in theatre.AllProvinces)
				{
					Consider(state, country, agent, id, seen, context);
				}
			}
		}
		int divisions = agent.Land.Count - agent.NonDivisions.Count;
		int keep = System.Math.Max(divisions, wanted);
		if (agent.HoldProvinces.Count > keep)
		{
			agent.HoldProvinces.RemoveRange(keep, agent.HoldProvinces.Count - keep);
		}
		agent.HoldProvincesMade = true;
	}

	/// <summary>The provinces of focus areas a puppet of the country holds where none of the puppet's land units is.</summary>
	private static void Puppets(CGameState state, CCountry country, CAgent agent, HashSet<int> seen,
		(bool AtPeace, bool Full) context)
	{
		foreach (CSupplyArea area in agent.FocusAreas)
		{
			if (state.Country(area.Controller) is not { } puppet || puppet.Overlord != country.Tag || Guarded(state, puppet, area))
			{
				continue;
			}
			foreach (int id in area.Provinces)
			{
				Consider(state, country, agent, id, seen, context);
			}
		}
	}

	private static bool Guarded(CGameState state, CCountry puppet, CSupplyArea area)
	{
		foreach (CUnit unit in puppet.AllUnits())
		{
			if (unit.Kind is EUnitKind.Navy or EUnitKind.Air || unit.IsRemoved)
			{
				continue;
			}
			if (area.Provinces.Contains(CAgentUnits.DestinationOrPlace(unit)))
			{
				return true;
			}
		}
		return false;
	}

	private static void Consider(CGameState state, CCountry country, CAgent agent, int id, HashSet<int> seen,
		(bool AtPeace, bool Full) context)
	{
		if (seen.Contains(id))
		{
			return;
		}
		long score = CHoldScore.Of(state, country, agent, state.Province(id), context);
		if (score < LeastScore)
		{
			return;
		}
		seen.Add(id);
		Insert(agent, id, (score + Whole / 2) / Whole * Whole);
	}

	private static void Insert(CAgent agent, int province, long score)
	{
		int at = 0;
		while (at < agent.HoldProvinces.Count && agent.HoldProvinces[at].Score >= score)
		{
			at++;
		}
		agent.HoldProvinces.Insert(at, new CHoldProvince(province, score));
	}
}
