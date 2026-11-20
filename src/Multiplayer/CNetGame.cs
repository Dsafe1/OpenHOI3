using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Multiplayer;

/// <summary>
/// This machine's part in a game between several machines, which outlives the sessions that play it
/// (<c>docs/multiplayer.md</c>, section 6): the connections, and the host's or the client's session of the game now
/// played. A game is sent and loaded again whenever a machine joins or has parted, and every load is a new session
/// around a new simulation; the boot scene keeps this, hands it to each of them, and loads the game it says to.
/// </summary>
public abstract class CNetGame : System.IDisposable
{
	/// <summary>Whether this machine hosts the game.</summary>
	public abstract bool IsHost { get; }

	/// <summary>The country this machine's player plays; none while a client has not been told.</summary>
	public CCountryTag? Country { get; protected set; }

	/// <summary>
	/// A game to load in place of the one played, on this machine as on every other: the save's bytes, and the countries
	/// played in it, the host's first.
	/// </summary>
	public event System.Action<byte[], IReadOnlyList<CCountryTag>>? GameToLoad;

	/// <summary>Why the game is over for this machine - the host gone, or this machine not let in; null while it is not.</summary>
	public string? Ended { get; protected set; }

	/// <summary>What a player is to be told of the others just now - who is waited for, a game being sent; empty for nothing.</summary>
	public abstract string Status { get; }

	/// <summary>
	/// Once a frame, whatever is on screen - a game, a loading screen: the connections looked after, and what of the
	/// lines that came is no part of play taken. Never reads the game.
	/// </summary>
	public abstract void Pump();

	/// <summary>A game loaded and its simulation made: the session of it made around that, and the others told.</summary>
	public abstract void Bind(CSimulation simulation);

	/// <summary>
	/// Once a frame while the game is this thread's to read: the lines of play taken, and on a client what came of the
	/// orders given between hours run. Answers the hours run here - none: a host's are run by its clock, a client's
	/// beside its frames (<see cref="StartHours"/>).
	/// </summary>
	public abstract int Frame();

	/// <summary>
	/// After a frame is drawn, the game being this thread's: on a client, the hours that have come started beside the
	/// frames that follow, as a game of one's are. A host's are started by its clock.
	/// </summary>
	public virtual void StartHours()
	{
	}

	public abstract void Dispose();

	/// <summary>
	/// Whether a game has been said to be loaded and its simulation is not yet made: the game on screen is over, and its
	/// session runs nothing more of it.
	/// </summary>
	public bool Loading { get; private set; }

	/// <summary>The simulation of the game loaded is made: for <see cref="Bind"/>.</summary>
	protected void Loaded()
	{
		Loading = false;
	}

	protected void SayGameToLoad(byte[] game, IReadOnlyList<CCountryTag> played)
	{
		Loading = true;
		GameToLoad?.Invoke(game, played);
	}
}
