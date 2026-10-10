using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// Makes and unmakes a country's convoys of supplies and fuel, to every block of its
/// land that the sea cuts off from its capital. See <c>docs/mechanics/convoys.md</c>,
/// section 7:
///
///     what a block needs is what was asked of its depot today, less what the harbours
///     its convoys end in can land;
///     while it needs more - or, at peace, while no convoy runs to the depot itself -
///     the harbour with no convoy yet that scores highest gets one: the depot first,
///     then the one furthest from it;
///     in play a convoy four days old goes again once its end is lost, nothing is asked
///     of the depot, or the block has harbour to spare of twice its own.
/// </summary>
public static class CSupplyConvoys
{
	/// <summary>What the depot itself scores as a harbour to send to, in thousandths.</summary>
	private const long DepotScore = 100000 * CFixedPoint.Scale;

	/// <summary>A convoy is not taken away before it is this many days old.</summary>
	private const int LeastDays = 4;

	/// <summary>How many times a harbour's capacity a block has to have to spare before the harbour's convoy goes.</summary>
	private const long SpareHarbours = 2;

	public static void Maintain(CGameState state, CSupplyNetwork network, CCountry country)
	{
		List<CSupplyArea> areas = AreasOf(network, country);
		if (areas.Count == 1 && country.Convoys.Count == 0)
		{
			return;
		}
		if (network.AreaOf(country.Capital) is not { } home)
		{
			return;
		}

		// What each block still needs once its convoys are counted; below nothing is room to spare.
		var stillNeeded = new Dictionary<CSupplyArea, long>();
		foreach (CSupplyArea area in areas)
		{
			if (area == home || area.Provinces.Count == 0)
			{
				continue;
			}
			CProvince depot = state.Province(DepotOrPort(state, network, state.Province(area.Provinces[0])));
			if (depot.Id == 0 || depot.IsSea)
			{
				continue;
			}

			long need = Math.Max(depot.SupplyDrawn[EGoods.Supplies].Raw, depot.SupplyDrawn[EGoods.Fuel].Raw);
			bool reachesDepot = false;
			if (need > 0)
			{
				foreach (CConvoy convoy in country.Convoys)
				{
					if (convoy.IsSupply && network.AreaOf(convoy.End) == area)
					{
						need -= CConvoyRules.PortCapacity(state, state.Province(convoy.End));
						reachesDepot |= convoy.End == depot.Id;
					}
				}
			}

			// At peace this goes on until every harbour has its convoy: nothing in the
			// loop says a convoy now reaches the depot.
			while (need > 0 || (!reachesDepot && !state.IsAtWar(country.Tag)))
			{
				long best = 0;
				int harbour = 0;
				foreach (int port in area.Ports)
				{
					if (HasSupplyConvoyTo(country, port))
					{
						continue;
					}
					long score = port == depot.Id ? DepotScore : network.HopsOf(port) * CFixedPoint.Scale;
					if (score > best)
					{
						best = score;
						harbour = port;
					}
				}
				if (harbour == 0)
				{
					break;
				}
				int from = CConvoyRules.NearestPort(state, home, harbour);
				if (!CConvoyRules.IsHarbour(state, state.Province(from)))
				{
					break;
				}
				CConvoyMaker.Create(state, country, from, harbour, forSupplies: true);
				need -= CConvoyRules.PortCapacity(state, state.Province(harbour));
			}
			stillNeeded[area] = need;
		}

		foreach (CConvoy convoy in country.Convoys.ToArray())
		{
			if (!convoy.IsSupply || (state.TotalHours - convoy.StartHour) / CGameState.HoursPerDay < LeastDays)
			{
				continue;
			}
			if (!IsStillWanted(state, network, country, home, convoy, stillNeeded))
			{
				CConvoyMaker.Remove(country, convoy);
			}
		}
	}

	private static bool IsStillWanted(CGameState state, CSupplyNetwork network, CCountry country, CSupplyArea home,
		CConvoy convoy, Dictionary<CSupplyArea, long> stillNeeded)
	{
		CProvince end = state.Province(convoy.End);
		CSupplyArea? endArea = network.AreaOf(convoy.End);
		CSupplyArea? startArea = network.AreaOf(convoy.Start);
		int depotId = DepotOrPort(state, network, end);
		CProvince depot = state.Province(depotId);
		if (end.Controller != country.Tag
			|| (depot.SupplyDrawn[EGoods.Supplies].Raw <= 0 && depot.SupplyDrawn[EGoods.Fuel].Raw <= 0)
			|| (endArea != home && endArea == startArea)
			|| startArea != home)
		{
			return false;
		}
		if (end.Id == depotId)
		{
			return true;
		}
		long need = endArea is not null && stillNeeded.TryGetValue(endArea, out long counted) ? counted : 0;
		return need >= 0 || -need < SpareHarbours * CConvoyRules.PortCapacity(state, end);
	}

	/// <summary>
	/// The province a block's need is counted at: the depot of one of its provinces
	/// where the province's holder holds that too, and otherwise the first harbour of
	/// the block the province is in - or none.
	/// </summary>
	public static int DepotOrPort(CGameState state, CSupplyNetwork network, CProvince province)
	{
		int depot = network.DepotOf(province.Id);
		if (state.Province(depot).Controller == province.Controller)
		{
			return depot;
		}
		return network.AreaOf(province.Id) is { Ports.Count: > 0 } area ? area.Ports[0] : 0;
	}

	/// <summary>A country's blocks of land, in the order the network made them.</summary>
	public static List<CSupplyArea> AreasOf(CSupplyNetwork network, CCountry country)
	{
		var areas = new List<CSupplyArea>();
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller == country.Tag)
			{
				areas.Add(area);
			}
		}
		return areas;
	}

	private static bool HasSupplyConvoyTo(CCountry country, int port)
	{
		foreach (CConvoy convoy in country.Convoys)
		{
			if (convoy.IsSupply && convoy.End == port)
			{
				return true;
			}
		}
		return false;
	}
}
