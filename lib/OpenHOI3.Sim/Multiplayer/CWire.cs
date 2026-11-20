using System.IO;
using System.Text;
using System.Text.Json;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// What two machines of one game say to each other, each message one line of text whose first letter is its kind
/// (<c>docs/multiplayer.md</c>, section 5). How the lines travel is no part of this: a session is handed each line that
/// came and a way to send one.
/// </summary>
public static class CWire
{
	/// <summary>Host to client: an entry of the host's recording, as a recording has it on disk.</summary>
	public const char Entry = 'e';

	/// <summary>Host to client: the state's hash as an hour began a day - the hour, then the hash.</summary>
	public const char Hash = 'h';

	/// <summary>
	/// Host to client: the hour is whole - every entry of it has been sent, and its hash where it turned a day. A client
	/// starts no hour before this has come for it: the hour's entries come one by one as the host runs it, and an hour
	/// begun on some of them would stop for want of the rest.
	/// </summary>
	public const char HourDone = 'd';

	/// <summary>Host to client: the host's clock - paused or not, and the speed.</summary>
	public const char Pace = 's';

	/// <summary>Host to client: who the game is waiting for, by their names, or nobody.</summary>
	public const char Waiting = 'w';

	/// <summary>Client to host: its player's order, as a recording writes a command.</summary>
	public const char Order = 'o';

	/// <summary>Client to host: it has run the game as far as this hour.</summary>
	public const char Confirmed = 'c';

	/// <summary>Client to host: its player pauses or resumes.</summary>
	public const char TogglePause = 'p';

	/// <summary>Client to host: its hash of this hour is not the host's.</summary>
	public const char Parted = 'x';

	/// <summary>Client to host, first of all: its play checksum, then its player's name.</summary>
	public const char Join = 'j';

	/// <summary>Host to client: it is let in.</summary>
	public const char Accepted = 'y';

	/// <summary>Host to client: it is not let in, and why - the host's play checksum where the two differ.</summary>
	public const char Refused = 'n';

	/// <summary>Host to client: the country its player now plays, by its tag.</summary>
	public const char Assigned = 'a';

	/// <summary>Host to client: a game is to be had - its hash, then its length in bytes.</summary>
	public const char GameOffered = 'g';

	/// <summary>Client to host: it holds that game already, and is sent nothing.</summary>
	public const char GameKept = 'k';

	/// <summary>Client to host: it has not got that game, and is to be sent it.</summary>
	public const char GameWanted = 't';

	/// <summary>Host to client: a piece of the game, base64.</summary>
	public const char GamePiece = 'b';

	/// <summary>Host to client: the game's last piece has been sent.</summary>
	public const char GameSent = 'G';

	/// <summary>Client to host: it has loaded the game and is ready to go on.</summary>
	public const char GameLoaded = 'r';

	/// <summary>Client to host, after it has asked to be let in: the country its player would play, by its tag.</summary>
	public const char Wanted = 'W';

	/// <summary>
	/// Host to client, before a game is offered: the countries played in it, by their tags with commas between, the
	/// host's first. A save names one player alone, and every machine has to load it with the same countries played.
	/// </summary>
	public const char Played = 'P';

	/// <summary>
	/// Host to client, while the host is in its lobby: the place of the bookmark it has chosen, then the players - each
	/// a name and the tag of the country chosen or nothing, the host's first - as <c>name=TAG</c> with <c>&amp;</c>
	/// between, the names escaped as a URL's are.
	/// </summary>
	public const char Lobby = 'l';

	/// <summary>
	/// A line of the lobby's chat. Client to host: what its player typed. Host to client: <c>t</c>, a name and what
	/// that player typed, or <c>k</c>, a localisation key and the name of the player it speaks of.
	/// </summary>
	public const char Chat = 'C';

	/// <summary>
	/// A line of the chat in play (<c>docs/mechanics/chat.md</c>, section 5), the kind <see cref="Chat"/> too, told
	/// from the lobby's by its first letter. Client to host: <c>c</c>, a channel's number and what its player typed,
	/// or <c>w</c>, a player's name and what was typed to him alone. Host to client: <c>p</c>, a channel, a name and
	/// the line; <c>i</c>, the name of a player who sent this one a line alone, and the line; <c>o</c>, such a line of
	/// this player's own come back, with the name it went to; <c>s</c>, a channel, a localisation key and the name it
	/// speaks of. Names are escaped as a URL's are.
	/// </summary>
	public const string PlayChatKinds = "cwpios";

	/// <summary>Host to client, in play: the players of the game, the host first, as <see cref="Lobby"/> writes them.</summary>
	public const char Users = 'u';

	/// <summary>
	/// Host to client, before a game is offered: that game is to be looked at in the lobby and not played - the save
	/// the host has chosen there, or the game that is running, for the map a country is chosen on.
	/// </summary>
	public const char View = 'V';

	/// <summary>Client to host: the game it was sent to look at in the lobby is here whole.</summary>
	public const char ViewTaken = 'v';

	/// <summary>
	/// Host to client, while the host is in its lobby: the Options panel's choices as numbers with spaces between -
	/// the difficulty, the game mode, then the five areas in the panel's order.
	/// </summary>
	public const char Options = 'O';

	public static string Line(char kind, string rest = "") => kind + " " + rest;

	/// <summary>A command as a recording writes one.</summary>
	public static string Write(CCommand command)
	{
		using var buffer = new MemoryStream();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			CCommandCodec.Write(writer, command);
		}
		return Encoding.UTF8.GetString(buffer.ToArray());
	}

	/// <summary>A command read against a game: it names that game's own countries and units.</summary>
	public static CCommand Read(string text, CGameState state)
	{
		using JsonDocument document = JsonDocument.Parse(text);
		return CCommandCodec.Read(document.RootElement, state);
	}
}
