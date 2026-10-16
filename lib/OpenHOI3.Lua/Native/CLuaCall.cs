using System;
using System.Collections.Generic;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// One call of a <see cref="CLuaFunction"/>: its arguments, counted from 1, and the way to push its
/// results. A method's object is argument 1. Nothing here can raise a Lua error; a wrong argument
/// throws <see cref="CLuaError"/>, which the trampoline raises.
/// </summary>
public readonly struct CLuaCall
{
	internal CLuaCall(CLuaState lua, IntPtr pointer)
	{
		Lua = lua;
		Pointer = pointer;
	}

	public CLuaState Lua { get; }

	internal IntPtr Pointer { get; }

	public int ArgumentCount => CLuaNative.lua_gettop(Pointer);

	public ELuaType TypeOf(int argument) => (ELuaType)CLuaNative.lua_type(Pointer, argument);

	/// <summary>The argument as <see cref="CLuaState"/> turns any value.</summary>
	public object? Value(int argument) => Lua.ToValue(Pointer, argument);

	/// <summary>The argument, which has to be an object of ours of type <typeparamref name="T"/>.</summary>
	public T Object<T>(int argument, string expected)
	{
		if (Lua.ToObject(Pointer, argument, out _) is T target)
		{
			return target;
		}
		throw new CLuaError($"bad argument #{argument} ({expected} expected, got {Describe(argument)})");
	}

	/// <summary>The argument, an object of ours of any class, and its class; null where it is something else.</summary>
	public object? ObjectAt(int argument, out CLuaClass? luaClass) => Lua.ToObject(Pointer, argument, out luaClass);

	public double Number(int argument)
	{
		if (TypeOf(argument) == ELuaType.Number)
		{
			return CLuaNative.lua_tonumber(Pointer, argument);
		}
		throw new CLuaError($"bad argument #{argument} (number expected, got {Describe(argument)})");
	}

	/// <summary>A number argument as luabind hands it to a parameter of type <c>int</c>: <c>lua_tointeger</c>.</summary>
	public int Integer(int argument)
	{
		if (TypeOf(argument) == ELuaType.Number)
		{
			return CLuaNative.lua_tointeger(Pointer, argument);
		}
		throw new CLuaError($"bad argument #{argument} (number expected, got {Describe(argument)})");
	}

	/// <summary>An argument as Lua's truth: false only for nil, false and no value.</summary>
	public bool Boolean(int argument) => CLuaNative.lua_toboolean(Pointer, argument) != 0;

	public string String(int argument)
	{
		if (TypeOf(argument) is ELuaType.String or ELuaType.Number)
		{
			return CLuaState.ToString(Pointer, argument);
		}
		throw new CLuaError($"bad argument #{argument} (string expected, got {Describe(argument)})");
	}

	/// <summary>
	/// Loads a chunk without running it and pushes the function, or where it does not load pushes the
	/// message; answers whether it loaded. Loading raises no error, so a searcher may use it.
	/// </summary>
	public bool Load(byte[] chunk, string name) => Lua.Load(Pointer, chunk, name);

	/// <summary>Pushes one result and answers 1, for <c>return call.Return(x);</c>.</summary>
	public int Return(object? value)
	{
		Lua.Push(Pointer, value);
		return 1;
	}

	/// <summary>Pushes several results in order and answers how many, for a function with more than one result.</summary>
	public int ReturnMany(params object?[] values)
	{
		foreach (object? value in values)
		{
			Lua.Push(Pointer, value);
		}
		return values.Length;
	}

	/// <summary>
	/// Answers a function a generic <c>for</c> walks over these values, in this order: the original's
	/// iterator over a container. A null among them ends the walk there.
	/// </summary>
	public int ReturnIterator(IReadOnlyList<object?> values)
	{
		Lua.PushIterator(Pointer, values);
		return 1;
	}

	private string Describe(int argument)
	{
		if (Lua.ToObject(Pointer, argument, out CLuaClass? luaClass) is not null && luaClass is not null)
		{
			return luaClass.Name;
		}
		return TypeOf(argument) switch
		{
			ELuaType.None => "no value",
			ELuaType.Nil => "nil",
			ELuaType.Boolean => "boolean",
			ELuaType.LightUserdata or ELuaType.Userdata => "userdata",
			ELuaType.Number => "number",
			ELuaType.String => "string",
			ELuaType.Table => "table",
			ELuaType.Function => "function",
			_ => "thread",
		};
	}
}
