using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// Each province's own day, after every country's, one province after another and each whole before the next, in the
/// original's order as far as it is built: the repair; the suppression in and around it; its modifier made again where
/// the repair carried a building across a whole level; the revolt risk's step; and the return of land held at peace.
/// So a province given back early in the pass is no longer its holder's when a later one's suppression is counted. See
/// <c>docs/mechanics/revolt.md</c>, section 3, and <c>docs/mechanics/war.md</c>, section 11.
/// </summary>
public static class CProvinceDay
{
	public static void RunDaily(CGameState state)
	{
		long speed = CProvinceRepair.Speed(state);
		// Each province's own suppression, worked out once for the pass and forgotten where its holder changes.
		var known = new long[state.Provinces.Count];
		System.Array.Fill(known, -1);
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0)
			{
				continue;
			}
			bool wholeLevel = CProvinceRepair.Repair(state, province, speed);
			province.Suppression = new CFixedPoint(CSuppression.Near(state, province, known));
			if (wholeLevel)
			{
				CProvinceModifiers.RebuildOne(state, province);
			}
			CRevoltRisk.StepDaily(state, province);
			Countries.CCountryTag? holder = province.Controller;
			COccupiedLand.Return(state, province);
			if (province.Controller != holder)
			{
				known[province.Id] = -1;
			}
		}
	}
}
