using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Wars;

/// <summary>What the AI's politics script asks before it frees a country. See <c>docs/mechanics/politics.md</c>, section 11.1.</summary>
public static class CLiberation
{
	/// <summary>
	/// Whether the exile's capital is safe to give back: not where we are at war and an enemy not in exile, with land and an
	/// acting capital, has that capital on the same continent; and not where the land beside it that the exile owns is
	/// held by a country at war with the exile.
	/// </summary>
	public static bool IsCapitalSafe(CGameState state, CCountry country, CCountry exile)
	{
		CProvince capital = state.Province(exile.Capital);
		if (state.IsAtWar(country.Tag))
		{
			foreach (CCountry enemy in CCountryLists.Enemies(state, country))
			{
				if (!enemy.InExile && CCountryLists.OwnedCount(state, enemy.Tag) > 0 && enemy.ActingCapital != 0
					&& state.Province(enemy.ActingCapital).Continent == capital.Continent)
				{
					return false;
				}
			}
		}
		foreach (CProvinceEdge edge in capital.Edges)
		{
			CProvince beside = state.Province(edge.To);
			if (beside.Owner == exile.Tag && CMovementRules.IsEnemy(state, beside.Controller, exile.Tag))
			{
				return false;
			}
		}
		return true;
	}
}
