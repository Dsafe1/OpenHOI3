using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's subordinates sorted onto its lists: over the whole country's units, those whose nearest agent up their
/// chain is this one - land units, those above a division, fleets, the fleets that carry troops, air units by range and
/// those that carry troops. Slot 78's sorting, without the committing and holding back, which are the third piece's. See
/// <c>docs/mechanics/agents.md</c>, section 5.1.
/// </summary>
public static class CSubordinates
{
	/// <summary>Carrying troops: a whole unit of carrying or more.</summary>
	private const long CarriesTroops = 1;

	public static void Sort(CGameState state, CCountry country, CAgent agent)
	{
		agent.Land.Clear();
		agent.NonDivisions.Clear();
		agent.Fleets.Clear();
		agent.TransportFleets.Clear();
		agent.Air.Clear();
		agent.AirTransports.Clear();
		agent.InvasionTargets.Clear();
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (!unit.IsRemoved && unit.Kind == EUnitKind.Navy && unit.Order == EUnitOrder.Invasion
				&& unit.OrderProvince > 0)
			{
				agent.InvasionTargets.Add(unit.OrderProvince);
			}
			if (unit.IsRemoved || !Belongs(unit, agent))
			{
				continue;
			}
			switch (unit.Kind)
			{
				case EUnitKind.Air:
					InsertByRange(agent.Air, unit);
					if (Transport.CTransport.Capacity(unit) >= CarriesTroops)
					{
						agent.AirTransports.Add(unit);
					}
					break;
				case EUnitKind.Navy:
					agent.Fleets.Add(unit);
					if (Transport.CTransport.Capacity(unit) >= CarriesTroops
						&& (unit.Passengers.Count > 0 || !CFleetRegroup.NeedsPort(state, unit)))
					{
						agent.TransportFleets.Add(unit);
					}
					break;
				default:
					agent.Land.Add(unit);
					// One the sealift or a drop has claimed goes on no other list, a headquarters as much as a division, and
					// so is not pulled in (FINDINGS-agents.md, C.6, step 5; FINDINGS-liveread.md, section 48).
					if (unit.Kind != EUnitKind.Division && !CTransportReservation.IsReserved(state, unit))
					{
						agent.NonDivisions.Add(unit);
					}
					break;
			}
		}
		agent.ReplanSubordinates = false;
		CCommitment.Run(state, country, agent);
	}

	/// <summary>
	/// Whether the unit is the agent's: the nearest agent up its chain is this one, or it is a carrier's air group whose
	/// fleet's is.
	/// </summary>
	private static bool Belongs(CUnit unit, CAgent agent)
	{
		if (ReferenceEquals(CAgentUnits.NearestAgent(unit), agent))
		{
			return true;
		}
		return unit.CarriedBy?.Unit is { } fleet && ReferenceEquals(CAgentUnits.NearestAgent(fleet), agent);
	}

	/// <summary>Kept in order by range, the shortest first, a unit after its equals (ours: the original's direction not read).</summary>
	private static void InsertByRange(List<CUnit> air, CUnit unit)
	{
		CFixedPoint range = CUnitOrders.RangeOf(unit);
		int at = air.Count;
		while (at > 0 && CUnitOrders.RangeOf(air[at - 1]) > range)
		{
			at--;
		}
		air.Insert(at, unit);
	}
}
