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
		_receiver = new CGameReceiver(link.Send, _ => null);
		link.Send(CClientSession.JoinLine(checksum, name));
		// With no country named the host gives one: the lobby, where one is chosen, is not shared yet.
		link.Send(CWire.Line(CWire.Wanted, wanted?.Value ?? ""));
	}

	public override bool IsHost => false;

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

	/// <summary>The players of the host's lobby, the host first: each one's name and the country chosen, if any.</summary>
	public IReadOnlyList<(string Name, CCountryTag? Country)> LobbyPlayers { get; private set; } = System.Array.Empty<(string, CCountryTag?)>();

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
				return "Receiving the game from the host";
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
		Drain();
		// What came of the orders given between hours runs here, at once; the hours are started after the frame is drawn.
		_session?.Step(mostHours: 0);
		return 0;
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
				EndedKey = rest == "country" ? "HOTJOIN_DECLINED" : rest == "kicked" ? "YOUKICKED" : "BAD_VERSION";
				Ended = rest == "country" ? "The host refused the country asked for: it is played already, or owns no land."
					: rest == "kicked" ? "The host has thrown this machine out of the lobby."
					: "The host plays other files than this machine: its play checksum differs.";
				return;
			case CWire.Chat:
				if (CLobbyChatLine.FromWire(rest) is { } said)
				{
					_chat.Add(said);
				}
				return;
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
				var players = new List<(string, CCountryTag?)>();
				foreach (string player in (space < 0 ? "" : rest[(space + 1)..]).Split('&', System.StringSplitOptions.RemoveEmptyEntries))
				{
					int equals = player.LastIndexOf('=');
					string tag = equals < 0 ? "" : player[(equals + 1)..];
					players.Add((System.Uri.UnescapeDataString(equals < 0 ? player : player[..equals]),
						tag.Length > 0 ? new CCountryTag(tag) : null));
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
				// The game played is over for this machine: what comes from here on is of the one offered.
				_session = null;
				_simulation = null;
				_waiting.Clear();
				_taking = true;
			}
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

	public override void Dispose()
	{
		_link.Dispose();
	}
}
