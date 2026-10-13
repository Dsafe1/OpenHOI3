using System;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A class's <c>==</c>, as luabind registers one for a pair of its own objects: given anything else it
/// is the error the original raises, since no overload takes it. See <c>docs/mechanics/lua.md</c>,
/// section 4.
/// </summary>
public static class CLuaEquality
{
	/// <summary>Equal when the two are one object here - a name, a group - which is what the original's comparison amounts to.</summary>
	public static CLuaFunction Same<T>(string className) where T : class => By<T>(className, ReferenceEquals);

	/// <summary>Equal by the comparison given.</summary>
	public static CLuaFunction By<T>(string className, Func<T, T, bool> equal) => call =>
	{
		if (call.ObjectAt(1, out _) is T left && call.ObjectAt(2, out _) is T right)
		{
			return call.Return(equal(left, right));
		}
		throw new CLuaError($"No matching overload found, candidates: void __eq(lua_State*,{className} const&,{className} const&)");
	};
}
