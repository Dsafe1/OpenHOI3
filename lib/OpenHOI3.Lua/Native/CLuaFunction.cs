namespace OpenHOI3.Lua.Native;

/// <summary>
/// A function Lua may call, written in C#: it reads its arguments from <paramref name="call"/>, pushes
/// its results and answers how many it pushed. To raise a Lua error it throws <see cref="CLuaError"/>.
/// </summary>
public delegate int CLuaFunction(CLuaCall call);
