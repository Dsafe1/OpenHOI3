using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Transport;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// Units made one: whether the merge button may be pressed for them, which of them the rest go into, and one unit merged
/// into another - the original's merge rule, its merge command's choice and the merge the join orders use too. See
/// <c>docs/mechanics/selection.md</c>, section 5.4.
/// </summary>
public static class CUnitMerge
{
	/// <summary>
	/// Whether these units may be merged: two or more, each in no battle, not falling back, going nowhere, in the first's
	/// province, with no attack delay, of the first's level and that a division, a fleet or an air unit, a group of carrier
	/// aircraft only with others, on the first's carrier where it is an air unit on one - and land units holding no more
	/// brigades together than a division may. The owner is not asked.
	/// </summary>
	public static bool MayMerge(CGameState state, CCountry country, IReadOnlyList<CUnit> units)
	{
		if (units.Count < 2)
		{
			return false;
		}
		CUnit first = units[0];
		bool firstCag = CCarriers.IsCarrierAirGroup(first);
		int brigades = 0;
		foreach (CUnit unit in units)
		{
			if (unit.Lender is not null || unit.IsFighting || unit.IsRetreating || unit.IsMoving || unit.Location != first.Location
				|| unit.AttackDelay > CFixedPoint.Zero || unit.Kind != first.Kind || first.Kind < EUnitKind.Division
				|| CCarriers.IsCarrierAirGroup(unit) != firstCag)
			{
				return false;
			}
			if (first.Kind == EUnitKind.Air && first.CarriedBy is { } carrier && !ReferenceEquals(unit.CarriedBy, carrier))
			{
				return false;
			}
			brigades += unit.SubUnits.Count;
		}
		return !CMovementRules.IsLandUnit(first) || brigades <= CUnitAvailability.MostBrigades(state, country);
	}

	/// <summary>Whether a unit, or any unit above it, is run by an AI agent: the button then does nothing.</summary>
	public static bool HasAgentAbove(IReadOnlyList<CUnit> units)
	{
		foreach (CUnit unit in units)
		{
			for (CUnit? above = unit; above is not null; above = above.Parent)
			{
				if (above.Agent is not null)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// The unit the rest are merged into: the highest of brigades times ten, its strength share in tenths cut down and its
	/// leader's skill twice over, the first of equals; the first unit where none scores above nought.
	/// </summary>
	public static CUnit Into(IReadOnlyList<CUnit> units)
	{
		CUnit? best = null;
		long most = 0;
		foreach (CUnit unit in units)
		{
			long score = unit.SubUnits.Count * 10L + StrengthShare(unit) * 10 / 1000;
			if (unit.Leader is { } leader)
			{
				score += leader.Skill * 2L;
			}
			if (score > most)
			{
				most = score;
				best = unit;
			}
		}
		return best ?? units[0];
	}

	/// <summary>The unit's strength over its brigades' most, in thousandths; -1 where the most is nought.</summary>
	public static long StrengthShare(CUnit unit)
	{
		long strength = 0;
		long most = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			strength += subUnit.Strength.Raw;
			most += subUnit.Stats[CSubUnitType.MaxStrength].Raw;
		}
		return most == 0 ? -1 : strength * CFixedPoint.Scale / most;
	}

	/// <summary>
	/// One unit's brigades handed to another, in their order; a fleet's passengers to a fleet and an air unit's to an air
	/// unit; the more skilled leader kept, or the giver's where the taker has none. The giver is left empty, to be taken
	/// out of the game by the caller.
	/// </summary>
	public static void MergeInto(CGameState state, CUnit into, CUnit from)
	{
		if (ReferenceEquals(into, from))
		{
			return;
		}
		foreach (CSubUnit subUnit in from.SubUnits.ToArray())
		{
			from.SubUnits.Remove(subUnit);
			into.SubUnits.Add(subUnit);
			subUnit.Unit = into;
		}
		if (from.Kind == EUnitKind.Navy && into.Kind == EUnitKind.Navy)
		{
			foreach (CUnit passenger in from.Passengers.ToArray())
			{
				from.Passengers.Remove(passenger);
				CTransport.Board(into, passenger);
				passenger.Staging = 0;
			}
		}
		else if (from.Kind == EUnitKind.Air && into.Kind == EUnitKind.Air)
		{
			foreach (CUnit passenger in from.Passengers.ToArray())
			{
				from.Passengers.Remove(passenger);
				CAirTransport.Board(into, passenger);
				passenger.SealiftTarget = 0;
			}
		}
		// The taker's leader is freed where the giver's is more skilled; the taker with none takes the giver's. His banked
		// hours go, and nobody's organisation.
		if (from.Leader is { } leader && (into.Leader is null || leader.Skill > into.Leader.Skill))
		{
			CLeaders.SetLeader(state, into, leader, true, false);
		}
		state.ForgetPositions();
	}

	/// <summary>
	/// The merge command's work: every unit but the one chosen merged into it and taken out of the game, and the one chosen
	/// given the best free leader where it has none and its country chooses its own.
	/// </summary>
	public static CUnit Merge(CGameState state, IReadOnlyList<CUnit> units)
	{
		CUnit into = Into(units);
		foreach (CUnit unit in units)
		{
			if (ReferenceEquals(unit, into))
			{
				continue;
			}
			MergeInto(state, into, unit);
			CCombatRetreat.RemoveFromGame(state, unit);
		}
		if (into.Leader is null && state.Country(into.Owner) is { AutoAssignLeaders: true } country)
		{
			CLeaders.AssignBest(state, country, into);
		}
		return into;
	}
}
