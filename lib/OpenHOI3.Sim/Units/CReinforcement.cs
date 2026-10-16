using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What the reinforcement share of a country's IC does: brings its brigades back to
/// strength, for IC and manpower. See <c>docs/mechanics/upkeep.md</c>, section 4.
///
/// The arithmetic is the original's step for step, dropped fractions and all, because
/// two of its oddities are what a player sees: a brigade is not touched until it is
/// short by a thousandth of its full strength, and a topping-up never quite fills it
/// in a country that repairs faster than usual.
/// </summary>
public static class CReinforcement
{
	public const string UnitRepair = "unit_repair";
	public const string Bonus = "reinforcement_bonus";

	/// <summary>The define a ship that is the pride of the fleet adds to its experience.</summary>
	public const string PrideBonus = "PRIDE_BONUS_EXP";

	/// <summary>A day at full speed brings back half a thirtieth of a brigade's full strength.</summary>
	private const long DaysInAMonth = 30;
	private const long ShareOfADay = 500;

	/// <summary>A brigade is priced as costing at least one IC a day to build.</summary>
	private const long LeastCost = 1000;

	/// <summary>A reserve costs five times the share of it the conscription law keeps back, and never less than a regular.</summary>
	private const long ReservePremium = 5000;

	private const long MostExperience = 100000;

