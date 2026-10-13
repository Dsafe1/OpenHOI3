using System;
using OpenHOI3.Lua.Native;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CCountryTag</c>: the country, whether it is the null tag, its letters, <c>==</c> on the tag and
/// <c>tostring</c> as its letters - what <c>_G['AI_' .. tostring(tag)]</c> finds a country's script by.
/// </summary>
public static class CCountryTagClass
{
	private const string Name = "CCountryTag";

	public static CLuaClass Define(CGameApi api)
	{
		return new CLuaClass(Name)
			.Method("GetCountry", call => call.Return(api.OfTag(Of(call))))
			.Method("IsValid", call => call.Return(IsValid(Of(call))))
			.Method("GetTag", call => call.Return(Of(call).Value))
			.Operator("__eq", CLuaEquality.By<CCountryTag>(Name, (a, b) => a == b))
			.Operator("__tostring", call => call.Return(Of(call).Value));
	}

	/// <summary>Not the null tag.</summary>
	public static bool IsValid(CCountryTag tag) => !string.IsNullOrEmpty(tag.Value) && !string.Equals(tag.Value, CGameApi.NullTag.Value, StringComparison.Ordinal);

	private static CCountryTag Of(CLuaCall call) => call.Object<CCountryTag>(1, Name);
}
