using System;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The two whole numbers a unit's counter shows, its attack and its defence, each from
/// 0 to 9. They are worked out of what the unit's brigades add up to and are for the
/// eye only: nothing fights by them. See <c>docs/mechanics/mapunits.md</c>, section 4.
/// </summary>
public static class CUnitFigures
{
	private const string SoftAttack = "soft_attack";
	private const string HardAttack = "hard_attack";
	private const string Defensiveness = "defensiveness";
	private const string SeaAttack = "sea_attack";
	private const string SeaDefence = "sea_defence";
	private const string AirAttack = "air_attack";
	private const string AirDefence = "air_defence";

	/// <summary>No counter shows more.</summary>
	public const int Most = 9;

	private const long LandAttackDivisor = 6;
	private const long LandDefenceDivisor = 16;
	private const long NavalDivisor = 25;
	private const long AirDivisor = 10;

	public static int Attack(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Navy => Figure(Sum(unit, SeaAttack), NavalDivisor),
			EUnitKind.Air => Figure(Sum(unit, AirAttack), AirDivisor),
			_ => Figure(Sum(unit, SoftAttack) + Sum(unit, HardAttack), LandAttackDivisor),
		};
	}

	public static int Defence(CUnit unit)
	{
		return unit.Kind switch
		{
			EUnitKind.Navy => Figure(Sum(unit, SeaDefence), NavalDivisor),
			EUnitKind.Air => Figure(Sum(unit, AirDefence), AirDivisor),
			// The original adds a brigade's toughness into its unit twice over.
			_ => Figure(Sum(unit, Defensiveness) + 2 * Sum(unit, CSubUnitStats.Toughness), LandDefenceDivisor),
		};
	}

	/// <summary>
	/// What the unit's brigades add up to in one statistic, in thousandths: every ship
	/// and wing, and on land every brigade that still counts towards its unit.
	/// </summary>
	private static long Sum(CUnit unit, string statistic)
	{
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit))
			{
				sum += subUnit.Stats[statistic].Raw;
			}
		}
		return sum;
	}

	/// <summary>
	/// A sum over a divisor as the original takes it: divided as whole thousandths,
	/// cutting, then rounded to a whole number with a half going to the even one, and
	/// held to <see cref="Most"/>.
	/// </summary>
	private static int Figure(long thousandths, long divisor)
	{
		long cut = thousandths * CFixedPoint.Scale / (divisor * CFixedPoint.Scale);
		long whole = cut / CFixedPoint.Scale;
		long rest = cut % CFixedPoint.Scale;
		if (rest * 2 > CFixedPoint.Scale || (rest * 2 == CFixedPoint.Scale && whole % 2 != 0))
		{
			whole++;
		}
		return (int)Math.Clamp(whole, 0, Most);
	}
}
