using System.Collections.Generic;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// What a game has to be made with for a recording to fit it: the countries played by a person, which change what the game
/// does, and the game's seed. See <c>docs/replay.md</c>, section 8.
/// </summary>
public sealed record CReplayHeader(IReadOnlyList<CCountryTag> Played, uint Seed);
