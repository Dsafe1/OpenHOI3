namespace OpenHOI3.Sim.Mods;

/// <summary>
/// What the simulation asks of a mod's Lua for what BiceLib - BlackICE's DLL in the original - gives it: the switches the
/// scripts turned on as they loaded, and the day's call back into them. The simulation references nothing, so it states
/// here what it asks and the Lua project answers it (<c>docs/mechanics/bicelib.md</c>).
/// </summary>
public interface IBiceLibScripts
{
	/// <summary>What the scripts turned on as they loaded, before any game was made; a game takes a copy.</summary>
	CBiceLibSwitches BiceLibSwitches { get; }

	/// <summary>
	/// <c>BiceLibDailyPeriodics(firstDay)</c>, where the scripts define it: once a game day, 1 on the first call of a game
	/// made new or loaded and else 0. Whatever it changes reaches the outbox as commands. A failure is reported.
	/// </summary>
	void RunBiceLibDaily(CGameState state, bool firstDay);
}
