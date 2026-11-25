namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A place in a game's recording: the recording by its name, and either a save's mark in it
/// (<see cref="CReplaySaved"/>) - where the game recorded was saved - or, for a save written from a game being played
/// back, how many of the recording's entries had been played when it was written. What a save holds of its recording,
/// and what a recording begun from a loaded save names as its parent (<c>docs/replay.md</c>, sections 10 and 13).
/// </summary>
/// <param name="Entries">The entries played, where the place is no save's mark; -1 where it is one.</param>
public sealed record CReplayLink(string Segment, int Mark, int Entries = -1)
{
	/// <summary>Whether the place is a count of entries played, and no mark of a save.</summary>
	public bool ByEntries => Entries >= 0;

	/// <summary>As one word: the recording's name, a colon, the mark - and after another colon the entries played, where it goes by them.</summary>
	public string Text => Segment + ":" + Whole(Mark) + (ByEntries ? ":" + Whole(Entries) : "");

	public static CReplayLink? Parse(string? text)
	{
		if (text is null)
		{
			return null;
		}
		string[] parts = text.Split(':');
		if (parts.Length is < 2 or > 3 || !IsName(parts[0]) || !Number(parts[1], out int mark))
		{
			return null;
		}
		if (parts.Length == 2)
		{
			return new CReplayLink(parts[0], mark);
		}
		return Number(parts[2], out int entries) ? new CReplayLink(parts[0], mark, entries) : null;
	}

	/// <summary>Whether a text may name a recording: letters and digits alone, since it is a file's name.</summary>
	public static bool IsName(string text)
	{
		if (text.Length == 0 || text.Length > 64)
		{
			return false;
		}
		foreach (char letter in text)
		{
			if (!(letter is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'))
			{
				return false;
			}
		}
		return true;
	}

	private static string Whole(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

	private static bool Number(string text, out int value) => int.TryParse(text, System.Globalization.NumberStyles.None,
		System.Globalization.CultureInfo.InvariantCulture, out value);
}
