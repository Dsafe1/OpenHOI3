using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Revolts;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// Whether an area, or a block of areas, borders land held against it: some area beside it whose controller is at war
/// with its own, or either of the two the rebels. The original's <c>COwnerArea_TouchesHostileArea</c> and
/// <c>RegionGroup_BordersEnemy</c>. See <c>docs/mechanics/agents.md</c>, section 7.5.
/// </summary>
public static class CAreaHostility
{
	public static bool TouchesHostileArea(CGameState state, CSupplyArea area)
	{
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (beside.Controller == area.Controller)
			{
				continue;
			}
			if (beside.Controller.Value == CRebelFactions.Rebels || area.Controller.Value == CRebelFactions.Rebels
				|| CCountryLists.AtWar(state, area.Controller, beside.Controller))
			{
				return true;
			}
		}
		return false;
	}

	public static bool GroupBordersEnemy(CGameState state, CSupplyGroup group)
	{
		foreach (CSupplyArea area in group.Areas)
		{
			if (TouchesHostileArea(state, area))
			{
				return true;
			}
		}
		return false;
	}
}
