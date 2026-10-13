using OpenHOI3.Lua.Native;
using OpenHOI3.Sim;

namespace OpenHOI3.Lua.Api;

/// <summary><c>CEU3Date</c>: the year, and the month counted from 0.</summary>
public static class CDateClass
{
	private const string Name = "CEU3Date";

	public static CLuaClass Define()
	{
		return new CLuaClass(Name)
			.Method("GetYear", call => call.Return(call.Object<CDate>(1, Name).Year))
			.Method("GetMonthOfYear", call => call.Return(call.Object<CDate>(1, Name).Month - 1));
	}
}
