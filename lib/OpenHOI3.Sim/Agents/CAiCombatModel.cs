using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// What the AI makes of a unit's circumstances in a battle it weighs: the same terms a battle has, worked out again and
/// added as fractions where the battle multiplies them, in thousandths. The two helpers of the original's
/// <c>CAIUnit::EstimateAttackOdds</c>. See <c>docs/mechanics/moving.md</c>, section 11.4.
/// </summary>
public static class CAiCombatModel
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>Brigades past three a province crowd.</summary>
	private const int PerProvince = 3;

	private const int MostCrowd = 99;

	private const int EnvelopedPast = 2;

	/// <summary>
	/// A half handed over as not a number (<see cref="CGameState.FirstLookModel"/>): the whole term is then not a number,
	/// which <see cref="CAttackOdds"/> takes as the original's floats take it.
	/// </summary>
	public const long NotANumber = long.MinValue;

	public static long Attack(CGameState state, CUnit unit, CProvince target, long brigades, long provinces)
	{
		if (state.Country(unit.FiguresOwner) is not { } owner)
		{
			return 0;
		}
		// The half that hangs on the unit alone, which the original caches: everything before the crowding.
		long v = state.FirstLookModel?.Invoke(unit, true) ?? Common(state, owner, unit, attacking: true);
		if (v == NotANumber)
		{
			return NotANumber;
		}
		v -= Crowding(state, owner, unit, brigades - PerProvince * provinces);
		v -= CWeatherEffects.LandCombat(state, target.Weather, target.Terrain, owner);
		bool landing = false;
		if (unit.Location > 0 && state.Province(unit.Location).EdgeTo(target.Id) is { } edge)
		{
			CDefines defines = state.Defines;
			if (edge.Kind == EProvinceEdgeKind.River)
			{
				long river = defines[CManpower.MilitarySection, CCombatModifiers.RiverPenalty].Raw
					+ CUnitTotals.Adjuster(unit, CCombatModifiers.RiverAdjuster, CCombatModifiers.AttackOf)
					+ CCombatModifiers.Trait(state, unit, ETraitEffect.RiverAttack);
				if (river < 0)
				{
					v += river;
				}
			}
			else if (edge.Kind == EProvinceEdgeKind.Crossing)
			{
				landing = true;
				long amphibious = defines[CManpower.MilitarySection, CCombatModifiers.LandingPenalty].Raw
					+ CUnitTotals.Adjuster(unit, CCombatModifiers.AmphibiousAdjuster, CCombatModifiers.AttackOf)
					+ CCombatModifiers.Trait(state, unit, ETraitEffect.AmphAttack);
				if (amphibious < 0)
				{
					// The original subtracts the negative penalty: a landing helps.
					v -= amphibious;
				}
			}
		}
		if (target.Terrain is { } terrain)
		{
			v += terrain.Attack.Raw + CUnitTotals.Adjuster(unit, terrain.Name, CCombatModifiers.AttackOf);
		}
		long fort = target.Modifier[landing ? CCombatModifiers.CoastalFortLevel : CCombatModifiers.FortLevel].Raw / Scale * Scale;
		v += fort * state.Defines[CManpower.MilitarySection, CCombatModifiers.FortPenalty].Raw / Scale;
		return v;
	}

	public static long Defence(CGameState state, CUnit unit, long crowd, int enemyProvinces)
	{
		if (state.Country(unit.FiguresOwner) is not { } owner)
		{
			return 0;
		}
		CDefines defines = state.Defines;
		// The half that hangs on the unit alone, which the original caches: everything but the crowding and the envelopment.
		long v = state.FirstLookModel?.Invoke(unit, false) ?? UnitDefence(state, owner, unit);
		if (v == NotANumber)
		{
			return NotANumber;
		}
		v -= Crowding(state, owner, unit, System.Math.Min(crowd, 100));
		if (enemyProvinces > EnvelopedPast && unit.Location > 0
			&& !CCombatModifiers.IsIsolatedLand(state, state.Province(unit.Location)))
		{
			long envelopment = (enemyProvinces - EnvelopedPast) * Scale
				* defines[CManpower.MilitarySection, CCombatModifiers.EnvelopmentPenalty].Raw / Scale;
			envelopment = CCombatModifiers.Scaled(envelopment, CCombatModifiers.Trait(state, unit, ETraitEffect.EnvelopmentBonus));
			// The original clamps a negative penalty to nought and so never takes it off.
			v -= System.Math.Max(envelopment, 0);
		}
		return v;
	}

	/// <summary>
	/// A defender's half that hangs on the unit alone: the fort defence trait, the terms both sides share, its dig-in, the
	/// ground it stands on and its being encircled.
	/// </summary>
	private static long UnitDefence(CGameState state, CCountry owner, CUnit unit)
	{
		CDefines defines = state.Defines;
		long v = CCombatModifiers.Trait(state, unit, ETraitEffect.FortDefence);
		v += Common(state, owner, unit, attacking: false);
		if (unit.DigIn > CFixedPoint.Zero)
		{
			long dug = unit.DigIn.Raw * defines[CManpower.MilitarySection, CCombatModifiers.DigInFactor].Raw / Scale;
			v += CCombatModifiers.Scaled(dug, CCombatModifiers.Trait(state, unit, ETraitEffect.DiginBonus));
		}
		if (unit.Location > 0 && state.Province(unit.Location).Terrain is { } terrain)
		{
			v += terrain.Defence.Raw + CUnitTotals.Adjuster(unit, terrain.Name, CCombatModifiers.DefenceOf);
		}
		// Encircled, as in a battle (reversing/findings/FINDINGS-aihelpers.md).
		if (unit.Location > 0 && CCombatModifiers.IsEncircled(state, state.Province(unit.Location), unit.Owner))
		{
			long encircled = CCombatModifiers.Scaled(defines[CManpower.MilitarySection, CCombatModifiers.EncircledPenalty].Raw,
				CCombatModifiers.Trait(state, unit, ETraitEffect.EncirclementBonus));
			v -= System.Math.Max(encircled, 0);
		}
		return v;
	}

	/// <summary>The terms both sides share: combined arms, experience, the leader, dissent, want of supplies and the difficulty.</summary>
	private static long Common(CGameState state, CCountry owner, CUnit unit, bool attacking)
	{
		CDefines defines = state.Defines;
		long v = CCombatModifiers.CombinedArms(state, owner, unit);
		long experience = CUnitTotals.AverageExperience(unit).Raw * 10 / Scale;
		v += CCombatModifiers.Scaled(experience, CCombatModifiers.Trait(state, unit, ETraitEffect.ExperienceBonus));
		if (CLeaders.HasRankFor(state, unit) && unit.Leader is { Skill: > 0 } leader)
		{
			long skill = leader.Skill * defines[CManpower.MilitarySection, CCombatModifiers.LeaderImpact].Raw;
			v += skill + CCombatModifiers.Trait(state, unit, attacking ? ETraitEffect.OffenceModifier : ETraitEffect.DefenceModifier);
		}
		if (owner.Dissent != CFixedPoint.Zero)
		{
			long dissent = owner.Dissent.Raw * 10 / Scale;
			dissent = defines[CManpower.MilitarySection, CCombatModifiers.DissentImpact].Raw * dissent / Scale;
			v -= CCombatModifiers.Scaled(dissent, CCombatModifiers.Trait(state, unit, ETraitEffect.DissentImpact));
		}
		if (unit.SuppliesReceived != CFixedPoint.One)
		{
			long missing = Scale - unit.SuppliesReceived.Raw;
			long lack = defines[CManpower.MilitarySection, CCombatModifiers.SupplyLackImpact].Raw * missing / Scale;
			v -= CCombatModifiers.Scaled(lack, CCombatModifiers.Trait(state, unit, ETraitEffect.OutOfSupplyModifier));
		}
		// A played country's unit counts the difficulty here as in a battle (FINDINGS-aihelpers.md; frontend.md, section 4).
		v += Settings.CDifficulty.Combat(state, owner);
		return v;
	}

	/// <summary>
	/// The crowding of n brigades over what a province holds, less the country's <c>unit_cooperation</c> and a hundredth for
	/// each point of the theatre leader's skill (<c>reversing/findings/FINDINGS-aihelpers.md</c>; measured,
	/// <c>reversing/findings/FINDINGS-liveread.md</c>, section 55).
	/// </summary>
	private static long Crowding(CGameState state, CCountry owner, CUnit unit, long n)
	{
		long raw = RawCrowding(state, n) - owner.Technology[CCombatModifiers.FreeStacking].Raw - CCombatModifiers.TheatreSkill(unit) * 10;
		return raw > 0 ? raw : 0;
	}

	/// <summary>The crowding of n brigades before anything of a unit's own comes off it.</summary>
	private static long RawCrowding(CGameState state, long n)
	{
		long each = Scale + state.Defines[CManpower.MilitarySection, CPositionCap.BaseStackingPenalty].Raw;
		long left = Scale;
		for (long counted = 1; counted < n && counted <= MostCrowd; counted++)
		{
			left = left * each / Scale;
		}
		return Scale - left;
	}

	/// <summary>
	/// What the battle the AI weighs would hold for every unit of one side, for the defence product of a unit no battle
	/// has worked out (an enhanced game's fix <c>ai_odds_fresh_defence</c>, <c>docs/mechanics/moving.md</c>, section
	/// 11.4): the provinces the attackers stand in, the side's crowding as the AI counts it - brigades over three a
	/// province for the attackers, and for the defenders what <see cref="Defence"/> is handed, held to a hundred - the
	/// night and the cut-off land over the target, what enemy fleets beside it do to the side, and whether an attacker
	/// stands at sea.
	/// </summary>
	public static CCombatModifiers.Setting Setting(CGameState state, IReadOnlyList<CUnit> side, bool attacking, CProvince target,
		long crowd, int attackersProvinces, bool fromTheSea)
	{
		long n = attacking ? crowd - PerProvince * attackersProvinces : System.Math.Min(crowd, 100);
		return new CCombatModifiers.Setting(attackersProvinces, RawCrowding(state, n), CDaylight.IsNight(state, target),
			CCombatModifiers.IsIsolatedLand(state, target), CCombatModifiers.ShoreBombardment(state, target, side), fromTheSea);
	}
}
