namespace OpenHOI3.Lua.Native;

/// <summary>
/// An object of ours handed to a script as an object of <paramref name="Class"/>. Pushing one object
/// twice gives the script the same value both times, so <c>==</c> between two is true exactly when
/// they are one object; a value type is a new value each time it is pushed.
/// </summary>
public readonly record struct CLuaObject(CLuaClass Class, object Target);
