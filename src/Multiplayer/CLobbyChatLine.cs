namespace OpenHOI3.Multiplayer;

/// <summary>
/// A line of the lobby's chat as it travels between machines (<c>docs/multiplayer.md</c>, section 7): what a player
/// typed, or what the lobby says of a player by a localisation key, which each machine writes in its own language.
/// </summary>
/// <param name="Key">The key of a line the lobby says - PLR_CONNECT, PLR_DISC, PLAYER_KICKED, PLAYER_BANNED; null for a typed line.</param>
/// <param name="Name">The player the line is by, or is about.</param>
/// <param name="Text">What was typed; empty for a line the lobby says.</param>
public sealed record CLobbyChatLine(string? Key, string Name, string Text)
{
	/// <summary>The line as it is sent: <c>t name text</c> or <c>k KEY name</c>, the name escaped as a URL's is.</summary>
	public string ToWire()
	{
		string name = System.Uri.EscapeDataString(Name);
		return Key is null ? "t " + name + " " + Text : "k " + Key + " " + name;
	}

	/// <summary>A line read back; null where it is not one.</summary>
	public static CLobbyChatLine? FromWire(string rest)
	{
		string[] parts = rest.Split(' ', 3);
		if (parts.Length < 2)
		{
			return null;
		}
		return parts[0] switch
		{
			"t" => new CLobbyChatLine(null, System.Uri.UnescapeDataString(parts[1]), parts.Length > 2 ? parts[2] : ""),
			"k" when parts.Length > 2 => new CLobbyChatLine(parts[1], System.Uri.UnescapeDataString(parts[2]), ""),
			_ => null,
		};
	}
}
