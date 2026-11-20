using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Multiplayer;

namespace OpenHOI3.Multiplayer;

/// <summary>
/// A client's part in a game between several machines (<c>docs/multiplayer.md</c>, section 6): its connection to the
/// host, the taking of each game the host sends, and the <see cref="CClientSession"/> of the game now played. It asks
/// to be let in with its play checksum and says what country its player would play; whenever the host offers a game
/// it lets go of the one it plays, takes that one and says it is to be loaded.
/// </summary>
public sealed class CNetClient : CNetGame
{
	private CSimulation? _simulation;

	private readonly CLineLink _link;
	private readonly CGameReceiver _receiver;
	private readonly List<string> _waiting = new();
	private IReadOnlyList<CCountryTag> _played = System.Array.Empty<CCountryTag>();
	private CClientSession? _session;

	/// <summary>Whether a game has been offered and is not yet here whole.</summary>
	private bool _taking;

	public CNetClient(CLineLink link, string checksum, string name, CCountryTag? wanted)
	{
		_link = link;
		_name = name;
		_receiver = new CGameReceiver(link.Send, OwnGame);
		link.Send(CClientSession.JoinLine(checksum, name));
		// With no country named the host says how its lobby stands, or who plays its running game, and the country is
		// chosen on this machine's lobby.
		if (wanted is { } named)
		{
			link.Send(CWire.Line(CWire.Wanted, named.Value));
		}
	}

	public override bool IsHost => false;

	/// <summary>The name this machine's player gave.</summary>
	private readonly string _name;

	public override string PlayerName => _name;

	public override void SayInPlay(int channel, string text)
	{
		_link.Send(CWire.Line(CWire.Chat, $"c {channel} {text}"));
	}

	public override void Whisper(string to, string text)
	{
		_link.Send(CWire.Line(CWire.Chat, $"w {System.Uri.EscapeDataString(to)} {text}"));
	}

	/// <summary>A line of the chat in play from the host (CWire.PlayChatKinds): taken into this machine's chat.</summary>
	private void TakePlayChat(string rest)
	{
		string[] parts = rest.Split(' ', 2);
		if (parts.Length < 2)
		{
			return;
		}
		switch (parts[0])
		{
			case "p":
			{
				string[] line = parts[1].Split(' ', 3);
				if (line.Length == 3 && int.TryParse(line[0], System.Globalization.NumberStyles.Integer,
					System.Globalization.CultureInfo.InvariantCulture, out int channel))
				{
					TakeChat(Interface.Chat.EChatLineKind.Line, channel, System.Uri.UnescapeDataString(line[1]), line[2]);
				}
				break;
			}
			case "i" or "o":
			{
				string[] line = parts[1].Split(' ', 2);
				if (line.Length == 2)
				{
					TakeChat(parts[0] == "i" ? Interface.Chat.EChatLineKind.WhisperIn : Interface.Chat.EChatLineKind.WhisperOut, 0,
						System.Uri.UnescapeDataString(line[0]), line[1]);
				}
				break;
			}
			case "s":
			{
				string[] line = parts[1].Split(' ', 3);
				if (line.Length == 3 && int.TryParse(line[0], System.Globalization.NumberStyles.Integer,
					System.Globalization.CultureInfo.InvariantCulture, out int channel))
				{
					TakeChat(Interface.Chat.EChatLineKind.System, channel, System.Uri.UnescapeDataString(line[2]), line[1]);
				}
				break;
			}
		}
	}

	private bool _primary = true;

	public override bool Primary => _primary;

	/// <summary>How many midnights of the game now played this machine's hash has been the host's.</summary>
	public int HashesAlike => _session?.HashesAlike ?? 0;

	/// <summary>Whether the host has let this machine in.</summary>
	public bool Admitted { get; private set; }

	/// <summary>How many times the host has said how its lobby stands; nought while it has said nothing of one.</summary>
	public int LobbyVersion { get; private set; }

	/// <summary>The place of the bookmark the host has chosen in its lobby; -1 for none or a save.</summary>
	public int LobbyBookmark { get; private set; } = -1;

	private bool _viewNext;
	private bool _takingView;

	/// <summary>The game the host has sent to be looked at in the lobby, as a save's bytes - the save it chose in its own lobby, or the game that runs; null where none has come.</summary>
	public byte[]? LobbyGame { get; private set; }

	/// <summary>How many such games have come: raised with each.</summary>
	public int LobbyGameVersion { get; private set; }

	/// <summary>The players of the host's lobby, the host first: each one's name and the country chosen, if any.</summary>
	public IReadOnlyList<(string Name, CCountryTag? Country, bool Secondary)> LobbyPlayers { get; private set; } = System.Array.Empty<(string, CCountryTag?, bool)>();

