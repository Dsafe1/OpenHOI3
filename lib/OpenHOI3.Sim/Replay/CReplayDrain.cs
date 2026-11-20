namespace OpenHOI3.Sim.Replay;

/// <summary>
/// The queue drained that many times with no hour begun, and something ran in the last of them: an order given while the
/// clock stood. A recording played back from its start has no need of it - the next hour's tick says how far to drain -
/// but a game following another as it runs, a client of a game between several players, would otherwise not run a
/// paused order until the clock ran again (<c>docs/multiplayer.md</c>, section 2: a player's order is acted on at once).
/// </summary>
public sealed record CReplayDrain(int Turn) : CReplayEntry;
