namespace OpenHOI3.Sim.Replay;

/// <summary>
/// The game was saved here, after that many drains of the queue: the mark a save names, with its recording, as the place
/// it was written at (<c>docs/replay.md</c>, section 10). A game played on from that save is another recording, which
/// names this one and this mark as where it came from; played back, the mark does nothing but drain the queue as far,
/// and is where a playback that goes on into such a recording saves and loads as the player did.
/// </summary>
public sealed record CReplaySaved(int Mark, int Turn) : CReplayEntry;