	/// <summary>This machine's player's choice of a country in the host's lobby, sent to the host, who gives it where nobody has it.</summary>
	public void Pick(CCountryTag country)
	{
		_link.Send(CWire.Line(CWire.Wanted, country.Value));
	}

	private readonly List<CLobbyChatLine> _chat = new();

	/// <summary>The lobby's chat lines that have come since they were last taken.</summary>
	public List<CLobbyChatLine> TakeChat()
	{
		var taken = new List<CLobbyChatLine>(_chat);
		_chat.Clear();
		return taken;
	}

	/// <summary>What this machine's player typed in the lobby's chat, sent to the host, who tells every machine - this one too.</summary>
	public void SendChat(string text)
	{
		_link.Send(CWire.Line(CWire.Chat, text));
	}

	public override string Status
	{
		get
		{
			if (_taking)
			{
				return $"Receiving the game from the host: {_receiver.Percent}%";
			}
			if (_session is { Parted: true })
			{
				return "Out of step with the host: waiting to be sent the game";
			}
			return _session is { WaitingFor.Count: > 0 } session ? "Waiting for " + string.Join(", ", session.WaitingFor) : "";
		}
	}

	public override void Pump()
	{
		// While a game is played its lines are the frame's, which takes them when the game is its to read.
		if (_session is null)
		{
			Drain();
		}
	}

	public override void Bind(CSimulation simulation)
	{
		_simulation = simulation;
		_hasPlayed = true;
		_session = new CClientSession(simulation, _link.Send);
		// Before anything else this machine says of the game: the host takes nothing as that game's play until it has come.
		_receiver.SayLoaded();
		foreach (string line in _waiting)
		{
			_session.Receive(line);
		}
		_waiting.Clear();
		Loaded();
	}

	public override int Frame()
	{
		// Before the lines are taken: the host's answer to a parted game is another game, which lets go of this one.
		DumpWhereParted();
		Drain();
		// What came of the orders given between hours runs here, at once; the hours are started after the frame is drawn.
		_session?.Step(mostHours: 0);
		DumpWhereParted();
		return 0;
	}

	private long _dumpedHour = -1;

	/// <summary>A game that has parted written once, every figure's hash as it stood at that hour, for setting beside the host's.</summary>
	private void DumpWhereParted()
	{
		if (_session is not { PartedFields: { } fields } session || session.PartedHour == _dumpedHour)
		{
			return;
		}
		_dumpedHour = session.PartedHour;
		string? path = Dump($"openhoi3_parted_{session.PartedHour}_client_{System.Environment.ProcessId}.txt", fields);
		Note($"out of step with the host at hour {session.PartedHour}; this machine's figures: {path ?? "not written"}");
	}

	public override void StartHours()
	{
		if (_session is not { HourReady: true } session || _simulation is not { IsBusy: false } simulation)
		{
			return;
		}
		// While they run no line is taken - that is the frame's, which holds off - so what they read stands still. As a
		// game of one's at the top speed, they run one after another for a while: a machine behind catches up so.
		simulation.BeginFollowing(() =>
		{
			var spent = Stopwatch.StartNew();
			return session.Step(mostHours: int.MaxValue, () => spent.Elapsed.TotalSeconds < CSimulation.BackToBackSeconds);
		});
	}

	private void Drain()
	{
		while (_link.TryReceive(out string line))
		{
			Take(line);
		}
		if (_link.Closed && Ended is null)
		{
			EndedKey = Admitted ? "CONNECTION_FAILED" : "GAME_NOT_FOUND";
			Ended = Admitted ? "The host has left the game." : "The host could not be reached.";
		}
	}

