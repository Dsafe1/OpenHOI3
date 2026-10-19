using System;
using System.Diagnostics;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The parts of the unit AI's pass timed where a tool has asked the game for a profile, under the pass's own line.
/// </summary>
public static class CAgentProfile
{
	public static void Time(CGameState state, string name, Action work)
	{
		if (state.Profile is null)
		{
			work();
			return;
		}
		long start = Stopwatch.GetTimestamp();
		work();
		state.Profile.Add("     of which " + name, Stopwatch.GetTimestamp() - start);
	}
}
