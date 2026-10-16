namespace OpenHOI3.Sim.Ai;

/// <summary>
/// A strategy's personality, numbered as the original numbers it and the Lua's <c>_AI_*_</c> constants do; a savegame
/// writes them <c>military</c>, <c>capital</c>, <c>diplomacy</c> and <c>balanced</c>. See
/// <c>docs/mechanics/aistrategy.md</c>, section 3.
/// </summary>
public enum EAiPersonality
{
	/// <summary>A strategy never made.</summary>
	None = 0,

	Military = 1,

	/// <summary>The Lua's industrialist.</summary>
	Capital = 2,

	Diplomacy = 3,

	Balanced = 4,
}
