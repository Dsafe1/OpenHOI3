namespace OpenHOI3.Sim.Settings;

/// <summary>
/// The game's difficulty and game mode, the lobby's first two options, saved as <c>gameplaysettings</c>. See
/// <c>docs/mechanics/frontend.md</c>, section 4; <c>reversing/findings/FINDINGS-frontend.md</c>, C and D.
/// </summary>
public sealed class CGamePlaySettings
{
	/// <summary>Normal, the game state's own start.</summary>
	public const int NormalDifficulty = 2;

	/// <summary>The levels, very easy to very hard.</summary>
	public const int DifficultyCount = 5;

	/// <summary>0 very easy, 1 easy, 2 normal, 3 hard, 4 very hard.</summary>
	public int Difficulty { get; set; } = NormalDifficulty;

	/// <summary>0 Normal, 1 Arcade - which only supply reads, and Arcade's supply is not built.</summary>
	public int GameMode { get; set; }

	/// <summary>Both set to another's.</summary>
	public void CopyFrom(CGamePlaySettings other)
	{
		Difficulty = other.Difficulty;
		GameMode = other.GameMode;
	}
}
