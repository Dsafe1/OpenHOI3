using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// Where a day's supplies and fuel come into being: where the factories are. Each
/// province of a country's own ground at home gets the share of what was made that its
/// industry is of all of it, into its own pool, and only the capital's share and what
/// the rounding leaves over are in the stockpile at once. The rest travels there with
/// the supply pass, a province a day. See <c>docs/mechanics/supply.md</c>, section 15.
/// </summary>
public static class CSupplyMaking
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>Puts what a country made of one of the two today where its factories are.</summary>
	public static void ShareOut(CGameState state, CCountry country, CGoodsPool stockpile, EGoods goods, CFixedPoint made)
	{
		long left = made.Raw;
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (country.CoreHomeIc > 0 && network.AreaOf(country.Capital) is { } home)
		{
			foreach (int id in home.Provinces)
			{
				CProvince province = state.Provinces[id];
				if (province.Owner != country.Tag || !province.Cores.Contains(country.Tag))
				{
					continue;
				}
				long ic = province.Modifier[CIndustrialCapacity.ProvinceIc].Raw;
				if (ic <= 0)
				{
					continue;
				}
				// Its share is cut to a thousandth before it is taken of what was made.
				long share = (province.Modifier[CIndustrialCapacity.ProvinceLocalIc].Raw + Scale) * ic / Scale
					* Scale / country.CoreHomeIc;
				long part = Math.Min(share, Scale) * made.Raw / Scale;
				CSupplyPass.Add(province.Pool, goods, part);
				if (id != country.Capital)
				{
					CSupplyPass.Add(country.SentTo, goods, part);
				}
				left -= part;
			}
		}
		if (left > 0)
		{
			CSupplyPass.Add(stockpile, goods, left);
		}
	}
}
