using System;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One side's hour of shooting at sea: its positioning creeps up, and then each ship in
/// turn picks a target, fires at it, and closes its distance or opens it; last, the
/// fleets that are breaking away count an hour more. Every roll is the game's own, in
/// the original's order. See <c>docs/mechanics/navalcombat.md</c>, section 5.
/// </summary>
public static class CNavalFire
{
	public const string SeaAttack = "sea_attack";
	public const string MaximumSpeed = "maximum_speed";
	public const string OrganisationDice = "NAVAL_COMBAT_ORG_DICE_SIZE";
	public const string StrengthDice = "NAVAL_COMBAT_STR_DICE_SIZE";
	public const string CriticalChance = "NAVAL_COMBAT_CRITICAL_HIT_DAMAGE_CHANCE";
	public const string CriticalDamage = "NAVAL_COMBAT_CRITICAL_HIT_DAMAGE_MUL";
	public const string StrengthDamage = "NAVAL_COMBAT_STR_DAMAGE_MODIFIER";
	public const string OrganisationDamage = "NAVAL_COMBAT_ORG_DAMAGE_MODIFIER";
	public const string AvoidChance = "CHANCE_TO_AVOID_HIT_AT_NO_DEF";

	/// <summary>How many times a crowded enemy's penalty goes onto each of the two dice.</summary>
	private const long OrganisationDiceForPenalty = 5000;
	private const long StrengthDiceForPenalty = 3000;

	/// <summary>No shot is avoided more surely than this.</summary>
	private const long MostAvoidChance = 99000;

	/// <summary>What the strength a convoy's ship takes off a raider is divided by, in thousandths: eighty.</summary>
	private const long ConvoyStrengthDivisor = 80000;

