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

	/// <summary>The country its player plays, or none yet: the only country its orders may be for.</summary>
	public Countries.CCountryTag? Country { get; set; }

	/// <summary>The hour it last said it had run the game to.</summary>
	public long ConfirmedHour { get; set; }

	/// <summary>Whether it has said its game is no longer the host's: it is sent the game again, and is not waited for.</summary>
	public bool Parted { get; set; }

	/// <summary>The hour at which it found its hash another than the host's.</summary>
	public long PartedHour { get; set; }

	/// <summary>Its hash of each part of the state at that hour, where it sent them; empty where it did not.</summary>
	public ulong[] PartedParts { get; set; } = System.Array.Empty<ulong>();
}