	private void Take(string line)
	{
		if (line.Length == 0)
		{
			return;
		}
		string rest = line.Length > 2 ? line[2..] : "";
		switch (line[0])
		{
			case CWire.Accepted:
				Admitted = true;
				return;
			case CWire.Refused:
				EndedKey = rest == "country" ? "HOTJOIN_DECLINED" : rest == "kicked" ? "YOUKICKED" : rest == "banned" ? "BANNED" : "BAD_VERSION";
				Ended = rest == "country" ? "The host refused the country asked for: it is played already, or owns no land."
					: rest == "kicked" ? "The host has thrown this machine out of the lobby."
					: rest == "banned" ? "The host has banned this machine from its game."
					: "The host plays other files than this machine: its play checksum differs.";
				return;
			case CWire.View:
				// The game next offered is to be looked at in the lobby, not played; a save the host chose comes with
				// its file's name, the game that runs with none.
				_viewNext = true;
				_viewName = System.Uri.UnescapeDataString(rest.Trim());
				return;
			case CWire.Chat:
				if (CLobbyChatLine.FromWire(rest) is { } said)
				{
					_chat.Add(said);
				}
				else
				{
					TakePlayChat(rest);
				}
				return;
			case CWire.Users:
				PlayUsers = ReadUsers(rest);
				PlayUsersVersion++;
				return;
			case CWire.Options:
			{
				// The host's Options panel: the difficulty, the game mode and the five areas.
				var values = new List<int>();
				foreach (string part in rest.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
				{
					if (int.TryParse(part, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
					{
						values.Add(value);
					}
				}
				LobbyOptions = values;
				LobbyOptionsVersion++;
				return;
			}
			case CWire.Played:
				_played = rest.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(tag => new CCountryTag(tag)).ToList();
				return;
			case CWire.Assigned:
			{
				// The tag, then whether this machine's player is that country's primary: 0 where another chose it first.
				string[] parts = rest.Split(' ');
				Country = new CCountryTag(parts[0]);
				_primary = parts.Length < 2 || parts[1] != "0";
				_session?.Receive(CWire.Line(CWire.Assigned, parts[0]));
				return;
			}
			case CWire.Lobby:
			{
				// The host's lobby as it stands: the bookmark's place, then the players.
				int space = rest.IndexOf(' ');
				string bookmark = space < 0 ? rest : rest[..space];
				LobbyBookmark = int.TryParse(bookmark, System.Globalization.NumberStyles.Integer,
					System.Globalization.CultureInfo.InvariantCulture, out int place) ? place : -1;
				var players = new List<(string, CCountryTag?, bool)>();
				foreach (string player in (space < 0 ? "" : rest[(space + 1)..]).Split('&', System.StringSplitOptions.RemoveEmptyEntries))
				{
					int equals = player.LastIndexOf('=');
					string tag = equals < 0 ? "" : player[(equals + 1)..];
					// An asterisk after the tag marks a player who shares the country and was not the first to choose it.
					bool secondary = tag.EndsWith("*");
					tag = tag.TrimEnd('*');
					players.Add((System.Uri.UnescapeDataString(equals < 0 ? player : player[..equals]),
						tag.Length > 0 ? new CCountryTag(tag) : null, secondary));
				}
				LobbyPlayers = players;
				LobbyVersion++;
				return;
			}
		}
		if (_receiver.Receive(line))
		{
			if (line[0] == CWire.GameOffered)
			{
				// A lobby's chat says a save is on its way, and then how far it has come (SayProgress).
				_quartersSaid = 0;
				if (!_hasPlayed)
				{
					// This machine's own copy is loaded, or the save is on its way.
					_chat.Add(new CLobbyChatLine(_receiver.Kept ? "LOADING_LOCAL_SAVE" : "TRANSERING_SAVE", "", ""));
				}
				if (_receiver.Kept)
				{
					Note(_viewNext ? "the save the host chose is among this machine's own: nothing was sent"
						: "the game the host offers is this machine's own as it stands: nothing was sent");
				}
				if (_viewNext)
				{
					// One to look at in the lobby: nothing that is played is let go for it.
					_viewNext = false;
					_takingView = true;
				}
				else
				{
					// The game played is over for this machine: what comes from here on is of the one offered.
					_takingView = false;
					_session = null;
					_simulation = null;
					_waiting.Clear();
					_taking = true;
				}
			}
			SayProgress();
			if (_receiver.Failed)
			{
				EndedKey = "CONNECTION_FAILED";
				Ended = "The game the host sent did not come whole.";
			}
			else if (_taking && _receiver.Game is { } game)
			{
				_taking = false;
				SayGameToLoad(game, _played);
			}
			else if (_takingView && _receiver.Game is { } shown)
			{
				_takingView = false;
				LobbyGame = shown;
				LobbyGameVersion++;
				KeepView(shown);
				// The host is told, whose chat says when every machine has the save.
				_link.Send(CWire.Line(CWire.ViewTaken));
			}
			return;
		}
		if (_session is { } session)
		{
			session.Receive(line);
		}
		else if (!_taking)
		{
			// Of the game being loaded: kept until its session is made.
			_waiting.Add(line);
		}
	}

	/// <summary>The file name of the save next offered to be looked at, as the host has it; empty for the game that runs.</summary>
	private string _viewName = "";

	/// <summary>
	/// The bytes of the game the host offers, where this machine can make or find them itself, so that nothing need
	/// be sent (docs/multiplayer.md, section 14). **A game to be played, offered while this machine is in step with
	/// the host**: every hour and order that has come is run, and the game written as the host writes its own - the
	/// two are then the same bytes, which the hash says. **A save the host chose in its lobby**: a file of this
	/// machine's saved games of that length and hash. Null where there is none; what is offered is then sent.
	/// </summary>
	private byte[]? OwnGame(string hash)
	{
		try
		{
			if (_viewNext)
			{
				if (_viewName.Length == 0 || SaveFolder is null || !System.IO.Directory.Exists(SaveFolder))
				{
					return null;
				}
				foreach (string file in System.IO.Directory.EnumerateFiles(SaveFolder, "*.hoi3"))
				{
					if (new System.IO.FileInfo(file).Length == _receiver.Total)
					{
						byte[] bytes = System.IO.File.ReadAllBytes(file);
						if (CGameSender.Hash(bytes) == hash)
						{
							return bytes;
						}
					}
				}
				return null;
			}
			// Joining from the lobby of a game that runs: the game it was shown there to choose a country on is the
			// game it now joins, the host having run no hour meanwhile - unless an order was given since, which the
			// hash says.
			if (_session is null && !_hasPlayed && LobbyGame is { } shown && shown.Length == _receiver.Total)
			{
				return shown;
			}
			if (_session is not { Parted: false } session || _simulation is not { IsBusy: false } simulation || SaveNow is not { } save)
			{
				return null;
			}
			// The host wrote its game with every hour and order run; so here, with all that has come of them.
			session.Step(mostHours: int.MaxValue);
			if (session.Parted)
			{
				return null;
			}
			simulation.Pump();
			byte[] own = save();
			return own.Length == _receiver.Total ? own : null;
		}
		catch (System.Exception failed) when (failed is System.IO.IOException or System.UnauthorizedAccessException
			or System.InvalidOperationException)
		{
			Note($"this machine's own copy of the game offered could not be made: {failed.Message}");
			return null;
		}
	}

	/// <summary>
	/// A save the host chose in its lobby, sent to this machine, kept among its saved games under the host's name
	/// for it with <c>Multiplayer_</c> before - so that it is here the next time the host offers it, and a file of
	/// the player's own of that name is left alone. The original writes it under the host's own name.
	/// </summary>
	private void KeepView(byte[] game)
	{
		if (_receiver.Kept || _viewName.Length == 0 || SaveFolder is null)
		{
			return;
		}
		try
		{
			string name = System.IO.Path.GetFileName(_viewName);
			if (!name.StartsWith("Multiplayer_", System.StringComparison.OrdinalIgnoreCase))
			{
				name = "Multiplayer_" + name;
			}
			System.IO.Directory.CreateDirectory(SaveFolder);
			System.IO.File.WriteAllBytes(System.IO.Path.Combine(SaveFolder, name), game);
			Note($"the save the host chose kept as {name}");
		}
		catch (System.Exception failed) when (failed is System.IO.IOException or System.UnauthorizedAccessException
			or System.ArgumentException)
		{
			Note($"the save the host chose could not be kept: {failed.Message}");
		}
	}

	/// <summary>Whether a game has been played over this connection: from then on there is no lobby whose chat could say anything.</summary>
	private bool _hasPlayed;

	/// <summary>How many quarters of the game now coming the lobby's chat has said.</summary>
	private int _quartersSaid;

	/// <summary>
	/// How much of a game the host is sending is here, in whole percent - one to be played, or one to look at in the
	/// lobby; null where none is on its way.
	/// </summary>
	public int? Progress => _taking || _takingView ? _receiver.Percent : null;

	/// <summary>
	/// The lobby's chat told how far the game that is coming has come: SAVE_PROG at each quarter, and SAVE_TRANSFER_DONE
	/// once it is here whole - in the chat by request, where the original writes the figure on the players' rows
	/// (docs/multiplayer.md, section 13).
	/// </summary>
	private void SayProgress()
	{
		if (_hasPlayed || _receiver.Failed || _receiver.Kept || _receiver.Total <= 0)
		{
			return;
		}
		int quarters = _receiver.Percent / 25;
		while (_quartersSaid < quarters)
		{
			_quartersSaid++;
			_chat.Add(new CLobbyChatLine("SAVE_PROG", "", (_quartersSaid * 25).ToString(System.Globalization.CultureInfo.InvariantCulture)));
			if (_quartersSaid == 4)
			{
				_chat.Add(new CLobbyChatLine("SAVE_TRANSFER_DONE", "", ""));
			}
		}
	}

	/// <summary>The host's Options panel as it last said it - the difficulty, the game mode and the five areas; null where it has said none.</summary>
	public IReadOnlyList<int>? LobbyOptions { get; private set; }

	/// <summary>How many times the host has said its options.</summary>
	public int LobbyOptionsVersion { get; private set; }

	public override void Dispose()
	{
		_link.Dispose();
	}
}
