using System.Collections.Generic;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A battle between air units: what may fight what, what an air unit does about the
/// province it has entered or stands in, and the battle's four hours. See
/// <c>docs/mechanics/aircombat.md</c>, sections 7 and 8.
/// </summary>
public static class CAirBattle
{
	/// <summary>An air battle is over at its fourth hour whoever has lost.</summary>
	public const int Hours = 4;

	/// <summary>The hours a unit found in a battle cannot be taken into another, that hour counted.</summary>
	public const int Grace = 2;

	/// <summary>
	/// Whether one air unit may fight another: two air units of two countries at war,
	/// neither falling back - and, where either sits at its own base, this one under a
	/// fighting order and the one at its base fit to fly. Two units in the air may fight
	/// whatever their orders.
	/// </summary>
	public static bool MayEngage(CGameState state, CUnit unit, CUnit other)
	{
		if (unit == other || unit.Owner == other.Owner
			|| unit.Kind != EUnitKind.Air || other.Kind != EUnitKind.Air
			|| unit.IsRemoved || other.IsRemoved || unit.IsRetreating || other.IsRetreating
			|| !CMovementRules.IsEnemy(state, unit.Owner, other.Owner))
		{
			return false;
		}
		if (CAirMissions.IsAtOwnBase(unit)
			&& (!CAirMissions.IsAirMission(unit.Order) || !CAirMissions.IsFitToFly(state, unit)))
		{
			return false;
		}
		if (CAirMissions.IsAtOwnBase(other)
			&& (!CAirMissions.IsAirMission(unit.Order) || !CAirMissions.IsFitToFly(state, other)))
		{
			return false;
		}
		return true;
	}

	/// <summary>
	/// What an air unit does about a province: it joins the air battle there if one will
	/// have it, and otherwise looks for an enemy air unit to start one with. Only a unit
	/// under a fighting order does either, and not in its hour of grace.
	/// </summary>
	public static void Look(CGameState state, CUnit unit, CProvince province)
	{
		if (province.Id == 0 || unit.IsRemoved || unit.SubUnits.Count == 0 || unit.Location != province.Id
			|| unit.NoBattleHours > 0 || !CAirMissions.IsAirMission(unit.Order))
		{
			return;
		}
		if (!CCombats.HasCombatIn(unit, province.Id)
			&& CCombats.In(state, province.Id, ECombatKind.Air) is { } there)
		{
			CCombats.Offer(state, there, unit);
		}
		if (!CCombats.HasCombatIn(unit, province.Id))
		{
			Start(state, province, unit);
		}
	}

	/// <summary>
	/// Starts a battle with the first air unit of the province that may be fought, where
	/// the province has no air battle already. Everyone else there is offered a side.
	/// </summary>
	public static CCombat? Start(CGameState state, CProvince province, CUnit unit)
	{
		if (CCombats.HasCombatIn(unit, province.Id) || unit.NoBattleHours > 0
			|| !CAirMissions.IsAirMission(unit.Order))
		{
			return null;
		}
		CUnit? found = null;
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (MayEngage(state, unit, other))
			{
				found = other;
				break;
			}
		}
		if (found is null || CCombats.In(state, province.Id, ECombatKind.Air) is not null)
		{
			return null;
		}

		var combat = new CCombat(province.Id, ECombatKind.Air);
		state.Combats.Add(combat);
		Join(combat.Attacker, unit);
		Join(combat.Defender, found);
		foreach (CUnit other in new List<CUnit>(state.UnitsIn(province.Id)))
		{
			if (other != unit && other != found)
			{
				CCombats.Offer(state, combat, other);
			}
		}
		return combat;
	}

	/// <summary>Takes a unit onto a side. Its wings forget whatever they were firing at.</summary>
	public static void Join(CCombatSide side, CUnit unit)
	{
		side.Add(unit);
		foreach (CSubUnit wing in unit.SubUnits)
		{
			wing.TargetId = 0;
		}
	}

	/// <summary>
	/// One hour of a battle in the air, attacker first at every step: the two dice,
	/// what the hour makes of each wing, the shooting, the settling, and at the fourth
	/// hour whether a side has lost - and the end, whether one has or not.
	/// </summary>
	public static bool RunHour(CGameState state, CCombat combat)
	{
		combat.HasBegun = true;
		combat.Duration++;
		if (combat.Hour == CCombat.NotStarted)
		{
			RollDice(state, combat);
			combat.Hour = 0;
		}
		RollDice(state, combat);

		CAirModifiers.Apply(state, combat, combat.Attacker);
		CAirModifiers.Apply(state, combat, combat.Defender);
		CAirFire.Attack(state, combat, combat.Attacker);
		CAirFire.Attack(state, combat, combat.Defender);
		Settle(combat.Attacker);
		Settle(combat.Defender);

		if (combat.Duration >= Hours)
		{
			if (CNavalBattle.HasLost(combat.Attacker))
			{
				CCombats.Conclude(state, combat, attackerLost: true);
				RetreatAll(state, combat.Attacker);
				return true;
			}
			if (CNavalBattle.HasLost(combat.Defender))
			{
				CCombats.Conclude(state, combat, attackerLost: false);
				RetreatAll(state, combat.Defender);
				return true;
			}
		}
		combat.Hour++;
		return combat.Duration >= Hours;
	}

	private static void RollDice(CGameState state, CCombat combat)
	{
		combat.Attacker.Dice = state.Random.Next(10);
		combat.Defender.Dice = state.Random.Next(10);
	}

	/// <summary>Takes every wing's notes off it: a hundredth of each, and neither more than it has.</summary>
	private static void Settle(CCombatSide side)
	{
		foreach (CUnit unit in side.Units)
		{
			foreach (CSubUnit wing in unit.SubUnits)
			{
				CCombatDamage.Settle(wing);
			}
		}
	}

	private static void RetreatAll(CGameState state, CCombatSide side)
	{
		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			CAirRetreat.Retreat(state, unit, side);
		}
	}
}
