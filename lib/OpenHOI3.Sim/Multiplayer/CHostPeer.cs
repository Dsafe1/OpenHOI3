using System;

namespace OpenHOI3.Sim.Multiplayer;

/// <summary>A machine following a host's game, as the host holds it: how to reach it and how far it has got.</summary>
public sealed class CHostPeer
{
	public CHostPeer(string name, Action<string> send, long confirmedHour)
	{
		Name = name;
		Send = send;
		ConfirmedHour = confirmedHour;
	}

	/// <summary>What the others are told it is called.</summary>
	public string Name { get; }

	/// <summary>Sends it one line.</summary>
	public Action<string> Send { get; }

	/// <summary>The hour it last said it had run the game to.</summary>
	public long ConfirmedHour { get; set; }

	/// <summary>Whether it has said its game is no longer the host's: it is sent the game again, and is not waited for.</summary>
	public bool Parted { get; set; }
}
