using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// The lists of the four windows a unit is ordered from: which columns each has, what a
/// column writes for a unit type, and the order a click on a column puts the list in.
/// See <c>docs/mechanics/builders.md</c>, section 3.
/// </summary>
public static class CBuilderLists
{
	public const string SortPrefix = "sort_";

	/// <summary>The button that sorts by a type's own priority, which no column shows.</summary>
	public const string SortByPriority = "sort_prio";

	public const string Priority = "priority";

	private const string BuildCostIc = CSubUnitStats.BuildCostIc;
	private const string BuildCostManpower = CSubUnitStats.BuildCostManpower;
	private const string BuildTime = CSubUnitStats.BuildTime;

	/// <summary>A land brigade: the division designer's picker and the brigade builder.</summary>
	public static readonly CBuilderColumn[] Land =
	{
		new("str", "max_strength", EBuilderFigure.Men),
		new("org", "default_organisation"),
		new("cw", "combat_width"),
		new("sa", "soft_attack"),
		new("ha", "hard_attack"),
		new("aa", "air_attack"),
		new("piercing", "ap_attack"),
		new("armored", "armor_value"),
		new("def", "defensiveness"),
		new("ad", "air_defence"),
		new("tou", "toughness"),
		new("sof", "softness", EBuilderFigure.Percent),
		new("speed", "maximum_speed", EBuilderFigure.Speed),
		new("sup", "suppression"),
		new("sc", "supply_consumption", decimals: 2),
		new("fc", "fuel_consumption", decimals: 2),
		new("ic", BuildCostIc, EBuilderFigure.IcCost, 2),
		new("mp", BuildCostManpower, EBuilderFigure.Manpower, 2),
		new("time", BuildTime, EBuilderFigure.Days),
		new("off", "officers"),
	};

	/// <summary>A ship.</summary>
	public static readonly CBuilderColumn[] Ship =
	{
		new("org", "default_organisation"),
		new("con_a", "convoy_attack"),
		new("sea_a", "sea_attack"),
		new("sub_a", "sub_attack"),
		new("air_a", "air_attack"),
		new("shore", "shore_bombardment"),
		new("sea_d", "sea_defence"),
		new("air_d", "air_defence"),
		new("sur_det", "surface_detection"),
		new("air_det", "air_detection"),
		new("sub_det", "sub_detection"),
		new("visibi", "visibility"),
		new("trans", "transport_capability"),
		new("range", "range"),
		new("distance", "distance", EBuilderFigure.Hundredfold),
		new("speed", "maximum_speed", EBuilderFigure.Speed),
		new("hull", "hull", decimals: 2),
		new("sc", "supply_consumption", decimals: 2),
		new("fc", "fuel_consumption", decimals: 2),
		new("ic", BuildCostIc, EBuilderFigure.IcCost, 2),
		new("mp", BuildCostManpower, EBuilderFigure.Manpower, 2),
		new("time", BuildTime, EBuilderFigure.Days),
		new("pos", "positioning", EBuilderFigure.Percent, 1),
		new("off", "officers"),
	};

	/// <summary>A wing.</summary>
	public static readonly CBuilderColumn[] Air =
	{
		new("org", "default_organisation"),
		new("hard_a", "hard_attack"),
		new("soft_a", "soft_attack"),
		new("sea_a", "sea_attack"),
		new("air_a", "air_attack"),
		new("strat_a", "strategic_attack"),
		new("surf_d", "surface_defence"),
		new("air_d", "air_defence"),
		new("sur_det", "surface_detection"),
		new("air_det", "air_detection"),
		new("trans", "transport_capability"),
		new("range", "range"),
		new("speed", "maximum_speed", EBuilderFigure.Speed),
		new("sc", "supply_consumption", decimals: 2),
		new("fc", "fuel_consumption", decimals: 2),
		new("ic", BuildCostIc, EBuilderFigure.IcCost, 2),
		new("mp", BuildCostManpower, EBuilderFigure.Manpower, 2),
		new("time", BuildTime, EBuilderFigure.Days),
		new("off", "officers"),
	};

