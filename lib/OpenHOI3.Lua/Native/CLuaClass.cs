using System;
using System.Collections.Generic;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// A class the scripts are handed objects of - <c>CCountry</c>, <c>CCountryTag</c> - by the name the
/// original gives it, with the methods a script calls on an object with <c>:</c>. A method is
/// called with the object as its first argument. The class is also a global of its name, holding its
/// static functions and its constants, and called to make one where it has a constructor. See
/// <c>docs/mechanics/lua.md</c>, section 4.
/// </summary>
public sealed class CLuaClass
{
	public CLuaClass(string name)
	{
		Name = name;
	}

	public string Name { get; }

	public Dictionary<string, CLuaFunction> Methods { get; } = new(StringComparer.Ordinal);

	/// <summary>
	/// Its operators, by their metamethod's name - <c>__eq</c>, <c>__lt</c>, <c>__le</c>, <c>__add</c>,
	/// <c>__sub</c>, <c>__mul</c>, <c>__div</c>, <c>__tostring</c> - each called with the two operands
	/// as Lua hands them, either of which may be the object.
	/// </summary>
	public Dictionary<string, CLuaFunction> Operators { get; } = new(StringComparer.Ordinal);

	/// <summary>Fields a script reads with <c>.</c>, each answered by a function of the object.</summary>
	public Dictionary<string, CLuaFunction> Fields { get; } = new(StringComparer.Ordinal);

	/// <summary>Functions of the class's global, called with <c>.</c>: <c>CCurrentGameState.GetFaction</c>.</summary>
	public Dictionary<string, CLuaFunction> Statics { get; } = new(StringComparer.Ordinal);

	/// <summary>Numbers of the class's global: <c>CGoodsPool._MONEY_</c>.</summary>
	public Dictionary<string, int> Constants { get; } = new(StringComparer.Ordinal);

	/// <summary>What calling the class's global does, its arguments from 2; none where it has no constructor.</summary>
	public CLuaFunction? Constructor { get; private set; }

	public CLuaClass Method(string name, CLuaFunction function)
	{
		Methods[name] = function;
		return this;
	}

	public CLuaClass Operator(string name, CLuaFunction function)
	{
		Operators[name] = function;
		return this;
	}

	public CLuaClass Field(string name, CLuaFunction function)
	{
		Fields[name] = function;
		return this;
	}

	public CLuaClass Static(string name, CLuaFunction function)
	{
		Statics[name] = function;
		return this;
	}

	public CLuaClass Constant(string name, int value)
	{
		Constants[name] = value;
		return this;
	}

	public CLuaClass Constructs(CLuaFunction function)
	{
		Constructor = function;
		return this;
	}
}
