using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A unit's strength and organisation, each the mean of its brigades' shares of their most, in thousandths. The
/// original's <c>GetUnitAverageStrengthAndOrganisation</c>. See <c>docs/mechanics/moving.md</c>, section 2.
/// </summary>
public static class CUnitShares
{
	public static void Of(CGameState state, CUnit unit, out long strength, out long organisation)
	{
		strength = 0;
		organisation = 0;
		int count = unit.SubUnits.Count;
		if (count == 0)
		{
			return;
		}
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			strength += CFleetRegroup.StrengthShare(brigade);
			organisation += CFleetRegroup.OrganisationShare(state, brigade);
		}
		strength /= count;
		organisation /= count;
	}
}
