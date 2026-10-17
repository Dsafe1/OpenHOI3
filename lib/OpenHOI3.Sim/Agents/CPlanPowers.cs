using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A plan's two powers: the AI's divisions in the theatre, a fort counting where they stand still at peace, and the
/// enemy's facing its area, weighted by how much of a front each province is; written only when either crosses a whole
/// number. The original's <c>UpdatePlanPower</c>, <c>CAIUnit_SumOurPower</c>, <c>EstimateTheatreNeed</c> and
/// <c>EstimateAreaNeed</c>. See <c>docs/mechanics/agents.md</c>, section 7.3.
/// </summary>
public static class CPlanPowers
{
	private const long Whole = 1000;

	/// <summary>A fort level's worth to a division standing in it, in thousandths.</summary>
	private const long PerFortLevel = 80;

	/// <summary>A province's neighbour in the area needs this much infrastructure for it to be a front, in thousandths.</summary>
	private const long FrontInfrastructure = 200;

	/// <summary>At or below this infrastructure a front province's floor is the lower one.</summary>
	private const long PoorInfrastructure = 300;

	private const long LowFloor = 500;

	private const long Relaxed = 500;

	private const long AtWarWithPact = 2000;

	/// <summary>The most provinces an area's need is spread over.</summary>
	private const int MostProvinces = 40;

