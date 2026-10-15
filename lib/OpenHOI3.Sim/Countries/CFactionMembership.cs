namespace OpenHOI3.Sim.Countries;

/// <summary>
/// A country joining or leaving a faction, kept in the original's order of members: a country joining goes to
/// the back, one promoted to the front - the first member being the leader. The history's members stand in the
/// countries' order. See <c>docs/mechanics/war.md</c>, section 13.
/// </summary>
public static class CFactionMembership
{
	/// <summary>The country is put at the back of the faction's members.</summary>
	public static void Join(CGameState state, CCountry country, string faction)
	{
		country.Faction = faction;
		country.FactionSeat = ++state.LastFactionSeat;
	}

	/// <summary>
	/// The country leaves its faction, and every allied objective it asked or was asked goes with it
	/// (<c>provinceview.md</c>, section 6).
	/// </summary>
	public static void Leave(CGameState state, CCountry country)
	{
		country.Faction = "";
		country.FactionSeat = 0;
		state.AlliedObjectives.Clear(country.Tag);
	}

	/// <summary>The member is put at the front of its faction, and so leads it.</summary>
	public static void MoveToFront(CGameState state, CCountry country)
	{
		country.FactionSeat = --state.FirstFactionSeat;
	}
}
