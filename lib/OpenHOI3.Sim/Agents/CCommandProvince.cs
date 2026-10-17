using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Where an agent's headquarters stand: the theatre's headquarters walked to the province a new one would be put in,
/// and every headquarters under it pulled in among its subordinates. Slot 82, the original's
/// <c>CAIUnit_MoveAgentUnitToCommandProvince</c> and <c>CAIUnit_PullHeadquartersIn</c>. See
/// <c>docs/mechanics/agents.md</c>, section 6.
/// </summary>
public static class CCommandProvince
{
	/// <summary>A province with less infrastructure than this, in thousandths, is no place for a headquarters.</summary>
	private const long LeastInfrastructure = 200;

	/// <summary>A province's mean distance is scored from this, and a score at or above it places nothing.</summary>
	private const long Unplaced = 100_000;

	private const long OwnStart = 6;

	private const long BesideForeign = 1000;

	private const long PoorRoads = 100_000;

	/// <summary>
	/// Slot 82, every hour: for a land headquarters with subordinates, its command province chosen again where it has none,
	/// on the forced hour, or where it stands on a front.
	/// </summary>
	public static void Choose(CGameState state, CCountry country, CAgent agent, bool force)
	{
		if (agent.Unit is not { } unit || !CTheatres.IsLand(unit) || unit.Children.Count == 0)
		{
			return;
		}
		bool onFront = unit.Location > 0 && CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false);
		if (agent.CommandProvince != 0 && !force && !onFront)
		{
			return;
		}
		if (unit.Kind == EUnitKind.Theatre && CAgents.TheatreOf(state, country, agent) is { } theatre)
		{
			agent.CommandProvince = CTheatreHeadquarters.PickProvince(state, country, theatre)?.Id ?? 0;
		}
	}

	/// <summary>Every hour: the agent's headquarters marched to its command province, where it is free to go.</summary>
	public static void March(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } unit || agent.CommandProvince <= 0 || unit.IsRetreating || unit.IsFighting
			|| unit.Location <= 0 || CAgentUnits.DestinationOrPlace(unit) == agent.CommandProvince)
		{
			return;
		}
		if (CRouteFinder.Find(state, country, unit, unit.Location, agent.CommandProvince) is not null)
		{
			state.Outbox.Add(new CMoveUnitCommand(country.Tag, unit.Id, agent.CommandProvince));
		}
	}

	/// <summary>
	/// On the full pass: each headquarters under the agent but its own - one on the move always, one standing still where a
	/// subordinate is out of its full reach or it stands on a front while the frontier is at war - sent to the province
	/// among its subordinates.
	/// </summary>
	public static void PullIn(CGameState state, CCountry country, CAgent agent)
	{
		foreach (CUnit unit in agent.NonDivisions)
		{
			if (ReferenceEquals(unit, agent.Unit) || unit.IsRemoved || unit.IsRetreating || unit.IsFighting
				|| unit.Location <= 0)
			{
				continue;
			}
			if (!unit.IsMoving && !OutOfReach(state, unit)
				&& !(agent.FrontierAtWar && CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false)))
			{
				continue;
			}
			int target = AmongSubordinates(state, country, unit);
			if (target <= 0 || target == CAgentUnits.DestinationOrPlace(unit))
			{
				continue;
			}
			if (CRouteFinder.Find(state, country, unit, unit.Location, target) is not null)
			{
				state.Outbox.Add(new CMoveUnitCommand(country.Tag, unit.Id, target));
			}
		}
	}

	private static bool OutOfReach(CGameState state, CUnit unit)
	{
		foreach (CUnit child in unit.Children)
		{
			if (!child.IsRemoved && CCommandReach.From(state, child, unit) < CCommandReach.Full)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The original's <c>CAIUnit_PickProvinceAmongSubordinates</c>: each land subordinate's destination or place that the
	/// headquarters can reach by areas no enemy holds, and its land neighbours in the same area, scored by the mean distance
	/// to the subordinates, the lowest winning.
	/// </summary>
	private static int AmongSubordinates(CGameState state, CCountry country, CUnit unit)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var places = new List<int>();
		foreach (CUnit child in unit.Children)
		{
			if (!child.IsRemoved && CTheatres.IsLand(child))
			{
				places.Add(CAgentUnits.DestinationOrPlace(child));
			}
		}
		if (places.Count == 0)
		{
			return 0;
		}
		int from = CAgentUnits.DestinationOrPlace(unit);
		var candidates = new List<int>();
		foreach (int place in places)
		{
			if (place <= 0 || candidates.Contains(place) || !CAreaPaths.Joined(state, country, from, place))
			{
				continue;
			}
			candidates.Add(place);
			CProvince province = state.Province(place);
			CSupplyArea? area = network.AreaOf(place);
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince beside = state.Province(edge.To);
				if (!beside.IsSea && area is not null && ReferenceEquals(network.AreaOf(edge.To), area)
					&& !candidates.Contains(edge.To))
				{
					candidates.Add(edge.To);
				}
			}
		}
		int best = 0;
		long least = Unplaced;
		foreach (int candidate in candidates)
		{
			CProvince province = state.Province(candidate);
			long total = 0;
			foreach (int place in places)
			{
				total += place <= 0 ? 0 : CMapDistance.Between(province, state.Province(place), state.MapWidth);
			}
			long mean = total / places.Count;
			if (mean == 0)
			{
				continue;
			}
			long score = mean;
			if (candidate == from)
			{
				score -= OwnStart;
			}
			if (CAreaFronts.BordersForeignLand(state, province))
			{
				score += BesideForeign;
			}
			if (CSupplyFigures.EffectiveInfrastructure(province) < LeastInfrastructure)
			{
				score += PoorRoads;
			}
			if (score < least)
			{
				least = score;
				best = candidate;
			}
		}
		return best;
	}
}
