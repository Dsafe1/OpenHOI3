using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// The list of every ship that has left the game: sunk in a battle at sea, finished by bombs, or gone with its
/// fleet when the fleet was disbanded or taken out of the game for any other reason. Nothing is ever taken off
/// it, and a save keeps it whole as <c>sunk_ships</c>. See <c>docs/mechanics/savegame.md</c>, section 7.
/// </summary>
public static class CSunkShips
{
	/// <summary>Puts a ship on the list, in the order ships leave the game.</summary>
	public static void Add(CGameState state, CCountryTag? owner, CSubUnit ship)
	{
		state.SunkShips.Add(new CSunkShip(owner, ship));
	}

	/// <summary>A ship on the list by its id, or null.</summary>
	public static CSubUnit? Find(CGameState state, int id)
	{
		foreach (CSunkShip sunk in state.SunkShips)
		{
			if (sunk.Ship.Id == id)
			{
				return sunk.Ship;
			}
		}
		return null;
	}
}
