namespace OpenHOI3.Interface.Chat;

/// <summary>What a line of the chat in play is (<c>docs/mechanics/chat.md</c>, section 4).</summary>
public enum EChatLineKind
{
	/// <summary>A player's line in a channel.</summary>
	Line,

	/// <summary>A line another player sent this one alone.</summary>
	WhisperIn,

	/// <summary>This player's own line to one other, as it comes back: "To NAME".</summary>
	WhisperOut,

	/// <summary>A line the chat itself says: its text is a localisation key, filled with the name.</summary>
	System,
}
