using System.Collections.Generic;
using OpenHOI3.Sim.Commands;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// What one of the AI's steps did in one hour: the commands it posted, in order, how many draws it took of the game's
/// generator, and how many unit numbers it took for the commands to carry. See <c>docs/replay.md</c>.
/// </summary>
public sealed record CReplayStep(long Hour, string Step, IReadOnlyList<CCommand> Commands, long Draws, int UnitIds) : CReplayEntry;
