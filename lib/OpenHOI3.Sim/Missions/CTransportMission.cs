using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// The transport mission: the fleet waits at its base for passengers, sails to its province - a friendly port - puts
/// them off there and goes home. It asks nothing of what the fleet carries when it is given. See
/// <c>docs/mechanics/transport.md</c>, section 7.
/// </summary>
public static class CTransportMission
{
	/// <summary>Where the fleet is in no battle, the province has a naval base, is in range, and the fleet may lie there.</summary>
	public static bool MayGive(CGameState state, CCountry country, CUnit unit, CProvince province)
	{
		return !unit.IsFighting
			&& CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero
			&& CUnitOrders.IsInRange(state, unit, EUnitOrder.Transport, province)
			&& CMovementRules.MayStandIn(state, country, unit, province);
	}

	public static bool RunHour(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		if (unit.IsFighting || unit.Mission.ReturnToBase)
		{
			return true;
		}
		CProvince target = state.Province(unit.OrderProvince);
		if (!CMovementRules.MayStandIn(state, country, unit, target))
		{
			if (unit.Location != unit.Base)
			{
				CMissions.ReturnToBase(state, country, unit);
			}
			return true;
		}
		if (unit.IsMoving)
		{
			return true;
		}
		bool carrying = unit.Passengers.Count > 0;
		if (unit.Location == unit.Base)
		{
			// It waits at its base for passengers.
			if (carrying)
			{
				CMissions.GoTo(state, country, unit, target.Id);
			}
			return true;
		}
		if (unit.Location == target.Id)
		{
			if (carrying)
			{
				CTransport.PutAllOff(state, unit);
			}
			else
			{
				CMissions.ReturnToBase(state, country, unit);
			}
			return true;
		}
		CMissions.SetPathFrom(state, country, unit, unit.Location, target.Id, safe: true);
		return true;
	}
}
