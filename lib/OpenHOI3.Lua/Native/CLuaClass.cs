using System;
using System.Collections.Generic;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// A class the scripts are handed objects of - <c>CCountry</c>, <c>CCountryTag</c> - by the name the
/// original gives it, with the methods a script calls on an object with <c>:</c>. A method is
/// called with the object as its first argument.
/// </summary>
public sealed class CLuaClass
{
	public CLuaClass(string name)
	{
		Name = name;
	}

	public string Name { get; }

	public Dictionary<string, CLuaFunction> Methods { get; } = new(StringComparer.Ordinal);

	public CLuaClass Method(string name, CLuaFunction function)
	{
		Methods[name] = function;
		return this;
	}
}