	public static void Attack(CGameState state, CCombat combat, CCombatSide side)
	{
		CCombatSide other = combat.Other(side);
		CTraitGain.BankHour(state, side);
		Creep(state, side);
		long theirPenalty = CNavalBattle.StackingPenalty(state, other);
		long ourSpeed = AverageSpeed(side);

		foreach (CUnit fleet in side.Units)
		{
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				CSubUnit? target = null;
				if (ship.Stats[SeaAttack].Raw == 0 || ship.Organisation.Raw < 1)
				{
					ship.TargetId = 0;
				}
				else
				{
					target = CNavalTargets.Pick(state, combat, side, other, ship);
					if (target is not null)
					{
						Fire(state, combat, side, fleet, ship, target, theirPenalty);
					}
				}
				Move(state, fleet, ship, target is not null, ourSpeed, other);
			}
		}
		CNavalDisengage.CountHour(state, combat, side);
	}

	/// <summary>
	/// A side's positioning comes up a step an hour while it is under what the side can
	/// hold, which its leader's skill raises as it raises the positioning itself.
	/// </summary>
	private static void Creep(CGameState state, CCombatSide side)
	{
		long cap = Math.Max(CNavalBattle.Base(side) - CNavalBattle.StackingPenalty(state, side),
			CNavalBattle.LeastPositioningCap);
		if (side.Positioning.Raw < cap)
		{
			side.Positioning = new CFixedPoint(side.Positioning.Raw + CNavalBattle.PositioningStep);
		}
	}

	/// <summary>
	/// The average speed of the ships of a side's fleets that are not breaking away:
	/// what a ship closes faster by, where the enemy's is the greater.
	/// </summary>
	public static long AverageSpeed(CCombatSide side)
	{
		long speed = 0;
		long ships = 0;
		foreach (CUnit fleet in side.Units)
		{
			if (fleet.Disengage < 1)
			{
				foreach (CSubUnit ship in fleet.SubUnits)
				{
					speed += ship.Stats[MaximumSpeed].Raw;
				}
				ships += fleet.SubUnits.Count * (long)CFixedPoint.Scale;
			}
		}
		return ships > 0 ? speed * CFixedPoint.Scale / ships : speed;
	}

	/// <summary>One ship's shots of the hour at the target it picked.</summary>
	private static void Fire(CGameState state, CCombat combat, CCombatSide side, CUnit fleet, CSubUnit ship,
		CSubUnit target, long theirPenalty)
	{
		CDefines defines = state.Defines;
		long attack = ship.Stats[SeaAttack].Raw;
		// A raider shoots at a convoy with what it has for convoys.
		if (combat.IsConvoyBattle && side.IsAttacker)
		{
			attack = ship.Stats[CConvoyRaid.ConvoyAttack].Raw;
		}
		if (CShipCensus.IsSubmarine(target))
		{
			attack = ship.Stats[CNavalTargets.SubAttack].Raw
				* (CFixedPoint.Scale + CCommandReach.Effect(state, fleet, ETraitEffect.SubmarineAttack)) / CFixedPoint.Scale;
		}
		long figure = ship.CombatAttack.Raw * attack / CFixedPoint.Scale;
		long shots = figure / CFixedPoint.Scale;
		if (state.Random.Next(100) < figure % CFixedPoint.Scale * 100000 / CFixedPoint.Scale / CFixedPoint.Scale)
		{
			shots++;
		}

		for (; shots > 0; shots--)
		{
			if (target.Strength.Raw - target.PendingStrengthDamage < 0)
			{
				break;
			}
			int organisationDie = Die(state, defines[CManpower.MilitarySection, OrganisationDice].Raw,
				OrganisationDiceForPenalty, theirPenalty);
			int strengthDie = Die(state, defines[CManpower.MilitarySection, StrengthDice].Raw,
				StrengthDiceForPenalty, theirPenalty);
			state.Random.Next();                           // a draw the original makes and does not read

			long avoid = Math.Min(defines[CManpower.MilitarySection, AvoidChance].Raw, MostAvoidChance);
			if (state.Random.Next(100) < avoid / CFixedPoint.Scale)
			{
				continue;
			}
			long organisation = (state.Random.Next(organisationDie) + 1) * (long)CFixedPoint.Scale;
			long strength = (state.Random.Next(strengthDie) + 1) * (long)CFixedPoint.Scale;
			if (state.Random.Next(100) < defines[CManpower.MilitarySection, CriticalChance].Raw / CFixedPoint.Scale)
			{
				strength = defines[CManpower.MilitarySection, CriticalDamage].Raw * strength / CFixedPoint.Scale;
			}
			long hull = target.Stats[CNavalBattle.Hull].Raw;
			if (hull > 0)
			{
				strength = strength * CFixedPoint.Scale / hull;
				organisation = organisation * CFixedPoint.Scale / hull;
			}
			strength = defines[CManpower.MilitarySection, StrengthDamage].Raw * strength / CFixedPoint.Scale;
			organisation = defines[CManpower.MilitarySection, OrganisationDamage].Raw * organisation / CFixedPoint.Scale;
			// A convoy's ships hardly hurt a raider and rattle it twice as much.
			if (combat.IsConvoyBattle && !side.IsAttacker)
			{
				strength = strength * CFixedPoint.Scale / ConvoyStrengthDivisor;
				organisation *= 2;
			}

			target.PendingOrganisationDamage += organisation;
			target.PendingStrengthDamage += strength;
			if (target.Strength.Raw - target.PendingStrengthDamage < 0)
			{
				target.SunkById = ship.Id;
			}
		}
	}

	/// <summary>
	/// How many sides one of a shot's two dice has: the define's, and so many more for
	/// the enemy's stacking penalty, with what is left of that a chance of one more.
	/// </summary>
	private static int Die(CGameState state, long define, long forPenalty, long theirPenalty)
	{
		long extra = forPenalty * theirPenalty / CFixedPoint.Scale;
		long sides = define / CFixedPoint.Scale + extra / CFixedPoint.Scale;
		if (state.Random.Next(100) < 100000 * (extra % CFixedPoint.Scale) / CFixedPoint.Scale / CFixedPoint.Scale)
		{
			sides++;
		}
		return (int)sides;
	}

	/// <summary>
	/// The ship closes on the enemy or draws away from him. One with a target comes in to
	/// half its reach at a quarter of its speed; one with none comes in at its whole
	/// speed; one whose fleet has a carrier holds off; and a ship that is spent, or whose
	/// fleet is breaking away, opens the distance by half its speed over its hull.
	/// </summary>
	private static void Move(CGameState state, CUnit fleet, CSubUnit ship, bool hasTarget, long ourSpeed,
		CCombatSide other)
	{
		long speed = ship.Stats[MaximumSpeed].Raw * CFixedPoint.Scale / 2000;
		long distance;
		if (fleet.Disengage < 1 && ship.Organisation.Raw > 0)
		{
			foreach (CSubUnit mate in fleet.SubUnits)
			{
				if (CShipCensus.IsCarrier(mate))
				{
					speed = 0;
					break;
				}
			}
			if (hasTarget)
			{
				if (ship.Distance.Raw <= CNavalTargets.ReachNow(state, ship) * CFixedPoint.Scale / 2000)
				{
					speed = 0;
				}
				speed = speed * CFixedPoint.Scale / 2000;
			}
			else
			{
				speed = speed * 2000 / CFixedPoint.Scale;
			}
			long theirSpeed = AverageSpeed(other);
			if (theirSpeed > ourSpeed)
			{
				speed += theirSpeed - ourSpeed;
			}
			distance = ship.Distance.Raw - speed;
		}
		else
		{
			long hull = ship.Stats[CNavalBattle.Hull].Raw;
			distance = ship.Distance.Raw + (hull == 0 ? 0 : speed * CFixedPoint.Scale / hull);
		}
		ship.Distance = new CFixedPoint(Math.Max(distance, CNavalBattle.NearestDistance));
	}
}
