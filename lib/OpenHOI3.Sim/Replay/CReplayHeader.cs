using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// What a game has to be made with for a recording to fit it: the countries played by a person, which change what the game
/// does, and the game's seed. See <c>docs/replay.md</c>, section 8. And for a recording of a game kept beside its saves
/// (section 10): its own name, the save it was begun from - none for a new game's - and what made the game, the engine
/// and files a playback has to be run with.
/// </summary>
public sealed record CReplayHeader(IReadOnlyList<CCountryTag> Played, uint Seed, string? Segment = null,
	CReplayLink? Parent = null, string Made = "");
