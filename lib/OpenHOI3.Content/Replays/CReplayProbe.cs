namespace OpenHOI3.Content.Replays;

/// <summary>
/// What is found of the recordings that lead to a save, before anything is played (<c>docs/replay.md</c>, section 11):
/// how many there are, and what the first of them that does not fit was made with - null where every one was made with
/// what would play it back.
/// </summary>
public sealed record CReplayProbe(int Recordings, string? OtherMade)
{
	public bool Fits => OtherMade is null;
}
