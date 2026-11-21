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
	/// The hour's look at every country nobody plays: its land units alone, the original's hourly pass asking a unit's
	/// slot 15 (reversing/findings/FINDINGS-aigaps.md, A) - an empty fleet or air unit of such a country is not taken here.
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
				if (Movement.CMovementRules.IsLandUnit(unit) && IsToGo(unit))
				{
					state.Outbox.Add(new CDisbandCommand(unit.Id));
				}
			}
		}
	}
}
