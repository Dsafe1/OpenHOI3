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
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A plan's two powers and its theatre's division need: the AI's divisions in the theatre, a fort counting where they
/// stand still at peace; the enemy's facing its areas, weighted by how much of a front each province is; and the
/// divisions each area calls for. The powers are written only when either crosses a whole number. The original's
/// <c>UpdatePlanPower</c>, <c>CAIUnit_SumOurPower</c>, <c>EstimateTheatreNeed</c> and <c>EstimateAreaNeed</c>. See
/// <c>docs/mechanics/agents.md</c>, sections 7.3 and 7.5.
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

	private const long AtWar = 2000;

	/// <summary>A front-worthy enemy surrendered past this share, not a war candidate, is weighed at half.</summary>
	private const long Beaten = 100;

	/// <summary>The most provinces an area's worth is spread over.</summary>
	private const int MostProvinces = 40;

	/// <summary>Italy's need about Addis Abeba is a third.</summary>
	private const int AddisAbeba = 9943;

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

	/// <summary>The divisions the theatre's areas call for, one list of areas visited for them all; and the enemy's power.</summary>
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
			need += AreaNeed(state, country, agent, area, visited, relaxed: true, ref power);
		}
		return need;
	}

	/// <summary>The divisions one area calls for, as the donor's keep and the sealift ask it.</summary>
	public static long NeedOf(CGameState state, CCountry country, CAgent agent, CSupplyArea area, bool relaxed = true)
	{
		long power = 0;
		return AreaNeed(state, country, agent, area, new HashSet<CSupplyArea>(), relaxed, ref power);
	}

	/// <summary><c>EstimateAreaNeed</c> whole, in its order (<c>agents.md</c>, section 7.5).</summary>
	private static long AreaNeed(CGameState state, CCountry country, CAgent agent, CSupplyArea area,
		HashSet<CSupplyArea> visited, bool relaxed, ref long power)
	{
		if (area.Provinces.Count == 0)
		{
			return 0;
		}
		CProvince first = state.Province(area.Provinces[0]);
		if (first.Controller is { } firstHolder && CCountryLists.IsEnemy(state, country, firstHolder))
		{
			return EnemyUnits(state, country, area);
		}
		if (country.Tag.Value == "ITA" && first.Controller?.Value == "ETH")
		{
			return 0;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CTheatre? theatre = CAgents.TheatreOf(state, country, agent);
		long need = Objectives(state, agent, area, theatre);
		long weightSum = 0;
		bool anyFront = false;
		var faced = new List<CSupplyArea>();
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		foreach (int id in agent.FrontProvinces)
		{
			CProvince province = state.Province(id);
			if (network.AreaOf(id) is not { } beside || !FacesArea(state, network, province, area)
				|| province.Controller is not { } holder)
			{
				continue;
			}
			CNap? nap = COfferRules.Nap(state, country.Tag, holder);
			if (nap is not null && IsYoung(state, nap))
			{
				continue;
			}
			if (!CFrontWorthy.Is(state, country, holder))
			{
				continue;
			}
			bool candidate = CFrontWorthy.IsWarCandidate(state, country, holder);
			bool halved = !candidate && state.Country(holder) is { } enemy && CSurrender.Progress(state, enemy) > Beaten;
			if (!halved)
			{
				anyFront = true;
			}
			long weight = relaxed && nap is null ? (candidate ? Whole : Relaxed) : (candidate ? AtWar : Whole);
			if (halved)
			{
				weight /= 2;
			}
			long floor = halved || CSupplyFigures.EffectiveInfrastructure(province) <= PoorInfrastructure ? LowFloor : Whole;
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
			long score = Math.Max(floor, count * weight);
			long theirs = strength * weight / Whole;
			if (state.Country(holder) is { } holderCountry && IsFactionLeader(state, holderCountry)
				&& ReferenceEquals(network.AreaOf(holderCountry.ActingCapital), beside))
			{
				score += weight;
				theirs += theirs * weight / Whole;
			}
			if (score > Whole && capitalArea is not null && !ReferenceEquals(capitalArea, area)
				&& capitalArea.Neighbours.Contains(beside))
			{
				theirs = theirs * Whole / score;
				score = Whole;
			}
			power += theirs;
			weightSum += score;
			if (!faced.Contains(beside))
			{
				faced.Add(beside);
			}
		}
		long brigades = 0;
		long facedProvinces = 0;
		foreach (CSupplyArea beside in faced)
		{
			facedProvinces += beside.Provinces.Count;
			foreach (int id in beside.Provinces)
			{
				foreach (CUnit unit in state.Positions.Of(id))
				{
					if (CTheatres.IsLand(unit) && unit.Owner == beside.Controller)
					{
						brigades += unit.SubUnits.Count;
					}
				}
			}
		}
		long fronts = HalfToEven(weightSum);
		long worth = first.Controller == country.Tag
			? Worth(state, country, agent, area, theatre, capitalArea, relaxed)
			: 0;
		need += anyFront && worth > fronts ? worth : fronts;
		if (area.Group is { } group && GroupProvinces(group) > 1)
		{
			foreach (CSupplyArea focus in agent.FocusAreas)
			{
				if (visited.Contains(focus) || !ReferenceEquals(focus.Group, group))
				{
					continue;
				}
				visited.Add(focus);
				need += AreaNeed(state, country, agent, focus, visited, relaxed, ref power);
			}
		}
		if (first.Controller == country.Tag && !ReferenceEquals(area, capitalArea)
			&& CAreaHostility.TouchesHostileArea(state, area) && facedProvinces > 3)
		{
			need += 4;
		}
		if (TheatreCount(state, country) > 1 && need > 2 * brigades)
		{
			need = Math.Max(Math.Max(need / 2, 2 * brigades), 1);
		}
		if (ReferenceEquals(area, capitalArea) && CWarTargetsMajor(country))
		{
			need = Math.Max(need, relaxed ? Math.Max(NavalLevels(state, area) / 4, area.Ports.Count / 2) : 3 * area.Ports.Count);
		}
		need = Math.Max(need, 1);
		if (first.Controller is not { } owner || (owner != country.Tag && state.Country(owner)?.Overlord != country.Tag))
		{
			if (need > 1)
			{
				need = Math.Max(need / 2, 1);
			}
		}
		if (country.Tag.Value == "ITA" && AboutAddisAbeba(state, network, country, area))
		{
			need /= 3;
		}
		return need;
	}

	/// <summary>The plan's objectives lying in the area and in the agent's theatre.</summary>
	private static long Objectives(CGameState state, CAgent agent, CSupplyArea area, CTheatre? theatre)
	{
		if (agent.Unit is not { } unit)
		{
			return 0;
		}
		long count = 0;
		foreach (CObjective objective in unit.Plan.Objectives)
		{
			if (!area.Provinces.Contains(objective.Province))
			{
				continue;
			}
			bool ours = theatre is null
				? unit.Plan.Area.Contains(objective.Province)
				: state.Province(objective.Province).TheatreId == theatre.Id;
			if (ours)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// The second estimate, for an area of the country's: what its theatre's provinces in it are worth, over at most
	/// forty of them, cut, and at most a quarter of its provinces; held down in a small area with no neighbour.
	/// </summary>
	private static long Worth(CGameState state, CCountry country, CAgent agent, CSupplyArea area, CTheatre? theatre,
		CSupplyArea? capitalArea, bool relaxed)
	{
		long sum = 0;
		IEnumerable<int> provinces = theatre is not null ? theatre.AllProvinces : (IEnumerable<int>?)agent.Unit?.Plan.Area ?? Array.Empty<int>();
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var context = CProvinceHoldValue.Context(state, country);
		foreach (int id in provinces)
		{
			if (ReferenceEquals(network.AreaOf(id), area))
			{
				sum += CProvinceHoldValue.Of(state, country, agent, state.Province(id), hold: false, context);
			}
		}
		int count = area.Provinces.Count;
		long worth = sum / Math.Min(count, MostProvinces) / Whole;
		long cap = count / 4;
		if (!relaxed)
		{
			cap = Math.Max(cap, 1);
		}
		worth = Math.Min(worth, cap);
		if (area.Neighbours.Count == 0 && !ReferenceEquals(area, capitalArea))
		{
			if (worth > 1 && count < 3)
			{
				worth = 1;
			}
			else if (worth > 2 && count < 15)
			{
				worth = 2;
			}
		}
		else if (worth == 0 && sum > 2 * Whole)
		{
			worth = 1;
		}
		return worth;
	}

	/// <summary>A sum in thousandths to wholes, half to even, as the original's <c>fistp</c> does.</summary>
	private static long HalfToEven(long thousandths)
	{
		long wholes = thousandths / Whole;
		long rest = thousandths % Whole;
		if (rest > Whole / 2 || (rest == Whole / 2 && wholes % 2 == 1))
		{
			wholes++;
		}
		return wholes;
	}

	/// <summary>The first member of a faction with members.</summary>
	private static bool IsFactionLeader(CGameState state, CCountry country)
	{
		List<CCountry> members = CCountryLists.FactionMembers(state, country.Faction);
		return members.Count > 0 && ReferenceEquals(members[0], country);
	}

	private static bool CWarTargetsMajor(CCountry country) => Ai.CWarTargets.Prunes(country);

	/// <summary>The built levels of the area's naval bases, in whole levels, summed.</summary>
	private static long NavalLevels(CGameState state, CSupplyArea area)
	{
		if (CTheatreFigures.NavalBase(state) is not { } naval)
		{
			return 0;
		}
		long sum = 0;
		foreach (int id in area.Ports)
		{
			sum += state.Province(id).BuildingMaxLevel(naval).Raw;
		}
		return sum / Whole;
	}

	private static int TheatreCount(CGameState state, CCountry country)
	{
		int count = 0;
		foreach (CTheatre theatre in state.Theatres.Values)
		{
			if (theatre.Country == country.Tag)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>Addis Abeba in the area, or in an area beside it held by a subject of Italy's.</summary>
	private static bool AboutAddisAbeba(CGameState state, CSupplyNetwork network, CCountry country, CSupplyArea area)
	{
		if (AddisAbeba >= state.Provinces.Count)
		{
			return false;
		}
		if (area.Provinces.Contains(AddisAbeba))
		{
			return true;
		}
		CProvince capital = state.Province(AddisAbeba);
		return network.AreaOf(AddisAbeba) is { } its && area.Neighbours.Contains(its)
			&& state.Country(capital.Controller)?.Overlord == country.Tag;
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
