namespace OpenHOI3.Sim.Countries;

/// <summary>
/// A country's mission, as far as it is built: none. The original's day looks at each country's mission - every country
/// that owns a province and is not the rebels, early in its day, before its spies - and with none it makes the mission's
/// state afresh, two scopes each seeded with a draw of the game's generator. No mission file ships with Their Finest Hour
/// or BlackICE, so those two draws are all the day does; missions themselves are not built.
/// See <c>docs/mechanics/espionage.md</c>, section 5, and <c>reversing/findings/FINDINGS-liveread.md</c>, section 121.
/// </summary>
public static class CCountryMission
{
	private const string Rebels = "REB";

	public static void RunCountry(CGameState state, CCountryFacts facts, CCountry country)
	{
		if (country.Tag.Value == Rebels || facts.OwnedCount(country.Tag) <= 0)
		{
			return;
		}
		state.Random.Next();                           // the mission's scope's seed
		state.Random.Next();                           // and its other scope's
	}
}
