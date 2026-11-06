using System;
using System.Globalization;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Content.Saves;

/// <summary>
/// How a save writes its values (<c>docs/mechanics/savegame.md</c>, section 3): a figure in thousandths, three places
/// (`55.000`); a share or a research progress in fifteen binary places, five decimal ones (`0.32000`); a date with its
/// hour (`1936.1.7.22`); an id as <c>{ id = N type = T }</c>. A value that is not there or does not read is nought.
/// </summary>
public static class CSaveValues
{
	public static CFixedPoint Fixed(string? text)
	{
		return CFixedPoint.TryParse(text, out CFixedPoint value) ? value : CFixedPoint.Zero;
	}

	public static CFixedPoint Fixed(ClausewitzNode? node, string key)
	{
		return Fixed(node?.Get(key)?.Value);
	}

	/// <summary>A five-place figure as the fifteen binary places it was written from.</summary>
	public static CFixed15 Fixed15(string? text)
	{
		return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
			? new CFixed15((long)Math.Round(value * CFixed15.Scale))
			: CFixed15.Zero;
	}

	/// <summary>A fifteen-binary-place figure with five decimal places, as the original writes a share and a progress.</summary>
	public static string FiveText(CFixed15 value)
	{
		decimal shown = (decimal)value.Raw / CFixed15.Scale;
		return shown.ToString("0.00000", CultureInfo.InvariantCulture);
	}

	/// <summary>A whole number, read without the float the parser's own reading goes through.</summary>
	public static long Whole(string? text)
	{
		return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;
	}

	public static long Whole(ClausewitzNode? node, string key)
	{
		return Whole(node?.Get(key)?.Value);
	}

	/// <summary>A date with its hour, as hours since the calendar's start; a date with no hour is at its midnight.</summary>
	public static bool TryHours(string? text, out long hours)
	{
		hours = 0;
		if (text is null)
		{
			return false;
		}
		string[] parts = text.Trim('"').Split('.');
		if (parts.Length < 3 || !CDate.TryParse(string.Join('.', parts[0], parts[1], parts[2]), out CDate date))
		{
			return false;
		}
		int hour = parts.Length > 3 && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h) ? h : 0;
		hours = (long)date.DayNumber * CGameState.HoursPerDay + hour;
		return true;
	}

	/// <summary>The original's null date, which a save writes for "never".</summary>
	public const string NullDate = "1.1.1.0";

	/// <summary>A date with its hour as hours since the calendar's start; null for the null date, or none.</summary>
	public static long? HoursOrNull(string? text)
	{
		return text?.Trim('"') != NullDate && TryHours(text, out long hours) ? hours : null;
	}

	/// <summary>Hours since the calendar's start as a save writes them: <c>1936.1.7.22</c>.</summary>
	public static string HoursText(long hours)
	{
		long day = Math.DivRem(hours, CGameState.HoursPerDay, out long hour);
		if (hour < 0)
		{
			hour += CGameState.HoursPerDay;
			day--;
		}
		return CDate.FromDayNumber((int)day) + "." + hour.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The serial of an id block, <c>{ id = N type = T }</c>; nought where there is none.</summary>
	public static int Id(ClausewitzNode? node)
	{
		return (int)Whole(node?.Get("id")?.Value);
	}
}
