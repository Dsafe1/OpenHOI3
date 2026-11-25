using System;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// A replay has reached the end of its recording: the hour asks for a step the recording does not hold. The game cannot go
/// on from there, its AI having nothing to say; whatever shows it stops the clock. See <c>docs/replay.md</c>.
/// </summary>
public class CReplayEndedException : InvalidOperationException
{
	public CReplayEndedException(string message)
		: base(message)
	{
	}
}
