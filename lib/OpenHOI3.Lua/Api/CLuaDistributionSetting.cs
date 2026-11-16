using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Lua.Api;

/// <summary>
/// One of a country's shares as a script holds it: the six of IC or the four of leadership, by the original's index -
/// <c>CDistributionSetting</c>. See <c>docs/mechanics/lua.md</c>, section 4.5.
/// </summary>
public sealed record CLuaDistributionSetting(CCountry Country, bool IsLeadership, int Index);
