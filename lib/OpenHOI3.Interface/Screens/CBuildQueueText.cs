using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What a line of the production screen's build queue shows, as the original builds it.
/// See <c>docs/mechanics/productionscreen.md</c>, section 6.
/// </summary>
public static class CBuildQueueText
{
	/// <summary>What stands for a date that will never come.</summary>
	public const string NoDate = "---";

	/// <summary>A line paid this much of a day or less is not shown as paid in full.</summary>
	private const long AllButPaid = 990;

	/// <summary>
	/// Which of a line's three backgrounds is shown: green for one paid in full the
	/// last time the queue was worked through, orange for one paid in part, red for one
	/// given nothing.
	/// </summary>
	public static EQueueBackground Background(CConstruction construction)
	{
		return construction.Status.Raw > AllButPaid ? EQueueBackground.Green
			: construction.Status.Raw > 0 ? EQueueBackground.Orange
			: EQueueBackground.Red;
	}

	/// <summary>How much of a day's work the line was last paid, as a whole percentage.</summary>
	public static string Speed(CConstruction construction)
	{
		return CNumberText.Format(new CFixedPoint(construction.Status.Raw * 100), 0) + "%";
	}

	/// <summary>What a full day of it costs, to two decimals, and the word for IC straight after.</summary>
	public static string Cost(Func<string, string> localise, CConstruction construction)
	{
		return CNumberText.Format(construction.Cost, 2) + localise("IC");
	}

	/// <summary>The day it will be done at the pace it was last paid for, or three dashes for a line given nothing.</summary>
	public static string Date(Func<string, string> localise, CDate today, CConstruction construction)
	{
		if (CBuildQueue.DaysLeft(construction) is not { } days)
		{
			return NoDate;
		}
		CDate done = today.AddDays((int)Math.Min(days, int.MaxValue / 2));
		return done.Year.ToString(CultureInfo.InvariantCulture) + " " + localise(done.MonthKey) + " "
			+ done.Day.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>How many more of the unit follow this one, with a plus before it; nothing for a line of one.</summary>
	public static string Series(CConstruction construction)
	{
		return construction.Size > 1 ? "+" + (construction.Size - 1).ToString(CultureInfo.InvariantCulture) : "";
	}

	/// <summary>How far the one being built has got, in hundredths.</summary>
	public static int Progress(CConstruction construction)
	{
		if (construction.Duration.Raw <= 0)
		{
			return 0;
		}
		long share = construction.Progress.Raw * CFixedPoint.Scale / construction.Duration.Raw;
		return (int)Math.Clamp(share * 100 / CFixedPoint.Scale, 0, 100);
	}

	/// <summary>What the line is called: the unit's name, or its first brigade's where it has none.</summary>
	public static string Name(CConstruction construction)
	{
		if (construction.Name.Length > 1)
		{
			return construction.Name;
		}
		return construction.Brigades.Count != 0 ? construction.Brigades[0].Name : "";
	}

	/// <summary>
	/// A building line's name, the original's slot 13: the building's, and " (" + the province's + ")" where it has one
	/// (<c>buildinglines.md</c>, section 7).
	/// </summary>
	public static string BuildingName(Func<string, string> localise, CGameState state, CConstruction construction)
	{
		string name = construction.Building is { } building ? localise(building.Key) : "";
		return construction.Location != 0 ? name + " (" + state.Province(construction.Location).Name + ")" : name;
	}

	/// <summary>
	/// A convoy line's name, the original's slot 13: "Convoy" or "Escort", with no count (<c>convoylines.md</c>,
	/// section 4).
	/// </summary>
	public static string ConvoyName(Func<string, string> localise, CConstruction construction)
	{
		return localise(construction.IsEscorts ? "CC_ESCORT" : "CC_CONVOY");
	}

	/// <summary>
	/// The sprite of the counter drawn on the line: the one named for its first brigade's type, for a building
	/// <c>icon_build_</c> and its key - which the underground has none of, and keeps the file's - and for a convoy line
	/// <c>icon_build_convoy</c> or <c>icon_build_escort</c>.
	/// </summary>
	public static string CounterSprite(CConstruction construction)
	{
		if (construction.IsConvoy)
		{
			return construction.IsEscorts ? "icon_build_escort" : "icon_build_convoy";
		}
		if (construction.Building is { } building)
		{
			return "icon_build_" + building.Key;
		}
		return construction.Brigades.Count != 0 ? "GFX_counter_" + construction.Brigades[0].Type.Key : "";
	}

	/// <summary>
	/// Which of the line's pictures says what kind of thing it is building. The original chooses only for a unit's line and
	/// leaves the four stacked as the file has them, the buildings' last and on top, which this shows alone
	/// (<c>convoylines.md</c>, section 4).
	/// </summary>
	public static string KindElement(CConstruction construction)
	{
		if (!construction.IsUnit)
		{
			return "production_queue_buildings";
		}
		if (construction.Brigades.Count == 0)
		{
			return "";
		}
		return construction.Brigades[0].Type.Branch switch
		{
			EUnitBranch.Naval => "production_queue_naval",
			EUnitBranch.Air => "production_queue_air",
			_ => "production_queue_division",
		};
	}
}
