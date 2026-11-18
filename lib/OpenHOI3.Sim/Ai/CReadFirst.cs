namespace OpenHOI3.Sim.Ai;

/// <summary>
/// What the AI's scripts read that is otherwise made on first use, made before they run side by side (the trading's,
/// the ministers'): each is made again only where the state has moved on, which a script cannot do, so while they run
/// it is only read. What is made on first use and kept for being asked at once - the controlled lists, the plan groups,
/// a sea distance's row, the continents' names - is not among them.
/// </summary>
public static class CReadFirst
{
	public static void Make(CGameState state)
	{
		Countries.COwnedCounts.Of(state);
		Countries.CCountryFacts.Of(state);
		Supply.CSupplyNetworks.Refresh(state);
		_ = state.NavalDistances;
		_ = state.Positions;
		_ = state.ControlledCount(default);
	}
}
