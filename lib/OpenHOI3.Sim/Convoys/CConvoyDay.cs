using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// A day of every country's convoys: early on each sails or has its broken way found
/// again, and one that has lost an end is taken away; late on the convoys a country
/// needs are made, those it does not are unmade, and its ships are shared out. See
/// <c>docs/mechanics/convoys.md</c>, section 10.
/// </summary>
public static class CConvoyDay
{
	/// <summary>Before the units draw: every convoy sails.</summary>
	public static void Sail(CGameState state)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CCountry country in state.Countries)
		{
			country.ConvoyedIn.Clear();
			country.ConvoyedOut.Clear();
		}
		foreach (CCountry country in state.Countries)
		{
			Sail(state, network, country);
		}
	}

	public static void Sail(CGameState state, CSupplyNetwork network, CCountry country)
	{
		foreach (CConvoy convoy in country.Convoys.ToArray())
		{
			if (convoy.Path.Count != 0)
			{
				if (!convoy.IsTrade && convoy.Path.Count > 1 && convoy.Transports != 0
					&& CConvoyRules.IsRouteUsable(state, network, convoy))
				{
					CConvoyDelivery.Run(state, network, convoy);
				}
				else if (convoy.Path.Count < 2 || CConvoyRules.IsPathBroken(state, convoy))
				{
					CConvoyMaker.FindPath(state, convoy);
				}
			}
			if (!convoy.IsLendLease && !convoy.IsTrade && HasLostAnEnd(state, country, convoy))
			{
				CConvoyMaker.Remove(country, convoy);
			}
		}
	}

	/// <summary>After the day's production: convoys are made and unmade, and the ships shared out.</summary>
	public static void Maintain(CGameState state)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CCountry country in state.Countries)
		{
			if (country.AutoSupplyConvoys)
			{
				CSupplyConvoys.Maintain(state, network, country);
			}
			if (country.AutoResourceConvoys)
			{
				CResourceConvoys.Maintain(state, network, country);
			}
			if (country.AutoMaintainConvoys)
			{
				CConvoyShips.Distribute(state, network, country);
			}
		}
	}

	/// <summary>
	/// Whether a convoy has no business any more: a run for resources whose loading
	/// harbour is neither the country's nor a puppet's, a supply convoy whose far
	/// harbour is not the country's, or either end in the hands of somebody at war with
	/// the country or of rebels.
	/// </summary>
	private static bool HasLostAnEnd(CGameState state, CCountry country, CConvoy convoy)
	{
		CProvince start = state.Province(convoy.Start);
		CProvince end = state.Province(convoy.End);
		if (convoy.IsSupply)
		{
			if (end.Controller != country.Tag)
			{
				return true;
			}
		}
		else if (!CConvoyRules.IsOwnersOrPuppets(state, start, country.Tag))
		{
			return true;
		}
		return CConvoyRules.IsHostileGround(state, end, country.Tag)
			|| CConvoyRules.IsHostileGround(state, start, country.Tag);
	}
}
