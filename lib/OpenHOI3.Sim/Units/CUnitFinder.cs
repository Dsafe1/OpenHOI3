using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Units;

/// <summary>A unit still in the game by its number, as a command finds the units it names.</summary>
public static class CUnitFinder
{
	/// <summary>Among one country's units.</summary>
	public static CUnit? Find(CGameState state, CCountryTag owner, int id)
	{
		if (state.Country(owner) is not { } country)
		{
			return null;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Id == id && !unit.IsRemoved)
			{
				return unit;
			}
		}
		return null;
	}

	/// <summary>A sub unit still in the game by its number, among one country's units.</summary>
	public static CSubUnit? FindSubUnit(CGameState state, CCountryTag owner, int id)
	{
		if (state.Country(owner) is not { } country)
		{
			return null;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.IsRemoved)
			{
				continue;
			}
			foreach (CSubUnit subUnit in unit.SubUnits)
			{
				if (subUnit.Id == id)
				{
					return subUnit;
				}
			}
		}
		return null;
	}
}
