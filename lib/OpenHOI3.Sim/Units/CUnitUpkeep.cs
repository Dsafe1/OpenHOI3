using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a day does to every unit before its country's economy is run: its men are
/// rotated out, and it draws its supplies and fuel. See <c>docs/mechanics/upkeep.md</c>,
/// sections 2 and 3. What the units asked for is counted from nothing each day by
/// <see cref="CSupplyPass.RunDaily"/>, which comes first.
/// </summary>
public static class CUnitUpkeep
{
	/// <summary>The modifier a conscription law sets: the share of an army that goes home in a year.</summary>
	public const string Rotation = "peacetime_manpower_rotation";

	public const string CountrySection = "country";

	/// <summary>The define that makes an army short of supplies lose men faster.</summary>
	public const string LowSupplyFactor = "PEACETIME_MANPOWER_ROTATION_LOW_SUPPLY_FACTOR";

	/// <summary>A year's rate is taken a day at a time.</summary>
	private const long DaysInAYear = 365;

	/// <summary>Every country's units, in the order the countries are declared and then in the units' own.</summary>
	public static void RunDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.UnitsInOrder())
			{
				// The men go first, by how well the unit was supplied yesterday; attrition last, as the original's day ends a unit.
				Rotate(state, country, unit);
				CSupplyDraw.Draw(state, country, unit);
				CAttrition.ApplyDaily(state, country, unit);
			}

			// A unit finished and not yet placed eats too, out of the capital, though
			// no men leave it.
			foreach (CUnit unit in country.Deployments)
			{
				CSupplyDraw.Draw(state, country, unit);
			}
		}
	}

	/// <summary>
	/// The share of a brigade's full strength that a day of rotation takes, in
	/// thousandths of the modifier: more of it for a unit that went short of supplies.
	/// </summary>
	public static long RotationRate(CGameState state, CCountry owner, CUnit unit)
	{
		long rate = owner.Modifier[Rotation].Raw;
		long supplied = unit.SuppliesReceived.Raw;
		if (supplied < CFixedPoint.Scale)
		{
			long factor = state.Defines[CountrySection, LowSupplyFactor].Raw;
			rate = ((CFixedPoint.Scale - supplied) * factor / CFixedPoint.Scale + CFixedPoint.Scale) * rate / CFixedPoint.Scale;
		}
		return rate;
	}

	/// <summary>
	/// Takes a day's rotation off each of an army's brigades. Ships and wings keep
	/// their men. It is not asked whether the country is at war.
	/// </summary>
	public static void Rotate(CGameState state, CCountry owner, CUnit unit)
	{
		if (unit.Kind == EUnitKind.Navy || unit.Kind == EUnitKind.Air)
		{
			return;
		}

		long rate = RotationRate(state, owner, unit);
		foreach (CSubUnit subUnit in unit.SubUnits)
		{
			long most = CUnits.MaxStrength(state, owner, subUnit).Raw;
			long lost = most * rate / CFixedPoint.Scale * CFixedPoint.Scale / (DaysInAYear * CFixedPoint.Scale);
			long left = Math.Max(0, Math.Min(subUnit.Strength.Raw - lost, most));
			subUnit.Strength = new CFixedPoint(left);
			if (subUnit.Strength > subUnit.Highest)
			{
				subUnit.Highest = subUnit.Strength;
			}
		}
	}
}
