namespace OpenHOI3.Sim.Replay;

/// <summary>
/// One thing a recording keeps, in the order it happened: one of the AI's steps (<see cref="CReplayStep"/>), a command
/// posted from outside the game - a player's (<see cref="CReplayPost"/>) - or an hour begun (<see cref="CReplayTick"/>).
/// See <c>docs/replay.md</c>.
/// </summary>
public abstract record CReplayEntry;
