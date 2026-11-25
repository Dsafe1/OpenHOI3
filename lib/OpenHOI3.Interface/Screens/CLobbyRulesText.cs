using System;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the lobby's window of rules says of an enhanced game's host's rule on other files (<c>docs/multiplayer.md</c>,
/// section 15): the keys a mod gives texts for, and the words in their place where its tables have none.
/// </summary>
public static class CLobbyRulesText
{
	public const string TitleKey = "OPENHOI_RULES_TITLE";
	public const string OtherFilesKey = "OPENHOI_RULE_OTHER_FILES";
	public const string OtherFilesTipKey = "OPENHOI_RULE_OTHER_FILES_TIP";

	public const string ReplayKey = "OPENHOI_RULE_RECORD_REPLAY";
	public const string ReplayTipKey = "OPENHOI_RULE_RECORD_REPLAY_TIP";

	public const string Title = "Game rules";
	public const string Replay = "Record a replay";

	public const string ReplayTip = "Records the game beside its saves, so that it can be played back from its start "
		+ "to any of its saves.\nA recorded game is saved and loaded once as it begins, and loaded twice when a save "
		+ "is played on. A recording plays back only on the engine and the files it was made with.";
	public const string OtherFiles = "Allow other files";

	public const string OtherFilesTip = "Lets a player in whose game files are not the host's, as long as the engine, "
		+ "the rules and the original checksum are the same.\nThe player is shown which files differ. Where they change "
		+ "how the game goes, the machines part and the game is sent again each midnight.";

	/// <summary>A key's text, or the words given where no table has the key.</summary>
	public static string Text(Func<string, string> localise, string key, string words)
	{
		string text = localise(key);
		return text.Length == 0 || text == key ? words : text;
	}
}
