namespace OpenHOI3.Interface.Chat;

/// <summary>What a line typed into the chat's box comes to (<see cref="CChatLog.Read"/>).</summary>
public enum EChatTyped
{
	/// <summary>Nothing: an empty line.</summary>
	Nothing,

	/// <summary>A line to the current channel.</summary>
	Line,

	/// <summary>A line to one player.</summary>
	Whisper,

	/// <summary>Not sent: an error said in the current channel on this machine alone, by its key.</summary>
	Error,
}
