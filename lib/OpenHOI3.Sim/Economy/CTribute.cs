using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// What a puppet pays its overlord each day: of money and the four resources, everything above a kept 2000. See
/// <c>docs/mechanics/stockpile.md</c>, section 7.
/// </summary>
public static class CTribute
{
	/// <summary>What a puppet keeps of each good [exe].</summary>
	private const long Kept = 2000 * CFixedPoint.Scale;

	private const int NoWay = -1;
	private const int Stockpile = 0;

	private static readonly EGoods[] Paid = { EGoods.Money, EGoods.CrudeOil, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials };

	public static void RunDaily(CGameState state)
	{
		ClearDaily(state);
		foreach (CCountry puppet in state.Countries)
		{
			RunCountry(state, puppet);
		}
	}

	/// <summary>Every country's ledger of the day's dues emptied, before the first country's day.</summary>
	public static void ClearDaily(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.TributeReceived.Clear();
			country.TributeSent.Clear();
		}
	}

	/// <summary>A puppet's dues to its overlord, in the puppet's own day.</summary>
	public static void RunCountry(CGameState state, CCountry puppet)
	{
		if (puppet.Overlord is { } tag && state.Country(tag) is { } overlord)
		{
			Pay(state, puppet, overlord);
		}
	}

	private static void Pay(CGameState state, CCountry puppet, CCountry overlord)
	{
		if (CCountryLists.OwnedCount(state, overlord.Tag) <= 0 || puppet.ActingCapital != puppet.Capital || puppet.InExile
			|| state.Stockpile(puppet) is not { } pool)
		{
			return;
		}
		int destination = Destination(state, puppet, overlord);
		if (destination == NoWay)
		{
			return;
		}
		foreach (EGoods goods in Paid)
		{
			long amount = pool[goods].Raw - Kept;
			if (amount <= 0)
			{
				continue;
			}
			var paid = new CFixedPoint(amount);
			puppet.TributeSent[goods] -= paid;
			pool[goods] -= paid;
			if (destination == Stockpile)
			{
				overlord.TributeReceived[goods] += paid;
				if (state.Stockpile(overlord) is { } theirs)
				{
					theirs[goods] += paid;
				}
			}
			else
			{
				state.Province(destination).Pool[goods] += paid;
			}
		}
	}

	/// <summary>
	/// Where it goes, along the neighbours of the puppet's home area: the overlord's stockpile for an area of the overlord's
	/// that holds its acting capital or has no harbour, which ends the walk, and otherwise that area's first harbour.
	/// </summary>
	private static int Destination(CGameState state, CCountry puppet, CCountry overlord)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (network.AreaOf(puppet.ActingCapital) is not { } home)
		{
			return NoWay;
		}
		CSupplyArea? capital = network.AreaOf(overlord.ActingCapital);
		int destination = NoWay;
		foreach (CSupplyArea area in home.Neighbours)
		{
			if (area.Controller != overlord.Tag)
			{
				continue;
			}
			if (area == capital || area.Ports.Count == 0)
			{
				return Stockpile;
			}
			destination = area.Ports[0];
		}
		return destination;
	}
}
