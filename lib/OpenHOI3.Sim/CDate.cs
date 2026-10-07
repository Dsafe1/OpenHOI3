using System;
using System.Globalization;

namespace OpenHOI3.Sim;

/// <summary>
/// A date as the game data writes it: <c>1936.1.1</c>, year first and no padding.
///
/// The original carries no RTTI for its date type, so this name is taken from
/// Clausewitz convention rather than recovered from the binary.
/// </summary>
public readonly record struct CDate(int Year, int Month, int Day) : IComparable<CDate>
{
	public static readonly CDate Zero = new(0, 0, 0);

	public bool IsValid => Month is >= 1 and <= 12 && Day is >= 1 and <= 31;

	/// <summary>
	/// Parses <c>year.month.day</c>. A malformed date yields false rather than
	/// throwing, because one bad entry should not stop a whole file loading.
	/// </summary>
	public static bool TryParse(string? text, out CDate date)
	{
		date = Zero;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		string[] parts = text.Trim().Split('.');
		if (parts.Length != 3)
		{
			return false;
		}
		if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
				out int year)
			|| !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture,
				out int month)
			|| !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture,
				out int day))
		{
			return false;
		}

		date = new CDate(year, month, day);
		return date.IsValid;
	}

	/// <summary>Every year has this many days. The original has no leap years.</summary>
	public const int DaysPerYear = 365;

	private static readonly int[] DaysBeforeMonth =
	{
		0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334,
	};

	/// <summary>
	/// Days since 1 January of year 0, on the original's calendar of 365-day years.
	/// See <c>docs/mechanics/tick.md</c>, section 1. A day past the end of its month,
	/// which the data files do contain, counts on into the next one.
	/// </summary>
	public int DayNumber =>
		Year * DaysPerYear + DaysBeforeMonth[Math.Clamp(Month, 1, 12) - 1] + Math.Max(Day, 1) - 1;

	public static CDate FromDayNumber(int dayNumber)
	{
		int year = Math.DivRem(dayNumber, DaysPerYear, out int dayOfYear);
		if (dayOfYear < 0)
		{
			dayOfYear += DaysPerYear;
			year--;
		}
		int month = 12;
		while (DaysBeforeMonth[month - 1] > dayOfYear)
		{
			month--;
		}
		return new CDate(year, month, dayOfYear - DaysBeforeMonth[month - 1] + 1);
	}

	public CDate AddDays(int days)
	{
		return FromDayNumber(DayNumber + days);
	}

	public int CompareTo(CDate other)
	{
		int byYear = Year.CompareTo(other.Year);
		if (byYear != 0)
		{
			return byYear;
		}
		int byMonth = Month.CompareTo(other.Month);
		return byMonth != 0 ? byMonth : Day.CompareTo(other.Day);
	}

	/// <summary>
	/// Localisation key for the month. The game keys these on the English name rather
	/// than a number, so "January" resolves to "Januar" in a German install.
	/// </summary>
	public string MonthKey => Month is >= 1 and <= 12 ? MonthKeys[Month - 1] : "";

	private static readonly string[] MonthKeys =
	{
		"January", "February", "March", "April", "May", "June",
		"July", "August", "September", "October", "November", "December",
	};

	/// <summary>
	/// The form the interface uses: <c>13:00, 1 January, 1936</c>. A date carries no
	/// hour of its own, so the caller supplies one. The month name comes through
	/// <paramref name="localise"/> because the string table belongs to the content
	/// library, which this one does not reference.
	/// </summary>
	public string ToDisplayString(Func<string, string> localise, int hour = 0)
	{
		string month = MonthKey.Length == 0 ? "" : localise(MonthKey);
		return $"{hour}:00, {Day} {month}, {Year}";
	}

	/// <summary>The compact form the data files themselves use.</summary>
	public override string ToString()
	{
		return $"{Year}.{Month}.{Day}";
	}
}
