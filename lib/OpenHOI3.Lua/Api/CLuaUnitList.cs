using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// A country's units as a script holds them - <c>country:GetUnits()</c>, a <c>CUnitList</c>: every unit it has at every
/// level, read as each method is asked, as the original's is a reference to the country's own list. See
/// <c>docs/mechanics/lua.md</c>, section 4.6.
/// </summary>
public sealed record CLuaUnitList(CCountry Country);