	public static CBuilderColumn[] For(EUnitBranch branch)
	{
		return branch switch
		{
			EUnitBranch.Naval => Ship,
			EUnitBranch.Air => Air,
			_ => Land,
		};
	}

	/// <summary>
	/// What a column shows for one brigade, ship or wing as it would be built: its own
	/// figures, and for the three costs what its country would pay.
	/// </summary>
	/// <param name="levels">The levels it is built with, all its technologies together.</param>
	public static string Text(CBuilderColumn column, CCountry country, CSubUnit subUnit, long levels, bool isReserve = false)
	{
		CFixedPoint value = subUnit.Stats[column.Statistic];
		switch (column.Figure)
		{
			case EBuilderFigure.Hundredfold:
				return CNumberText.Format(value * 100, column.Decimals);
			case EBuilderFigure.Percent:
				return CNumberText.Format(value * 100, column.Decimals) + "%";
			case EBuilderFigure.Men:
				return CBuilderText.WithSuffix(value * 100);
			case EBuilderFigure.Speed:
				return CBuilderText.ListSpeed(value);
			case EBuilderFigure.IcCost:
				return CNumberText.Format(new CFixedPoint(CBuildCosts.BuildCost(country, subUnit, levels)), column.Decimals);
			case EBuilderFigure.Manpower:
				return CNumberText.Format(CBuilderText.ManpowerOf(country, subUnit, isReserve), column.Decimals);
			case EBuilderFigure.Days:
				return CBuildCosts.BuildTime(country, subUnit, levels).ToString(CultureInfo.InvariantCulture);
			default:
				return CNumberText.Format(value, column.Decimals);
		}
	}

	/// <summary>
	/// The statistic a sort button sorts by: its column's, or the type's priority.
	/// Nothing for a button that is not one of the list's.
	/// </summary>
	public static string? SortStatistic(CBuilderColumn[] columns, string button)
	{
		if (button.Equals(SortByPriority, StringComparison.OrdinalIgnoreCase))
		{
			return Priority;
		}
		foreach (CBuilderColumn column in columns)
		{
			if (column.SortButton.Equals(button, StringComparison.OrdinalIgnoreCase))
			{
				return column.Statistic;
			}
		}
		return null;
	}

	/// <summary>
	/// A list in the order a sort leaves it: by one statistic of each as it would be
	/// built, largest first - or smallest first - with equals left in the order they
	/// came in. The statistic is the brigade's own, whoever builds it: sorting by cost
	/// goes by the type's cost, not the country's price.
	/// </summary>
	public static List<CSubUnit> Sorted(IEnumerable<CSubUnit> subUnits, string statistic, bool smallestFirst)
	{
		var sorted = new List<CSubUnit>();
		foreach (CSubUnit subUnit in subUnits)
		{
			long value = subUnit.Stats[statistic].Raw;
			int at = sorted.Count;
			while (at > 0)
			{
				long before = sorted[at - 1].Stats[statistic].Raw;
				if (!(smallestFirst ? before > value : before < value))
				{
					break;
				}
				at--;
			}
			sorted.Insert(at, subUnit);
		}
		return sorted;
	}

	/// <summary>
	/// One of each type a country may order of a branch, as it would be built today:
	/// with the country's own level of every technology.
	/// </summary>
	public static List<CSubUnit> AsBuiltToday(CGameState state, CCountry country, EUnitBranch branch)
	{
		var subUnits = new List<CSubUnit>();
		foreach (CSubUnitType type in CUnitAvailability.Buildable(state, country, branch))
		{
			subUnits.Add(AsBuiltToday(country, type, out long _));
		}
		return subUnits;
	}

	/// <summary>One of a type with the country's own level of every technology.</summary>
	public static CSubUnit AsBuiltToday(CCountry country, CSubUnitType type, out long levels)
	{
		var order = new CBrigadeOrder(type);
		CBuildQueue.SetModelToCountrys(country, order);
		return CBuildQueue.AsBuilt(order, false, out levels);
	}

	/// <summary>The levels a brigade holds, all its technologies together.</summary>
	public static long LevelsOf(CSubUnit subUnit)
	{
		long levels = 0;
		foreach (int level in subUnit.TechnologyLevels.Values)
		{
			levels += level;
		}
		return levels;
	}
}
