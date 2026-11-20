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
	/// <summary>What a lend-lease convoy regains of its own efficiency a day, in the <c>economy</c> defines.</summary>
	public const string LendLeaseRegain = "LL_CONVOY_EFF_REGAIN";

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
				// A cancelled trade's convoy, carrying money, delivers nothing on its way out (ours).
				if (!convoy.IsTrade && !convoy.Carries[(int)Economy.EGoods.Money] && convoy.Path.Count > 1 && convoy.Transports != 0
					&& CConvoyRules.IsRouteUsable(state, network, convoy))
				{
					CConvoyDelivery.Run(state, network, convoy);
				}
				else if (convoy.Path.Count < 2 || CConvoyRules.IsPathBroken(state, convoy))
				{
					CConvoyMaker.FindPath(state, convoy);
				}
			}
			// A lend-lease convoy regains LL_CONVOY_EFF_REGAIN a day of its own efficiency, to a whole (aid.md, section 3).
			if (convoy.IsLendLease && convoy.LendLeaseEfficiency < CFixedPoint.Scale)
			{
				convoy.LendLeaseEfficiency = System.Math.Min(CFixedPoint.Scale,
					convoy.LendLeaseEfficiency + state.Defines[Economy.CResources.EconomySection, LendLeaseRegain].Raw);
			}
			// A trade convoy is looked at in its owner's own day, before its trade (LookAtTradeConvoys).
			if (!convoy.IsTrade && !convoy.IsLendLease
				&& (convoy.Carries[(int)Economy.EGoods.Money] || HasLostAnEnd(state, country, convoy)))
			{
				CConvoyMaker.Remove(country, convoy);
			}
		}
	}

	/// <summary>
	/// A country's trade convoys looked at in its own day, after the countries before it have traded and before its own
	/// trade: one with no business any more is taken away, and its route then goes for want of it. The original's day runs
	/// a whole country before the next, so a route of an earlier country's is still paid on the day a later country's look
	/// takes its convoy away (<c>docs/mechanics/trade.md</c>, section 5; <c>reversing/findings/FINDINGS-liveread.md</c>,
	/// section 152).
	/// </summary>
	public static void LookAtTradeConvoys(CGameState state, CCountry country)
	{
		foreach (CConvoy convoy in country.Convoys.ToArray())
		{
			if (convoy.IsTrade && HasNoTrade(state, country, convoy))
			{
				CConvoyMaker.Remove(country, convoy);
			}
		}
	}

	/// <summary>After the day's production: convoys are made and unmade, and the ships shared out, every country's.</summary>
	public static void Maintain(CGameState state)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CCountry country in state.Countries)
		{
			MaintainCountry(state, network, country);
		}
	}

	/// <summary>
	/// One country's convoys made and unmade and its ships shared out, at the end of its own economy in the day - so a
	/// convoy given its ships here carries the trade of the countries after it that day, as in the original's day, which
	/// runs a whole country before the next (<c>docs/mechanics/convoys.md</c>, section 10;
	/// <c>reversing/findings/FINDINGS-liveread.md</c>, section 152).
	/// </summary>
	public static void MaintainCountry(CGameState state, CSupplyNetwork network, CCountry country)
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

	/// <summary>
	/// Whether a trade convoy has no business any more: no route serves it between its owner and whoever holds its far
	/// end - or its start, where the owner holds the far end - so an end the partner has lost to a third country takes the
	/// convoy away, and its route with it; or either end is in the hands of somebody at war with its owner or of rebels. A
	/// convoy carrying money that is no trade convoy - one whose route was cancelled - goes too. See
	/// <c>docs/mechanics/trade.md</c>, section 5.
	/// </summary>
	private static bool HasNoTrade(CGameState state, CCountry country, CConvoy convoy)
	{
		CProvince end = state.Province(convoy.End);
		CCountryTag? partner = end.Controller == country.Tag ? state.Province(convoy.Start).Controller : end.Controller;
		if (partner is not { } holder || !state.TradeRoutes.Exists(route => route.Convoy == convoy && route.Joins(country.Tag, holder)))
		{
			return true;
		}
		return CConvoyRules.IsHostileGround(state, state.Province(convoy.End), country.Tag)
			|| CConvoyRules.IsHostileGround(state, state.Province(convoy.Start), country.Tag);
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
