using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Whether a division is spoken for by a drop or a sea lift: an airborne army with a staging province and a paratroop
/// brigade; or one sent across the sea to its plan's debark while it walks to or waits in the embark area, or is bound
/// into the debark area from outside it. The original's <c>CAIUnit::IsUnitReservedForTransport</c>. See
/// <c>docs/mechanics/moving.md</c>, section 2.
/// </summary>
public static class CTransportReservation
{
	public static bool IsReserved(CGameState state, CUnit unit)
	{
		if (unit.Kind is EUnitKind.Navy or EUnitKind.Air)
		{
			return false;
		}
		if (unit.Staging > 0 && unit.SubUnits.Exists(brigade => brigade.Type.Key == "paratrooper_brigade"))
		{
			return true;
		}
		if (unit.SealiftTarget <= 0 || Topmost(unit) is not { Unit: { } headquarters })
		{
			return false;
		}
		CUnitPlan plan = headquarters.Plan;
		if (plan.Debark != unit.SealiftTarget)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int destination = CAgentUnits.DestinationOrPlace(unit);
		CSupplyArea? bound = network.AreaOf(destination);
		CSupplyArea? debark = network.AreaOf(plan.Debark);
		if (bound is not null && ReferenceEquals(bound, debark) && !ReferenceEquals(network.AreaOf(unit.Location), debark))
		{
			return true;
		}
		return plan.Embark > 0 && bound is not null && ReferenceEquals(bound, network.AreaOf(plan.Embark));
	}

	/// <summary>The topmost agent up the unit's chain.</summary>
	private static CAgent? Topmost(CUnit unit)
	{
		CAgent? found = null;
		for (CUnit? at = unit; at is not null; at = at.Parent)
		{
			if (at.Agent is { } agent)
			{
				found = agent;
			}
		}
		return found;
	}
}
