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

	/// <summary>A client gone: the game pauses (<c>docs/multiplayer.md</c>, section 2). Its country going to the AI is not built.</summary>
	public void Drop(CHostPeer peer)
	{
		if (_peers.Remove(peer))
		{
			_simulation.Pace.Pause();
		}
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
				_simulation.Post(CWire.Read(rest, _simulation.State));
				break;
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
			ulong hash = CStateHash.Whole(CStateHash.Of(state));
			Broadcast(CWire.Line(CWire.Hash, hour + " " + hash.ToString(CultureInfo.InvariantCulture)));
		}
		// Last of the hour: a client may now run it.
		Broadcast(CWire.Line(CWire.HourDone, hour));
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
