using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// Makes and unmakes a country's convoys for what its land across the sea yields, and
/// says which harbour that yield waits in. See <c>docs/mechanics/convoys.md</c>,
/// section 8.
/// </summary>
public static class CResourceConvoys
{
	private static readonly EGoods[] Resources =
	{
		EGoods.CrudeOil, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	public static void Maintain(CGameState state, CSupplyNetwork network, CCountry country)
	{
		List<CSupplyArea> areas = CSupplyConvoys.AreasOf(network, country);
		if (areas.Count == 1 && country.Convoys.Count == 0)
		{
			return;
		}
		if (network.AreaOf(country.ActingCapital) is not { } home)
		{
			return;
		}

		foreach (CSupplyArea area in areas)
		{
			if (area == home || area.Ports.Count == 0)
			{
				continue;
			}
			CProvince port = state.Province(area.Ports[0]);
			// The harbour's pool is also the stockpile of a country whose capital it is,
			// so a ship is sent where that is all that lies there.
			if (!HoldsResources(port.Pool) || HasResourceConvoyFrom(network, country, area))
			{
				continue;
			}
			CProvince to = state.Province(CConvoyRules.NearestPort(state, home, port.Id));
			if (CConvoyRules.IsHarbour(state, port) && CConvoyRules.IsHarbour(state, to))
			{
				CConvoyMaker.Create(state, country, port.Id, to.Id, forSupplies: false);
			}
		}

		// A puppet across the sea is fetched from as well, where none of its master's
		// land touches the land its capital stands on.
		foreach (CCountry puppet in state.Countries)
		{
			if (puppet.Overlord != country.Tag || network.AreaOf(puppet.ActingCapital) is not { } theirs
				|| HasAnyConvoyFrom(state, network, theirs) || TouchesLandOf(theirs, country))
			{
				continue;
			}
			if (theirs.Ports.Count == 0)
			{
				continue;
			}
			CProvince port = state.Province(theirs.Ports[0]);
			CProvince to = state.Province(CConvoyRules.NearestPort(state, home, port.Id));
			if (CConvoyRules.IsHarbour(state, port) && CConvoyRules.IsHarbour(state, to))
			{
				CConvoyMaker.Create(state, country, port.Id, to.Id, forSupplies: false);
			}
		}

		foreach (CConvoy convoy in country.Convoys.ToArray())
		{
			if (convoy.IsTrade || convoy.IsLendLease || convoy.IsSupply)
			{
				continue;
			}
			CProvince start = state.Province(convoy.Start);
			CProvince end = state.Province(convoy.End);
			if (network.AreaOf(convoy.Start) == network.AreaOf(convoy.End)
				|| !IsFriend(state, country, start) || !IsFriend(state, country, end))
			{
				CConvoyMaker.Remove(country, convoy);
			}
		}
	}

	/// <summary>
	/// The harbour the yield of a block of land waits in for its ship: where a convoy
	/// of the country's for resources already loads in the block, and otherwise the
	/// block's first harbour. 0 where it has none.
	/// </summary>
	public static int LoadingPort(CSupplyNetwork network, CCountry country, CSupplyArea area)
	{
		foreach (CConvoy convoy in country.Convoys)
		{
			if (!convoy.IsSupply && convoy.Start != 0 && network.AreaOf(convoy.Start) == area)
			{
				return convoy.Start;
			}
		}
		return area.Ports.Count > 0 ? area.Ports[0] : 0;
	}

	public static bool HoldsResources(CGoodsPool pool)
	{
		foreach (EGoods goods in Resources)
		{
			if (pool[goods] != CFixedPoint.Zero)
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasResourceConvoyFrom(CSupplyNetwork network, CCountry country, CSupplyArea area)
	{
		foreach (CConvoy convoy in country.Convoys)
		{
			if (!convoy.IsSupply && network.AreaOf(convoy.Start) == area)
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasAnyConvoyFrom(CGameState state, CSupplyNetwork network, CSupplyArea area)
	{
		foreach (CCountry owner in state.Countries)
		{
			foreach (CConvoy convoy in owner.Convoys)
			{
				if (network.AreaOf(convoy.Start) == area)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool TouchesLandOf(CSupplyArea area, CCountry country)
	{
		foreach (CSupplyArea neighbour in area.Neighbours)
		{
			if (neighbour.Controller == country.Tag)
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsFriend(CGameState state, CCountry country, CProvince province)
	{
		return state.Country(province.Controller) is { } holder && CSupplyRelations.IsFriendly(state, country, holder);
	}
}
