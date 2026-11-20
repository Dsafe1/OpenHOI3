using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// The host's side of a game between several machines (<c>docs/multiplayer.md</c>, sections 2 and 5): its game is
/// recorded, and every entry of the recording sent to every client as it is made; a client's order is posted here and
/// comes back to it in the recording; the host starts no hour while a client is more than the window behind; anybody's
/// pause is taken; and each midnight's hash is sent for the clients to set beside their own.
/// </summary>
public sealed class CHostSession
{
	/// <summary>How many hours a client may be behind before the host waits. To be measured; a guess.</summary>
	public const int DefaultWindow = 3;

	private readonly CSimulation _simulation;
	private readonly List<CHostPeer> _peers = new();
	private bool _toldPaused;
	private int _toldSpeed = -1;
	private string _toldWaiting = "";

	public CHostSession(CSimulation simulation, int window = DefaultWindow)
	{
		_simulation = simulation;
		Window = window;
		simulation.Pace.Multiplayer = true;
		simulation.Pace.IsHost = true;
		simulation.State.Replay = CReplay.Recording(entry => Broadcast(CWire.Line(CWire.Entry, CReplayFile.ToLine(entry))));
		simulation.HourGate = () => WaitingFor().Count == 0;
		simulation.HourPassed += TellHash;
	}

	public int Window { get; }

	public IReadOnlyList<CHostPeer> Peers => _peers;

	/// <summary>A client whose game is no longer the host's: it has to be sent the game again.</summary>
	public event Action<CHostPeer>? Parted;

	/// <summary>How many orders were dropped for naming another country than their sender's.</summary>
	public int OrdersDropped { get; private set; }

	/// <summary>
	/// A machine's first line read, before it is a client: let in where its play checksum is the host's
	/// (<c>mechanics/checksum.md</c>, section 8), and told so; told the host's where it is not, and not let in. Answers
	/// the name it gave, or null where it was refused or the line was no asking.
	/// </summary>
	public static string? Admit(string line, string hostChecksum, Action<string> send)
	{
		if (line.Length < 2 || line[0] != CWire.Join)
		{
			return null;
		}
		string rest = line[2..];
		int space = rest.IndexOf(' ');
		string checksum = space < 0 ? rest : rest[..space];
		string name = space < 0 ? "" : rest[(space + 1)..];
		if (checksum != hostChecksum)
		{
			send(CWire.Line(CWire.Refused, hostChecksum));
			return null;
		}
		send(CWire.Line(CWire.Accepted));
		return name;
	}

	/// <summary>
	/// A client that holds the host's game as it stands - made the same way, or loaded from the host's save - joined at
	/// this hour. Sending it the game is not this class's.
	/// </summary>
	public CHostPeer Add(string name, Action<string> send)
	{
		var peer = new CHostPeer(name, send, _simulation.State.TotalHours);
		_peers.Add(peer);
		peer.Send(PaceLine());
		return peer;
	}

	/// <summary>The country the host's own player plays, which a client may share with it; null where it is not said.</summary>
	public Countries.CCountryTag? HostCountry { get; set; }

	/// <summary>
	/// A client gone: the game pauses, and its country goes to the AI (<c>docs/multiplayer.md</c>, section 2) - unless
	/// another player, the host among them, still plays it (section 8).
	/// </summary>
	public void Drop(CHostPeer peer)
	{
		if (!_peers.Remove(peer))
		{
			return;
		}
		_simulation.Pace.Pause();
		if (peer.Country is { } country && country != HostCountry && !_peers.Any(other => other.Country == country))
		{
			_simulation.Post(new Commands.CSetPlayedCommand(country, played: false));
		}
	}

	/// <summary>The countries a player may take up: those owning land that nobody plays, in the countries' order.</summary>
	public List<Countries.CCountryTag> Unplayed()
	{
		CGameState state = _simulation.State;
		var owned = Countries.COwnedCounts.Of(state);
		return state.Countries.Where(country => !state.Played.Contains(country.Tag) && owned.Count(country.Tag) > 0)
			.Select(country => country.Tag).ToList();
	}

	/// <summary>
	/// A client's player given a country nobody plays - any of them: one who comes back chooses among them all again,
	/// the one he had or another (<c>docs/multiplayer.md</c>, section 4.1). The country he held before, if any, goes to
	/// the AI. Answers whether it was his to take.
	/// </summary>
	public bool Assign(CHostPeer peer, Countries.CCountryTag country)
	{
		CGameState state = _simulation.State;
		if (state.Country(country) is null || state.Played.Contains(country))
		{
			return false;
		}
		if (peer.Country is { } before)
		{
			_simulation.Post(new Commands.CSetPlayedCommand(before, played: false));
		}
		peer.Country = country;
		_simulation.Post(new Commands.CSetPlayedCommand(country, played: true));
		peer.Send(CWire.Line(CWire.Assigned, country.Value));
		return true;
	}

