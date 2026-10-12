namespace OpenHOI3.Lua.Native;

/// <summary>What a function pushed to Lua carries to find its way back: its state and itself.</summary>
internal sealed record CLuaBinding(CLuaState State, CLuaFunction Function);
