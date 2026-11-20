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
	/// Whether this machine's player is the primary player of the country: the only one, or of several who share it the
	/// first to have chosen it, who alone is shown its events (docs/multiplayer.md, section 8).
	/// </summary>
	public virtual bool Primary => true;

	/// <summary>
	/// A game to load in place of the one played, on this machine as on every other: the save's bytes, and the countries
	/// played in it, the host's first.
	/// </summary>
	public event System.Action<byte[], IReadOnlyList<CCountryTag>>? GameToLoad;

	/// <summary>Why the game is over for this machine - the host gone, or this machine not let in; null while it is not.</summary>
	public string? Ended { get; protected set; }

	/// <summary>The line of the original's connect log that says why (<c>frontend.md</c>, section 8), by its key; with <see cref="Ended"/>.</summary>
	public string? EndedKey { get; protected set; }

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
	/// The folder a game that has parted is written into, each machine's own figures at the hour it parted, so that what
	/// differed can be named afterwards (docs/multiplayer.md, section 9): the logs' folder; null writes nothing.
	/// </summary>
	public string? DumpFolder { get; set; }

	private readonly List<string> _notes = new();

	/// <summary>What there is to say in the log since it was last taken.</summary>
	public List<string> TakeNotes()
	{
		var taken = new List<string>(_notes);
		_notes.Clear();
		return taken;
	}

	protected void Note(string text)
	{
		_notes.Add(text);
	}

	/// <summary>Every figure's hash written to a file of the dump folder, a figure a line; answers the path, or null.</summary>
	protected string? Dump(string name, IEnumerable<KeyValuePair<string, ulong>> fields)
	{
		if (DumpFolder is null)
		{
			return null;
		}
		try
		{
			System.IO.Directory.CreateDirectory(DumpFolder);
			string path = System.IO.Path.Combine(DumpFolder, name);
			System.IO.File.WriteAllLines(path, System.Linq.Enumerable.Select(fields, field => field.Key + "\t" + field.Value.ToString("X16")));
			return path;
		}
		catch (System.Exception failed) when (failed is System.IO.IOException or System.UnauthorizedAccessException)
		{
			return null;
		}
	}

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
