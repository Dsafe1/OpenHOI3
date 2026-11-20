using System.Collections.Generic;
using OpenHOI3.Sim.Commands;

namespace OpenHOI3.Sim.Replay;

/// <summary>
/// What one of the AI's steps did in one hour: the commands it posted, in order, how many draws it took of the game's
/// generator and of the AI's, how many unit numbers it took for the commands to carry - and whether its asking made the
/// game's table of who stands where or its supply network, which are made by whoever asks first: a machine that plays
/// the step back makes them at the same place, or it would make them at its own next asking, another moment
/// (<c>docs/multiplayer.md</c>, section 3). See <c>docs/replay.md</c>.
/// </summary>
public sealed record CReplayStep(long Hour, string Step, IReadOnlyList<CCommand> Commands, long Draws, int UnitIds, long AiDraws = 0,
	bool MadePositions = false, bool MadeSupplyNetwork = false)
	: CReplayEntry;
