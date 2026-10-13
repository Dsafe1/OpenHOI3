using System;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua.Api;

/// <summary><c>CString</c>: its text with <c>tostring</c>, and <c>==</c> on the text.</summary>
public static class CStringClass
{
	public static CLuaClass Define()
	{
		return new CLuaClass("CString")
			.Method("GetString", call => call.Return(call.Object<CLuaText>(1, "CString").Value))
			.Operator("__tostring", call => call.Return(call.Object<CLuaText>(1, "CString").Value))
			.Operator("__eq", CLuaEquality.By<CLuaText>("CString", (a, b) => string.Equals(a.Value, b.Value, StringComparison.Ordinal)));
	}
}
