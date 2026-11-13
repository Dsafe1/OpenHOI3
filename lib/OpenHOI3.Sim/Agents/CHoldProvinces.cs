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
			// The day of the month counted from nought, so the 1st, 8th, 15th, 22nd and 29th (FINDINGS-liveread.md, section 68).
			|| (full && ((state.Date.Day - 1) % 7 == 0 || !country.UnitAi.RunsUnits));
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
		// A subject skips only its puppets' areas: the theatre's own provinces are looked at all the same (FINDINGS-liveread.md, section 15).
		if (agent.Unit is null || agent.Unit.Kind == EUnitKind.Theatre)
		{
			if (country.Overlord is null)
			{
				Puppets(state, country, agent, seen, context);
			}
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

	/// <summary>
	/// The provinces of focus areas a puppet of the country holds, each but where one of the puppet's land units stands or
	/// is bound - province by province, not the area as a whole (FINDINGS-liveread.md, section 52).
	/// </summary>
	private static void Puppets(CGameState state, CCountry country, CAgent agent, HashSet<int> seen,
		(bool AtPeace, bool Full) context)
	{
		var guarded = new Dictionary<CCountryTag, HashSet<int>>();
		foreach (CSupplyArea area in agent.FocusAreas)
		{
			if (state.Country(area.Controller) is not { } puppet || puppet.Overlord != country.Tag)
			{
				continue;
			}
			if (!guarded.TryGetValue(puppet.Tag, out HashSet<int>? held))
			{
				held = Guarded(puppet);
				guarded[puppet.Tag] = held;
			}
			foreach (int id in area.Provinces)
			{
				if (!held.Contains(id))
				{
					Consider(state, country, agent, id, seen, context);
				}
			}
		}
	}

	/// <summary>Where the puppet's land units stand, and where those on the move are bound.</summary>
	private static HashSet<int> Guarded(CCountry puppet)
	{
		var held = new HashSet<int>();
		foreach (CUnit unit in puppet.AllUnits())
		{
			if (unit.Kind is EUnitKind.Navy or EUnitKind.Air || unit.IsRemoved)
			{
				continue;
			}
			held.Add(unit.Location);
			if (unit.IsMoving)
			{
				held.Add(unit.Path[^1]);
			}
		}
		return held;
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
		// Not marked as seen, as the original's list is not added to: a province of a puppet's area and of the theatre comes twice.
		// The score is rounded to a whole number as the FPU does, a half to the even.
		Insert(agent, id, (long)System.Math.Round(score / (double)Whole, System.MidpointRounding.ToEven) * Whole);
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
