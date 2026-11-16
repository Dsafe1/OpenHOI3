namespace OpenHOI3.Sim.Mods;

/// <summary>
/// The call BiceLib makes into the mod's Lua once a game day, <c>BiceLibDailyPeriodics(firstDay)</c>: in the original
/// from the first hour of the day on that is one o'clock or later, after the scheduled scripts of one o'clock, in every
/// game of a session - which BlackICE uses to set the headquarters' limits from each country's technology. Here at the end
/// of hour 1, after the hour's AI, and not while a new game is being made. <c>firstDay</c> is 1 on a game's first call,
/// which a loaded game makes again. The scripts' part is taken as a step of the replay's, kept only where it posted
/// something (<c>docs/replay.md</c>, section 3), so a replay plays its commands back without the Lua. Ours as to the
/// place in the hour; <c>docs/mechanics/bicelib.md</c>, section 4.
/// </summary>
public static class CBiceLibDaily
{
	/// <summary>The hour of the day it is made in.</summary>
	public const int Hour = 1;

	public static void RunHourly(CGameState state)
	{
		if (state.Hour != Hour || state.IsBeingMade || state.AiScripts is not IBiceLibScripts scripts)
		{
			return;
		}
		bool first = !state.BiceLib.DailyCalled;
		state.BiceLib.DailyCalled = true;
		if (state.Replay is { } replay)
		{
			replay.RunCalledStep(state, "BiceLibDailyPeriodics", s => scripts.RunBiceLibDaily(s, first));
		}
		else
		{
			scripts.RunBiceLibDaily(state, first);
		}
	}
}
