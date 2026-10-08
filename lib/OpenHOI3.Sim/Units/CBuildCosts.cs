using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// What a kind of unit costs a country in IC and in days. Upgrades are priced from it,
/// and the build queue will be. See <c>docs/mechanics/upkeep.md</c>, section 5.
/// </summary>
public static class CBuildCosts
{
	/// <summary>The modifier a law or a minister sets: everything cheaper and quicker to build, or dearer and slower.</summary>
	public const string IndustrialEfficiency = "industrial_efficiency";

	/// <summary>What the technology of industrial efficiency adds to it, level by level.</summary>
	public const string TechnologyEfficiency = "ic_efficiency";

	public const string RecruitmentTime = "unit_recruitment_time";

	/// <summary>An ability is measured against this much, as research measures it.</summary>
	private const long AbilityExpected = 5000;

	/// <summary>A point short of that makes a unit a tenth dearer; a point over, a twentieth cheaper.</summary>
	private const long PerPointShort = 100;
	private const long PerPointOver = 50;

	/// <summary>Nothing is ever more than this much cheaper.</summary>
	private const long MostDiscount = 990;

	/// <summary>And nothing is built in less than a twentieth of its time.</summary>
	private const long LeastTime = 50;

	/// <summary>
	/// How much cheaper and quicker a country builds what a practical covers, in
	/// thousandths; below nothing for a country with little of it. Past one point over,
	/// it is the original's rough square root of the surplus that counts.
	/// </summary>
	public static long CategoryDiscount(CCountry country, string category)
	{
		long discount = 0;
		// A type that names no practical has none to be measured against.
		if (category.Length != 0)
		{
			long counted = Math.Max(0, country.Ability(category).Raw) - AbilityExpected;
			if (counted < 0)
			{
				discount = counted * PerPointShort / CFixedPoint.Scale;
			}
			else
			{
				if (counted > CFixedPoint.Scale)
				{
					counted = CResearch.RoughSquareRoot(counted);
				}
				discount = counted * PerPointOver / CFixedPoint.Scale;
			}
		}
		discount += country.Modifier[IndustrialEfficiency].Raw + country.Technology[TechnologyEfficiency].Raw;
		return Math.Min(discount, MostDiscount);
	}

	/// <summary>The modifier that speeds or slows the building of a kind of unit.</summary>
	public static string SpeedModifier(CSubUnitType type)
	{
		if (type.Flag("is_rocket"))
		{
			return "rocket_build_speed";
		}
		if (type.Flag("is_armor"))
		{
			return "tank_build_speed";
		}
		return type.Branch switch
		{
			EUnitBranch.Air => "air_build_speed",
			EUnitBranch.Naval => "naval_build_speed",
			_ => "land_build_speed",
		};
	}

	/// <summary>
	/// The days the country would take to build this brigade as it is now equipped, in
	/// whole days.
	/// </summary>
	public static long BuildTime(CCountry country, CSubUnit subUnit)
	{
		CSubUnitType type = subUnit.Type;
		long factor = CFixedPoint.Scale + country.Modifier[RecruitmentTime].Raw
			- CategoryDiscount(country, type.OnCompletion);
		long speed = CFixedPoint.Scale + country.Modifier[SpeedModifier(type)].Raw;
		long scaled = Math.Max(speed * factor / CFixedPoint.Scale, LeastTime);
		return scaled * subUnit.Stats[CSubUnitStats.BuildTime].Raw / CFixedPoint.Scale / CFixedPoint.Scale;
	}

	/// <summary>
	/// The IC a day the country would pay to build this brigade as it is now equipped,
	/// in thousandths. A reserve costs the share of it that the conscription law leaves.
	/// </summary>
	public static long BuildCost(CCountry country, CSubUnit subUnit)
	{
		long multiplier = CFixedPoint.Scale - CategoryDiscount(country, subUnit.Type.OnCompletion);
		long cost = multiplier * subUnit.Stats[CSubUnitStats.BuildCostIc].Raw / CFixedPoint.Scale;
		if (subUnit.IsReserve)
		{
			cost = (CFixedPoint.Scale + country.Modifier[CUnits.ReservesPenaltySize].Raw) * cost / CFixedPoint.Scale;
		}
		return cost;
	}
}
