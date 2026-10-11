using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// Where a beaten air unit goes. It looks at the provinces beside the battle that it may
/// fly to and takes the sea province with the most naval bases around it; with no sea
/// beside it, its base's province. The original's - a unit beside the sea flies out to
/// sea and not home. See <c>docs/mechanics/aircombat.md</c>, section 8.
/// </summary>
public static class CAirRetreat
{
	/// <summary>What a sea province is worth to fall back to, and what each port beside it adds.</summary>
	private const long SeaWorth = 1000;
	private const long PortWorth = 2000;

	/// <summary>A province has to be worth more than this to be fallen back to at all.</summary>
	private const long LeastWorth = 10;

	/// <summary>
	/// A beaten unit leaves the battle and falls back. With nowhere to go, or no way
	/// there, it is removed from the game. It keeps its order.
	/// </summary>
	public static void Retreat(CGameState state, CUnit unit, CCombatSide side)
	{
		int to = side.Combat.Province != unit.Location ? unit.Location : FindRetreatProvince(state, unit);
		unit.Combats.Remove(side.Combat);
		side.Remove(state, unit);
		if (state.Country(unit.Owner) is not { } country || to == 0)
		{
			CCombatRetreat.RemoveFromGame(state, unit);
			return;
		}
		if (to == unit.Location)
		{
			CMovement.Cancel(unit);
			return;
		}
		List<int>? route = CRouteFinder.Find(state, country, unit, unit.Location, to);
		if (route is null)
		{
			CCombatRetreat.RemoveFromGame(state, unit);
			return;
		}
		CMovement.SetPath(unit, route, clear: true);
		unit.IsRetreating = true;
	}

	/// <summary>The province a beaten air unit falls back to, or 0.</summary>
	public static int FindRetreatProvince(CGameState state, CUnit unit)
	{
		if (state.Country(unit.Owner) is not { } country)
		{
			return 0;
		}
		CProvince here = state.Province(unit.Location);
		int best = 0;
		long most = 0;
		foreach (CProvinceEdge edge in here.Edges)
		{
			CProvince province = state.Province(edge.To);
			// A land province beside it is worth nothing, which is never enough.
			if (!province.IsSea || !CMovementRules.MayStandIn(state, country, unit, province))
			{
				continue;
			}
			long worth = SeaWorth;
			foreach (CProvinceEdge further in province.Edges)
			{
				CProvince shore = state.Province(further.To);
				if (further.Kind != EProvinceEdgeKind.Impassable && !shore.IsSea
					&& CMovementRules.BuildingLevel(state, shore, CMovementRules.NavalBase) >= CFixedPoint.One
					&& CMovementRules.MayStandIn(state, country, unit, shore))
				{
					worth += PortWorth;
				}
			}
			if (worth > LeastWorth && (best == 0 || worth > most))
			{
				best = province.Id;
				most = worth;
			}
		}
		if (best != 0)
		{
			return best;
		}
		return unit.Base;
	}
}
