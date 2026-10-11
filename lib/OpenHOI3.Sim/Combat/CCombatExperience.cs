using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Leaders;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// The experience an hour of fighting gives: the only experience a leader ever gains, and
/// what a unit's brigades, ships and wings gain in battle. Worked in the 15-place fixed
/// point the original works it in, so that it truncates where the original does. See
/// <c>docs/mechanics/leaders.md</c>, section 11.
/// </summary>
public static class CCombatExperience
{
	public const string GainLand = "EXP_GAIN_LAND";
	public const string GainNaval = "EXP_GAIN_NAVAL";
	public const string GainAir = "EXP_GAIN_AIR";
	public const string GainConvoy = "EXP_GAIN_CONVOY_MODIFIER";
	public const string GainDivision = "EXP_GAIN_DIV";
	public const string GainLeader = "EXP_GAIN_LEADER";

	/// <summary>
	/// The hundredth the original multiplies by, twice: a double of 328.18 truncated to 328,
	/// in 15 places - a hair over a hundredth.
	/// </summary>
	public const long Hundredth = 328;

	/// <summary>The most the size of the enemy comes to, before its third is taken.</summary>
	private static readonly CFixed15 MostEnemy = CFixed15.FromInt(6);

	/// <summary>A brigade, ship or wing has a hundred experience at most.</summary>
	private const long MostSubUnitExperience = 100000;

	/// <summary>What a headquarters' share is divided by: its number of units below it, held between these.</summary>
	private const int LeastChildren = 2;
	private const int MostChildren = 5;

	/// <summary>
	/// A unit's hour in a battle, or a bombing, or a raid on a convoy: how much the enemy
	/// is, a die, the defines and the leaders' <c>xp_gain</c> make one figure, a share of
	/// which goes to each of its brigades, ships and wings and a share to its leader and,
	/// a part at each step, to every leader above him.
	/// </summary>
	public static void Grant(CGameState state, CUnit unit, bool convoyRaid)
	{
		CDefines defines = state.Defines;
		string military = CManpower.MilitarySection;

		long enemy = 0;
		foreach (CCombat combat in unit.Combats)
		{
			enemy += Enemy(state, combat, unit);
		}
		if (unit.Bombing is { } bombing && !Movement.CMovementRules.IsLandUnit(unit))
		{
			enemy += Enemy(state, bombing, unit);
		}
		long size = (enemy * Hundredth) >> CFixed15.Places;
		if (size == 0)
		{
			return;
		}
		size = size / 4;
		size = Math.Clamp(size, 0, MostEnemy.Raw);
		long figure = (size << CFixed15.Places) / (3 * CFixed15.Scale)
			+ (state.Random.Next(6) + 1) * CFixed15.Scale;

		string? gain = unit.Kind switch
		{
			EUnitKind.Navy => GainNaval,
			EUnitKind.Air => GainAir,
			_ when Movement.CMovementRules.IsLandUnit(unit) => GainLand,
			_ => null,
		};
		if (gain is not null)
		{
			figure = (Fifteen(defines[military, gain].Raw) * figure) >> CFixed15.Places;
		}
		long xpGain = CCommandReach.Effect(state, unit, ETraitEffect.XpGain);
		figure = ((Fifteen(xpGain) + CFixed15.Scale) * figure) >> CFixed15.Places;
		if (convoyRaid)
		{
			figure = (Fifteen(defines[military, GainConvoy].Raw) * figure) >> CFixed15.Places;
		}
		figure = (figure * Hundredth) >> CFixed15.Places;

		// Each brigade, ship and wing: a ship only while it has something to shoot at.
		long subUnitGain = ((figure * 1000 * CFixed15.Scale) >> 30) * defines[military, GainDivision].Raw / 1000;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (unit.Kind == EUnitKind.Navy && subUnit.TargetId == 0)
			{
				continue;
			}
			long experience = Math.Min(subUnit.Experience.Raw + subUnitGain, MostSubUnitExperience);
			subUnit.Experience = new CFixedPoint(experience);
		}

		// The leader, and every leader above him of the same kind, at a share each step.
		var leaderGain = new CFixed15((Fifteen(defines[military, GainLeader].Raw) * figure) >> CFixed15.Places);
		if (unit.Leader is { } leader)
		{
			CLeaders.AddExperience(leader, leaderGain);
		}
		bool isLand = Movement.CMovementRules.IsLandUnit(unit);
		for (CUnit? above = unit.Parent; above is not null; above = above.Parent)
		{
			if (Movement.CMovementRules.IsLandUnit(above) != isLand)
			{
				break;
			}
			int children = Math.Clamp(above.Children.Count, LeastChildren, MostChildren);
			leaderGain = new CFixed15((leaderGain.Raw << CFixed15.Places) / (children * CFixed15.Scale));
			if (above.Leader is { } commander)
			{
				CLeaders.AddExperience(commander, leaderGain);
			}
		}
	}

	/// <summary>
	/// How much the other side of a battle is, in 15 places: the average organisation of its
	/// units times how many brigades, ships or wings they have - nothing where the unit's
	/// country is on neither side.
	/// </summary>
	private static long Enemy(CGameState state, CCombat combat, CUnit unit)
	{
		CCombatSide? other = combat.Attacker.Standing.Contains(unit.Owner) ? combat.Defender
			: combat.Defender.Standing.Contains(unit.Owner) ? combat.Attacker
			: null;
		if (other is null)
		{
			return 0;
		}
		long count = 0;
		foreach (CUnit enemy in other.Units)
		{
			count += enemy.SubUnits.Count;
		}
		long organisation = Fifteen(AverageOrganisation(other.Units));
		return ((count << CFixed15.Places) * organisation) >> CFixed15.Places;
	}

	/// <summary>
	/// The average organisation of a list of units: each one's average weighted by how many
	/// brigades, ships or wings it has.
	/// </summary>
	public static long AverageOrganisation(List<CUnit> units)
	{
		long sum = 0;
		long count = 0;
		foreach (CUnit unit in units)
		{
			sum += unit.SubUnits.Count * (long)CFixedPoint.Scale * CUnitTotals.AverageOrganisation(unit).Raw / CFixedPoint.Scale;
			count += unit.SubUnits.Count;
		}
		return count == 0 ? sum : sum * CFixedPoint.Scale / (count * CFixedPoint.Scale);
	}

	/// <summary>Thousandths as 15 places, as the original converts them: truncated.</summary>
	private static long Fifteen(long thousandths)
	{
		return CFixed15.FromThousandths(new CFixedPoint(thousandths)).Raw;
	}
}
