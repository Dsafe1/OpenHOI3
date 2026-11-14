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
		CAiMoves.Move(state, country, unit, agent.CommandProvince, safe: true);
	}

	/// <summary>
	/// On the full pass: each headquarters under the agent but its own - one on the move always, one standing still where a
	/// subordinate is out of its full reach or it stands on a front while the frontier is at war - sent to the province
	/// among its subordinates, where the land reach test (the original's <c>CMapProvince::CanUnitReach</c>) says it can get
	/// there. A land headquarters in a battle is looked at too, a land battle always letting it go
	/// (<c>CAIUnit_PullHeadquartersIn</c> in the fact base's record).
	/// </summary>
	public static void PullIn(CGameState state, CCountry country, CAgent agent)
	{
		foreach (CUnit unit in agent.NonDivisions)
		{
			// One in a battle is left only where it may not leave it, and a land or an air battle always lets a unit go.
			if (ReferenceEquals(unit, agent.Unit) || unit.IsRemoved || unit.IsRetreating || unit.Location <= 0
				|| (unit.IsFighting && !CTheatres.IsLand(unit)))
			{
				continue;
			}
			if (!unit.IsMoving && !OutOfReach(state, unit) && !IsTrespassing(state, country, unit)
				&& !(agent.FrontierAtWar && CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false)))
			{
				continue;
			}
			int target = AmongSubordinates(state, country, unit);
			if (state.AiTrace is { } pickTrace && pickTrace.Country == country.Tag)
			{
				pickTrace.Write($"  pick {unit.Name}#{unit.Id} at {unit.Location} to {CAgentUnits.DestinationOrPlace(unit)}: children " + string.Join(" ",
					System.Linq.Enumerable.Select(unit.Children, child => $"{state.OriginalSerial(child.Id)}:{child.Location}>{CAgentUnits.Destination(child)}:g{(child.IsGarrison ? 1 : 0)}")) + $" chose {target}");
			}
			if (target <= 0 || target == CAgentUnits.DestinationOrPlace(unit) || !CAiMoves.ReachesByLand(state, country, unit, target))
			{
				continue;
			}
			// Through the AI's move, so that a headquarters above this one sees where it is bound (FINDINGS-liveread.md, section 14).
			CAiMoves.Move(state, country, unit, target, safe: true);
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
	/// Whether the unit stands where its owner may not enter - the original's trespassing mark, which it makes again every hour
	/// and on any change of pact, so asked here as it stands; never the rebels' (FINDINGS-war.md; FINDINGS-liveread.md,
	/// section 47).
	/// </summary>
	private static bool IsTrespassing(CGameState state, CCountry country, CUnit unit)
	{
		return country.Tag.Value != Revolts.CRebelFactions.Rebels && unit.Location > 0
			&& !CMovementRules.MayEnter(state, country, state.Province(unit.Location));
	}

	/// <summary>
	/// The original's <c>CAIUnit_PickProvinceAmongSubordinates</c>, as read on the listing: a headquarters trespassing on the
	/// move answers where it is bound at once, and so is left on its way; otherwise from each land child's destination
	/// or place that the headquarters can reach by areas no enemy holds, and - whether or not it can - that place's land
	/// neighbours in its area, the province with the least mean distance to the children, leaving out the garrisons where any
	/// child is none, skipped at nought, 6 less for where it starts, 1000 more bordering foreign land, 100000 more with poor
	/// roads; its own province where none is under 100000. Single precision throughout, as the original's.
	/// </summary>
	private static int AmongSubordinates(CGameState state, CCountry country, CUnit unit)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int from = CAgentUnits.DestinationOrPlace(unit);
		if (unit.IsMoving && IsTrespassing(state, country, unit))
		{
			return from;
		}
		if (network.AreaOf(from) is null)
		{
			return 0;
		}
		bool anyNotGarrison = false;
		var candidates = new List<int>();
		foreach (CUnit child in unit.Children)
		{
			if (child.IsRemoved || !CTheatres.IsLand(child))
			{
				continue;
			}
			anyNotGarrison |= !child.IsGarrison;
			int place = CAgentUnits.DestinationOrPlace(child);
			if (place <= 0 || network.AreaOf(place) is not { } area || state.Province(place).IsSea)
			{
				continue;
			}
			if (CAreaPaths.Joined(state, country, from, place))
			{
				candidates.Add(place);
			}
			foreach (CProvinceEdge edge in state.Province(place).Edges)
			{
				if (edge.Kind != EProvinceEdgeKind.Impassable && !state.Province(edge.To).IsSea && ReferenceEquals(network.AreaOf(edge.To), area))
				{
					candidates.Add(edge.To);
				}
			}
		}
		var places = new List<int>();
		foreach (CUnit child in unit.Children)
		{
			if (!child.IsRemoved && CTheatres.IsLand(child) && !(anyNotGarrison && child.IsGarrison))
			{
				places.Add(CAgentUnits.DestinationOrPlace(child));
			}
		}
		int best = unit.Location;
		float least = Unplaced;
		foreach (int candidate in candidates)
		{
			CProvince province = state.Province(candidate);
			float total = 0;
			foreach (int place in places)
			{
				total = (float)((double)CMapDistance.Between(province, state.Province(place), state.MapWidth) + total);
			}
			float score = places.Count > 0 ? (float)(total / (double)places.Count) : total;
			if (score == 0)
			{
				continue;
			}
			if (candidate == from)
			{
				score = (float)(score - (double)OwnStart);
			}
			if (CAreaFronts.BordersForeignLand(state, province))
			{
				score = (float)(score + (double)BesideForeign);
			}
			// The infrastructure built, not what the province has the use of: the original reads its maximum here.
			if (CSupplyFigures.MostInfrastructure(state, province) < LeastInfrastructure)
			{
				score = (float)(score + (double)PoorRoads);
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
