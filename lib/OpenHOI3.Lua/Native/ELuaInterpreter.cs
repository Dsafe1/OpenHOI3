namespace OpenHOI3.Lua.Native;

/// <summary>Which interpreter runs the install's Lua (<see cref="CLuaInterpreter"/>; docs/mechanics/lua.md, section 1).</summary>
public enum ELuaInterpreter
{
	/// <summary>LuaJIT's interpreter, built from <c>native/luajit</c>: the default, about a fifth less time on the AI's scripts.</summary>
	LuaJit,

	/// <summary>The original's Lua 5.1.4, built from <c>native/lua</c>: a table's <c>pairs</c> in the original's order.</summary>
	Lua514,
}
