using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the confirm-build dialog says of a building ordered from the production screen: its cost for so many at once,
/// its time for so many one after another, the colour of what the share leaves, and what it teaches. See
/// <c>docs/mechanics/buildinglines.md</c>, section 2.
/// </summary>
public static class CConfirmBuildText
{
	/// <summary>The most either count's plus allows; the minus stops at one.</summary>
	public const int Most = 99;

	/// <summary>How many a press with Ctrl moves a count by.</summary>
	public const int Bundle = 10;

	/// <summary>The days a time is written in days up to; beyond, in months of thirty.</summary>
	public const int DaysShown = 365;

	/// <summary>One building's cost a day and its days for the country, as a line of it is priced.</summary>
	public static (CFixedPoint Cost, int Days) Price(CCountry country, CBuilding building)
	{
		var line = new CConstruction(country.Tag) { Building = building };
		CBuildingLines.Price(country, line);
		return (line.Cost, (int)(line.Duration.Raw / CFixedPoint.Scale));
	}

	/// <summary><c>ic_cost_value</c>: parallel times the cost a day, to two places.</summary>
	public static string Cost(int parallel, CFixedPoint cost)
	{
		return CNumberText.Format(new CFixedPoint(cost.Raw * parallel), 2);
	}

	/// <summary><c>build_time_value</c>: serial times the days, as days to a year and months of thirty beyond.</summary>
	public static string Time(Func<string, string> localise, int serial, int days)
	{
		long total = (long)serial * days;
		return total <= DaysShown
			? total.ToString(CultureInfo.InvariantCulture) + " " + localise("DAYS")
			: (total / 30).ToString(CultureInfo.InvariantCulture) + " " + localise("MONTHS");
	}

	/// <summary>
	/// Which of the three marks shows: what the production share gives a day less every line's cost a day is the spare;
	/// green where it pays parallel times the cost, orange where it is above nought, red otherwise.
	/// </summary>
	public static string Mark(CCountry country, int parallel, CFixedPoint cost)
	{
		long share = country.ProductionShares[(int)Sim.Economy.EProductionCategory.Production].ToThousandths().Raw;
		long spare = share * country.TotalIc - CBuildQueue.QueueCost(country).Raw;
		if (spare - cost.Raw * parallel >= 0)
		{
			return "selected_brigade_green";
		}
		return spare > 0 ? "selected_brigade_orange" : "selected_brigade_red";
	}

	/// <summary><c>on_complete_impact</c>: "+" and the practical it teaches, to one place.</summary>
	public static string Impact(CBuilding building)
	{
		return "+" + CNumberText.Format(building.CompletionSize, 1);
	}

	/// <summary><c>on_complete_icon</c>'s sprite: <c>icon_</c> and the category it teaches.</summary>
	public static string CategoryIcon(CBuilding building) => "icon_" + building.OnCompletion;

	/// <summary><c>photo</c>'s sprite: <c>picture_</c> and the building's key.</summary>
	public static string Picture(CBuilding building) => "picture_" + building.Key;
}
