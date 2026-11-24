using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Multiplayer;

namespace OpenHOI3.Multiplayer;

/// <summary>
/// The host's part in a game between several machines (<c>docs/multiplayer.md</c>, section 6): its open port, the
/// machines connected, and the <see cref="CHostSession"/> of the game now played. A machine is let in by its play
/// checksum and says what country it would play; the game is then saved and sent to every machine, the host loads it
/// as they do, and they play on - which is also what is done for a machine whose game has parted from the host's.
/// </summary>
public sealed class CNetHost : CNetGame
{
	private readonly CLineListener _listener;
	private readonly string _checksum;
	private readonly List<CNetPeer> _peers = new();
	private CHostSession? _session;

	/// <summary>Whether a machine has said its game is no longer the host's since a game was last sent.</summary>
	private bool _parted;

	/// <param name="checksum">The host's play checksum, which a machine has to share to be let in.</param>
	/// <param name="country">The country the host's own player plays.</param>
	/// <param name="banned">The addresses of the machines banned, kept by the caller across the games it hosts.</param>
	/// <param name="files">The list of the files the checksum is made of, sent to a machine whose checksum is another.</param>
	public CNetHost(int port, string checksum, CCountryTag? country, HashSet<string>? banned = null,
		IReadOnlyList<string>? files = null)
	{
		_listener = new CLineListener(port);
		_checksum = checksum;
		_files = files;
		_banned = banned ?? new HashSet<string>();
		Country = country;
	}

	/// <summary>
	/// The addresses of the machines the host has banned: one that connects from such an address is told so and let go
	/// before it is asked anything, in the lobby and in a game that runs (docs/multiplayer.md, section 13).
	/// </summary>
	private readonly HashSet<string> _banned;

	private readonly IReadOnlyList<string>? _files;

	/// <summary>
	/// The host's rule on other files, asked as each machine asks to be let in: whether one whose play checksum is not
	/// the host's is let in where its engine, rules and original checksum are (docs/multiplayer.md, section 15).
	/// </summary>
	public System.Func<bool>? AllowOtherFiles { get; set; }

	/// <summary>The Options panel's line as it stands, sent to every machine in the lobby that has not been sent it; null outside the lobby.</summary>
	private string? _options;

