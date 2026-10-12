using System;

namespace OpenHOI3.Lua.Native;

/// <summary>
/// Thrown by a <see cref="CLuaFunction"/> to raise a Lua error with this message: the trampoline turns
/// it into one, so the script sees an ordinary <c>error</c> and a <c>pcall</c> in it can catch it.
/// </summary>
public sealed class CLuaError : Exception
{
	public CLuaError(string message) : base(message)
	{
	}
}
