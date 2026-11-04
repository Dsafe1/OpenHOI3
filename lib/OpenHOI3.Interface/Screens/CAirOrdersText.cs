using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Weather;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the air orders window says and decides beyond its list: which row it ticks first,
/// what its sliders give, its figures, its three boxes of the hours, its dates and its
/// readout of the day. See <c>docs/mechanics/fleetsandair.md</c>, section 4, and
/// <c>docs/mechanics/aircombat.md</c>, sections 1 and 2.
/// </summary>
public static class CAirOrdersText
{
	public const string PointButton = "point_target_button";
	public const string RegionButton = "region_target_button";
	public const string CircleButton = "circle_target_button";
	public const string ConeButton = "cone_target_button";
	public const string RangeSlider = "range_slider";
	public const string AngleSlider = "angle_slider";
	public const string WidthSlider = "width_slider";
	public const string RangeValue = "range_value";
	public const string AngleValue = "angle_value";
	public const string WidthValue = "width_value";
	public const string SecondTargetText = "second_target";
	public const string DayBox = "day_checkbox";
	public const string NightBox = "night_checkbox";
	public const string BothBox = "both_checkbox";
	public const string ContinuousBox = "continous_checkbox";
	public const string DayTimeTitle = "daytime_title";
	public const string DayTimeIcon = "daytime_icon";
	public const string DayTimeDescription = "daytime_icondesc";

	/// <summary>The window a date is set in, and the two places the air window puts one.</summary>
	public const string DateWidget = "datewidget";
	public const string StartPlace = "mission_timewidget_start";
	public const string EndPlace = "mission_timewidget_stop";

	/// <summary>The hint written where an order cannot be accepted.</summary>
	public const string NoSecondTargetKey = "ORDER_NO_SECOND_TARGET";

	/// <summary>Where the window sets the three sliders, in thousandths of each: the range, the angle, the width.</summary>
	public const long RangeStart = 0;
	public const long AngleStart = 16;
	public const long WidthStart = 250;

	private const long Thousand = 1000;
	private const double Pi = 3.1415927410125732;
	private const long HoursPerYear = 8760;

	/// <summary>The button of a shape.</summary>
	public static string ShapeButton(EAirAreaShape shape)
	{
		return shape switch
		{
			EAirAreaShape.Circle => CircleButton,
			EAirAreaShape.Cone => ConeButton,
			EAirAreaShape.Region => RegionButton,
			_ => PointButton,
		};
	}

	/// <summary>The range slider works for a circle and a cone.</summary>
	public static bool UsesRange(EAirAreaShape shape) => shape is EAirAreaShape.Circle or EAirAreaShape.Cone;

	/// <summary>The angle and width sliders work for a cone alone.</summary>
	public static bool UsesArc(EAirAreaShape shape) => shape == EAirAreaShape.Cone;

	/// <summary>
	/// Whether the window takes a slider where it now stands: only where it has moved by
	/// more than a twentieth of where it now is from the value taken last. From nothing to
	/// nothing is no move; to nothing from anything is.
	/// </summary>
	public static bool Takes(long taken, long now)
	{
		return now == 0 ? taken != 0 : Math.Abs(taken - now) * 20 > now;
	}

	/// <summary>The range a slider in thousandths gives: that share of how far the unit can fly.</summary>
	public static CFixedPoint RangeAt(CUnit unit, long slider)
	{
		return new CFixedPoint((slider * CUnitOrders.RangeOf(unit).Raw + Thousand / 2) / Thousand);
	}

	/// <summary>The angle or width a slider in thousandths gives: that share of a turn, in radians.</summary>
	public static float TurnAt(long slider)
	{
		double share = slider / (double)Thousand;
		return (float)((share + share) * Pi);
	}

	/// <summary><c>range_value</c>: <c>ORDER_RANGE_VAL</c>, the range to the nearest whole.</summary>
	public static string RangeFigure(Func<string, string> localise, CFixedPoint range)
	{
		string value = (range.Raw / (double)Thousand).ToString("F0", CultureInfo.InvariantCulture);
		return CTextTemplate.Fill(localise("ORDER_RANGE_VAL"), ("VAL", value));
	}

	/// <summary><c>angle_value</c> and <c>width_value</c>: degrees to two places.</summary>
	public static string DegreesFigure(Func<string, string> localise, string key, float radians)
	{
		string value = (radians * 180.0 / Pi).ToString("F2", CultureInfo.InvariantCulture);
		return CTextTemplate.Fill(localise(key), ("VAL", value));
	}

