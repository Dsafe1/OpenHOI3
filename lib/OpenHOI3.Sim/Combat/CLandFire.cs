using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One side's shooting for an hour of a land battle. Every unit on the front draws one
/// enemy front unit and fires at it as many shots as its attack comes to; the target
/// meets the first few with its defence and the rest with luck; and a shot that lands
/// does a rolled amount of damage, which is only added up here and taken off when the
/// hour is settled. See <c>docs/mechanics/landcombat.md</c>, section 5.
/// </summary>
public static class CLandFire
{
	public const string OrganisationDice = "LAND_COMBAT_ORG_DICE_SIZE";
	public const string StrengthDice = "LAND_COMBAT_STR_DICE_SIZE";
	public const string OrganisationDiceOnSoft = "LAND_COMBAT_ORG_ARMOR_ON_SOFT_DICE_SIZE";
	public const string StrengthDiceOnSoft = "LAND_COMBAT_STR_ARMOR_ON_SOFT_DICE_SIZE";
	public const string OrganisationDeflection = "LAND_COMBAT_ORG_ARMOR_DEFLECTION_FACTOR";
	public const string StrengthDeflection = "LAND_COMBAT_STR_ARMOR_DEFLECTION_FACTOR";
	public const string OrganisationDamage = "LAND_COMBAT_ORG_DAMAGE_MODIFIER";
	public const string StrengthDamage = "LAND_COMBAT_STR_DAMAGE_MODIFIER";
	public const string ChanceToAvoid = "BASE_CHANCE_TO_AVOID_HIT";
	public const string ChanceToAvoidUndefended = "CHANCE_TO_AVOID_HIT_AT_NO_DEF";

	/// <summary>No chance to avoid a hit is better than this, in thousandths of a per cent.</summary>
	private const long MostChanceToAvoid = 99000;

	/// <summary>What a tenth of a unit's strength adds to the damage of its hits, in thousandths.</summary>
	private const long DamagePerTenthOfStrength = 100;

	/// <summary>
	/// One side's hour: its front is seen to, its countries gain the land practical, one
	/// draw of the game's generator starts its own rolls, and every unit on its front fires;
	/// at midnight, then, the allies' part of strategic warfare (<c>politics.md</c>, section 6).
	/// </summary>
	public static void Attack(CGameState state, CCombat combat, CCombatSide side)
	{
		CTraitGain.BankHour(state, side);
		CFrontLine.Update(state, combat, side);
		CCombatPractical.Land(state, side);
		var rolls = new CCombatStream(state.Random.Next());
		CCombatSide enemy = combat.Other(side);
		foreach (CUnit unit in new List<CUnit>(side.Front))
		{
			Fire(state, combat, side, rolls, unit, enemy);
		}
		if (state.Hour == 0)
		{
			Politics.CStrategicWarfareImpacts.AlliesFought(state, combat, side);
		}
	}

