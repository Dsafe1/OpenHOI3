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

	/// <summary>
	/// <c>total_of_ours_sunk</c>: the ships on the list that were the country's - a ship disbanded or removed with its fleet
	/// as well as one sunk. With no country, the ships of nobody (<c>docs/mechanics/triggers.md</c>, section 5).
	/// </summary>
	public static int OursSunk(CGameState state, CCountryTag? country)
	{
		int count = 0;
		foreach (CSunkShip sunk in state.SunkShips)
		{
			if (sunk.Owner == country)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// <c>total_sunk_by_us</c>: the ships on the list whose sinker can still be found and is the country's now - a brigade,
	/// ship or wing of a unit of its, or a ship of its on the list itself. A sinker gone from the game - a wing or a
	/// brigade disbanded - is found by nobody, so the count can fall (<c>docs/mechanics/triggers.md</c>, section 5).
	/// </summary>
	public static int SunkBy(CGameState state, CCountryTag? country)
	{
		System.Collections.Generic.Dictionary<int, CCountryTag?>? owners = null;
		int count = 0;
		foreach (CSunkShip sunk in state.SunkShips)
		{
			if (sunk.Ship.SunkById == 0)
			{
				continue;
			}
			owners ??= Owners(state);
			if (owners.TryGetValue(sunk.Ship.SunkById, out CCountryTag? owner) && owner == country)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>Whose every sub unit still in the game is, by its number: the units' first, then the list's.</summary>
	private static System.Collections.Generic.Dictionary<int, CCountryTag?> Owners(CGameState state)
	{
		var owners = new System.Collections.Generic.Dictionary<int, CCountryTag?>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.IsRemoved)
				{
					continue;
				}
				foreach (CSubUnit subUnit in unit.SubUnits)
				{
					owners.TryAdd(subUnit.Id, unit.Owner);
				}
			}
		}
		foreach (CSunkShip sunk in state.SunkShips)
		{
			owners.TryAdd(sunk.Ship.Id, sunk.Owner);
		}
		return owners;
	}
}
