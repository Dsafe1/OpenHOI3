using System;

namespace OpenHOI3.Lua.Native;

/// <summary>A chunk that did not load, or Lua code that raised an error, as C# sees it.</summary>
public sealed class CLuaException : Exception
{
	public CLuaException(string message) : base(message)
	{
	}
}
