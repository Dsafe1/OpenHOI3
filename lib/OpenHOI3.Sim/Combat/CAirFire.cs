using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// A side's shooting in a battle in the air: every wing with an air attack keeps the
/// target it had or picks a new one, and fires its attack's worth of shots at it. A
/// shot that is not avoided does a die of organisation and a die of strength, and one
/// strength hit in ten is ten times as hard. Nothing of the target comes into it but
/// whether it is a carrier's air group. Written to roll in the original's order, the
/// roll nothing uses with it. See <c>docs/mechanics/aircombat.md</c>, section 8.
/// </summary>
public static class CAirFire
{
	public const string AvoidChance = "CHANCE_TO_AVOID_HIT_AT_NO_DEF";
	public const string OrganisationDie = "AIR_COMBAT_ORG_DICE_SIZE";
	public const string StrengthDie = "AIR_COMBAT_STR_DICE_SIZE";
	public const string CriticalMultiplier = "AIR_COMBAT_CRITICAL_HIT_DAMAGE_MUL";
	public const string CriticalChance = "AIR_COMBAT_CRITICAL_HIT_DAMAGE_CHANCE";
	public const string OrganisationDamage = "AIR_COMBAT_ORG_DAMAGE_MODIFIER";
	public const string StrengthDamage = "AIR_COMBAT_STR_DAMAGE_MODIFIER";
	public const string CagOrganisationDamage = "AIR_COMBAT_CAG_ORG_DAMAGE_MOD";
	public const string CagStrengthDamage = "AIR_COMBAT_CAG_STR_DAMAGE_MOD";

	/// <summary>A wing's own figure of defence in the air, which the original works out and never uses.</summary>
	public const string AirDefence = "air_defence";

	/// <summary>
	/// An enhanced game's define, in the section <c>military</c>: the chance in a hundred that a shot a wing can still
	/// meet with its defence is avoided, under the fix <c>air_defence_counts</c>. Where a mod gives none it is the land
	/// battle's <c>BASE_CHANCE_TO_AVOID_HIT</c>.
	/// </summary>
	public const string DefendedAvoidChance = "OPENHOI_AIR_CHANCE_TO_AVOID_HIT";

	public const string BomberTargeting = "bomber_targeting";
	public const string FighterTargeting = "fighter_targeting";
	public const string Priority = "priority";
	public const string IsCag = "is_cag";

	/// <summary>The most a shot is ever avoided by, in thousandths of a hundred.</summary>
	private const long MostAvoided = 99000;

	/// <summary>How many of the other side's wings a wing chooses among at most.</summary>
	private const int MostTargets = 1024;

	/// <summary>What every target is worth before the strength it has lost.</summary>
	private const long BaseWorth = 250;

	/// <summary>A wing takes a hundredth of what is rolled against it, when it is settled.</summary>
	private const long WingShare = 10;

	public static void Attack(CGameState state, CCombat combat, CCombatSide side)
	{
		if (state.CombatTrace is { } trace)
		{
			var wings = new List<string>();
			foreach (CUnit flying in side.Units)
			{
				foreach (CSubUnit wing in flying.SubUnits)
				{
					wings.Add($"{wing.Id}:{wing.CombatAttack.Raw},{wing.CombatDefence.Raw},{wing.Strength.Raw}/{wing.Organisation.Raw}");
				}
			}
			trace($"AIRSIDE atk={(side.IsAttacker ? 1 : 0)} draws={state.Random.Draws} wings={string.Join(" ", wings)}");
		}
		CTraitGain.BankHour(state, side);
		CCombatPractical.Air(state, side);
		CCombatSide other = combat.Other(side);
		CDefines defines = state.Defines;
		string military = CManpower.MilitarySection;
		long avoid = Math.Min(defines[military, AvoidChance].Raw, MostAvoided) / CFixedPoint.Scale;
		int organisationDie = (int)(defines[military, OrganisationDie].Raw / CFixedPoint.Scale);
		int strengthDie = (int)(defines[military, StrengthDie].Raw / CFixedPoint.Scale);
		// An enhanced game's fix air_defence_counts: a wing meets as many of the hour's shots as its defence is whole, as a
		// unit does on land, each of those avoided at the better chance. The shots it has met are counted here and kept
		// nowhere: this side fires once an hour.
		bool defended = state.Rules.Has(Rules.EFix.AirDefenceCounts);
		long better = defines[military, defines.Has(military, DefendedAvoidChance) ? DefendedAvoidChance : CLandFire.ChanceToAvoid].Raw;
		better = Math.Min(better, MostAvoided) / CFixedPoint.Scale;
		Dictionary<CSubUnit, long>? met = defended ? new Dictionary<CSubUnit, long>() : null;

		foreach (CUnit unit in new List<CUnit>(side.Units))
		{
			CCountry? owner = state.Country(unit.FiguresOwner);
			foreach (CSubUnit wing in new List<CSubUnit>(unit.SubUnits))
			{
				long attack = wing.Stats[CAirMissions.AirAttack].Raw;
				if (attack == 0 || PickTarget(state, side, other, wing, owner) is not { } target)
				{
					continue;
				}
				long worth = attack * wing.CombatAttack.Raw / CFixedPoint.Scale;
				long shots = worth / CFixedPoint.Scale;
				if (state.Random.Next(100) < worth % CFixedPoint.Scale * 100 / CFixedPoint.Scale)
				{
					shots++;
				}
				for (; shots > 0; shots--)
				{
					if (StrengthLeft(target) < 0)
					{
						break;
					}
					int spare = state.Random.Next();               // a roll nothing uses - but the fix, for the defence's part
					long chance = met is not null && Meets(target, met, spare) ? better : avoid;
					if (state.Random.Next(100) < chance)
					{
						continue;
					}
					long organisation = (state.Random.Next(Math.Max(1, organisationDie)) + 1) * (long)CFixedPoint.Scale;
					long strength = (state.Random.Next(Math.Max(1, strengthDie)) + 1) * (long)CFixedPoint.Scale;
					if (state.Random.Next(100) < defines[military, CriticalChance].Raw / CFixedPoint.Scale)
					{
						strength = defines[military, CriticalMultiplier].Raw * strength / CFixedPoint.Scale;
					}
					strength = defines[military, StrengthDamage].Raw * strength / CFixedPoint.Scale;
					organisation = defines[military, OrganisationDamage].Raw * organisation / CFixedPoint.Scale;
					if (target.Type.Flag(IsCag))
					{
						strength = defines[military, CagStrengthDamage].Raw * strength / CFixedPoint.Scale;
						organisation = defines[military, CagOrganisationDamage].Raw * organisation / CFixedPoint.Scale;
					}
					target.PendingOrganisationDamage += organisation;
					target.PendingStrengthDamage += strength;
				}
			}
		}
	}

