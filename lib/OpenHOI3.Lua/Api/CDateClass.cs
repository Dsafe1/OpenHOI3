using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;

namespace OpenHOI3.Lua.Api;

/// <summary><c>CEU3Date</c>: the year, the month and the day of the month each counted from 0, and the days.</summary>
public static class CDateClass
{
	private const string Name = "CEU3Date";

	public static CLuaClass Define()
	{
		return new CLuaClass(Name)
			.Method("GetYear", call => call.Return(call.Object<CDate>(1, Name).Year))
			.Method("GetMonthOfYear", call => call.Return(call.Object<CDate>(1, Name).Month - 1))
			// The day of the month counted from 0, as the month is (foreignminister.md, section 3).
			.Method("GetDayOfMonth", call => call.Return(call.Object<CDate>(1, Name).Day - 1))
			// Days from the calendar's start; ours its origin, which no script built reads.
			.Method("GetTotalDays", call => call.Return(call.Object<CDate>(1, Name).DayNumber));
	}
}
