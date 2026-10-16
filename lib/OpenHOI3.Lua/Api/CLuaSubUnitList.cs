using System.Collections.Generic;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// The original's <c>SubUnitList</c>, the brigades of a unit order as the production script appends them, in order. Nil
/// may be appended; the order made from it then refuses itself.
/// </summary>
public sealed class CLuaSubUnitList
{
	public List<CSubUnitType?> Types { get; } = new();
}