	/// <summary>
	/// Whether a wing can still meet a shot with its defence this hour, and the shot spent where it can: its
	/// <c>air_defence</c> by what the hour made of its defence, the whole part so many shots and the rest a chance in a
	/// hundred of one more, judged by <paramref name="spare"/> - the roll the original makes for each shot and reads
	/// nowhere, so the fix draws nothing more.
	/// </summary>
	public static bool Meets(CSubUnit target, Dictionary<CSubUnit, long> met, int spare)
	{
		long defence = target.Stats[AirDefence].Raw * target.CombatDefence.Raw / CFixedPoint.Scale;
		long slots = defence / CFixedPoint.Scale;
		if ((spare & int.MaxValue) % 100 < defence % CFixedPoint.Scale * 100 / CFixedPoint.Scale)
		{
			slots++;
		}
		long used = met.TryGetValue(target, out long before) ? before : 0;
		if (slots <= used)
		{
			return false;
		}
		met[target] = used + 1;
		return true;
	}

	/// <summary>A wing's strength less what this hour has rolled against it so far, as it will be settled.</summary>
	public static long StrengthLeft(CSubUnit wing)
	{
		return wing.Strength.Raw - wing.PendingStrengthDamage * WingShare / CFixedPoint.Scale;
	}

	/// <summary>
	/// The wing a wing fires at: the one it had while that is still on the other side;
	/// otherwise, by one roll against a tenth of its targeting skill, the best of the
	/// other side's wings or a random one. None where the other side has none left.
	/// </summary>
	public static CSubUnit? PickTarget(CGameState state, CCombatSide side, CCombatSide other, CSubUnit wing,
		CCountry? owner)
	{
		state.CombatTrace?.Invoke($"PICK draws={state.Random.Draws}");
		if (wing.TargetId != 0 && Find(other, wing.TargetId) is { } kept)
		{
			return kept;
		}

		List<CSubUnit> targets = side.Targets;
		targets.Clear();
		foreach (CUnit unit in other.Units)
		{
			foreach (CSubUnit candidate in unit.SubUnits)
			{
				if (StrengthLeft(candidate) >= 0 && targets.Count < MostTargets)
				{
					targets.Add(candidate);
				}
			}
		}

		long skill = TargetingSkill(wing, owner);
		int roll = state.Random.Next(100);
		CSubUnit? target = null;
		if (targets.Count > 0)
		{
			target = skill / 10 < roll ? targets[state.Random.Next(targets.Count)] : Best(targets, owner);
		}
		wing.TargetId = target?.Id ?? 0;
		return target;
	}

	/// <summary>
	/// A wing's skill at picking its target: ten for each point of its unit's leader's skill,
	/// whatever his rank, and its country's two targeting figures in thousandths added together.
	/// </summary>
	public static long TargetingSkill(CSubUnit wing, CCountry? owner)
	{
		long skill = wing.Unit is { } unit ? CCombatLeaders.UnitSkill(unit) * SkillTargeting : 0;
		return owner is null ? skill : skill + owner.Technology[BomberTargeting].Raw + owner.Technology[FighterTargeting].Raw;
	}

	/// <summary>What each point of a leader's skill adds to a ship's or a wing's targeting, from a float of 10.5.</summary>
	public const long SkillTargeting = 10;

	/// <summary>
	/// The target worth most: its type's priority, times a quarter and a hundredth of
	/// the strength it has lost, times one and the firing country's targeting against
	/// its kind. The first among equals; one worth nothing is never the best.
	/// </summary>
	public static CSubUnit? Best(IReadOnlyList<CSubUnit> targets, CCountry? owner)
	{
		CSubUnit? best = null;
		long most = 0;
		foreach (CSubUnit target in targets)
		{
			long lost = (target.Stats[CSubUnitType.MaxStrength].Raw - target.Strength.Raw) * WingShare / CFixedPoint.Scale;
			long worth = target.Stats[Priority].Raw * (lost + BaseWorth) / CFixedPoint.Scale;
			long targeting = owner is null ? 0
				: owner.Technology[target.Type.Flag(CAirModifiers.IsBomber) ? BomberTargeting : FighterTargeting].Raw;
			worth = (targeting + CFixedPoint.Scale) * worth / CFixedPoint.Scale;
			if (worth > most)
			{
				most = worth;
				best = target;
			}
		}
		return best;
	}

	private static CSubUnit? Find(CCombatSide side, int id)
	{
		foreach (CUnit unit in side.Units)
		{
			foreach (CSubUnit wing in unit.SubUnits)
			{
				if (wing.Id == id)
				{
					return wing;
				}
			}
		}
		return null;
	}
}
