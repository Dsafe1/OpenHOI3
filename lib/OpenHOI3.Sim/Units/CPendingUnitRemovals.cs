using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The original's queue of units to be removed: a unit is put on it by its number, and the very end of the hour takes each
/// off in turn and out of the game - the hour's last step, so that nothing is freed in the middle of a pass. A number whose
/// unit is gone by then, or is queued twice, is passed over. Each unit goes as the original's <c>CGameState::RemoveUnit</c>
/// with its flags 0 and 1 takes it: its owner loses its share of officers, what it carries goes with it, and what it
/// commanded is left at the top of the order of battle. See <c>docs/mechanics/effects.md</c>, section 9.
/// </summary>
public static class CPendingUnitRemovals
{
	public static void Queue(CGameState state, CUnit unit)
	{
		state.PendingUnitRemovals.Add(unit.Id);
	}

	/// <summary>The end of the hour: every queued unit still in the game taken out of it, in the order queued.</summary>
	public static void Drain(CGameState state)
	{
		if (state.PendingUnitRemovals.Count == 0)
		{
			return;
		}
		var queued = new System.Collections.Generic.List<int>(state.PendingUnitRemovals);
		state.PendingUnitRemovals.Clear();
		foreach (int id in queued)
		{
			if (Find(state, id) is { } unit)
			{
				Remove(state, unit);
			}
		}
	}

	/// <summary>
	/// <c>RemoveUnit</c> with its first flag clear and its second set: the unit and everything it carries lose their
	/// officers, then go, what they commanded left with nobody above it.
	/// </summary>
	public static void Remove(CGameState state, CUnit unit)
	{
		LoseOfficers(state, unit);
		foreach (CUnit passenger in unit.Passengers)
		{
			LoseOfficers(state, passenger);
		}
		CCombatRetreat.RemoveFromGame(state, unit, childrenToTop: true);
		state.ForgetPositions();
	}

	/// <summary>
	/// The officers a unit takes with it out of the game: its brigades' <c>officers</c> together, times its strength over
	/// the most its brigades could have, times its country's officer ratio - the lender's for a unit on loan - off the
	/// country's officers, never below nought. Nothing where its brigades have no most at all.
	/// </summary>
	public static void LoseOfficers(CGameState state, CUnit unit)
	{
		long officers = 0;
		long strength = 0;
		long most = 0;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			officers += brigade.Stats[CSubUnitType.Officers].Raw;
			strength += brigade.Strength.Raw;
			most += brigade.Stats[CSubUnitType.MaxStrength].Raw;
		}
		if (most <= 0 || state.Country(unit.FiguresOwner) is not { } country)
		{
			return;
		}
		// In whole thousandths at each step, as the original's 64-bit sums are cut back to 32 bits.
		int share = unchecked((int)(strength * CFixedPoint.Scale / most));
		int lost = unchecked((int)((long)share * officers / CFixedPoint.Scale));
		long fall = country.OfficerRatio.Raw * lost / CFixedPoint.Scale;
		country.Officers = new CFixedPoint(System.Math.Max(0, country.Officers.Raw - fall));
	}

	private static CUnit? Find(CGameState state, int id)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.Id == id && !unit.IsRemoved)
				{
					return unit;
				}
			}
		}
		return null;
	}
}
