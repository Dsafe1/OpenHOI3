using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// The sea between a stockpile and a depot that is no capital, <b>standing in for
/// convoys until they are built</b>: what such a depot was asked for today reaches it
/// out of its holder's stockpile, and what it holds too much of goes back, both without
/// ships and without limit. In the original convoys carry it, into ports, as much as
/// the ports and the ships allow. See <c>docs/mechanics/supply.md</c>, section 15.
/// </summary>
public static class CSupplyBySea
{
	private static readonly EGoods[] Carried = { EGoods.Supplies, EGoods.Fuel };

	/// <summary>After a pass: every depot that is no capital is sent what was asked of it today.</summary>
	public static void Deliver(CGameState state, CSupplyNetwork network)
	{
		foreach (int id in network.Depots)
		{
			CProvince depot = state.Provinces[id];
			if (state.Country(depot.Controller) is not { } holder || holder.Capital == id
				|| holder.Capital == 0 || state.Stockpile(holder) is not { } stockpile)
			{
				continue;
			}
			foreach (EGoods goods in Carried)
			{
				long sent = Math.Min(CSupplyPass.Held(depot.SupplyDrawn, goods), CSupplyPass.Held(stockpile, goods));
				if (sent > 0)
				{
					CSupplyPass.Add(stockpile, goods, -sent);
					CSupplyPass.Add(depot.Pool, goods, sent);
					CSupplyPass.Add(holder.UnitDrawn, goods, sent);
				}
			}
		}
	}

	/// <summary>During a pass: a depot that is no capital sends home what it holds too much of.</summary>
	public static void SendHome(CGameState state, CCountry holder, CProvince depot, long supplies, long fuel)
	{
		if (holder.Capital == 0 || holder.Capital == depot.Id || state.Stockpile(holder) is not { } stockpile)
		{
			return;
		}
		if (supplies > 0)
		{
			CSupplyPass.Add(depot.Pool, EGoods.Supplies, -supplies);
			CSupplyPass.Add(stockpile, EGoods.Supplies, supplies);
			CSupplyPass.Add(holder.SentBack, EGoods.Supplies, supplies);
		}
		if (fuel > 0)
		{
			CSupplyPass.Add(depot.Pool, EGoods.Fuel, -fuel);
			CSupplyPass.Add(stockpile, EGoods.Fuel, fuel);
			CSupplyPass.Add(holder.SentBack, EGoods.Fuel, fuel);
		}
	}
}
