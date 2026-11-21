using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// A rebel unit worn to nothing: in its own hour, one in no battle whose brigades' strength adds up to less than a tenth
/// of a point is put on the queue of units to remove, and goes at the hour's end. The original asks it of every unit of
/// the rebels' tag, whatever its kind, and of one with no brigades too. See <c>docs/mechanics/rebels.md</c>, section 13.
/// </summary>
public static class CSpentRebels
{
	/// <summary>The strength all a rebel unit's brigades must have together to stay, in thousandths.</summary>
	public const long LeastStrength = 100;

	/// <summary>Whether a unit is to go: the rebels', in no battle, its brigades under a tenth of a point together.</summary>
	public static bool IsSpent(CUnit unit)
	{
		if (unit.IsRemoved || unit.Combats.Count > 0 || !CRebelFactions.IsRebels(unit.Owner))
		{
			return false;
		}
		long strength = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			strength += brigade.Strength.Raw;
		}
		return strength < LeastStrength;
	}

	/// <summary>The hour's look at every unit of the rebels.</summary>
	public static void RunHourly(CGameState state)
	{
		if (state.Country(CRebelFactions.Tag) is not { } rebels)
		{
			return;
		}
		foreach (CUnit unit in rebels.AllUnits())
		{
			if (IsSpent(unit))
			{
				CPendingUnitRemovals.Queue(state, unit);
			}
		}
	}
}
