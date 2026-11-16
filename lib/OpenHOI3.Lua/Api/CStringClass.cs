using System;
using OpenHOI3.Lua.Native;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// <c>CString</c>: its text with <c>tostring</c>, and <c>==</c> on the text; <c>CString(text)</c> makes one of a Lua string,
/// the original's one constructor, which takes a <c>char const*</c> and so nothing but a string.
/// </summary>
public static class CStringClass
{
	public const string Name = "CString";

	public static CLuaClass Define()
	{
		CLuaClass luaClass = new CLuaClass(Name)
			.Method("GetString", call => call.Return(call.Object<CLuaText>(1, Name).Value))
			.Operator("__tostring", call => call.Return(call.Object<CLuaText>(1, Name).Value))
			.Operator("__eq", CLuaEquality.By<CLuaText>(Name, (a, b) => string.Equals(a.Value, b.Value, StringComparison.Ordinal)));
		return luaClass.Constructs(call =>
		{
			if (call.ArgumentCount != 2 || call.TypeOf(2) != ELuaType.String)
			{
				throw new CLuaError("No matching overload found, candidates: void __init(luabind::argument const&,char const*)");
			}
			return call.Return(new CLuaObject(luaClass, new CLuaText(call.String(2))));
		});
	}
}
