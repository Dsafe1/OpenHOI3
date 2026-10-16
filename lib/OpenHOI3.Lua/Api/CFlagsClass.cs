using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Script;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CFlags</c>, a country's flags as <c>CCountry:GetFlags</c> hands them: whether a flag is there and set, matched
/// without case as script matches it. See <c>docs/mechanics/foreignminister.md</c>, section 3.
/// </summary>
public static class CFlagsClass
{
	public const string Name = "CFlags";

	public static CLuaClass Define()
	{
		return new CLuaClass(Name)
			.Method("IsFlagSet", call => call.Return(call.Object<CFlags>(1, Name).IsSet(call.String(2))));
	}
}