	/// <summary>
	/// The three boxes of the hours as one choice, by the hours chosen last: "both" ticked
	/// from day or night, then night from day or both, then day from night or both, each
	/// clearing the other two. Clearing the box that is ticked changes nothing, and leaves
	/// none ticked.
	/// </summary>
	public static int ChooseTime(int time, CTimeBoxes ticked, out CTimeBoxes shown)
	{
		shown = ticked;
		if (time is CMission.ByDay or CMission.ByNight && ticked.Both)
		{
			shown = new CTimeBoxes(false, false, true);
			return CMission.DayAndNight;
		}
		if (time is CMission.ByDay or CMission.DayAndNight && ticked.Night)
		{
			shown = new CTimeBoxes(false, true, false);
			return CMission.ByNight;
		}
		if (time is CMission.ByNight or CMission.DayAndNight && ticked.Day)
		{
			shown = new CTimeBoxes(true, false, false);
			return CMission.ByDay;
		}
		return time;
	}

	/// <summary>The hour of the game a month on or back, the hour of the day kept.</summary>
	public static long AddMonths(long hour, int months)
	{
		CDate date = CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay)).AddMonths(months);
		return (long)date.DayNumber * CGameState.HoursPerDay + hour % CGameState.HoursPerDay;
	}

	/// <summary>
	/// A date widget's arrow: a year of 8,760 hours, a month, a day or an hour on or back,
	/// then held between the defines' start and end.
	/// </summary>
	public static long Step(CGameState state, long hour, EDateStep step, int by)
	{
		long next = step switch
		{
			EDateStep.Year => hour + by * HoursPerYear,
			EDateStep.Month => AddMonths(hour, by),
			EDateStep.Day => hour + by * CGameState.HoursPerDay,
			_ => hour + by,
		};
		long first = (long)state.StartDate.DayNumber * CGameState.HoursPerDay;
		long last = (long)state.EndDate.DayNumber * CGameState.HoursPerDay;
		if (next > last)
		{
			next = last;
		}
		if (next < first)
		{
			next = first;
		}
		return next;
	}

	/// <summary>
	/// A date widget's <c>date</c>: the year, the month's name cut to three letters, the day and the hour, under the four
	/// columns of arrows - "1936 Jan 4 11:00". <i>Measured</i> on the maintainer's screenshots of the land orders window
	/// (<c>docs/mechanics/landorders.md</c>, section 2); the original's formatter was not read.
	/// </summary>
	public static string DateText(Func<string, string> localise, long hour)
	{
		CDate date = CDate.FromDayNumber((int)(hour / CGameState.HoursPerDay));
		string month = date.MonthKey.Length == 0 ? "" : localise(date.MonthKey);
		if (month.Length > 3)
		{
			month = month[..3];
		}
		return $"{date.Year} {month} {date.Day} {hour % CGameState.HoursPerDay}:00";
	}

	/// <summary>The hour a "continuous" mission ends: the defines' end date.</summary>
	public static long ContinuousEnd(CGameState state)
	{
		return (long)state.EndDate.DayNumber * CGameState.HoursPerDay;
	}

	/// <summary><c>daytime_title</c>: <c>ORDER_DAY_TIME</c> with the province's name.</summary>
	public static string DayTimeText(Func<string, string> localise, string province)
	{
		return CTextTemplate.Fill(localise("ORDER_DAY_TIME"), ("PROV", province));
	}

	/// <summary>Whether it is night in the window's province at the start's hour of the day and month.</summary>
	public static bool IsNightAtStart(CGameState state, CProvince province, long start)
	{
		CDate date = CDate.FromDayNumber((int)(start / CGameState.HoursPerDay));
		return CDaylight.IsNight(state, province, (int)(start % CGameState.HoursPerDay), date.Month - 1);
	}

	/// <summary><c>daytime_icon</c>'s frame: the second at night, the first by day.</summary>
	public static int DayTimeFrame(bool night) => night ? 2 : 1;

	/// <summary><c>daytime_icondesc</c>'s key.</summary>
	public static string DayTimeKey(bool night) => night ? "ORDER_NIGHT" : "ORDER_DAY";

	/// <summary>
	/// A unit's wings counted three ways: not bombers; bombers whose strategic attack is
	/// above half their soft and hard attack together; and the other bombers.
	/// </summary>
	public static (int Fighters, int Tactical, int Strategic) CountWings(CUnit unit)
	{
		int fighters = 0;
		int tactical = 0;
		int strategic = 0;
		foreach (CSubUnit wing in unit.SubUnits)
		{
			if (!wing.Type.Flag(Sim.Combat.CAirModifiers.IsBomber))
			{
				fighters++;
				continue;
			}
			long half = (wing.Stats[SoftAttack].Raw + wing.Stats[HardAttack].Raw) * Thousand / 2000;
			if (half < wing.Stats[StrategicAttack].Raw)
			{
				strategic++;
			}
			else
			{
				tactical++;
			}
		}
		return (fighters, tactical, strategic);
	}

	private const string SoftAttack = "soft_attack";
	private const string HardAttack = "hard_attack";
	private const string StrategicAttack = "strategic_attack";
	private const string SeaAttack = "sea_attack";

	/// <summary>
	/// The row the air window ticks when its list is made, or -1: a change of base where
	/// the unit may stand there and could be given one; otherwise the row of the order to
	/// keep; otherwise the original's ladder - by what the unit's wings are and what the
	/// province has. A rung ticks only a row that can be given. The second rung asks
	/// nothing of what the first ticked and so replaces it.
	/// </summary>
	/// <param name="keep">The order whose row to tick where it can be given, or none.</param>
	public static int Suggested(IReadOnlyList<COrderRow> rows, CGameState state, CCountry country, CUnit unit,
		CProvince province, EUnitOrder keep)
	{
		int Row(EUnitOrder order)
		{
			for (int index = 0; index < rows.Count; index++)
			{
				if (rows[index].Order == order && rows[index].MayGive)
				{
					return index;
				}
			}
			return -1;
		}

		int rebase = Row(EUnitOrder.RebaseAir);
		if (rebase >= 0 && CMovementRules.MayStandIn(state, country, unit, province)
			&& !CMovementRules.IsEnemy(state, province.Controller, country.Tag)
			&& CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero)
		{
			return rebase;
		}
		int kept = keep == EUnitOrder.None ? -1 : Row(keep);
		if (kept >= 0)
		{
			return kept;
		}

		int ticked = -1;
		(int fighters, int tactical, int strategic) = CountWings(unit);
		if (tactical < fighters && strategic < fighters)
		{
			int superiority = Row(EUnitOrder.AirSuperiority);
			ticked = superiority >= 0 && CMovementRules.IsEnemy(state, province.Controller, country.Tag)
				? superiority
				: Row(EUnitOrder.AirIntercept);
		}
		if (Has(unit, SeaAttack))
		{
			int port = Row(EUnitOrder.PortStrike);
			int naval = Row(EUnitOrder.NavalStrike);
			if (port >= 0 && province.Owner is not null && province.PortSea != 0
				&& CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) > CFixedPoint.Zero)
			{
				ticked = port;
			}
			else if (naval >= 0 && province.IsSea)
			{
				ticked = naval;
			}
		}
		if (ticked < 0 && tactical < strategic && CMovementRules.IsEnemy(state, province.Controller, country.Tag))
		{
			int runway = Row(EUnitOrder.RunwayCratering);
			int installation = Row(EUnitOrder.InstallationStrike);
			int bombing = Row(EUnitOrder.StrategicBombing);
			if (runway >= 0 && CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) > CFixedPoint.Zero)
			{
				ticked = runway;
			}
			else if (installation >= 0 && HasInstallation(state, province))
			{
				ticked = installation;
			}
			else if (bombing >= 0 && province.Modifier[Sim.Economy.CIndustrialCapacity.ProvinceIc] > CFixedPoint.Zero)
			{
				ticked = bombing;
			}
		}
		if (ticked < 0 && (Has(unit, SoftAttack) || Has(unit, HardAttack)))
		{
			int ground = Row(EUnitOrder.GroundAttack);
			ticked = ground >= 0 ? ground : Row(EUnitOrder.Interdiction);
		}
		return ticked;
	}

	/// <summary>The three buildings the original's ladder takes for an installation.</summary>
	private static readonly string[] Installations = { "radar_station", "nuclear_reactor", "rocket_test" };

	private static bool HasInstallation(CGameState state, CProvince province)
	{
		foreach (string key in Installations)
		{
			if (CMovementRules.BuildingLevel(state, province, key) > CFixedPoint.Zero)
			{
				return true;
			}
		}
		return false;
	}

	private static bool Has(CUnit unit, string statistic)
	{
		foreach (CSubUnit wing in unit.SubUnits)
		{
			if (wing.Stats[statistic] > CFixedPoint.Zero)
			{
				return true;
			}
		}
		return false;
	}
}
