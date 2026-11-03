namespace OpenHOI3.Sim.Replay;

/// <summary>
/// An hour begun, with the drain of the queue its tick was posted after: a client paused between hours drains the queue
/// each frame, and what a command posted runs in those drains, so a replay drains as far before the hour. See
/// <c>docs/replay.md</c>, section 8.
/// </summary>
public sealed record CReplayTick(int Turn) : CReplayEntry;
