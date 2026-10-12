using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// What the simulation asks of the install's Lua, which the original's AI is written in. The
/// simulation references nothing, so it states here what it asks and the Lua project answers it.
/// See <c>docs/mechanics/lua.md</c>.
/// </summary>
public interface IAiScripts
{
	/// <summary>
	/// <c>AI_GenerateNonHistoricalRelation(country, other)</c>: the relation a new game gives
	/// <paramref name="country"/> towards <paramref name="other"/> where nothing else has, in whole
	/// points - or null where the script has no such function or failed, which it reports.
	/// </summary>
	int? NonHistoricalRelation(CGameState state, CCountry country, CCountry other);
}
