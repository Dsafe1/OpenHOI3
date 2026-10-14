using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// How well a country knows a province, from 0 to 9, as the missions and battles ask it: the level
/// <see cref="CProvinceIntel"/> keeps (<c>docs/mechanics/intelligence.md</c>). An air mission picks its targets only
/// where this is 2 or better (<c>docs/mechanics/aircombat.md</c>, section 6).
/// </summary>
public static class CIntelligence
{
	/// <summary>The least a country has to know a province by to pick a target in it.</summary>
	public const int LeastToPick = 2;

	public static int Level(CGameState state, CProvince province, CCountryTag country)
	{
		return CProvinceIntel.Level(state, province, country);
	}

	/// <summary>Whether two countries are allies or members of one faction.</summary>
	public static bool IsFriend(CGameState state, CCountryTag a, CCountryTag b)
	{
		foreach (CAlliance alliance in state.Alliances)
		{
			if (alliance.Joins(a, b))
			{
				return true;
			}
		}
		return state.Country(a) is { Faction.Length: > 0 } first && state.Country(b) is { } second
			&& first.Faction == second.Faction;
	}
}
