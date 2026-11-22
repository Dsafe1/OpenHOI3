using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// What a battle's hits do to a unit. A hit is spread over the unit's brigades and only
/// noted; at the end of the hour each brigade's notes are taken off its strength and
/// its organisation together, the unit's country pays in officers and gets some of the
/// men back, and a unit worn too thin shatters. See
/// <c>docs/mechanics/landcombat.md</c>, section 6.
/// </summary>
public static class CCombatDamage
{
	public const string OfficerLoss = "OFFICER_COMBAT_LOSS";
	public const string Trickleback = "casualty_trickleback";

	/// <summary>A brigade loses a hundredth of what its hits came to.</summary>
	private const long BrigadeShare = 10;

	/// <summary>How many men one point of a brigade's strength is.</summary>
	public const long MenPerStrength = 100;

	/// <summary>The strength, in thousandths of full, under which a unit shatters whatever its officers.</summary>
	public const long MostShatterThreshold = 500;

	/// <summary>How many thousandths each whole point of average experience takes off that, over a thousand.</summary>
	public const long ShatterExperienceWeight = 3;

	/// <summary>How many thousandths each point of the unit's leader's skill takes off it, from a float of 10.5.</summary>
	public const long ShatterSkillWeight = 10;

	/// <summary>
	/// One hit on a unit, spread over its brigades: the strength by each brigade's
	/// share of the unit's full strength, the organisation evenly. Nothing is taken off
	/// here.
	/// </summary>
	public static void Take(CGameState state, CUnit unit, long strength, long organisation)
	{
		long full = CUnitTotals.FullStrength(unit).Raw;
		int count = unit.SubUnits.Count;
		if (full <= 0 || count <= 0 || state.Country(unit.FiguresOwner) is not { } owner)
		{
			return;
		}
		long evenly = CFixedPoint.Scale * (long)CFixedPoint.Scale / (count * (long)CFixedPoint.Scale);
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			long share = CUnits.MaxStrength(state, owner, subUnit).Raw * CFixedPoint.Scale / full;
			subUnit.PendingOrganisationDamage += organisation * evenly / CFixedPoint.Scale;
			subUnit.PendingStrengthDamage += share * strength / CFixedPoint.Scale;
		}
	}

	/// <summary>
	/// Whether every brigade of a unit is already dead on this hour's notes, which is
	/// what stops a unit being shot at for nothing.
	/// </summary>
	public static bool IsOutOfTheFight(CUnit unit)
	{
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Strength.Raw - subUnit.PendingStrengthDamage * BrigadeShare / CFixedPoint.Scale >= 0)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Takes a brigade's notes off it and clears them: a hundredth of each, and neither
	/// more than it has. Answers the men lost, in thousandths.
	/// </summary>
	public static long Settle(CSubUnit subUnit)
	{
		long strength = Math.Min(subUnit.PendingStrengthDamage * BrigadeShare / CFixedPoint.Scale, subUnit.Strength.Raw);
		long organisation = Math.Min(subUnit.PendingOrganisationDamage * BrigadeShare / CFixedPoint.Scale,
			subUnit.Organisation.Raw);
		subUnit.Strength = new CFixedPoint(subUnit.Strength.Raw - strength);
		subUnit.Organisation = new CFixedPoint(subUnit.Organisation.Raw - organisation);
		subUnit.PendingStrengthDamage = 0;
		subUnit.PendingOrganisationDamage = 0;
		return MenPerStrength * CFixedPoint.Scale * strength / CFixedPoint.Scale;
	}

	/// <summary>
	/// The end of the hour for one unit: its brigades are settled, some of the men lost
	/// come back to its country, the country loses officers by them, and the unit
	/// shatters if what it has left is under its threshold. Answers the men lost.
	/// </summary>
	public static long SettleUnit(CGameState state, CUnit unit)
	{
		long men = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			men += Settle(subUnit);
		}
		if (state.Country(unit.FiguresOwner) is not { } owner)
		{
			return men;
		}

		GiveBack(owner, unit, men);
		int count = unit.SubUnits.Count;
		if (men > 0 && count != 0)
		{
			long officers = men * CUnitTotals.Sum(unit, CSubUnitType.Officers).Raw / CFixedPoint.Scale;
			officers = officers * CFixedPoint.Scale / (count * 100L * CFixedPoint.Scale);
			officers = officers * state.Defines[CManpower.MilitarySection, OfficerLoss].Raw / CFixedPoint.Scale;
			owner.Officers = new CFixedPoint(Math.Max(0, owner.Officers.Raw - officers));
		}

		if (men > 0)
		{
			if (StrengthShare(unit) < ShatterThreshold(state, owner, unit))
			{
				CCombatRetreat.Shatter(state, owner, unit);
			}
		}
		return men;
	}

	/// <summary>
	/// How much of its full strength a unit has to be under to shatter, in thousandths:
	/// a half, or less for a country short of officers the other way about - one whole
	/// less half its officer ratio, where that is lower - and less again by a hundredth
	/// for each point of its own leader's skill, whatever his rank, and for experience.
	/// </summary>
	/// <summary>
	/// A unit's strength in thousandths of the most its brigades have ever had together -
	/// not of what their types may have - or -1 where that is nothing. What is set against
	/// <see cref="ShatterThreshold"/>.
	/// </summary>
	public static long StrengthShare(CUnit unit)
	{
		long ceiling = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			ceiling += subUnit.Highest.Raw;
		}
		return ceiling == 0 ? -1 : CUnitTotals.Strength(unit).Raw * CFixedPoint.Scale / ceiling;
	}

	public static long ShatterThreshold(CGameState state, CCountry owner, CUnit unit)
	{
		long threshold = CFixedPoint.Scale - MostShatterThreshold * owner.OfficerRatio.Raw / CFixedPoint.Scale;
		if (threshold > MostShatterThreshold)
		{
			threshold = MostShatterThreshold;
		}
		return threshold
			- (unit.Leader?.Skill ?? 0) * ShatterSkillWeight
			- CUnitTotals.AverageExperienceWithPride(state, unit).Raw * ShatterExperienceWeight / CFixedPoint.Scale;
	}

	/// <summary>
	/// The share of a country's casualties that returns to its manpower: its modifier's and its technologies' figure
	/// of the same name together, held at nought (reversing/findings/FINDINGS-manpower.md, casualty trickleback).
	/// </summary>
	public static CFixedPoint TricklebackOf(CCountry owner)
	{
		return new CFixedPoint(Math.Max(0, owner.Modifier[Trickleback].Raw + owner.Technology[Trickleback].Raw));
	}

	/// <summary>
	/// What comes back to the manpower pool of the men a unit lost: they are priced as
	/// replacing them is, and the country's trickleback says how much of that returns.
	/// </summary>
	private static void GiveBack(CCountry owner, CUnit unit, long men)
	{
		long manpower = CUnitTotals.Sum(unit, CSubUnitStats.BuildCostManpower).Raw * men / CFixedPoint.Scale;
		long full = CUnitTotals.Sum(unit, CSubUnitType.MaxStrength).Raw;
		if (full > 0)
		{
			manpower = manpower * CFixedPoint.Scale / full;
		}
		// The modifier and the technologies' figure of the same name together, held at nought (reversing/findings/
		// FINDINGS-manpower.md, casualty trickleback; read on the listing 2026-11-21).
		long trickleback = TricklebackOf(owner).Raw;
		manpower = trickleback * manpower / CFixedPoint.Scale;
		manpower = manpower * CFixedPoint.Scale / (100L * CFixedPoint.Scale);
		owner.Manpower = new CFixedPoint(Math.Max(0, owner.Manpower.Raw + manpower));
	}
}
