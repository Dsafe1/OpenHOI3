using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// Where a fleet goes when its side has lost a battle at sea: the port it lies off, or
/// the water with the most friendly harbours about it. See
/// <c>docs/mechanics/navalcombat.md</c>, section 6.
/// </summary>
public static class CNavalRetreat
{
	/// <summary>What a port is worth to fall back to, a sea province, and each harbour beside that sea.</summary>
	private const long Port = 10000;
	private const long Sea = 1000;
	private const long HarbourBeside = 2000;

	/// <summary>A province has to be worth more than this to be fallen back to at all.</summary>
	private const long LeastWorth = 10;

	/// <summary>
	/// A beaten fleet leaves the battle and falls back. With nowhere to go, or no way
	/// there, it is removed from the game. It keeps its order.
	/// </summary>
	public static void Retreat(CGameState state, CUnit fleet, CCombatSide side)
	{
		int to = side.Combat.Province != fleet.Location ? fleet.Location : FindRetreatProvince(state, fleet);
		fleet.Combats.Remove(side.Combat);
		side.Remove(state, fleet);
		if (state.Country(fleet.Owner) is not { } country || to == 0)
		{
			Units.CPendingUnitRemovals.Remove(state, fleet);
			return;
		}
		if (to == fleet.Location)
		{
			CMovement.Cancel(fleet);
			return;
		}
		List<int>? route = CRouteFinder.Find(state, country, fleet, fleet.Location, to);
		if (route is null)
		{
			Units.CPendingUnitRemovals.Remove(state, fleet);
			return;
		}
		CMovement.SetPath(fleet, route, clear: true);
		fleet.IsRetreating = true;
	}

	/// <summary>The province next to a fleet that is worth most to fall back to, or 0.</summary>
	public static int FindRetreatProvince(CGameState state, CUnit fleet)
	{
		if (state.Country(fleet.Owner) is not { } country)
		{
			return 0;
		}
		CProvince here = state.Province(fleet.Location);
		int best = 0;
		long most = 0;
		foreach (CProvinceEdge edge in here.Edges)
		{
			CProvince province = state.Province(edge.To);
			if (!CMovementRules.MayStandIn(state, country, fleet, province)
				|| CMovementRules.IsCrossingBlocked(state, country, fleet, edge))
			{
				continue;
			}
			long worth;
			if (province.IsSea)
			{
				worth = Sea;
				foreach (CProvinceEdge beside in province.Edges)
				{
					CProvince harbour = state.Province(beside.To);
					if (beside.Kind != EProvinceEdgeKind.Impassable && !harbour.IsSea
						&& CMovementRules.BuildingLevel(state, harbour, CMovementRules.NavalBase) >= CFixedPoint.One
						&& CMovementRules.MayStandIn(state, country, fleet, harbour))
					{
						worth += HarbourBeside;
					}
				}
			}
			else
			{
				if ((here.IsSea && province.PortSea != here.Id)
					|| !CCombatRetreat.MayRetreatTo(state, country, fleet, province))
				{
					continue;
				}
				worth = Port;
			}
			if (worth > LeastWorth && (best == 0 || worth > most))
			{
				best = province.Id;
				most = worth;
			}
		}
		return best;
	}
}
