namespace OpenHOI3.Sim.Countries;

/// <summary>
/// The four switches a country nobody plays has on every day: automatic leader assignment and the three automatic convoy
/// switches - maintaining, supply and resource convoys - set in each such country's day, so a save's <c>no</c> on one lasts
/// until its first midnight and a country handed back to the AI is on again the next day. Nothing else of the AI writes
/// them; only a player unticks his own (<c>docs/mechanics/leaders.md</c>, section 3).
/// </summary>
public static class CAutomationSwitches
{
	public static void ForceDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (state.Played.Contains(country.Tag))
			{
				continue;
			}
			country.AutoSupplyConvoys = true;
			country.AutoResourceConvoys = true;
			country.AutoMaintainConvoys = true;
			country.AutoAssignLeaders = true;
		}
	}
}
