using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// Makes a convoy, gives it its way, and takes it away again. See
/// <c>docs/mechanics/convoys.md</c>, section 3.
/// </summary>
public static class CConvoyMaker
{
	/// <summary>The four goods a run for resources carries home.</summary>
	private static readonly EGoods[] Resources =
	{
		EGoods.CrudeOil, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	/// <summary>
	/// Makes a convoy of a country's between two harbours: for supplies and fuel, or for
	/// the four resources. One made while a game is being set up is manned for nothing -
	/// the transports it would have and one escort, out of no pool; one made in play
	/// starts empty and waits for the sharing out.
	/// </summary>
	public static CConvoy Create(CGameState state, CCountry country, int from, int to, bool forSupplies)
	{
		var convoy = new CConvoy
		{
			Id = ++state.LastConvoyId,
			Owner = country.Tag,
			Start = from,
			End = to,
			StartHour = state.TotalHours,
		};
		if (forSupplies)
		{
			convoy.Carries[(int)EGoods.Supplies] = true;
			convoy.Carries[(int)EGoods.Fuel] = true;
		}
		else
		{
			foreach (EGoods goods in Resources)
			{
				convoy.Carries[(int)goods] = true;
			}
		}
		FindPath(state, convoy);
		country.Convoys.Add(convoy);
		if (state.IsBeingMade)
		{
			convoy.Transports = CConvoyRules.DesiredTransports(state, convoy);
			convoy.Escorts = 1;
		}
		return convoy;
	}

	/// <summary>
	/// Makes a trade convoy of the buyer's between two harbours, carrying every good its route's terms
	/// move, money among them. It starts with no transports. See <c>docs/mechanics/trade.md</c>, section 5.
	/// </summary>
	public static CConvoy CreateTrade(CGameState state, CCountry buyer, int from, int to, bool[] carries)
	{
		var convoy = new CConvoy
		{
			Id = ++state.LastConvoyId,
			Owner = buyer.Tag,
			Start = from,
			End = to,
			StartHour = state.TotalHours,
			IsTrade = true,
		};
		carries.CopyTo(convoy.Carries, 0);
		FindPath(state, convoy);
		buyer.Convoys.Add(convoy);
		return convoy;
	}

	/// <summary>
	/// Makes a receiver's lend-lease convoy from a giver's harbour to its own; it carries nothing and starts
	/// with no transports. See <c>docs/mechanics/aid.md</c>, section 3.
	/// </summary>
	public static CConvoy CreateLendLease(CGameState state, CCountry receiver, CCountryTag giver, int from, int to)
	{
		var convoy = new CConvoy
		{
			Id = ++state.LastConvoyId,
			Owner = receiver.Tag,
			Start = from,
			End = to,
			StartHour = state.TotalHours,
			IsLendLease = true,
			LendLeaseFrom = giver,
		};
		FindPath(state, convoy);
		receiver.Convoys.Add(convoy);
		return convoy;
	}

	/// <summary>Throws a convoy's way away and looks for it again.</summary>
	public static void FindPath(CGameState state, CConvoy convoy)
	{
		convoy.Path.Clear();
		List<int>? path = CConvoyRoute.Find(state, convoy.Start, convoy.End, convoy.Owner);
		if (path is not null)
		{
			convoy.Path.AddRange(path);
		}
	}

	/// <summary>A convoy by its number, with the country it belongs to.</summary>
	public static (CCountry Owner, CConvoy Convoy)? Find(CGameState state, int id)
	{
		foreach (CCountry country in state.Countries)
		{
			foreach (CConvoy convoy in country.Convoys)
			{
				if (convoy.Id == id)
				{
					return (country, convoy);
				}
			}
		}
		return null;
	}

	/// <summary>Takes a convoy away. Its transports and its escorts go back to the country's free ones.</summary>
	public static void Remove(CCountry country, CConvoy convoy)
	{
		if (country.Convoys.Remove(convoy))
		{
			country.Transports += convoy.Transports;
			country.Escorts += convoy.Escorts;
			convoy.Transports = 0;
			convoy.Escorts = 0;
		}
	}
}
