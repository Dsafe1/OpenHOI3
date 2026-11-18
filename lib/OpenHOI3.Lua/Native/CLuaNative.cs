using System;
using System.Runtime.InteropServices;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// The parts of Lua 5.1.4's C API this project calls, and OpenHOI3's trampoline, in
/// <c>openhoi3_lua.dll</c> built from <c>native/lua</c>. <b>Only <see cref="lua_pcall"/> may run Lua code</b>:
/// a Lua error is a <c>longjmp</c>, and one through a .NET frame is undefined - so nothing here that
/// could raise is called outside a protected call, and a function written in C# reports its error to
/// the trampoline instead (<see cref="ohoi_pushfunction"/>). See <c>docs/mechanics/lua.md</c>, section 2.
///
/// <b>A call marked <c>[SuppressGCTransition]</c> must allocate nothing in Lua</b>: an allocation may run
/// the collector, the collector an object's <c>__gc</c>, and that is C# - which a call without the
/// transition must never re-enter. Reading a value, a type or a raw slot and pushing a number, a boolean
/// or nil allocate nothing; anything that makes a string, a table or a userdata, or sets a slot, may.
/// </summary>
internal static unsafe class CLuaNative
{
	private const string Library = CLuaLibrary.Name;

	public const int LUA_REGISTRYINDEX = -10000;
	public const int LUA_ENVIRONINDEX = -10001;
	public const int LUA_GLOBALSINDEX = -10002;
	public const int LUA_MULTRET = -1;

	public const int LUA_OK = 0;
	public const int LUA_YIELD = 1;
	public const int LUA_ERRRUN = 2;
	public const int LUA_ERRSYNTAX = 3;
	public const int LUA_ERRMEM = 4;
	public const int LUA_ERRERR = 5;

	public static int lua_upvalueindex(int i) => LUA_GLOBALSINDEX - i;

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr luaL_newstate();

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_close(IntPtr L);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern int luaL_loadbuffer(IntPtr L, byte* buffer, nuint size, byte* name);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern int lua_pcall(IntPtr L, int nargs, int nresults, int errfunc);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern int lua_gettop(IntPtr L);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_settop(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_pushvalue(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_remove(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_insert(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern int lua_type(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern int lua_rawequal(IntPtr L, int index1, int index2);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern double lua_tonumber(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern int lua_tointeger(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern int lua_toboolean(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern byte* lua_tolstring(IntPtr L, int index, nuint* length);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern IntPtr lua_touserdata(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_pushnil(IntPtr L);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_pushnumber(IntPtr L, double n);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_pushinteger(IntPtr L, int n);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_pushlstring(IntPtr L, byte* s, nuint length);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_pushboolean(IntPtr L, int b);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_pushlightuserdata(IntPtr L, IntPtr p);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_pushcclosure(IntPtr L, IntPtr fn, int n);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_rawget(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern void lua_rawgeti(IntPtr L, int index, int n);

	/// <summary>What a table is, as an address; the same table always answers the same.</summary>
	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern IntPtr lua_topointer(IntPtr L, int index);

	/// <summary>Keeps the value on top in the table at <paramref name="t"/>, popping it, and answers its key.</summary>
	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern int luaL_ref(IntPtr L, int t);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_rawset(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_rawseti(IntPtr L, int index, int n);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void lua_createtable(IntPtr L, int narr, int nrec);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern IntPtr lua_newuserdata(IntPtr L, nuint size);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	[SuppressGCTransition]
	public static extern int lua_getmetatable(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern int lua_setmetatable(IntPtr L, int index);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void ohoi_pushfunction(IntPtr L,
		delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> function, IntPtr context);

	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void ohoi_pushiterator(IntPtr L);

	/// <summary>Pushes the message handler that writes a Lua error's stack under it, for <see cref="lua_pcall"/>.</summary>
	[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
	public static extern void ohoi_pushtraceback(IntPtr L);
}