	/// <summary>
	/// Whether a brigade is in a state to be brought back to strength at all: short of
	/// what it may have, its unit not being carried anywhere, not falling back, and on
	/// land not in a battle. A ship is brought back to strength in port only.
	/// </summary>
	public static bool CanBeReinforced(CGameState state, CCountry owner, CUnit unit, CSubUnit subUnit)
	{
		long most = CUnits.MaxStrength(state, owner, subUnit).Raw;
		if (subUnit.Strength.Raw >= most || most <= 0)
		{
			return false;
		}
		if (unit.IsRetreating || (unit.IsFighting && unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air))
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Navy)
		{
			return unit.Location == unit.Base;
		}
		return unit.Kind == EUnitKind.Air || (unit.CarriedBy is null && unit.Transport is null);
	}

	/// <summary>
	/// How fast a unit's brigades are brought back, in thousandths of the usual. A unit
	/// that went short of supplies is reinforced that much slower, and one that got none
	/// not at all; so are the ships and wings of a crowded base.
	/// </summary>
	/// <param name="crowding">How much of its work the unit's base does, in thousandths: <see cref="CBaseCrowding.Of"/>.</param>
	public static long Speed(CCountry owner, CUnit unit, long crowding = CFixedPoint.Scale)
	{
		long speed = (CFixedPoint.Scale + owner.Modifier[Bonus].Raw)
			* (CFixedPoint.Scale + owner.Modifier[UnitRepair].Raw) / CFixedPoint.Scale;
		speed = speed * crowding / CFixedPoint.Scale;
		return speed * unit.SuppliesReceived.Raw / CFixedPoint.Scale;
	}

	/// <summary>
	/// One day of the share. Works out what bringing everything back would cost - the
	/// share's need - and, when <paramref name="apply"/> is set, does as much of it as
	/// the allocation and the manpower allow. Answers the IC it spent.
	/// </summary>
	public static CFixedPoint Run(CGameState state, CCountry country, CFixedPoint allocation, bool apply,
		Action<CFixedPoint>? charge = null)
	{
		long budget = allocation.Raw;
		long spent = 0;
		long cost = 0;
		long manpowerNeeded = 0;
		long manpowerUsed = 0;

		long reserveCost = Math.Max(CFixedPoint.Scale,
			-country.Modifier[CUnits.ReservesPenaltySize].Raw * ReservePremium / CFixedPoint.Scale);
		long startExperience = country.Modifier[CUnits.UnitStartExperience].Raw;
		long prideBonus = state.Defines[CManpower.MilitarySection, PrideBonus].Raw;

		var crowding = new CBaseCrowding(state);
		foreach (CUnit unit in CUnits.InWorkingOrder(country, unit => unit.MayReinforce))
		{
			long speed = Speed(country, unit, crowding.Of(unit));
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (!CanBeReinforced(state, country, unit, subUnit))
				{
					continue;
				}

				long most = CUnits.MaxStrength(state, country, subUnit).Raw;
				long full = subUnit.Stats[CSubUnitType.MaxStrength].Raw;
				long scale = full == 0 ? -1 : most * CFixedPoint.Scale / full;
				long missing = most - subUnit.Strength.Raw;

				// In thousandths of what it may have, and a brigade short by less is left alone.
				long share = missing * CFixedPoint.Scale / most;
				if (share <= 0)
				{
					continue;
				}

				long manpowerWhole = subUnit.Stats[CSubUnitStats.BuildCostManpower].Raw * scale / CFixedPoint.Scale;
				long needed = share * manpowerWhole / CFixedPoint.Scale;

				long aDay = full * CFixedPoint.Scale / (DaysInAMonth * CFixedPoint.Scale) * ShareOfADay / CFixedPoint.Scale;
				long gain = speed * aDay / CFixedPoint.Scale;
				long manpower = gain * manpowerWhole / CFixedPoint.Scale * CFixedPoint.Scale / most;
				if (manpower <= 0)
				{
					continue;
				}

				// How much of the day's work there are men for.
				long pool = country.Manpower.Raw;
				long afford = pool <= 0 ? 0 : Math.Min(pool * CFixedPoint.Scale / manpower, CFixedPoint.Scale);
				gain = gain * afford / CFixedPoint.Scale;
				long used = manpower * afford / CFixedPoint.Scale;

				long ic = Math.Max(subUnit.Stats[CSubUnitStats.BuildCostIc].Raw, LeastCost) * afford / CFixedPoint.Scale;
				if (subUnit.IsReserve)
				{
					ic = ic * reserveCost / CFixedPoint.Scale;
				}
				ic = ic * subUnit.Stats[CSubUnitStats.RepairCostMultiplier].Raw / CFixedPoint.Scale;

				// A day would fill it and more: only what is missing is done and paid for.
				// The original divides what is missing by the speed here, so a country that
				// repairs faster than usual leaves a little unfilled each time.
				if (gain + subUnit.Strength.Raw >= most)
				{
					long exact = speed == 0 ? -1 : missing * CFixedPoint.Scale / speed;
					long part = gain == 0 ? -1 : exact * CFixedPoint.Scale / gain;
					used = used * part / CFixedPoint.Scale;
					ic = ic * part / CFixedPoint.Scale;
					gain = exact;
				}

				if (apply && budget > 0)
				{
					long paid = ic * afford / CFixedPoint.Scale;
					if (paid > 0)
					{
						charge?.Invoke(new CFixedPoint(paid));
						spent += paid;
					}

					subUnit.Strength = new CFixedPoint(Math.Max(0, Math.Min(subUnit.Strength.Raw + gain, most)));
					if (subUnit.Strength > subUnit.Highest)
					{
						subUnit.Highest = subUnit.Strength;
					}

					// The new men come with the experience of a new unit, and water down the rest.
					long added = gain * CFixedPoint.Scale / most;
					long experience = Math.Max(0, subUnit.Experience.Raw);
					if (subUnit.IsPride)
					{
						experience += prideBonus;
					}
					experience = (CFixedPoint.Scale - added) * experience / CFixedPoint.Scale
						+ added * startExperience / CFixedPoint.Scale;
					subUnit.Experience = new CFixedPoint(Math.Min(experience, MostExperience));

					country.Manpower = new CFixedPoint(Math.Max(0, country.Manpower.Raw - used));
					budget -= ic;
					manpowerUsed += used;
				}

				cost += ic;
				manpowerNeeded += needed;
			}
		}

		country.ReinforcementCost = new CFixedPoint(cost);
		country.ReinforcementManpowerNeeded = new CFixedPoint(manpowerNeeded);
		country.ReinforcementManpowerUsed = new CFixedPoint(manpowerUsed);
		return new CFixedPoint(spent);
	}
}
