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
	/// <summary>How long a frame may go on running hours that have come, in milliseconds: a machine behind catches up within it.</summary>
	private const double FrameBudget = 10;

	private readonly CLineLink _link;
	private readonly CGameReceiver _receiver;
	private readonly List<string> _waiting = new();
	private IReadOnlyList<CCountryTag> _played = System.Array.Empty<CCountryTag>();
	private CClientSession? _session;

	/// <summary>Whether a game has been offered and is not yet here whole.</summary>
	private bool _taking;

	public CNetClient(CLineLink link, string checksum, string name, CCountryTag wanted)
	{
		_link = link;
		_receiver = new CGameReceiver(link.Send, _ => null);
		link.Send(CClientSession.JoinLine(checksum, name));
		link.Send(CWire.Line(CWire.Wanted, wanted.Value));
	}

	public override bool IsHost => false;

	/// <summary>How many midnights of the game now played this machine's hash has been the host's.</summary>
	public int HashesAlike => _session?.HashesAlike ?? 0;

	/// <summary>Whether the host has let this machine in.</summary>
	public bool Admitted { get; private set; }

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
		if (_session is not { } session)
		{
			return 0;
		}
		int hours = 0;
		var spent = Stopwatch.StartNew();
		while (session.Step(mostHours: 1) > 0)
		{
			hours++;
			if (spent.Elapsed.TotalMilliseconds >= FrameBudget)
			{
				break;
			}
		}
		return hours;
	}

	private void Drain()
	{
		while (_link.TryReceive(out string line))
		{
			Take(line);
		}
		if (_link.Closed && Ended is null)
		{
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
				Ended = rest == "country"
					? "The host refused the country asked for: it is played already, or owns no land."
					: "The host plays other files than this machine: its play checksum differs.";
				return;
			case CWire.Played:
				_played = rest.Split(',', System.StringSplitOptions.RemoveEmptyEntries).Select(tag => new CCountryTag(tag)).ToList();
				return;
			case CWire.Assigned:
				Country = new CCountryTag(rest);
				_session?.Receive(line);
				return;
		}
		if (_receiver.Receive(line))
		{
			if (line[0] == CWire.GameOffered)
			{
				// The game played is over for this machine: what comes from here on is of the one offered.
				_session = null;
				_waiting.Clear();
				_taking = true;
			}
			if (_receiver.Failed)
			{
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
