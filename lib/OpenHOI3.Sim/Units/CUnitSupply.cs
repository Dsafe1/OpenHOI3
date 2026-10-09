using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a unit uses in a day, in supplies and in fuel. See
/// <c>docs/mechanics/upkeep.md</c>, section 1. The taking of it is
/// <see cref="Supply.CSupplyDraw"/>: a unit draws on the province it stands in, which
/// the supply network keeps filled.
/// </summary>
public static class CUnitSupply
{
	/// <summary>The modifier a law or a minister sets: more or less of everything a unit uses.</summary>
	public const string Consumption = "supply_consumption";

	public const string SupplyStatistic = "supply_consumption";
	public const string FuelStatistic = "fuel_consumption";

	/// <summary>Every level of anything a brigade holds makes it use a hundredth more.</summary>
	private const long PerLevel = 10;

	/// <summary>
	/// What everything a country's units use is multiplied by, in thousandths. The
	/// original takes off for the skill of the army group's commander and adds what the
	/// chain of command and the unit's own leader bring; there are no leaders here.
	/// </summary>
	public static long Potency(CCountry owner)
	{
		return Math.Max(0, CFixedPoint.Scale + owner.Modifier[Consumption].Raw);
	}

	/// <summary>The levels a brigade holds, of all its technologies added together.</summary>
	public static long LevelsHeld(CSubUnit subUnit)
	{
		long levels = 0;
		foreach (CTechnology technology in subUnit.Type.Technologies)
		{
			levels += subUnit.TechnologyLevels.GetValueOrDefault(technology.Name);
		}
		return levels;
	}

	/// <summary>
	/// The supplies a unit's own brigades use in a day. A brigade under strength uses
	/// that much less; a ship uses the same whatever state it is in.
	/// </summary>
	public static CFixedPoint SupplyConsumption(CCountry owner, CUnit unit)
	{
		long potency = Potency(owner);
		long total = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			long used = subUnit.Stats[SupplyStatistic].Raw * (potency + LevelsHeld(subUnit) * PerLevel) / CFixedPoint.Scale;
			if (unit.Kind != EUnitKind.Navy)
			{
				long full = subUnit.Stats[CSubUnitType.MaxStrength].Raw;
				long share = full == 0 ? -1 : subUnit.Strength.Raw * CFixedPoint.Scale / full;
				used = used * share / CFixedPoint.Scale;
			}
			total += used;
		}
		return new CFixedPoint(total);
	}

	/// <summary>The fuel a unit's own brigades use in a day when they use any. Strength does not come into it.</summary>
	public static CFixedPoint FuelConsumption(CCountry owner, CUnit unit)
	{
		long potency = Potency(owner);
		long total = 0;
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			total += subUnit.Stats[FuelStatistic].Raw * (potency + LevelsHeld(subUnit) * PerLevel) / CFixedPoint.Scale;
		}
		return new CFixedPoint(total);
	}

	/// <summary>
	/// Whether a unit burns fuel today: one that is moving, and a fleet that is not in
	/// its own port. An army or an air unit standing still burns none.
	/// </summary>
	public static bool BurnsFuel(CUnit unit)
	{
		return unit.IsMoving || (unit.Kind == EUnitKind.Navy && unit.Location != unit.Base);
	}

	/// <summary>What all of a country's units use in a day, whether or not they burn fuel today.</summary>
	public static (CFixedPoint Supplies, CFixedPoint Fuel) DailyUse(CCountry country)
	{
		CFixedPoint supplies = CFixedPoint.Zero;
		CFixedPoint fuel = CFixedPoint.Zero;
		foreach (CUnit unit in country.AllUnits())
		{
			supplies += SupplyConsumption(country, unit);
			fuel += FuelConsumption(country, unit);
		}
		return (supplies, fuel);
	}
}
