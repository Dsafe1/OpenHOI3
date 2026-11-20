using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>
/// A client's side of a game between several machines (<c>docs/multiplayer.md</c>, sections 2 and 5): its game follows
/// the host's recording as the entries come; its player's orders and pauses go to the host and nowhere else; it tells
/// the host how far it has run; and each midnight it sets its hash beside the host's, and says so where they differ.
/// </summary>
public sealed class CClientSession
{
	private readonly CSimulation _simulation;
	private readonly Action<string> _send;
	private readonly Queue<CReplayEntry> _entries = new();
	private readonly Dictionary<long, ulong> _hostHashes = new();

	/// <summary>The last hour the host has said is whole.</summary>
	private long _wholeHour;

	public CClientSession(CSimulation simulation, Action<string> send)
	{
		_simulation = simulation;
		_send = send;
		simulation.Pace.Multiplayer = true;
		simulation.Pace.IsHost = false;
		simulation.Pace.AskHostToTogglePause = () => _send(CWire.Line(CWire.TogglePause));
		simulation.State.Replay = CReplay.Following(() => _entries.Count > 0 ? _entries.Dequeue() : null);
		simulation.ToHost = command =>
		{
			_send(CWire.Line(CWire.Order, CWire.Write(command)));
			return true;
		};
	}

	/// <summary>A machine's first line to a host: its play checksum and its player's name.</summary>
	public static string JoinLine(string checksum, string name) => CWire.Line(CWire.Join, checksum + " " + name);

	/// <summary>Whether this game is no longer the host's, by a midnight's hash: nothing more is run until it is sent the game again.</summary>
	public bool Parted { get; private set; }

	/// <summary>The hour at which this game's hash was another than the host's.</summary>
	public long PartedHour { get; private set; }

	/// <summary>Every figure's own hash as this game stood at that hour; null while the game has not parted.</summary>
	public List<KeyValuePair<string, ulong>>? PartedFields { get; private set; }

	/// <summary>How many midnights' hashes have been set beside the host's and found the same.</summary>
	public int HashesAlike { get; private set; }

	/// <summary>The country this machine's player plays, as the host has given it; none yet.</summary>
	public Countries.CCountryTag? Country { get; private set; }

	/// <summary>Who the game is waiting for, as the host says: names, or none.</summary>
	public IReadOnlyList<string> WaitingFor { get; private set; } = Array.Empty<string>();

	/// <summary>A line from the host.</summary>
	public void Receive(string line)
	{
		if (line.Length < 2)
		{
			return;
		}
		string rest = line[2..];
		switch (line[0])
		{
			case CWire.Entry:
				_entries.Enqueue(CReplayFile.FromLine(rest, _simulation.State));
				break;
			case CWire.Hash:
			{
				string[] parts = rest.Split(' ');
				_hostHashes[long.Parse(parts[0], CultureInfo.InvariantCulture)] = ulong.Parse(parts[1], CultureInfo.InvariantCulture);
				break;
			}
			case CWire.Assigned:
				Country = new Countries.CCountryTag(rest);
				break;
			case CWire.HourDone:
				_wholeHour = Math.Max(_wholeHour, long.Parse(rest, CultureInfo.InvariantCulture));
				break;
			case CWire.Pace:
			{
				// This machine's clock paces nothing; it shows the host's.
				string[] parts = rest.Split(' ');
				_simulation.Pace.Set(parts[0] == "1", int.Parse(parts[1], CultureInfo.InvariantCulture));
				break;
			}
			case CWire.Waiting:
				WaitingFor = rest.Length == 0 ? Array.Empty<string>() : rest.Split(',');
				break;
		}
	}

	/// <summary>
	/// Whether the next hour may be run: the host has said it is whole and its tick is next in what came. Asked after
	/// <see cref="Step"/> has run what came between hours - with no hours allowed, where only that is wanted.
	/// </summary>
	public bool HourReady => !Parted && _simulation.State.TotalHours < _wholeHour && _simulation.HourHasCome;

	/// <summary>
	/// Once a frame: what has come of the orders given between hours is run, and then the hours that have come, as many
	/// as <paramref name="mostHours"/> - a machine that is behind catches up by running more of them a frame. Answers the
	/// hours run.
	/// </summary>
	/// <param name="goOn">Asked after each hour but the last allowed: false stops there. Null runs them all.</param>
	public int Step(int mostHours = 1, Func<bool>? goOn = null)
	{
		if (Parted)
		{
			return 0;
		}
		_simulation.Follow();
		int ran = 0;
		// An hour is run once the host has said it is whole: its entries come one by one as the host runs it.
		while (ran < mostHours && (ran == 0 || goOn?.Invoke() != false) && HourReady)
		{
			_simulation.RunHours(1);
			ran++;
			long hour = _simulation.State.TotalHours;
			_send(CWire.Line(CWire.Confirmed, hour.ToString(CultureInfo.InvariantCulture)));
			if (_hostHashes.Remove(hour, out ulong hosts))
			{
				ulong[] parts = CStateHash.Of(_simulation.State);
				if (hosts != CStateHash.Whole(parts))
				{
					Parted = true;
					// With this machine's hash of each part, and every figure's kept, so that what differs can be named
					// on both machines (docs/multiplayer.md, section 9).
					PartedHour = hour;
					PartedFields = CStateHash.Fields(_simulation.State).ToList();
					_send(CWire.Line(CWire.Parted, hour.ToString(CultureInfo.InvariantCulture) + " "
						+ string.Join(" ", parts.Select(part => part.ToString(CultureInfo.InvariantCulture)))));
					break;
				}
				HashesAlike++;
			}
			_simulation.Follow();
		}
		return ran;
	}
}
