using OpenHOI3.Sim.Commands;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A command posted from outside the game - a player's order - with the drain of the queue it was posted after
/// (<see cref="CCommand.Turn"/>): a replay drains the queue that far before posting it, so it runs where it ran. See
/// <c>docs/replay.md</c>, section 8.
/// </summary>
public sealed record CReplayPost(int Turn, CCommand Command) : CReplayEntry;
