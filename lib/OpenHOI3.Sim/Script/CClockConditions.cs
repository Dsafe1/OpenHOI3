using System.Globalization;

namespace OpenHOI3.Sim.Script;

/// <summary>
/// The conditions on the clock - <c>date</c>, <c>year</c>, <c>month</c> - and <c>always</c>:
/// <c>docs/mechanics/triggers.md</c>, section 5.
/// </summary>
public static class CClockConditions
{
	public static bool TryAnswer(CGameState state, CTrigger trigger, CEventScope scope, out bool answer)
	{
		long wanted = CConditions.WholeOf(trigger.Argument.Amount);
		switch (trigger.Keyword)
		{
			case "date":
				answer = state.TotalHours + scope.LookAheadDays * (long)CGameState.HoursPerDay >= Hour(trigger.Argument.Text);
				return true;
			case "year":
				answer = state.Date.Year >= wanted;
				return true;
			case "month":
				// Counted from nought.
				answer = state.Date.Month - 1 >= wanted;
				return true;
			case "always":
				answer = trigger.Argument.IsYes;
				return true;
			default:
				answer = false;
				return false;
		}
	}

	/// <summary>
	/// The hour a date names, <c>year.month.day</c> and an hour after it if there is one. A date that
	/// does not read is before time began.
	/// </summary>
	public static long Hour(string text)
	{
		string[] parts = text.Split('.');
		if (parts.Length is < 3 or > 4)
		{
			return long.MinValue;
		}
		int[] numbers = new int[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
			{
				return long.MinValue;
			}
		}
		var date = new CDate(numbers[0], numbers[1], numbers[2]);
		if (!date.IsValid)
		{
			return long.MinValue;
		}
		int hour = parts.Length == 4 ? numbers[3] : 0;
		return date.DayNumber * (long)CGameState.HoursPerDay + hour;
	}
}
