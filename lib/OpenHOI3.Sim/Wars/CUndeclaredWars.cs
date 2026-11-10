using System.Linq;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>Starting, asking about and ending the wars nobody declared. See <c>docs/mechanics/war.md</c>, section 15.</summary>
public static class CUndeclaredWars
{
	public static CUndeclaredWar? Between(CGameState state, CCountryTag one, CCountryTag other)
	{
		return state.UndeclaredWars.Find(war => war.Joins(one, other));
	}

	/// <summary>The pair's war made if it has none, the attacker first, and the region added to it every time.</summary>
	public static void AddRegion(CGameState state, CCountryTag attacker, CCountryTag defender, string region)
	{
		CUndeclaredWar? war = Between(state, attacker, defender);
		if (war is null)
		{
			war = new CUndeclaredWar();
			war.Attackers.Add(attacker);
			war.Defenders.Add(defender);
			state.UndeclaredWars.Add(war);
		}
		war.Regions.Add(region);
	}

	/// <summary>Whether the country is on either side of a war nobody declared: the original's <c>in_undeclared_war</c>.</summary>
	public static bool Involves(CGameState state, CCountryTag country)
	{
		return state.UndeclaredWars.Exists(war => war.Attackers.Contains(country) || war.Defenders.Contains(country));
	}

	/// <summary>Whether the two have such a war and the province lies in one of its regions.</summary>
	public static bool Covers(CGameState state, CCountryTag one, CCountryTag other, int province)
	{
		if (state.UndeclaredWars.Count == 0 || province <= 0 || Between(state, one, other) is not { } war)
		{
			return false;
		}
		CProvince where = state.Province(province);
		return where.Regions.Any(index => war.Regions.Contains(state.Regions[index].Name));
	}

	/// <summary>The pair's war gone, as a formal declaration, a call to arms or an annexation ends it.</summary>
	public static void End(CGameState state, CCountryTag one, CCountryTag other)
	{
		state.UndeclaredWars.RemoveAll(war => war.Joins(one, other));
	}
}
