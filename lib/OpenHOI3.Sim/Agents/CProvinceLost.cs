using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent told that a province has changed hands - the original's <c>CAIUnit::OnProvinceControllerChanged</c>. It
/// answers only a province its own country lost, beside its theatre, in an area of three provinces or fewer, and sends
/// its land units in by the plain way until it has twice the enemy's brigades there. A counter-attack into a small area:
/// an island, an enclave, a province risen in revolt. See <c>docs/mechanics/moving.md</c>, section 7.7.
/// </summary>
public static class CProvinceLost
{
	/// <summary>The most provinces the lost one's area may have.</summary>
	private const int SmallArea = 3;

	/// <summary>The least mean strength a unit is sent at, in thousandths: a third.</summary>
	private const long LeastStrength = 330;

	/// <summary>A score at or above this is no candidate.</summary>
	private const double Unreachable = 10_000_000;

	/// <summary>The idle divisions beside a war candidate a unit is weighed against.</summary>
	private const int IdleScale = 10;

	/// <summary>
	/// Every agent of the country that lost the province is told, in its list's order. Only while the country's AI
	/// runs its units, or for an agent whose headquarters the player has handed to it.
	/// </summary>
	public static void ControllerChanged(CGameState state, CProvince province, CCountryTag? old)
	{
		if (state.IsBeingMade || state.Country(old) is not { } country)
		{
			return;
		}
		foreach (CAgent agent in new List<CAgent>(country.UnitAi.Agents))
		{
			if (agent.Unit is { } unit && (country.UnitAi.RunsUnits || unit.Agent is not null))
			{
				Answer(state, country, agent, province);
			}
		}
	}

	/// <summary>One agent's answer to a province its country lost.</summary>
	public static void Answer(CGameState state, CCountry country, CAgent agent, CProvince province)
	{
		if (!BesideTheatre(state, agent, province))
		{
			return;
		}
		// The area as it stands after the change, worked out alone: the network itself is not made again here, which the
		// recording's replay would not do (docs/replay.md, section 3).
		List<int> area = CSupplyNetworks.AreaAround(state, province.Id);
		if (area.Count == 0 || area.Count > SmallArea)
		{
			return;
		}
		var inArea = new HashSet<int>(area);
		int enemy = 0;
		foreach (int id in area)
		{
			foreach (CUnit other in state.Positions.Of(id))
			{
				if (CMovementRules.IsLandUnit(other) && CMovementRules.IsEnemyIn(state, country.Tag, other.Owner, id))
				{
					enemy += other.SubUnits.Count;
				}
			}
		}
		var listed = new List<CUnit>();
		int ours = 0;
		foreach (CUnit unit in agent.Land)
		{
			int place = CAgentUnits.DestinationOrPlace(unit);
			if (place > 0 && (inArea.Contains(place) || HasEdgeInto(state, place, inArea)))
			{
				ours += unit.SubUnits.Count;
				listed.Add(unit);
			}
		}
		int need = 2 * enemy - ours;
		while (need > 0)
		{
			CUnit? best = null;
			double least = Unreachable;
			foreach (CUnit unit in agent.Land)
			{
				if (listed.Contains(unit) || unit.IsRemoved || unit.Location <= 0
					|| !CUnitMayLeave.Allows(state, country, unit, null, strict: false, notDefending: true)
					|| CTransportReservation.IsReserved(state, unit))
				{
					continue;
				}
				CUnitShares.Of(state, unit, out long strength, out _);
				long speed = CMovementSpeed.SlowestBrigade(unit).Raw / CFixedPoint.Scale;
				if (strength < LeastStrength || speed <= 0)
				{
					continue;
				}
				int idle = CUnitMayLeave.IdleBeside(state, country, unit, listed);
				if (idle < 1)
				{
					continue;
				}
				double score = (double)CMapDistance.Between(state.Province(unit.Location), province, state.MapWidth) / speed;
				if (CArmyRole.Of(unit) == CArmyRole.Headquarters)
				{
					score *= 10;
				}
				score *= System.Math.Max(IdleScale - idle, 1);
				if (unit.IsFighting)
				{
					score *= 2;
				}
				if (score < least && CAiMoves.CanReach(state, country, unit, province.Id, ERouteSafety.Plain)
					&& CUnitMayLeave.Allows(state, country, unit, null, strict: true, notDefending: true))
				{
					best = unit;
					least = score;
				}
			}
			if (best is null)
			{
				return;
			}
			listed.Add(best);
			need -= best.SubUnits.Count;
			CAiMoves.Move(state, country, best, province.Id, safe: false);
		}
	}

	/// <summary>Some edge of the province, not impassable, to a land province stamped with the agent's theatre.</summary>
	private static bool BesideTheatre(CGameState state, CAgent agent, CProvince province)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (edge.Kind != EProvinceEdgeKind.Impassable && !beside.IsSea && beside.TheatreId == agent.Theatre)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether a province has an edge into the area.</summary>
	private static bool HasEdgeInto(CGameState state, int province, HashSet<int> area)
	{
		foreach (CProvinceEdge edge in state.Province(province).Edges)
		{
			if (area.Contains(edge.To))
			{
				return true;
			}
		}
		return false;
	}
}
