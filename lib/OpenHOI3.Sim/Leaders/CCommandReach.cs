using System;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// How far a headquarters' command reaches the units under it, by radio: all of it within
/// a radius its rank and its own brigade's radio give it, less and less out to twice that,
/// and none beyond. And what a leader's traits are worth to the units under him, which
/// comes down the chain of command by the same reach and is halved at every step. See
/// <c>docs/mechanics/leaders.md</c>, section 8.
///
/// The arithmetic is the original's step for step, in thousandths with every division
/// dropping its remainder.
/// </summary>
public static class CCommandReach
{
	/// <summary>A brigade's figure for the strength of its radio.</summary>
	public const string RadioStrength = "radio_strength";

	/// <summary>A headquarters' radius by its level, a theatre's first: map distances at a radio of 1.</summary>
	private static readonly string[] RadioDistances =
	{
		"RADIO_THEATHRE_LEADER_DISTANCE", "RADIO_ARMYGROUP_LEADER_DISTANCE",
		"RADIO_ARMY_LEADER_DISTANCE", "RADIO_CORPS_LEADER_DISTANCE",
	};

	/// <summary>All of a command, in thousandths.</summary>
	public const long Full = 1000;

	/// <summary>The lowest level that is a headquarters: a corps.</summary>
	private const int LowestHeadquarters = 3;

	/// <summary>
	/// A unit's level in the original's numbering: a theatre 0, an army group 1, an army
	/// 2, a corps 3, a division 4, and a fleet or an air unit 5.
	/// </summary>
	public static int Level(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Theatre => 0,
			EUnitKind.ArmyGroup => 1,
			EUnitKind.Army => 2,
			EUnitKind.Corps => 3,
			EUnitKind.Division => 4,
			_ => 5,
		};
	}

	/// <summary>
	/// How far a headquarters' command reaches in full, in the game's kilometres, which are
	/// the map distance's quarters of a pixel: the radio of its first brigade times the
	/// define for its level. A unit with no brigade has no radio and reaches nothing.
	/// </summary>
	public static long Radius(CGameState state, CUnit headquarters)
	{
		if (headquarters.SubUnits.Count == 0)
		{
			return 0;
		}
		long radio = headquarters.SubUnits[0].Stats[RadioStrength].Raw;
		int level = Level(headquarters);
		if (level <= LowestHeadquarters)
		{
			radio = state.Defines[CManpower.MilitarySection, RadioDistances[level]].Raw * radio / CFixedPoint.Scale;
		}
		return radio / CFixedPoint.Scale;
	}

	/// <summary>
	/// How much of a headquarters' command reaches one unit: all of it where the unit is
	/// within its radius, the radius over the distance out to twice the radius, and none
	/// beyond. Nothing for a unit that is nowhere, and all of it from a headquarters that is
	/// nowhere or is not one.
	/// </summary>
	public static long From(CGameState state, CUnit unit, CUnit? headquarters)
	{
		if (unit.Location == 0 || headquarters is null)
		{
			return 0;
		}
		if (Level(headquarters) > LowestHeadquarters || headquarters.Location == 0)
		{
			return Full;
		}

		long radius = Radius(state, headquarters);
		long squared = CMapDistance.Squared(state.Province(unit.Location), state.Province(headquarters.Location), state.MapWidth);
		if (squared == 0 || squared <= radius * radius)
		{
			return Full;
		}
		if (squared > radius * radius * 4)
		{
			return 0;
		}
		long distance = CMapDistance.WholeRoot(squared);
		long scaled = distance * CFixedPoint.Scale;
		if (scaled == 0)
		{
			return Full + 1;
		}
		return Full - (distance - radius) * CFixedPoint.Scale * CFixedPoint.Scale / scaled;
	}

	/// <summary>
	/// How much of the command of the headquarters at a level reaches a unit: what each
	/// link of the chain between them lets through, multiplied together. All of it where
	/// the unit is at that level.
	/// </summary>
	public static long Through(CGameState state, CUnit unit, int level)
	{
		if (Level(unit) == level)
		{
			return Full;
		}
		long reach = Full;
		CUnit lower = unit;
		for (CUnit? headquarters = unit.Parent; headquarters is not null && Level(headquarters) >= level; headquarters = headquarters.Parent)
		{
			reach = From(state, lower, headquarters) * reach / CFixedPoint.Scale;
			lower = headquarters;
		}
		return reach;
	}

	/// <summary>
	/// Whether any link of the chain of command above a unit, up to the top, lets less than
	/// all of its command through. It is what the unit panel and the lines of the hierarchy
	/// say a unit is out of radio range for.
	/// </summary>
	public static bool IsOutOfRange(CGameState state, CUnit unit)
	{
		CUnit lower = unit;
		for (CUnit? headquarters = unit.Parent; headquarters is not null; headquarters = headquarters.Parent)
		{
			if (From(state, lower, headquarters) < Full)
			{
				return true;
			}
			lower = headquarters;
		}
		return false;
	}

	/// <summary>
	/// What one kind of trait effect comes to for a unit, in thousandths: its own leader's,
	/// every trait's effects of that kind added up, and half of what the unit above it has
	/// of it, by how much of that unit's command reaches it. So a commander's trait is
	/// worth half to the units under him, a quarter to theirs, and so on down.
	/// </summary>
	public static long Effect(CGameState state, CUnit unit, ETraitEffect kind)
	{
		long total = 0;
		if (unit.Parent is { } headquarters)
		{
			long above = Effect(state, headquarters, kind);
			if (above != 0)
			{
				long reach = Math.Max(0, From(state, unit, headquarters));
				long share = reach * above / CFixedPoint.Scale;
				total += share * CFixedPoint.Scale / (2 * CFixedPoint.Scale);
			}
		}
		if (unit.Leader is { } leader)
		{
			foreach (CTrait trait in leader.Traits)
			{
				foreach (CTraitEffect effect in trait.Effects)
				{
					if (effect.Kind == kind)
					{
						total += effect.Value.Raw;
					}
				}
			}
		}
		return total;
	}
}
