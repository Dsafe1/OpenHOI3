using System.Collections.Generic;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Each of an agent's divisions committed to its line or held back, by how well its plan's powers stand; then the reserve
/// sized by the stance - the fittest recalled, the worst sent back. Slot 78's middle, <c>CAIUnit::ManageSubordinates</c>.
/// See <c>docs/mechanics/moving.md</c>, section 2.
/// </summary>
public static class CCommitment
{
	private const long Whole = 1000;

	private const long LeastOrganisation = 300;

	/// <summary>The least organisation a held-back division is recalled at, as the original's single-precision constant.</summary>
	private const float RecallOrganisationShare = 0.33f;

	/// <summary>The least strength a held-back division is recalled at.</summary>
	private const float RecallStrengthShare = 0.75f;

	/// <summary>A reserve is kept only with more committed than this, below a theatre agent.</summary>
	private const int FewDivisions = 10;

	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		agent.Committed.Clear();
		agent.CommittedAtFirst.Clear();
		agent.HeldBack.Clear();
		agent.OnAlliedSoil = 0;
		if (agent.Unit is not { } headquarters)
		{
			return;
		}
		bool airTransports = agent.AirTransports.Count > 0;
		long factor = Factor(headquarters.Plan);
		// How many have been held back where they stand: the test's first, then the reserve's (FINDINGS-liveread.md, section 19).
		var counted = new Dictionary<int, int>();
		CTheatre? theatre = CAgents.TheatreOf(state, country, agent);
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? largest = theatre is null ? null : CTheatreProvinces.LargestArea(theatre);
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		foreach (CUnit unit in agent.Land)
		{
			if (unit.Kind != EUnitKind.Division || unit.IsRemoved)
			{
				continue;
			}
			if (CTransportReservation.IsReserved(state, unit))
			{
				unit.IsHeldBack = false;
				continue;
			}
			bool airborne = CArmyRole.Of(unit) == CArmyRole.Airborne;
			if (airborne && airTransports)
			{
				continue;
			}
			// Read on the listing (FINDINGS-liveread.md, section 16): outside the theatre's largest area a division is
			// committed and its mark cleared; at peace it keeps what it had; at war one held back stays held back, the bar
			// for coming back in the original never being reached; otherwise the bars.
			bool outside = headquarters.Kind == EUnitKind.Theatre && largest is not null
				&& !ReferenceEquals(network.AreaOf(unit.Location), largest);
			bool hold;
			if (outside)
			{
				hold = false;
			}
			else if (!agent.FrontierAtWar || unit.IsHeldBack || airborne)
			{
				hold = unit.IsHeldBack || (agent.FrontierAtWar && airborne);
			}
			else
			{
				CUnitShares.Of(state, unit, out long strength, out long organisation);
				hold = factor > strength || LeastOrganisation > organisation;
			}
			unit.IsHeldBack = hold;
			(hold ? agent.HeldBack : agent.Committed).Add(unit);
			if (hold)
			{
				counted[unit.Location] = counted.GetValueOrDefault(unit.Location) + 1;
			}
			agent.CommittedAtFirst.Add(unit);
			if (!hold && IsAlliedSoil(state, country, network, capitalArea, unit))
			{
				agent.OnAlliedSoil++;
			}
		}
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"  commitment {headquarters.Name}: factor {factor}, powers {headquarters.Plan.OurPower}/{headquarters.Plan.TheirPower}, committed {agent.Committed.Count}, held by the test {agent.HeldBack.Count}, stance {headquarters.Plan.Stance}");
		}
		Reserve(state, country, agent, headquarters, theatre, largest, network, counted);
	}

	/// <summary>The least strength a division is committed at: the better the powers stand, the higher.</summary>
	private static long Factor(CUnitPlan plan)
	{
		// The original's GetPowerRatio: a negative figure for them answers 1, and nought answers -1, which is the lowest bar but one.
		long ratio = plan.TheirPower < 0 ? Whole : plan.TheirPower == 0 ? -1 : plan.OurPower * Whole / plan.TheirPower;
		if (ratio > 2000)
		{
			return 700;
		}
		if (ratio > 1500)
		{
			return 600;
		}
		if (ratio < 250)
		{
			return 200;
		}
		if (ratio < 500)
		{
			return 300;
		}
		return ratio < 750 ? 400 : 500;
	}

	private static bool IsAlliedSoil(CGameState state, CCountry country, CSupplyNetwork network, CSupplyArea? capitalArea,
		CUnit unit)
	{
		if (unit.Location <= 0 || capitalArea?.RegionGroup is not { } group)
		{
			return false;
		}
		CProvince here = state.Province(unit.Location);
		return here.Controller is { } holder && holder != country.Tag
			&& ReferenceEquals(network.AreaOf(unit.Location)?.RegionGroup, group);
	}

	private static void Reserve(CGameState state, CCountry country, CAgent agent, CUnit headquarters, CTheatre? theatre,
		CSupplyArea? largest, CSupplyNetwork network, Dictionary<int, int> counted)
	{
		int stance = headquarters.Plan.Stance;
		int surplus = -2;
		if (agent.FrontierAtWar)
		{
			int divisions = agent.Committed.Count;
			if (divisions > FewDivisions || headquarters.Kind != EUnitKind.Theatre)
			{
				int area = agent.Area.Count;
				surplus = 0;
				if (divisions * 2 > area * 3)
				{
					int k = stance is CUnitPlan.Blitzing or CUnitPlan.Prepare ? 10 : stance == CUnitPlan.Attacking ? 7 : 5;
					surplus = divisions / k;
				}
				if (area > divisions - surplus)
				{
					surplus = divisions - area;
				}
			}
		}
		while (agent.HeldBack.Count > surplus + 2 && Recall(state, agent))
		{
		}
		while (agent.HeldBack.Count < surplus && Detach(state, country, agent, headquarters, theatre, largest, network, counted))
		{
		}
	}

	private static bool Recall(CGameState state, CAgent agent)
	{
		// In single precision, as the original's: two full divisions differ by what a float keeps of their shares, and the
		// first of equals wins (FINDINGS-liveread.md, section 49).
		CUnit? best = null;
		float bestScore = 0f;
		foreach (CUnit unit in agent.HeldBack)
		{
			CUnitShares.OfSingle(state, unit, out float strength, out float organisation);
			if (RecallOrganisationShare > organisation || RecallStrengthShare > strength)
			{
				continue;
			}
			float score = (float)((double)strength + organisation);
			if (CArmyRole.Of(unit) == CArmyRole.Airborne)
			{
				score = (float)((double)score + 1.0);
			}
			if (score > bestScore)
			{
				best = unit;
				bestScore = score;
			}
		}
		if (best is null)
		{
			return false;
		}
		agent.HeldBack.Remove(best);
		best.IsHeldBack = false;
		agent.Committed.Add(best);
		return true;
	}

	private static bool Detach(CGameState state, CCountry country, CAgent agent, CUnit headquarters, CTheatre? theatre,
		CSupplyArea? largest, CSupplyNetwork network, Dictionary<int, int> counted)
	{
		int stance = headquarters.Plan.Stance;
		CUnit? best = null;
		float bestScore = -100_000f;
		foreach (CUnit unit in agent.Committed)
		{
			int place = CAgentUnits.DestinationOrPlace(unit);
			if (place <= 0)
			{
				continue;
			}
			CSupplyArea? area = network.AreaOf(place);
			if (headquarters.Kind == EUnitKind.Theatre && theatre is not null && !ReferenceEquals(area, largest)
				&& (area is null || !agent.FocusAreas.Contains(area)))
			{
				continue;
			}
			CProvince destination = state.Province(place);
			bool onFrontier = false;
			bool preparing = false;
			foreach (CProvinceEdge edge in destination.Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable)
				{
					continue;
				}
				CProvince beside = state.Province(edge.To);
				if (beside.IsSea || network.AreaOf(edge.To) is null || beside.Controller is not { } holder
					|| holder == country.Tag)
				{
					continue;
				}
				if (CCountryLists.IsEnemy(state, country, holder))
				{
					onFrontier = true;
					continue;
				}
				if (CWarTargets.IsPreparingWarWith(state, country, holder))
				{
					preparing = true;
				}
			}
			if (preparing)
			{
				continue;
			}
			// From the frontier only where no land battle is on and at least two units of more than one brigade stand
			// (FINDINGS-liveread.md, section 16).
			if (onFrontier && (CLineFill.HasLandBattle(state, place) || MultiBrigadeUnits(state, place) < 2))
			{
				continue;
			}
			// In single precision and in this order, read on the listing (FINDINGS-liveread.md, section 19). The first brigade's
			// type counts only at an attacking stance: below it the branch for the role goes past that test.
			CUnitShares.OfSingle(state, unit, out float strength, out float organisation);
			float score = (float)(((1.0 - organisation) + (1.0 - strength)) * 5.0);
			if (unit.AttackDelay.Raw > 0)
			{
				score = (float)((double)score + 1.0);
			}
			int role = CArmyRole.Of(unit);
			if (role == CArmyRole.Airborne)
			{
				score = (float)((double)score + 0.9f);
			}
			if (unit.IsHeldBack)
			{
				score = (float)((double)score + 0.2f);
			}
			if (stance >= CUnitPlan.Attacking)
			{
				if (role == CArmyRole.Armour)
				{
					score = (float)((double)score - 2.0);
				}
				else if (role == CArmyRole.Fast)
				{
					score = (float)((double)score - 1.5);
				}
				if (unit.SubUnits.Count > 0)
				{
					string key = unit.SubUnits[0].Type.Key;
					score = key == "militia_brigade" ? (float)((double)score + 2.0) : key == "bergsjaeger_brigade" ? (float)((double)score - 2.0) : score;
				}
			}
			else if (role == CArmyRole.Fast)
			{
				score = (float)((double)score + 2.0);
			}
			else if (role == CArmyRole.Armour)
			{
				score = (float)((double)score + 1.0);
			}
			if (onFrontier)
			{
				score = (float)((double)score - (float)counted.GetValueOrDefault(place));
			}
			if (score > bestScore)
			{
				best = unit;
				bestScore = score;
			}
		}
		if (best is null)
		{
			return false;
		}
		agent.Committed.Remove(best);
		best.IsHeldBack = true;
		agent.HeldBack.Add(best);
		counted[best.Location] = counted.GetValueOrDefault(best.Location) + 1;
		return true;
	}

	/// <summary>An air unit there, or two units or more.</summary>
	private static int MultiBrigadeUnits(CGameState state, int province)
	{
		int count = 0;
		foreach (CUnit unit in state.Positions.Of(province))
		{
			if (CTheatres.IsLand(unit) && unit.SubUnits.Count > 1)
			{
				count++;
			}
		}
		return count;
	}
}
