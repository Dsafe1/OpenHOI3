using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Settings;

/// <summary>
/// What the difficulty does, which is two things [exe]: a static modifier on every country - a played country's level's
/// <c>_player</c> block, any other's <c>_ai</c> block, none on normal - and a combat modifier on every unit of a played
/// country. See <c>docs/mechanics/frontend.md</c>, section 4; <c>reversing/findings/FINDINGS-frontend.md</c>, D.1.
/// </summary>
public static class CDifficulty
{
	public const string CombatImpact = "COMBAT_DIFFICULTY_IMPACT";

	private static readonly string[] Blocks = { "very_easy", "easy", "", "hard", "very_hard" };

	/// <summary>The combat bonus of each level before <see cref="CombatImpact"/>: +2, +1, 0, -1, -2 [exe].</summary>
	private static readonly long[] CombatSteps = { 2000, 1000, 0, -1000, -2000 };

	/// <summary>
	/// The static modifier's name for the country, <c>very_easy_player</c> to <c>very_hard_ai</c>; null on normal [mod:
	/// common/static_modifiers.txt for what is in them].
	/// </summary>
	public static string? BlockName(CGameState state, CCountry country)
	{
		int level = state.GamePlay.Difficulty;
		if (level < 0 || level >= Blocks.Length || Blocks[level].Length == 0)
		{
			return null;
		}
		return Blocks[level] + (state.Played.Contains(country.Tag) ? "_player" : "_ai");
	}

	/// <summary>
	/// The combat modifier of a unit of the country, thousandths: the level's step times <c>COMBAT_DIFFICULTY_IMPACT</c>
	/// [mod: common/defines.lua] for a played country, nothing for any other.
	/// </summary>
	public static long Combat(CGameState state, CCountry? country)
	{
		int level = state.GamePlay.Difficulty;
		if (country is null || !state.Played.Contains(country.Tag) || level < 0 || level >= CombatSteps.Length)
		{
			return 0;
		}
		return CombatSteps[level] * state.Defines[Economy.CManpower.MilitarySection, CombatImpact].Raw / CFixedPoint.Scale;
	}
}
