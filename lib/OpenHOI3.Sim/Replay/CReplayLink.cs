namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A place in a game's recording: the recording by its name and the save's mark in it (<see cref="CReplaySaved"/>).
/// What a save holds of its recording, and what a recording begun from a loaded save names as its parent
/// (<c>docs/replay.md</c>, section 10).
/// </summary>
public sealed record CReplayLink(string Segment, int Mark)
{
	/// <summary>As one word: the recording's name, a colon, the mark.</summary>
	public string Text => Segment + ":" + Mark.ToString(System.Globalization.CultureInfo.InvariantCulture);

	public static CReplayLink? Parse(string? text)
	{
		if (text is null)
		{
			return null;
		}
		int colon = text.LastIndexOf(':');
		return colon > 0 && int.TryParse(text[(colon + 1)..], System.Globalization.NumberStyles.None,
			System.Globalization.CultureInfo.InvariantCulture, out int mark) && IsName(text[..colon])
			? new CReplayLink(text[..colon], mark)
			: null;
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
}