	/// <summary>One unit's shots for the hour, all at the one target it drew.</summary>
	public static void Fire(CGameState state, CCombat combat, CCombatSide side, CCombatStream rolls,
		CUnit attacker, CCombatSide enemy)
	{
		state.CombatTrace?.Invoke($"FIRE u={attacker.Id} atk={(side.IsAttacker ? 1 : 0)} x={rolls.State:X8} draws={state.Random.Draws} " +
			$"eFront={enemy.Front.Count} prod={attacker.CombatAttack.Raw},{attacker.CombatDefence.Raw},{attacker.LandingAttack.Raw},{attacker.LandingDefence.Raw}");
		if (enemy.Front.Count < 1 || state.Country(attacker.FiguresOwner) is not { } owner)
		{
			return;
		}
		CUnit target = enemy.Front[rolls.Next(enemy.Front.Count)];

		// The target's softness decides how much of each kind of attack reaches it.
		long hard = CUnitTotals.Sum(attacker, CUnitTotals.HardAttack).Raw
			* (owner.Modifier[CUnitTotals.HardAttack].Raw + CFixedPoint.Scale) / CFixedPoint.Scale;
		long soft = CUnitTotals.Sum(attacker, CUnitTotals.SoftAttack).Raw
			* (owner.Modifier[CUnitTotals.SoftAttack].Raw + CFixedPoint.Scale) / CFixedPoint.Scale;
		long softness = CUnitTotals.AverageSoftness(target).Raw;
		hard = (CFixedPoint.Scale - softness) * hard / CFixedPoint.Scale;
		soft = softness * soft / CFixedPoint.Scale;

		// A unit landing from the sea fires its figure times its landing's attack scale (transport.md, section 9).
		long figure = attacker.CombatAttack.Raw * attacker.LandingAttack.Raw / CFixedPoint.Scale;
		long attack = figure * (soft + hard) / CFixedPoint.Scale;
		long shots = attack / CFixedPoint.Scale;
		long part = attack % CFixedPoint.Scale * 100 / CFixedPoint.Scale;
		if (rolls.Next(100) < part)
		{
			shots++;
		}
		state.CombatTrace?.Invoke($"SHOTS u={attacker.Id} target={target.Id} shots={shots} attack={attack} soft={soft} hard={hard} " +
			$"mod={owner.Modifier[CUnitTotals.SoftAttack].Raw}/{owner.Modifier[CUnitTotals.HardAttack].Raw} events={owner.EventModifiers.Count}");

		CDefines defines = state.Defines;
		for (; shots > 0 && !CCombatDamage.IsOutOfTheFight(target); shots--)
		{
			long organisationDice = defines[CManpower.MilitarySection, OrganisationDice].Raw;
			long strengthDice = defines[CManpower.MilitarySection, StrengthDice].Raw;
			if (CUnitTotals.Best(target, CUnitTotals.Piercing) < CUnitTotals.Best(attacker, CUnitTotals.Armour))
			{
				strengthDice = defines[CManpower.MilitarySection, StrengthDiceOnSoft].Raw;
				organisationDice = defines[CManpower.MilitarySection, OrganisationDiceOnSoft].Raw;
			}

			// An attacker is met with toughness, a defender with defensiveness.
			long stand = CUnitTotals.Sum(target, side.IsAttacker ? CUnitTotals.Defensiveness : CSubUnitStats.Toughness).Raw;
			long defence = target.CombatDefence.Raw * target.LandingDefence.Raw / CFixedPoint.Scale * stand / CFixedPoint.Scale;
			if (!Hits(state, target, defence))
			{
				continue;
			}

			long organisation = (rolls.Next(organisationDice / CFixedPoint.Scale) + 1) * (long)CFixedPoint.Scale;
			long strength = (rolls.Next(strengthDice / CFixedPoint.Scale) + 1) * (long)CFixedPoint.Scale;
			long factor = (side.IsAttacker ? combat.TacticAttacker : combat.TacticDefender).Raw
				+ StrengthFactor(attacker) + CFixedPoint.Scale;
			strength = strength * factor / CFixedPoint.Scale;
			organisation = organisation * factor / CFixedPoint.Scale;

			if (CUnitTotals.Best(attacker, CUnitTotals.Piercing) < CUnitTotals.Best(target, CUnitTotals.Armour))
			{
				strength = defines[CManpower.MilitarySection, StrengthDeflection].Raw * strength / CFixedPoint.Scale;
				organisation = defines[CManpower.MilitarySection, OrganisationDeflection].Raw * organisation / CFixedPoint.Scale;
			}
			strength = defines[CManpower.MilitarySection, StrengthDamage].Raw * strength / CFixedPoint.Scale;
			organisation = defines[CManpower.MilitarySection, OrganisationDamage].Raw * organisation / CFixedPoint.Scale;
			CCombatDamage.Take(state, target, strength, organisation);
		}
	}

	/// <summary>
	/// Whether one shot lands, and it spends the target's defence. The whole part of the
	/// defence is how many shots the target can meet this hour, and the rest a chance at
	/// one more; a shot it can meet is judged by the better chance to avoid a hit, and
	/// every shot after by the worse. Two draws of the game's generator.
	/// </summary>
	public static bool Hits(CGameState state, CUnit target, long defence)
	{
		long slots = defence / CFixedPoint.Scale;
		long part = defence % CFixedPoint.Scale * 100 / CFixedPoint.Scale;
		if (state.Random.Next(100) < part)
		{
			slots++;
		}

		long avoid;
		if (slots <= target.DefencesUsed)
		{
			avoid = state.Defines[CManpower.MilitarySection, ChanceToAvoidUndefended].Raw;
		}
		else
		{
			avoid = state.Defines[CManpower.MilitarySection, ChanceToAvoid].Raw;
			target.DefencesUsed++;
		}
		if (avoid > MostChanceToAvoid)
		{
			avoid = MostChanceToAvoid;
		}
		return avoid / CFixedPoint.Scale <= state.Random.Next(100);
	}

	/// <summary>
	/// What a unit's own strength adds to the damage of its hits, in thousandths: a
	/// hundred for each tenth of full strength it has, and never less than a hundred. A
	/// unit at full strength hits twice as hard as the roll.
	/// </summary>
	public static long StrengthFactor(CUnit unit)
	{
		long full = CUnitTotals.FullStrength(unit).Raw;
		long share = full == 0 ? -1 : CUnitTotals.Strength(unit).Raw * CFixedPoint.Scale / full;
		long tenths = share * 10 / CFixedPoint.Scale;
		if (tenths < 1)
		{
			tenths = 1;
		}
		return tenths * DamagePerTenthOfStrength;
	}
}
