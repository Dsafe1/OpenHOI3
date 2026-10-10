using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Missions;

/// <summary>
/// How an air mission weighs an enemy air unit it might go for: never one at its own
/// base, never one that enough wings are after already, and otherwise by its strength.
/// See <c>docs/mechanics/aircombat.md</c>, section 5.
/// </summary>
public static class CAirTargets
{
	/// <summary>What a search starts from: anything that is scored at all beats it.</summary>
	public const long Nothing = -10000;

	/// <summary>With this unit's own, more wings than this are not sent after one enemy unit.</summary>
	private const int MostWingsAfterOne = 3;

	/// <summary>
	/// What a candidate is worth to a unit looking for a target: its strength in all, or
	/// null where it is never to be picked.
	/// </summary>
	/// <param name="needsWings">The intercept's one test more: a candidate with no wings is never picked.</param>
	public static long? Score(CGameState state, CUnit unit, CUnit candidate, bool needsWings)
	{
		if (CAirMissions.IsAtOwnBase(candidate)
			|| !CMovementRules.IsEnemy(state, candidate.Owner, unit.Owner)
			|| (needsWings && candidate.SubUnits.Count == 0))
		{
			return null;
		}

		// The wings already after it: every air unit at war with it whose mission has it for target.
		int after = 0;
		foreach (CCountry country in state.Countries)
		{
			if (!CMovementRules.IsEnemy(state, country.Tag, candidate.Owner))
			{
				continue;
			}
			foreach (CUnit other in country.AllUnits())
			{
				if (other.Kind == EUnitKind.Air && !other.IsRemoved && CAirMissions.IsAirMission(other.Order)
					&& other.Mission.Target == candidate)
				{
					after += other.SubUnits.Count;
				}
			}
		}
		if (after > 0 && unit.SubUnits.Count + after > MostWingsAfterOne)
		{
			return null;
		}
		return CUnitTotals.Strength(candidate).Raw;
	}

	/// <summary>The next province on a unit's path, or 0 where it is going nowhere.</summary>
	public static int NextProvince(CUnit unit)
	{
		return unit.Path.Count > 0 ? unit.Path[0] : 0;
	}
}