	/// <summary>The host's Options panel as it stands: the difficulty, the game mode and the five areas, as numbers.</summary>
	public void SetOptions(IEnumerable<int> values)
	{
		_options = string.Join(" ", values.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
	}

	/// <summary>Whether the host's own chat has been told the save now shown is being sent, and that every machine has it.</summary>
	private bool _viewStartSaid;
	private bool _viewDoneSaid;

	/// <summary>The country the host's own player plays, once the lobby's Play has said: a host made with its lobby has none till then.</summary>
	public void SetHostCountry(CCountryTag country)
	{
		Country = country;
		_lobby = null;
		_options = null;
	}

	/// <summary>Counts the choices of a country as they are made, the host's and the others'.</summary>
	private long _choices;

	/// <summary>When the host chose its own country.</summary>
	private long _hostChoseAt;

	/// <summary>The host's own player is its country's primary where no other player chose that country before it.</summary>
	public override bool Primary => !_peers.Any(peer => peer.Country == Country && peer.ChoseAt < _hostChoseAt);

	/// <summary>Whether a machine's player is the primary of the country it plays: nobody chose that country before it.</summary>
	private bool IsPrimary(CNetPeer peer)
	{
		return !(Country == peer.Country && _hostChoseAt < peer.ChoseAt)
			&& !_peers.Any(other => other != peer && other.Country == peer.Country && other.ChoseAt < peer.ChoseAt);
	}

	/// <summary>A machine told the country its player plays, and whether as its primary.</summary>
	private void SayAssigned(CNetPeer peer)
	{
		bool primary = IsPrimary(peer);
		peer.PrimarySent = primary;
		peer.Link.Send(CWire.Line(CWire.Assigned, peer.Country!.Value.Value + (primary ? " 1" : " 0")));
	}

	/// <summary>The lobby's line as it stands, sent to every machine let in that has not been sent it; null outside the lobby.</summary>
	private string? _lobby;

	/// <summary>
	/// The host's lobby as it stands (docs/multiplayer.md, section 7): the bookmark chosen and the host's own name and
	/// country. Every machine let in is told of it, and of the others' choices, whenever any of it changes.
	/// </summary>
	public void SetLobby(int bookmark, string hostName, CCountryTag? hostCountry)
	{
		if (hostCountry != Country)
		{
			_hostChoseAt = ++_choices;
		}
		Country = hostCountry;
		_lobbyHostName = hostName;
		_lobby = bookmark.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + string.Join("&",
			LobbyPlayers().Select(player => System.Uri.EscapeDataString(player.Name) + "=" + (player.Country?.Value ?? "") + (player.Secondary ? "*" : "")));
	}

	private string _lobbyHostName = "";

	/// <summary>The players of the lobby, the host first: each one's name and the country chosen, if any.</summary>
	public List<(string Name, CCountryTag? Country, bool Secondary)> LobbyPlayers()
	{
		// Of several players of one country every one but the first to have chosen it is marked.
		var players = new List<(string, CCountryTag?, bool)>
		{
			(_lobbyHostName, Country, Country is not null && _peers.Any(peer => peer.Wanted == Country && peer.ChoseAt < _hostChoseAt)),
		};
		players.AddRange(_peers.Where(peer => peer.Name is not null).Select(peer => (peer.Name!, peer.Wanted, peer.Wanted is not null
			&& ((Country == peer.Wanted && _hostChoseAt < peer.ChoseAt)
				|| _peers.Any(other => other != peer && other.Wanted == peer.Wanted && other.ChoseAt < peer.ChoseAt)))));
		return players;
	}

	/// <summary>The place of no bookmark that says a game is running: a machine joining chooses a country and joins with its Play.</summary>
	public const int RunningGame = -2;

	private byte[]? _view;
	private string _viewName = "";
	private string? _viewHash;

	/// <summary>
	/// The game the machines in the lobby are shown, as a save's bytes: the save the host has chosen in its lobby, or
	/// null for none - the start, by its bookmark - and, where a game runs, that game as it stands for a machine that
	/// would join it (docs/multiplayer.md, section 11). Sent where it is new to a machine.
	/// </summary>
	/// <param name="name">The file's name where it is a save the host chose, which a machine keeps it under; empty for the game that runs.</param>
	public void SetView(byte[]? game, string name = "")
	{
		if (game is null)
		{
			if (_view is not null)
			{
				_view = null;
				_viewHash = null;
				foreach (CNetPeer peer in _peers)
				{
					peer.ViewHash = null;
				}
			}
			return;
		}
		string hash = CGameSender.Hash(game);
		if (hash != _viewHash)
		{
			_view = game;
			_viewHash = hash;
			_viewName = name;
			_viewStartSaid = false;
			_viewDoneSaid = false;
		}
	}

	/// <summary>
	/// The machines in the lobby of the game that runs: let in, told of it, and not yet playing - choosing a country.
	/// While there is one no hour runs. None while the host is in its own lobby.
	/// </summary>
	private IEnumerable<CNetPeer> Choosing()
	{
		return _session is null ? Enumerable.Empty<CNetPeer>()
			: _peers.Where(peer => peer.Name is not null && peer.Country is null && !peer.Direct && peer.LobbySent is not null);
	}

	/// <summary>
	/// Whether a machine that would join the running game has not been shown it: the game is to be saved and given to
	/// <see cref="SetView"/>.
	/// </summary>
	public bool ViewWanted => _session is not null && _peers.Any(peer => peer.Name is not null && peer.Country is null
		&& !peer.Direct && peer.LobbySent is not null && peer.ViewHash is null && peer.Wanted is null);

	/// <summary>What a machine joining a running game is told in a lobby's line's form: no bookmark, and who plays what.</summary>
	private string RunningGameLine()
	{
		if (_lobbyHostName.Length == 0)
		{
			_lobbyHostName = "Host";
		}
		return RunningGame.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + string.Join("&",
			LobbyPlayers().Select(player => System.Uri.EscapeDataString(player.Name) + "=" + (player.Country?.Value ?? "")
				+ (player.Secondary ? "*" : "")));
	}

	/// <summary>Whether the lobby's game may not be started: a player has chosen no country.</summary>
	public bool LobbyBlocksPlay => _peers.Any(peer => peer.Name is not null && peer.Wanted is null);

	public override bool IsHost => true;

	public int Port => _listener.Port;

	public IReadOnlyList<CNetPeer> Peers => _peers;

	public override string Status
	{
		get
		{
			string[] loading = _peers.Where(peer => peer.Loading).Select(peer => (peer.Name ?? "") + SendingText(peer)).ToArray();
			if (loading.Length > 0)
			{
				return "Sending the game to " + string.Join(", ", loading);
			}
			string[] waiting = _session?.WaitingFor().Select(peer => peer.Name)
				.Concat(Choosing().Select(peer => peer.Name + " to choose a country")).ToArray() ?? System.Array.Empty<string>();
			return waiting.Length > 0 ? "Waiting for " + string.Join(", ", waiting) : "";
		}
	}

	/// <summary>
	/// Whether the game is to be saved and sent to every machine: one waits to join, or one's game has parted - and no
	/// sending is under way, which is let end first.
	/// </summary>
	public bool ResendWanted => _session is not null && !_peers.Any(peer => peer.Loading)
		&& (_parted || _peers.Any(peer => peer.WaitsToJoin));

	public override void Pump()
	{
		while (_listener.TryAccept(out CLineLink link))
		{
			// A banned machine is told so and let go, whatever it would say.
			if (_banned.Contains(link.RemoteAddress))
			{
				link.Send(CWire.Line(CWire.Refused, "banned"));
				link.Dispose();
				continue;
			}
			_peers.Add(new CNetPeer(link));
		}
		foreach (CNetPeer peer in _peers.ToList())
		{
			if (peer.Name is null)
			{
				// Its first line: let in by its play checksum, or told the host's and let go.
				if (peer.Link.TryReceive(out string first))
				{
					CAdmission admission = CHostSession.Admit(first, _checksum, peer.Link.Send,
						AllowOtherFiles?.Invoke() == true, _files);
					peer.Name = admission.Name;
					if (peer.Name is null)
					{
						Remove(peer);
						continue;
					}
					if (admission.OtherFiles)
					{
						// Said on every machine of the lobby, and in the host's log: a game with such a player may part.
						Godot.GD.Print($"Multiplayer: {peer.Name} is let in with other files than the host's, by the host's rule.");
						if (_lobby is not null)
						{
							Tell(new CLobbyChatLine(null, EngineName, $"{peer.Name} plays with other files than the host's."));
						}
					}
					// In the lobby every machine's chat says a player has joined.
					if (_lobby is not null)
					{
						SayOfPlayer("PLR_CONNECT", peer.Name);
					}
				}
			}
			else if (peer.Country is null)
			{
				while (peer.Link.TryReceive(out string line))
				{
					if (line.Length >= 2 && line[0] == CWire.Wanted && line[2..].Trim().Length == 0)
					{
						peer.AnyCountry = true;
					}
					else if (line.Length > 2 && line[0] == CWire.Wanted)
					{
						// A country another player has is not given: the choice stays as it was, which the lobby's line says.
						var asked = new CCountryTag(line[2..].Trim());
						// Any country, one another player has among them: players may share one, as the original lets them,
						// and the first to have chosen it is its primary (docs/multiplayer.md, section 8).
						if (asked != peer.Wanted)
						{
							peer.Wanted = asked;
							peer.ChoseAt = ++_choices;
						}
						// Named before it was told anything of a lobby: a machine started with its country given, which
						// chooses on no screen and is let go where it cannot have it.
						if (peer.LobbySent is null)
						{
							peer.Direct = true;
						}
					}
					else if (line.Length > 2 && line[0] == CWire.Chat && _lobby is not null)
					{
						SayTyped(peer.Name, line[2..]);
					}
					else if (line.Length > 0 && line[0] is CWire.GameKept or CWire.GameWanted)
					{
						// Its answer to a game offered it to look at.
						Answer(peer, peer.ViewSender, line);
					}
					else if (line.Length > 0 && line[0] == CWire.ViewTaken)
					{
						// The game it was last offered to look at is with it whole.
						peer.ViewTaken = peer.ViewHash;
					}
				}
				// The host's Options panel, to a machine in its lobby, whenever a choice there changes.
				if (_lobby is not null && _options is not null && peer.LobbySent is not null && peer.OptionsSent != _options)
				{
					peer.OptionsSent = _options;
					peer.Link.Send(CWire.Line(CWire.Options, _options));
				}
				// The lobby as it stands - or, where a game is running, the players of it under the place -2 of no
				// bookmark: a machine joining that game chooses its country on its own lobby's map and says so with its
				// Play (docs/multiplayer.md, section 10).
				string? lobby = _lobby ?? (_session is not null ? RunningGameLine() : null);
				if (lobby is not null && peer.LobbySent != lobby)
				{
					peer.LobbySent = lobby;
					peer.Link.Send(CWire.Line(CWire.Lobby, lobby));
				}
				// The game to look at in its lobby: the save the host has chosen in its own, again whenever that changes,
				// or the game that runs, once - the map a country is chosen on (docs/multiplayer.md, section 11).
				if (_view is not null && peer.LobbySent is not null && !peer.Direct
					&& (peer.ViewHash is null || (_lobby is not null && peer.ViewHash != _viewHash)))
				{
					peer.ViewHash = _viewHash;
					// The host's own chat says a save is on its way, once for each save chosen, as the original's does.
					if (_lobby is not null && !_viewStartSaid)
					{
						_viewStartSaid = true;
						_chat.Add(new CLobbyChatLine("TRANSERING_SAVE", "", ""));
					}
					peer.Link.Send(CWire.Line(CWire.View, System.Uri.EscapeDataString(_viewName)));
					peer.ViewSender = new CGameSender(peer.Link.Send, _view);
					peer.ViewSender.Offer();
				}
			}
			else if (peer.Sender is { } sender)
			{
				// What it says of the game offered; anything else it sent before it had that game is of the game before, and dropped.
				while (!sender.Loaded && peer.Link.TryReceive(out string line))
				{
					Answer(peer, sender, line);
				}
			}
			// One gone that plays no country is forgotten here; one that does is dropped by the frame, which may post - after
			// the host's own load where it went during that, its country being played in the game every machine loads.
			if (peer.Link.Closed && peer.Country is null)
			{
				Remove(peer);
			}
		}
		// And that every machine in the lobby has the save, once they all have said so.
		if (_lobby is not null && _viewStartSaid && !_viewDoneSaid
			&& _peers.Where(peer => peer.Name is not null && peer.Country is null && !peer.Direct).All(peer => peer.ViewTaken == _viewHash))
		{
			_viewDoneSaid = true;
			_chat.Add(new CLobbyChatLine("SAVE_TRANSFER_DONE", "", ""));
		}
	}

	public override void Bind(CSimulation simulation)
	{
		// A game begins or is loaded again: what was shown in the lobby is of the lobby, and a machine that joins from
		// here on is shown the game as it then stands.
		SetView(null);
		var session = new CHostSession(simulation) { HostCountry = Country };
		session.Parted += peer =>
		{
			_parted = true;
			// Which parts of the state differed, and the host's own figures of that midnight written beside the client's.
			if (session.MidnightAt(peer.PartedHour) is { } kept)
			{
				string[] differing = Sim.Replay.CStateHash.Parts.Where((_, i) => i < peer.PartedParts.Length && peer.PartedParts[i] != kept.Parts[i]).ToArray();
				string? path = Dump($"openhoi3_parted_{peer.PartedHour}_host.txt", kept.Fields);
				Note($"{peer.Name} is out of step at hour {peer.PartedHour}, in {(differing.Length > 0 ? string.Join(", ", differing) : "parts not said")}; the host's figures: {path ?? "not written"}");
			}
			else
			{
				Note($"{peer.Name} is out of step at hour {peer.PartedHour}; that hour's figures are no longer kept here");
			}
		};
		foreach (CNetPeer peer in _peers.Where(peer => peer.Country is not null))
		{
			peer.HostPeer = session.Add(peer.Name ?? "", peer.Link.Send);
			peer.HostPeer.Country = peer.Country;
		}
		// No hour until every machine has loaded the game (docs/multiplayer.md, section 4.1).
		System.Func<bool>? waits = simulation.HourGate;
		// And none while a machine chooses its country to join: the game it is shown stays the game that is played, and
		// the others are told who is waited for (section 10).
		simulation.HourGate = () => !_peers.Any(peer => peer.Loading) && !Choosing().Any() && waits?.Invoke() != false;
		session.AlsoWaitingFor = () => Choosing().Select(peer => peer.Name + " to choose a country");
		_session = session;
		Loaded();
	}

	public override int Frame()
	{
		if (_session is not { } session)
		{
			return 0;
		}
		foreach (CNetPeer peer in _peers.Where(peer => peer.HostPeer is not null).ToList())
		{
			if (peer.Link.Closed)
			{
				// The game pauses and its country goes to the AI, where no other player has it.
				session.Drop(peer.HostPeer!);
				Remove(peer);
				continue;
			}
			// Told when it becomes its country's primary player, the one before it having gone.
			if (peer.PrimarySent != IsPrimary(peer))
			{
				SayAssigned(peer);
			}
			while (!peer.Loading && peer.Link.TryReceive(out string line))
			{
				if (line.Length > 2 && line[0] == CWire.Chat)
				{
					TakePlayChat(peer, line[2..]);
					continue;
				}
				session.Receive(peer.HostPeer!, line);
			}
		}
		TellUsers();
		session.Tell();
		return 0;
	}

	/// <summary>
	/// The game as it stands sent to every machine, the ones waiting to join among them, and said to be loaded here too.
	/// A machine that asked for a country it cannot have - one played, or owning no land - is told so and let go.
	/// Answers whether anything was sent: not where the only cause was such a machine.
	/// </summary>
	/// <param name="state">The game, every hour and order of it run.</param>
	/// <param name="save">Makes the save of it.</param>
	public bool SendGame(CGameState state, System.Func<byte[]> save)
	{
		if (_session is not { } session)
		{
			return false;
		}
		List<CCountryTag> free = session.Unplayed();
		var played = new List<CCountryTag> { Country!.Value };
		played.AddRange(_peers.Where(peer => peer.Country is not null).Select(peer => peer.Country!.Value));
		bool joined = false;
		foreach (CNetPeer peer in _peers.Where(peer => peer.WaitsToJoin).ToList())
		{
			// One that named none is given the country nobody plays that owns the most land - a stand-in until the lobby,
			// where a country is chosen, is shared (docs/multiplayer.md, section 6).
			Sim.Countries.COwnedCounts owned = Sim.Countries.COwnedCounts.Of(state);
			CCountryTag? given = peer.Wanted ?? free.Where(tag => !played.Contains(tag))
				.OrderByDescending(tag => owned.Count(tag)).Select(tag => (CCountryTag?)tag).FirstOrDefault();
			// A country somebody plays may be shared; one nobody plays has to own land.
			if (given is not { } wanted || (!free.Contains(wanted) && !played.Contains(wanted)))
			{
				if (peer.Direct || peer.Wanted is null)
				{
					peer.Link.Send(CWire.Line(CWire.Refused, "country"));
					Remove(peer);
				}
				else
				{
					// Chosen on its lobby's map: told the country does not exist at this time, and left to choose again.
					peer.Link.Send(CWire.Line(CWire.Chat, new CLobbyChatLine("NOT_EXIST", peer.Wanted.Value.Value, "").ToWire()));
					peer.Wanted = null;
					peer.ChoseAt = 0;
				}
				continue;
			}
			peer.Country = wanted;
			if (peer.ChoseAt == 0)
			{
				peer.ChoseAt = ++_choices;
			}
			if (!played.Contains(wanted))
			{
				played.Add(wanted);
			}
			joined = true;
		}
		played = played.Distinct().ToList();
		if (!joined && !_parted)
		{
			return false;
		}
		byte[] game = save();
		string playedLine = CWire.Line(CWire.Played, string.Join(",", played.Select(tag => tag.Value)));
		foreach (CNetPeer peer in _peers.Where(peer => peer.Country is not null))
		{
			peer.HostPeer = null;
			peer.Link.Send(playedLine);
			SayAssigned(peer);
			peer.Sender = new CGameSender(peer.Link.Send, game);
			peer.Sender.Offer();
		}
		_session = null;
		_parted = false;
		SayGameToLoad(game, played);
		return true;
	}

	/// <summary>
	/// A machine's answer to a game offered it, handed to that sending - and where it asks for the game, how much is
	/// given its connection for it marked, so that how far the sending has come can be said (docs/multiplayer.md,
	/// section 14): the connection sends by itself, and this frame goes on.
	/// </summary>
	private static void Answer(CNetPeer peer, CGameSender? sender, string line)
	{
		long before = peer.Link.QueuedChars;
		sender?.Receive(line);
		if (line.Length > 0 && line[0] == CWire.GameWanted)
		{
			peer.SendFrom = before;
			peer.SendChars = peer.Link.QueuedChars - before;
		}
		else if (line.Length > 0 && line[0] == CWire.GameKept)
		{
			peer.SendChars = 0;
		}
	}

	/// <summary>How far the game a machine asked for has gone to it, in whole percent; null where it asked for none.</summary>
	private static int? SendingPercent(CNetPeer peer)
	{
		return peer.SendChars <= 0 ? null
			: (int)System.Math.Clamp(100L * (peer.Link.SentChars - peer.SendFrom) / peer.SendChars, 0L, 100L);
	}

	/// <summary>What the status says after a machine's name of the game going to it: the percent sent, then that it is loading.</summary>
	private static string SendingText(CNetPeer peer)
	{
		return SendingPercent(peer) switch
		{
			null => "",
			>= 100 => " (loading)",
			int percent => $" ({percent}%)",
		};
	}

	/// <summary>The host's own player's name in the chat: the lobby's, or "Host" where the game was hosted by a switch.</summary>
	private string HostName => _lobbyHostName.Length > 0 ? _lobbyHostName : "Host";

	/// <summary>The machines that play, or are being sent the game to play: whoever the chat in play reaches.</summary>
	private IEnumerable<CNetPeer> InPlay => _peers.Where(peer => peer.Country is not null && peer.Name is not null);

	private string _usersTold = "";

	/// <summary>
	/// The chat's users kept and told (docs/mechanics/chat.md, section 3): whenever who plays what changes every
	/// machine is sent the list, and the General channel says who joined and who is gone. A machine that has not been
	/// sent the list as it stands - one that has just joined - is sent it.
	/// </summary>
	private void TellUsers()
	{
		var users = new List<(string Name, CCountryTag? Country)> { (HostName, Country) };
		users.AddRange(InPlay.Select(peer => (peer.Name!, peer.Country)));
		string text = UsersText(users);
		if (text != _usersTold)
		{
			// Not as the game begins, when everybody is new.
			if (_usersTold.Length > 0)
			{
				foreach (string joined in users.Select(user => user.Name).Except(PlayUsers.Select(user => user.Name)).ToList())
				{
					SayOfPlayerInPlay("CHAT_USERJOINEDCHANNEL", joined);
				}
				foreach (string gone in PlayUsers.Select(user => user.Name).Except(users.Select(user => user.Name)).ToList())
				{
					SayOfPlayerInPlay("CHAT_USERDISCONNECTED", gone);
				}
			}
			_usersTold = text;
			PlayUsers = users;
			PlayUsersVersion++;
		}
		foreach (CNetPeer peer in InPlay.Where(peer => peer.UsersSent != text))
		{
			peer.UsersSent = text;
			peer.Link.Send(CWire.Line(CWire.Users, text));
		}
	}

	/// <summary>A line the chat itself says of a player, in the General channel on every machine.</summary>
	private void SayOfPlayerInPlay(string key, string name)
	{
		TakeChat(Interface.Chat.EChatLineKind.System, Interface.Chat.CChatChannels.General, name, key);
		string sent = CWire.Line(CWire.Chat, $"s {Interface.Chat.CChatChannels.General} {key} {System.Uri.EscapeDataString(name)}");
		foreach (CNetPeer peer in InPlay)
		{
			peer.Link.Send(sent);
		}
	}

	/// <summary>Whether a player's country puts him in a channel: General holds everybody, a faction's channel its players.</summary>
	private bool InChannel(CCountryTag? country, int channel)
	{
		return channel == Interface.Chat.CChatChannels.General
			|| (country is { } tag && ChannelOf?.Invoke(tag) == channel);
	}

	/// <summary>
	/// A player's line to a channel, sent to the machines of the players in that channel and to none other - the
	/// sender's among them, who is shown his line as it comes back. One not in the channel sends nothing.
	/// </summary>
	private void Route(string name, CCountryTag? country, int channel, string text)
	{
		if (!Interface.Chat.CChatChannels.Is(channel) || !InChannel(country, channel) || text.Length == 0)
		{
			return;
		}
		if (InChannel(Country, channel))
		{
			TakeChat(Interface.Chat.EChatLineKind.Line, channel, name, text);
		}
		string sent = CWire.Line(CWire.Chat, $"p {channel} {System.Uri.EscapeDataString(name)} {text}");
		foreach (CNetPeer peer in InPlay.Where(peer => InChannel(peer.Country, channel)))
		{
			peer.Link.Send(sent);
		}
	}

	/// <summary>A line to one player: shown him as from its sender, and its sender as to him. To nobody where no player has that name.</summary>
	private void RouteWhisper(string from, CNetPeer? fromPeer, string to, string text)
	{
		CNetPeer? target = InPlay.FirstOrDefault(peer => string.Equals(peer.Name, to, System.StringComparison.OrdinalIgnoreCase));
		bool toHost = target is null && string.Equals(HostName, to, System.StringComparison.OrdinalIgnoreCase);
		if ((target is null && !toHost) || text.Length == 0)
		{
			return;
		}
		if (toHost)
		{
			TakeChat(Interface.Chat.EChatLineKind.WhisperIn, 0, from, text);
		}
		else
		{
			target!.Link.Send(CWire.Line(CWire.Chat, $"i {System.Uri.EscapeDataString(from)} {text}"));
		}
		string name = toHost ? HostName : target!.Name!;
		if (fromPeer is null)
		{
			TakeChat(Interface.Chat.EChatLineKind.WhisperOut, 0, name, text);
		}
		else
		{
			fromPeer.Link.Send(CWire.Line(CWire.Chat, $"o {System.Uri.EscapeDataString(name)} {text}"));
		}
	}

	/// <summary>A machine's line of the chat in play: <c>c</c>, a channel and the text, or <c>w</c>, a name and the text.</summary>
	private void TakePlayChat(CNetPeer peer, string rest)
	{
		string[] parts = rest.Split(' ', 3);
		if (parts.Length < 3 || peer.Name is null)
		{
			return;
		}
		if (parts[0] == "c" && int.TryParse(parts[1], System.Globalization.NumberStyles.Integer,
			System.Globalization.CultureInfo.InvariantCulture, out int channel))
		{
			Route(peer.Name, peer.Country, channel, parts[2]);
		}
		else if (parts[0] == "w")
		{
			RouteWhisper(peer.Name, peer, System.Uri.UnescapeDataString(parts[1]), parts[2]);
		}
	}

	public override string PlayerName => HostName;

	public override void SayInPlay(int channel, string text)
	{
		Route(HostName, Country, channel, text);
	}

	public override void Whisper(string to, string text)
	{
		RouteWhisper(HostName, null, to, text);
	}

	private void Remove(CNetPeer peer)
	{
		_peers.Remove(peer);
		peer.Link.Dispose();
		// In the lobby every machine left is told a player has gone.
		if (_lobby is not null && peer.Name is not null)
		{
			SayOfPlayer("PLR_DISC", peer.Name);
		}
	}

	/// <summary>The lobby's chat lines that have come since they were last taken, for this machine's own chat log.</summary>
	public List<CLobbyChatLine> TakeChat()
	{
		var taken = new List<CLobbyChatLine>(_chat);
		_chat.Clear();
		return taken;
	}

	private readonly List<CLobbyChatLine> _chat = new();

	/// <summary>A line a player typed in the lobby's chat: every machine's chat log gets it, this one's with them.</summary>
	public void SayTyped(string name, string text)
	{
		Tell(new CLobbyChatLine(null, name, text));
	}

	/// <summary>A line the lobby says of a player by a localisation key - joined, left, kicked - on every machine.</summary>
	private void SayOfPlayer(string key, string name)
	{
		Tell(new CLobbyChatLine(key, name, ""));
	}

	/// <summary>Who says what the engine itself says in a chat.</summary>
	public const string EngineName = "OpenHOI3";

	private void Tell(CLobbyChatLine line)
	{
		_chat.Add(line);
		string sent = CWire.Line(CWire.Chat, line.ToWire());
		foreach (CNetPeer peer in _peers.Where(peer => peer.Name is not null && peer.Country is null))
		{
			peer.Link.Send(sent);
		}
	}

	/// <summary>
	/// A player thrown out of the lobby by the host, at once and with no question: told so, its connection closed, and
	/// every machine's chat told (docs/mechanics/frontend.md, section 9). A banned machine's address is kept, and
	/// nothing that connects from it is let in again while the program runs (docs/multiplayer.md, section 13).
	/// </summary>
	public void Kick(string name, bool ban)
	{
		if (_peers.FirstOrDefault(peer => peer.Name == name && peer.Country is null) is not { } peer)
		{
			return;
		}
		if (ban && peer.Link.RemoteAddress is { Length: > 0 } address)
		{
			_banned.Add(address);
		}
		peer.Link.Send(CWire.Line(CWire.Refused, ban ? "banned" : "kicked"));
		_peers.Remove(peer);
		peer.Link.Dispose();
		SayOfPlayer("PLR_DISC", name);
		SayOfPlayer(ban ? "PLAYER_BANNED" : "PLAYER_KICKED", name);
	}

	public override void Dispose()
	{
		foreach (CNetPeer peer in _peers)
		{
			peer.Link.Dispose();
		}
		_peers.Clear();
		_listener.Dispose();
	}
}
