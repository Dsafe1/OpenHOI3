using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A digit pressed: the selection is let go and the group's units are selected, in its order, each that is still the
/// player's, not selected yet and of the first one's kind; one no longer there is skipped. See
/// <c>docs/mechanics/selection.md</c>, section 7.
/// </summary>
public static class CSelectionGroups
{
	public static void Recall(CCountry player, int group, CSelection selection)
	{
		selection.Clear();
		if (group < 0 || group >= CCountry.SelectionGroupCount)
		{
			return;
		}
		var units = new Dictionary<int, CUnit>();
		foreach (CUnit unit in player.AllUnits())
		{
			units.TryAdd(unit.Id, unit);
		}
		foreach (int id in player.SelectionGroups[group])
		{
			if (units.TryGetValue(id, out CUnit? unit))
			{
				selection.Add(unit, player.Tag);
			}
		}
	}
}
