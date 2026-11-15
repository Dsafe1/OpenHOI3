using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Countries;

/// <summary>
/// Where a country's government sits, the original's acting capital (<c>CCountry::UpdateActingCapital</c>): looked at
/// every hour and at once when its province changes hands. See <c>docs/mechanics/war.md</c>, section 14.
/// </summary>
public static class CActingCapital
{
	/// <summary>Every country owning a province, once an hour; and first an exile whose seat an enemy holds.</summary>
	public static void RunHourly(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (country.InExile && CCountryLists.AtWar(state, country.Tag, state.Province(country.ActingCapital).Controller ?? default)
				&& COfferRules.RealFaction(country) && state.Country(COfferRules.Leader(state, country.Faction)) is { } leader
				&& leader != country)
			{
				country.ActingCapital = leader.ActingCapital;
			}
			if (CCountryLists.OwnedCount(state, country.Tag) > 0)
			{
				Update(state, country);
			}
		}
	}

	/// <summary>
	/// At the head of each country's day, the original's <c>CCountry::EnsureCapital</c>: a country that no longer owns
	/// its declared capital, and owns anything, declares the province of its own scoring highest - its manpower in
	/// whole men plus one, a hundred times over for one of its cores - the first of equals. Occupation never moves
	/// it, and a country owning nothing keeps its old one. The seat is not touched.
	/// </summary>
	public static void EnsureCapitals(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (state.Province(country.Capital).Owner == country.Tag || CCountryLists.OwnedCount(state, country.Tag) == 0)
			{
				continue;
			}
			int best = 0;
			long bestScore = long.MinValue;
			foreach (CProvince province in state.Provinces)
			{
				if (province.Owner != country.Tag)
				{
					continue;
				}
				long score = province.Manpower.Raw / 1000 + 1;
				if (province.Cores.Contains(country.Tag))
				{
					score *= 100;
				}
				if (score > bestScore)
				{
					best = province.Id;
					bestScore = score;
				}
			}
			// Logged in the country's own record, so the first capital so declared is its original capital.
			int seat = country.ActingCapital;
			country.DeclareCapital(best);
			country.ActingCapital = seat;
		}
	}

	/// <summary>
	/// Held by the country: the government goes back to the original capital where that differs and is held again,
	/// otherwise stays. Lost, holding nothing: it sits in the faction leader's seat, where there is a real faction
	/// led by another. Otherwise it goes to the best of the provinces the country owns and holds - victory points
	/// and IC, times the provinces of its area, three times on the seat's continent - the first of equals; failing
	/// any, the best it holds whoever owns it; failing that, it stays.
	/// </summary>
	public static void Update(CGameState state, CCountry country)
	{
		CProvince seat = state.Province(country.ActingCapital);
		if (seat.Controller == country.Tag)
		{
			// The original capital is the first declared in play, else the declared one - never the history's first, which
			// the original keeps apart: so China, back in Nanjing's control as Japan's puppet on the 1939 bookmark's first
			// day, keeps its government in Chongqing (war.md, section 14).
			int original = country.OriginalCapitalOrDeclared;
			if (original != country.ActingCapital && original != 0 && state.Province(original).Controller == country.Tag)
			{
				country.ActingCapital = original;
			}
			return;
		}
		if (Holds(state, country) == 0)
		{
			if (COfferRules.RealFaction(country) && state.Country(COfferRules.Leader(state, country.Faction)) is { } leader
				&& leader != country)
			{
				country.ActingCapital = leader.ActingCapital;
			}
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int best = Best(state, network, country, seat.Continent, owned: true);
		if (best == 0)
		{
			best = Best(state, network, country, seat.Continent, owned: false);
		}
		if (best != 0)
		{
			country.ActingCapital = best;
		}
	}

	private static int Best(CGameState state, CSupplyNetwork network, CCountry country, string continent, bool owned)
	{
		int best = 0;
		long bestScore = -1000;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller != country.Tag || (owned && province.Owner != country.Tag)
				|| network.AreaOf(province.Id) is not { } area)
			{
				continue;
			}
			long score = (province.Points * 1000L + province.Modifier[Economy.CIndustrialCapacity.ProvinceIc].Raw)
				* (area.Provinces.Count * 1000L) / 1000;
			if (continent.Length > 0 && province.Continent == continent)
			{
				score *= 3;
			}
			if (score > bestScore)
			{
				best = province.Id;
				bestScore = score;
			}
		}
		return best;
	}

	private static int Holds(CGameState state, CCountry country)
	{
		int count = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller == country.Tag)
			{
				count++;
			}
		}
		return count;
	}
}
