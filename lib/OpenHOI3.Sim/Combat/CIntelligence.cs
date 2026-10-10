using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Combat;

/// <summary>
/// How well a country knows a province, from 0 to 9: 9 where it or a friend - an ally or
/// a member of its own faction - holds it, and otherwise what its own units there gave
/// it at the day's turn, 9 on land and 2 at sea. An air mission picks its targets only
/// where this is 2 or better. The original adds up to 8 for radar, which is not built.
/// See <c>docs/mechanics/aircombat.md</c>, section 6.
/// </summary>
public static class CIntelligence
{
	/// <summary>The least a country has to know a province by to pick a target in it.</summary>
	public const int LeastToPick = 2;

	public static int Level(CGameState state, CProvince province, CCountryTag country)
	{
		if (province.Controller is { } holder && (holder == country || IsFriend(state, holder, country)))
		{
			return CNavalIntelligence.LevelOfOwnGround;
		}
		return state.SeaIntelligence.TryGetValue((province.Id, country), out int level) ? level : 0;
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
