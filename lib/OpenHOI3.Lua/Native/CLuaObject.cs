namespace OpenHOI3.Lua.Native;

/// <summary>
/// An object of ours handed to a script as an object of <paramref name="Class"/>. Each push is a new
/// userdata, as luabind makes one, so <c>==</c> between two pushes of one object is the class's own
/// operator - or the error that it has none. See <c>docs/mechanics/lua.md</c>, section 4.
/// </summary>
public readonly record struct CLuaObject(CLuaClass Class, object Target);
