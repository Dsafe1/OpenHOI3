namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A replay has found its recording holding something else than the game asks for next: the game is no longer the one
/// that was recorded - other rules, another engine, other files. It ends there as at the recording's end, and nothing
/// follows it. See <c>docs/replay.md</c>, sections 3 and 12.
/// </summary>
public sealed class CReplayOutOfStepException : CReplayEndedException
{
	public CReplayOutOfStepException(string message)
		: base(message)
	{
	}
}