	public static void Update(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } unit)
		{
			return;
		}
		agent.OurPower = 0;
		agent.DivisionCount = SumOurs(state, agent, out long ours);
		agent.OurPower = ours;
		agent.TheirPower = 0;
		agent.DivisionNeed = Need(state, country, agent, out long theirs);
		agent.TheirPower = theirs;
		if (unit.Plan.OurPower / Whole != ours / Whole || unit.Plan.TheirPower / Whole != theirs / Whole)
		{
			state.Outbox.Add(new CSetPlanCommand(country.Tag, unit.Id, CSetPlanCommand.Unchanged,
				CSetPlanCommand.UnchangedAirOrNaval, CSetPlanCommand.UnchangedAirOrNaval, ours, theirs));
		}
	}

	/// <summary>Every division of the agent's land, with its fort where it stands still below an attacking stance; how many.</summary>
	private static int SumOurs(CGameState state, CAgent agent, out long power)
	{
		power = 0;
		int counted = 0;
		int stance = agent.Unit?.Plan.Stance ?? CUnitPlan.Defensive;
		foreach (CUnit unit in agent.Land)
		{
			if (unit.Kind != EUnitKind.Division || unit.IsRemoved)
			{
				continue;
			}
			long value = CUnitPower.Of(unit);
			if (!CCommandLadder.IsBlocked(unit) && stance < CUnitPlan.Attacking && unit.Location > 0)
			{
				long fort = state.Province(unit.Location).Modifier[CCombatModifiers.FortLevel].Raw / Whole;
				value = value * (Whole + fort * PerFortLevel) / Whole;
			}
			power += value;
			counted++;
		}
		return counted;
	}

	/// <summary>The enemy's power over the theatre's areas, and the divisions it calls for.</summary>
	private static long Need(CGameState state, CCountry country, CAgent agent, out long power)
	{
		power = 0;
		if (CAgents.TheatreOf(state, country, agent) is not { } theatre)
		{
			return 0;
		}
		long need = 0;
		var visited = new HashSet<CSupplyArea>();
		foreach (CSupplyArea area in theatre.Areas)
		{
			need += AreaNeed(state, country, agent, area, visited, ref power);
		}
		return need;
	}

	/// <summary>
	/// One area: an area the enemy holds counts its units; otherwise each of the agent's front provinces beside the area
	/// weighs its foreign units - 500 at peace, 1000 at war, 2000 at war under a pact - with a floor by its roads; and the
	/// agent's focus areas in the same block of one side's land are counted with it, each once.
	/// </summary>
	private static long AreaNeed(CGameState state, CCountry country, CAgent agent, CSupplyArea area,
		HashSet<CSupplyArea> visited, ref long power)
	{
		if (area.Provinces.Count == 0)
		{
			return 0;
		}
		visited.Add(area);
		long need = OneArea(state, country, agent, area, ref power);
		foreach (CSupplyArea focus in agent.FocusAreas)
		{
			if (visited.Contains(focus) || focus.Group is null || !ReferenceEquals(focus.Group, area.Group)
				|| GroupProvinces(focus.Group) <= 1)
			{
				continue;
			}
			need += AreaNeed(state, country, agent, focus, visited, ref power);
		}
		return need;
	}

	/// <summary>The divisions one area calls for, as the donor's keep asks it (<c>agents.md</c>, section 10.2).</summary>
	public static long NeedOf(CGameState state, CCountry country, CAgent agent, CSupplyArea area)
	{
		long power = 0;
		return area.Provinces.Count == 0 ? 0 : OneArea(state, country, agent, area, ref power);
	}

	private static int GroupProvinces(CSupplyGroup group)
	{
		int count = 0;
		foreach (CSupplyArea area in group.Areas)
		{
			count += area.Provinces.Count;
		}
		return count;
	}

	private static long OneArea(CGameState state, CCountry country, CAgent agent, CSupplyArea area, ref long power)
	{
		if (CCountryLists.IsEnemy(state, country, area.Controller))
		{
			return EnemyUnits(state, country, area);
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		long weightSum = 0;
		foreach (int id in agent.FrontProvinces)
		{
			CProvince province = state.Province(id);
			if (network.AreaOf(id) is null || !FacesArea(state, network, province, area) || province.Controller is not { } holder)
			{
				continue;
			}
			CNap? nap = COfferRules.Nap(state, country.Tag, holder);
			if (nap is not null && IsYoung(state, nap))
			{
				continue;
			}
			bool atWar = CFrontWorthy.IsWarCandidate(state, country, holder);
			long weight = Whole;
			if (nap is null)
			{
				if (!atWar)
				{
					weight = Relaxed;
				}
			}
			else if (atWar)
			{
				weight = AtWarWithPact;
			}
			long floor = CSupplyFigures.EffectiveInfrastructure(province) <= PoorInfrastructure ? LowFloor : Whole;
			long count = 0;
			long strength = 0;
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (!CTheatres.IsLand(unit) || !CAgentArea.IsHostile(state, country, unit.Owner))
				{
					continue;
				}
				count++;
				strength += CUnitPower.Of(unit);
			}
			power += strength * weight / Whole;
			weightSum += Math.Max(floor, count * weight);
		}
		int provinces = area.Provinces.Count;
		// The sum in wholes, rounded half to even as the original's fistp does, over the area's provinces (at most 40).
		// Ours: how the original folds in the float sum of the areas it recurses into here is not settled.
		long wholes = weightSum / Whole;
		long rest = weightSum % Whole;
		if (rest > Whole / 2 || (rest == Whole / 2 && wholes % 2 == 1))
		{
			wholes++;
		}
		long need = wholes / Math.Min(provinces, MostProvinces);
		need = Math.Min(need, provinces / 4);
		if (need == 0 && weightSum > 2 * Whole)
		{
			need = 1;
		}
		return need;
	}

	private static bool FacesArea(CGameState state, CSupplyNetwork network, CProvince province, CSupplyArea area)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince beside = state.Province(edge.To);
			if (!beside.IsSea && ReferenceEquals(network.AreaOf(edge.To), area)
				&& CSupplyFigures.EffectiveInfrastructure(beside) >= FrontInfrastructure)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>A pact younger than two thirds of <c>NAP_UNBREAKABLE_MONTHS</c>, in whole months.</summary>
	private static bool IsYoung(CGameState state, CNap nap)
	{
		int months = (int)(COfferRules.Define(state, "NAP_UNBREAKABLE_MONTHS").Raw * 2 / 3 / Whole);
		return state.TotalHours < COfferRules.AddMonths(nap.Start, months);
	}

	private static long EnemyUnits(CGameState state, CCountry country, CSupplyArea area)
	{
		long count = 0;
		foreach (int id in area.Provinces)
		{
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (CTheatres.IsLand(unit) && unit.Kind == EUnitKind.Division && CCountryLists.IsEnemy(state, country, unit.Owner))
				{
					count++;
				}
			}
		}
		return count;
	}
}
