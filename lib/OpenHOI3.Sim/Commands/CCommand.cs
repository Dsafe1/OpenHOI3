namespace OpenHOI3.Sim.Commands;

/// <summary>
/// One change to a game. Nothing alters a <see cref="CGameState"/> except a command
/// being executed, and commands are executed in the order they were posted - a player's
/// click, the AI's decision and the passing of an hour alike. See
/// <c>docs/mechanics/tick.md</c>, section 4.
///
/// <b>A local input may choose which commands are posted, never what a command does
/// when it runs.</b> In a game between several players every machine executes the same
/// commands, so one that reads anything belonging to a single machine - a pressed key,
/// what is selected, which country that player is - does something different on each.
/// A command carries its own targets, and reads nothing but itself and the game state.
/// </summary>
public abstract class CCommand
{
	/// <summary>Its place in the order of everything posted to the game; -1 until posted.</summary>
	public long Serial { get; internal set; } = -1;

	/// <summary>Which drain of the queue it was posted during; -1 until posted.</summary>
	public int Turn { get; internal set; } = -1;

	/// <summary>
	/// Asked just before execution. A command that no longer makes sense - its target
	/// is gone, the game has moved on - answers false and is dropped.
	/// </summary>
	public virtual bool MayExecute(CGameState state)
	{
		return true;
	}

	/// <summary>
	/// Asked as it is posted, before it takes a place in the order: a command that answers false is thrown away and never
	/// queued. What it reads is the game as the post finds it, before anything posted with it has run.
	/// </summary>
	public virtual bool MayPost(CGameState state)
	{
		return true;
	}

	/// <summary>Takes out of a list of posted commands those their post would throw away, before any of them runs.</summary>
	public static void DropRefused(CGameState state, System.Collections.Generic.List<CCommand> posted)
	{
		posted.RemoveAll(command => !command.MayPost(state));
	}

	public abstract void Execute(CGameState state);
}