	/// <summary>
	/// The clients the host is waiting for: those not parted that another hour would put more than the window behind.
	/// So no client is ever more than <see cref="Window"/> hours behind the host; at a window of one the host runs an
	/// hour only when every client has run the last.
	/// </summary>
	public List<CHostPeer> WaitingFor()
	{
		long least = _simulation.State.TotalHours - Window;
		return _peers.Where(peer => !peer.Parted && peer.ConfirmedHour <= least).ToList();
	}

	/// <summary>A line from a client.</summary>
	public void Receive(CHostPeer peer, string line)
	{
		if (line.Length < 2)
		{
			return;
		}
		string rest = line[2..];
		switch (line[0])
		{
			case CWire.Order:
			{
				// One for another country than its sender's is dropped; no more is checked (COrderCheck).
				Commands.CCommand order = CWire.Read(rest, _simulation.State);
				if (COrderCheck.MayGive(order, peer.Country))
				{
					_simulation.Post(order);
				}
				else
				{
					OrdersDropped++;
				}
				break;
			}
			case CWire.Confirmed:
				peer.ConfirmedHour = Math.Max(peer.ConfirmedHour, long.Parse(rest, CultureInfo.InvariantCulture));
				break;
			case CWire.TogglePause:
				// Anybody may pause and resume.
				_simulation.Pace.TogglePause();
				break;
			case CWire.Parted:
				if (!peer.Parted)
				{
					peer.Parted = true;
					// The hour its hash differed at, and its hash of each part of the state where it sent them.
					string[] said = rest.Split(' ');
					peer.PartedHour = long.TryParse(said[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long at) ? at : 0;
					peer.PartedParts = said.Skip(1).Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture,
						out ulong hash) ? hash : 0).ToArray();
					Parted?.Invoke(peer);
				}
				break;
		}
	}

	/// <summary>Once a frame, after the game's own: the clients told of the clock and of who is waited for, where either changed.</summary>
	public void Tell()
	{
		if (_simulation.Pace.Paused != _toldPaused || _simulation.Pace.Speed != _toldSpeed)
		{
			Broadcast(PaceLine());
		}
		string waiting = string.Join(",", WaitingFor().Select(peer => peer.Name));
		if (waiting != _toldWaiting)
		{
			_toldWaiting = waiting;
			Broadcast(CWire.Line(CWire.Waiting, waiting));
		}
	}

	private string PaceLine()
	{
		_toldPaused = _simulation.Pace.Paused;
		_toldSpeed = _simulation.Pace.Speed;
		return CWire.Line(CWire.Pace, (_toldPaused ? "1" : "0") + " " + _toldSpeed.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// After each hour: where it turned a day, the hash of the game as it then stands, for every client to set beside its
	/// own; and that the hour is whole.
	/// </summary>
	private void TellHash()
	{
		CGameState state = _simulation.State;
		string hour = state.TotalHours.ToString(CultureInfo.InvariantCulture);
		if (state.Hour == 0)
		{
			ulong[] parts = CStateHash.Of(state);
			ulong hash = CStateHash.Whole(parts);
			Broadcast(CWire.Line(CWire.Hash, hour + " " + hash.ToString(CultureInfo.InvariantCulture)));
			// Kept for the last few midnights, with every figure's own hash: where a client says its game has parted, what
			// differs can then be named (docs/multiplayer.md, section 9). Only while a client follows.
			if (_peers.Count > 0)
			{
				_midnights[state.TotalHours] = (parts, CStateHash.Fields(state).ToList());
				foreach (long old in _midnights.Keys.Where(kept => kept < state.TotalHours - 72).ToList())
				{
					_midnights.Remove(old);
				}
			}
		}
		// Last of the hour: a client may now run it.
		Broadcast(CWire.Line(CWire.HourDone, hour));
	}

	private readonly Dictionary<long, (ulong[] Parts, List<KeyValuePair<string, ulong>> Fields)> _midnights = new();

	/// <summary>
	/// The host's hash of each part of the state, and of every figure, as a midnight of the last three days stood; null
	/// where that hour is not kept.
	/// </summary>
	public (ulong[] Parts, List<KeyValuePair<string, ulong>> Fields)? MidnightAt(long hour)
	{
		return _midnights.TryGetValue(hour, out (ulong[], List<KeyValuePair<string, ulong>>) kept) ? kept : null;
	}

	private void Broadcast(string line)
	{
		foreach (CHostPeer peer in _peers)
		{
			if (!peer.Parted)
			{
				peer.Send(line);
			}
		}
	}
}
