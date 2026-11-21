using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What becomes of a unit left with no brigades: each hour one not allowed to stand empty is disbanded. A country nobody
/// plays has it done here; a played country's has it done by the player's own client, which alone knows what is selected
/// - a selected unit is left until it is let go. See <c>docs/mechanics/reorganise.md</c>, section 5.
/// </summary>
public static class CEmptyUnits
{
	/// <summary>Whether a unit is to go: it has no brigades and was not made to stand so. Nothing here upgrades a brigade out of its unit, which also keeps one in the original.</summary>
	public static bool IsToGo(CUnit unit)
	{
		return unit.SubUnits.Count == 0 && !unit.IsAllowedEmpty && !unit.IsRemoved;
	}

	/// <summary>
	/// The hour's look at every country nobody plays: its empty units of every kind. The original has two arms - its
	/// country's hourly pass takes an empty land unit out at once, and a unit's own hour posts a disband for an empty unit
	/// whatever it is (reversing/findings/FINDINGS-effectrun.md, section 3.13) - and an empty unit has no officers to
	/// lose by either, so the one disband stands for both.
	/// </summary>
	public static void RunHourly(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (state.Played.Contains(country.Tag))
			{
				continue;
			}
			foreach (CUnit unit in country.AllUnits())
			{
				if (IsToGo(unit))
				{
					state.Outbox.Add(new CDisbandCommand(unit.Id));
				}
			}
		}
	}
}
