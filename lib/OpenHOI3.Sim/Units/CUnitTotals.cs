using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a unit's brigades come to together, as the original makes one set of figures
/// for a unit out of its brigades': most are added up, softness is averaged, and of
/// armour and piercing the unit has its best brigade's. A battle is fought with these.
/// See <c>docs/mechanics/landcombat.md</c>, section 2.
/// </summary>
public static class CUnitTotals
{
	public const string SoftAttack = "soft_attack";
	public const string HardAttack = "hard_attack";
	public const string Defensiveness = "defensiveness";
	public const string Softness = "softness";
	public const string Armour = "armor_value";
	public const string Piercing = "ap_attack";

	/// <summary>The country modifier that widens or narrows what its units take of a front.</summary>
	public const string WidthModifier = "combat_width";

	/// <summary>The least a unit takes of a front, whatever its brigades come to.</summary>
	public static readonly CFixedPoint LeastWidth = CFixedPoint.One;

	/// <summary>The least average strength, and organisation, of a unit that can still fight.</summary>
	public static readonly CFixedPoint LeastStrengthToFight = new(100);

	public static readonly CFixedPoint LeastOrganisationToFight = CFixedPoint.One;

	/// <summary>
	/// One statistic added up over the brigades that still count towards the unit. The
	/// original adds toughness in twice.
	/// </summary>
	public static CFixedPoint Sum(CUnit unit, string statistic)
	{
		// A name nothing was ever given holds nought in every brigade; else it is found by its key, once.
		if (!CModifierKey.TryFind(statistic, out CModifierKey key))
		{
			return CFixedPoint.Zero;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit))
			{
				sum += subUnit.Stats[key].Raw;
			}
		}
		bool twice = statistic.Equals(CSubUnitStats.Toughness, StringComparison.OrdinalIgnoreCase);
		return new CFixedPoint(twice ? sum * 2 : sum);
	}

	/// <summary>
	/// A statistic the original averages for a unit - its organisation and its morale: the figures of the brigades that
	/// still count towards it over how many brigades it has, all of them, so a broken support brigade takes its share
	/// away and still divides (<c>reversing/findings/FINDINGS-unitdef.md</c>).
	/// </summary>
	public static CFixedPoint Mean(CUnit unit, string statistic)
	{
		if (unit.SubUnits.Count == 0 || !CModifierKey.TryFind(statistic, out CModifierKey key))
		{
			return CFixedPoint.Zero;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit))
			{
				sum += subUnit.Stats[key].Raw;
			}
		}
		return new CFixedPoint(sum * CFixedPoint.Scale / (unit.SubUnits.Count * (long)CFixedPoint.Scale));
	}

	/// <summary>How soft the unit is: its brigades' softness over how many brigades it has, all of them.</summary>
	public static CFixedPoint AverageSoftness(CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return CFixedPoint.Zero;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Stats[Softness].Raw;
		}
		return new CFixedPoint(sum * CFixedPoint.Scale / (unit.SubUnits.Count * (long)CFixedPoint.Scale));
	}

	/// <summary>The largest figure any of its brigades has in one statistic.</summary>
	public static CFixedPoint Best(CUnit unit, string statistic)
	{
		long best = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			best = Math.Max(best, subUnit.Stats[statistic].Raw);
		}
		return new CFixedPoint(best);
	}

	/// <summary>
	/// How much of a front the unit takes: its brigades' widths, by its country's
	/// modifier, and never less than one.
	/// </summary>
	public static CFixedPoint Width(CCountry owner, CUnit unit)
	{
		long width = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			width += subUnit.Stats[CSubUnitType.CombatWidth].Raw;
		}
		width = width * (owner.Modifier[WidthModifier].Raw + CFixedPoint.Scale) / CFixedPoint.Scale;
		return CFixedPoint.Max(new CFixedPoint(width), LeastWidth);
	}

	/// <summary>The strength its brigades have now, added up.</summary>
	public static CFixedPoint Strength(CUnit unit)
	{
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Strength.Raw;
		}
		return new CFixedPoint(sum);
	}

	/// <summary>The strength its brigades' types have at their fullest, added up.</summary>
	public static CFixedPoint FullStrength(CUnit unit)
	{
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Stats[CSubUnitType.MaxStrength].Raw;
		}
		return new CFixedPoint(sum);
	}

	public static CFixedPoint AverageOrganisation(CUnit unit)
	{
		if (unit.SubUnits.Count == 0)
		{
			return CFixedPoint.Zero;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += subUnit.Organisation.Raw;
		}
		return new CFixedPoint(sum * CFixedPoint.Scale / (unit.SubUnits.Count * (long)CFixedPoint.Scale));
	}

	/// <summary>The average of its brigades' experience, none counted as less than nothing.</summary>
	public static CFixedPoint AverageExperience(CUnit unit)
	{
		return AverageExperience(unit, CFixedPoint.Zero);
	}

	/// <summary>The same, with what a pride of the fleet is counted as having besides.</summary>
	public static CFixedPoint AverageExperienceWithPride(CGameState state, CUnit unit)
	{
		return AverageExperience(unit, state.Defines[Economy.CManpower.MilitarySection, CReinforcement.PrideBonus]);
	}

	private static CFixedPoint AverageExperience(CUnit unit, CFixedPoint prideBonus)
	{
		if (unit.SubUnits.Count == 0)
		{
			return CFixedPoint.Zero;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			sum += Math.Max(0, subUnit.Experience.Raw) + (subUnit.IsPride ? prideBonus.Raw : 0);
		}
		return new CFixedPoint(sum * CFixedPoint.Scale / (unit.SubUnits.Count * (long)CFixedPoint.Scale));
	}

	/// <summary>
	/// What a named circumstance - a terrain, a river, a fort, the night - does to the
	/// unit in one respect: the figures of its brigades that count towards it over how many
	/// brigades it has, all of them, in thousandths - so a broken support brigade takes its
	/// share away and still divides (<c>reversing/findings/FINDINGS-unitdef.md</c>).
	/// </summary>
	public static long Adjuster(CUnit unit, string name, string kind)
	{
		// A name or a kind nothing was ever given is held by no brigade; else each is found by its key, once.
		if (unit.SubUnits.Count == 0 || !CModifierKey.TryFind(name, out CModifierKey nameKey)
			|| !CModifierKey.TryFind(kind, out CModifierKey kindKey))
		{
			return 0;
		}
		long sum = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (CUnits.CountsTowardsItsUnit(unit, subUnit) && subUnit.Stats.Adjusters.TryGetValue(nameKey, out CModifier adjuster))
			{
				sum += adjuster[kindKey].Raw;
			}
		}
		return sum / unit.SubUnits.Count;
	}

	/// <summary>
	/// Whether a land unit can still fight: over its brigades that hold frontage, an
	/// average strength of a tenth and an average organisation of one. A unit with no
	/// such brigade cannot.
	/// </summary>
	public static bool CanFight(CUnit unit)
	{
		long strength = 0;
		long organisation = 0;
		int count = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			if (subUnit.Stats[CSubUnitType.CombatWidth] > CFixedPoint.Zero)
			{
				strength += subUnit.Strength.Raw;
				organisation += subUnit.Organisation.Raw;
				count++;
			}
		}
		if (count == 0)
		{
			return false;
		}
		long whole = count * (long)CFixedPoint.Scale;
		return strength * CFixedPoint.Scale / whole >= LeastStrengthToFight.Raw
			&& organisation * CFixedPoint.Scale / whole >= LeastOrganisationToFight.Raw;
	}
}
