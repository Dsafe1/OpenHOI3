using System;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What is said of a game's replay (<c>docs/replay.md</c>, section 11): the lobby's button that plays back the game a
/// loaded save came of, with its tooltips, the line over the loading screen where a playback loads a save as the player
/// did, and the notice at its end. The keys a mod gives texts for, and the words in their place where its tables have
/// none.
/// </summary>
public static class CReplayText
{
	public const string WatchKey = "OPENHOI_WATCH_REPLAY";
	public const string WatchTipKey = "OPENHOI_WATCH_REPLAY_TIP";
	public const string OtherEngineKey = "OPENHOI_WATCH_REPLAY_OTHER_ENGINE";
	public const string LoadingKey = "OPENHOI_REPLAY_LOADING";
	public const string EndedKey = "OPENHOI_REPLAY_ENDED";

	public const string Watch = "Watch replay";

	public const string WatchTip = "Plays back the game that led to this save, from its start, through every save and "
		+ "load made on the way. You look on: nothing you press changes the game. It is made of $COUNT$ recordings.";

	public const string OtherEngine = "This save's game was recorded with another engine or other rules ($MADE$) than "
		+ "this one ($NOW$). It may not play back as it was played: where the game goes another way the replay stops.";

	public const string Loading = "Replay: the game was saved and loaded here...";
	public const string Ended = "The replay has ended";

	public const string PartedKey = "OPENHOI_REPLAY_PARTED";
	public const string Parted = "The replay no longer fits this engine and stops here";

	/// <summary>A key's text, or the words given where no table has the key.</summary>
	public static string Text(Func<string, string> localise, string key, string words)
	{
		string text = localise(key);
		return text.Length == 0 || text == key ? words : text;
	}

	/// <summary>The button's tooltip: what it does where the recording fits this engine, and the warning where it was made with another.</summary>
	public static string Tooltip(Func<string, string> localise, int recordings, string? otherMade, string madeNow)
	{
		return otherMade is null
			? Text(localise, WatchTipKey, WatchTip).Replace("$COUNT$", recordings.ToString(System.Globalization.CultureInfo.InvariantCulture))
			: Text(localise, OtherEngineKey, OtherEngine).Replace("$MADE$", otherMade).Replace("$NOW$", madeNow);
	}
}
