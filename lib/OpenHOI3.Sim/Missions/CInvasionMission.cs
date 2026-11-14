using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The invasion mission: the fleet sails to a sea beside its province chosen when it is given, and sends its passengers
/// ashore on the province - or on its flanks while too few are bound for it - and waits, or goes home. See
/// <c>docs/mechanics/transport.md</c>, section 10.
/// </summary>
public static class CInvasionMission
{
	/// <summary>
	/// Where the fleet is in no battle, the province is land and in range, the fleet carries something and its first
	/// passenger may stand there. Neither a war nor a coast is asked for.
	/// </summary>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, CProvince province, bool anyRange = false)
	{
		return !unit.IsFighting && !province.IsSea
			&& (anyRange || CUnitOrders.IsInRange(state, unit, EUnitOrder.Invasion, province))
			&& unit.Passengers.Count > 0
			&& CMovementRules.MayStandIn(state, country, unit.Passengers[0], province);
	}

	/// <summary>
	/// Chooses the sea the landing is made from: of the province's neighbours, in the order of its edges, each sea the
	/// fleet may go to - the first, then any nearer by sea to the province's own port sea. A distance of nought counts as
	/// nothing chosen yet, so the next candidate always displaces it.
	/// </summary>
	public static void Start(CGameState state, CCountry country, CUnit unit)
	{
		CProvince target = state.Province(unit.OrderProvince);
		CProvince? named = target.PortSea > 0 && target.PortSea < state.Provinces.Count ? state.Province(target.PortSea) : null;
		long best = 0;
		int chosen = 0;
		foreach (CProvinceEdge edge in target.Edges)
		{
			if (edge.To <= 0 || edge.To >= state.Provinces.Count)
			{
				continue;
			}
			CProvince candidate = state.Province(edge.To);
			if (!candidate.IsSea || !CMovementRules.MayMoveTo(state, country, unit, candidate))
			{
				continue;
			}
			long distance = named is null ? 0 : state.NavalDistances.Between(named, candidate);
			if (best == 0 || distance < best)
			{
				chosen = candidate.Id;
				best = distance;
			}
		}
		unit.Mission.LandingSea = chosen;
	}

	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		CMission mission = unit.Mission;
		if (unit.Base == 0)
		{
			return false;
		}
		if (unit.IsFighting)
		{
			return true;
		}
		if (mission.ReturnToBase || unit.IsMoving)
		{
			return true;
		}
		bool carrying = unit.Passengers.Count > 0;
		if (unit.Location != mission.LandingSea)
		{
			if (!carrying)
			{
				CMissions.ReturnToBase(state, country, unit);
				return false;
			}
			if (mission.LandingSea != 0)
			{
				CMissions.GoTo(state, country, unit, mission.LandingSea);
			}
			return true;
		}

		CProvince target = state.Province(unit.OrderProvince);
		if (!carrying)
		{
			if (!CMovementRules.IsEnemy(state, target.Controller, unit.Owner))
			{
				CMissions.ReturnToBase(state, country, unit);
				return false;
			}
			if (CCombats.In(state, target.Id) is not null)
			{
				return true;
			}
			CMissions.ReturnToBase(state, country, unit);
			return true;
		}
		return SendAshore(state, country, unit, target);
	}

	/// <summary>The fleet at its landing sea with passengers: each sent to the province or to a flank.</summary>
	private static bool SendAshore(CGameState state, CCountry country, CUnit unit, CProvince target)
	{
		List<int> flanks = Flanks(state, unit, target);
		int heading = 0;
		foreach (CUnit passenger in unit.Passengers)
		{
			if (passenger.IsMoving && passenger.Path[0] == target.Id)
			{
				heading++;
			}
		}
		var setAside = new List<CUnit>();
		int nextFlank = 0;
		foreach (CUnit passenger in new List<CUnit>(unit.Passengers))
		{
			if (passenger.IsFighting)
			{
				continue;
			}
			if (!CUnitTotals.CanFight(passenger))
			{
				if (!passenger.IsMoving)
				{
					setAside.Add(passenger);
				}
				continue;
			}
			if (!CMovementRules.MayMoveTo(state, country, passenger, target))
			{
				CMissions.ReturnToBase(state, country, unit);
				return false;
			}
			if (passenger.IsMoving)
			{
				continue;
			}
			int enemies = EnemyLandUnits(state, unit, target);
			if (flanks.Count > 0 && heading < enemies + 6 && heading < 2 * enemies)
			{
				Land(state, country, passenger, flanks[nextFlank % flanks.Count]);
				nextFlank++;
			}
			else
			{
				Land(state, country, passenger, target.Id);
				heading += passenger.SubUnits.Count;
			}
		}
		foreach (CUnit passenger in setAside)
		{
			if (CLanding.OtherFighterBoundFor(state, country, passenger, target.Id))
			{
				Land(state, country, passenger, target.Id);
			}
			else if (flanks.Count > 0 && CLanding.OtherFighterBoundFor(state, country, passenger, flanks[0]))
			{
				Land(state, country, passenger, flanks[0]);
			}
			else
			{
				CMissions.ReturnToBase(state, country, unit);
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// The province's flanks: each neighbour over an edge that is not impassable, that is land, held by an enemy of the
	/// fleet's country, beside the landing sea, and with no enemy land unit in it.
	/// </summary>
	private static List<int> Flanks(CGameState state, CUnit fleet, CProvince target)
	{
		var flanks = new List<int>();
		CProvince sea = state.Province(fleet.Mission.LandingSea);
		foreach (CProvinceEdge edge in target.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable || edge.To <= 0 || edge.To >= state.Provinces.Count)
			{
				continue;
			}
			CProvince flank = state.Province(edge.To);
			if (flank.IsSea || !CMovementRules.IsEnemy(state, flank.Controller, fleet.Owner) || sea.EdgeTo(flank.Id) is null
				|| EnemyLandUnits(state, fleet, flank) > 0)
			{
				continue;
			}
			flanks.Add(flank.Id);
		}
		return flanks;
	}

	/// <summary>How many land units of the fleet's country's enemies are in a province.</summary>
	private static int EnemyLandUnits(CGameState state, CUnit fleet, CProvince province)
	{
		int count = 0;
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (CMovementRules.IsLandUnit(other) && !other.IsRemoved && CMovementRules.IsEnemy(state, other.Owner, fleet.Owner))
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>Gives a passenger the way ashore, the safe way, where it may be sent there; and notes the landing.</summary>
	private static void Land(CGameState state, CCountry country, CUnit passenger, int province)
	{
		if (passenger.Location == province || !CMovementRules.MayMoveTo(state, country, passenger, state.Province(province)))
		{
			return;
		}
		List<int> route = CRouteFinder.Find(state, country, passenger, passenger.Location, province, safe: true) ?? new List<int>();
		CMovement.SetPath(passenger, route, clear: true);
		CInvasionWarnings.Note(state, passenger);
	}
}
