using OpenHOI3.Sim.Replay;

namespace OpenHOI3.Content.Replays;

/// <summary>
/// One recording of the chain that leads to a save (<c>docs/replay.md</c>, section 10): its file, what it says its game
/// is made with, and the mark it is played back to - where the save was written that the next recording, or the save
/// asked for, was begun from.
/// </summary>
public sealed record CReplaySegment(string Path, CReplayHeader Header, int UntilMark);
