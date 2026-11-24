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

	/// <summary>
	/// An enhanced game's rule, the lobby's choice: whether every country's figures are kept each day
	/// (<c>docs/mechanics/statisticsrecord.md</c>). On unless it is turned off; a faithful game keeps none either way.
	/// </summary>
	public bool CollectStatistics { get; set; } = true;

	/// <summary>
	/// A host's rule, the lobby's choice, of any game: whether a machine whose play checksum is not the host's is let in
	/// all the same, where its engine, its rules and the original's file checksum are the host's
	/// (<c>docs/multiplayer.md</c>, section 15). Off unless it is turned on, and in no save: it is the session's.
	/// </summary>
	public bool AllowOtherFiles { get; set; }

	/// <summary>Both set to another's.</summary>
	public void CopyFrom(CGamePlaySettings other)
	{
		Difficulty = other.Difficulty;
		GameMode = other.GameMode;
		CollectStatistics = other.CollectStatistics;
		AllowOtherFiles = other.AllowOtherFiles;
	}
}
