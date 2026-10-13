namespace OpenHOI3.Lua.Native;

/// <summary>
/// What a function pushed to Lua carries to find its way back: its state and itself, and for a class's
/// method its name - <c>CCountry:GetTotalIC</c> - which the state's profile counts it under.
/// </summary>
internal sealed record CLuaBinding(CLuaState State, CLuaFunction Function, string? Name = null);
