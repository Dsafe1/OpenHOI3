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
	public CNetHost(int port, string checksum, CCountryTag country)
	{
		_listener = new CLineListener(port);
		_checksum = checksum;
		Country = country;
	}

	public override bool IsHost => true;

	public int Port => _listener.Port;

	public IReadOnlyList<CNetPeer> Peers => _peers;

	public override string Status
	{
		get
		{
			string[] loading = _peers.Where(peer => peer.Loading).Select(peer => peer.Name ?? "").ToArray();
			if (loading.Length > 0)
			{
				return "Sending the game to " + string.Join(", ", loading);
			}
			string[] waiting = _session?.WaitingFor().Select(peer => peer.Name).ToArray() ?? System.Array.Empty<string>();
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
			_peers.Add(new CNetPeer(link));
		}
		foreach (CNetPeer peer in _peers.ToList())
		{
			if (peer.Name is null)
			{
				// Its first line: let in by its play checksum, or told the host's and let go.
				if (peer.Link.TryReceive(out string first))
				{
					peer.Name = CHostSession.Admit(first, _checksum, peer.Link.Send);
					if (peer.Name is null)
					{
						Remove(peer);
						continue;
					}
				}
			}
			else if (peer.Country is null)
			{
				while (peer.Link.TryReceive(out string line))
				{
					if (line.Length > 2 && line[0] == CWire.Wanted)
					{
						peer.Wanted = new CCountryTag(line[2..].Trim());
					}
				}
			}
			else if (peer.Sender is { } sender)
			{
				// What it says of the game offered; anything else it sent before it had that game is of the game before, and dropped.
				while (!sender.Loaded && peer.Link.TryReceive(out string line))
				{
					sender.Receive(line);
				}
			}
			// One gone that plays no country is forgotten here; one that does is dropped by the frame, which may post - after
			// the host's own load where it went during that, its country being played in the game every machine loads.
			if (peer.Link.Closed && peer.Country is null)
			{
				Remove(peer);
			}
		}
	}

	public override void Bind(CSimulation simulation)
	{
		var session = new CHostSession(simulation);
		session.Parted += _ => _parted = true;
		foreach (CNetPeer peer in _peers.Where(peer => peer.Country is not null))
		{
			peer.HostPeer = session.Add(peer.Name ?? "", peer.Link.Send);
			peer.HostPeer.Country = peer.Country;
		}
		// No hour until every machine has loaded the game (docs/multiplayer.md, section 4.1).
		System.Func<bool>? waits = simulation.HourGate;
		simulation.HourGate = () => !_peers.Any(peer => peer.Loading) && waits?.Invoke() != false;
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
				// The game pauses and its country goes to the AI.
				session.Drop(peer.HostPeer!);
				Remove(peer);
				continue;
			}
			while (!peer.Loading && peer.Link.TryReceive(out string line))
			{
				session.Receive(peer.HostPeer!, line);
			}
		}
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
			CCountryTag wanted = peer.Wanted!.Value;
			if (!free.Contains(wanted) || played.Contains(wanted))
			{
				peer.Link.Send(CWire.Line(CWire.Refused, "country"));
				Remove(peer);
				continue;
			}
			peer.Country = wanted;
			played.Add(wanted);
			joined = true;
		}
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
			peer.Link.Send(CWire.Line(CWire.Assigned, peer.Country!.Value.Value));
			peer.Sender = new CGameSender(peer.Link.Send, game);
			peer.Sender.Offer();
		}
		_session = null;
		_parted = false;
		SayGameToLoad(game, played);
		return true;
	}

	private void Remove(CNetPeer peer)
	{
		_peers.Remove(peer);
		peer.Link.Dispose();
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
