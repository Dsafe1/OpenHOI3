using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// Shares a country's transports and escorts out among its convoys. See
/// <c>docs/mechanics/convoys.md</c>, section 9:
///
///     a convoy that cannot sail, or has nothing to load, gives everything back;
///     supply convoys come first - the one made last before the others - then the runs
///     for resources by what they bring;
///     a convoy with fewer transports than it would have gives them all back and is
///     filled again in its turn;
///     the poorest runs give theirs up whenever a supply convoy is short.
/// </summary>
public static class CConvoyShips
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>A run for resources worth this much or more goes before every other.</summary>
	private const long Rich = 100 * Scale;

	/// <summary>One worth more than this is still among those served first.</summary>
	private const long Worthwhile = 20 * Scale;

	/// <summary>One worth more than this goes ahead of the rest of the last.</summary>
	private const long Something = 1 * Scale;

	/// <summary>A good counts double where the load is above this share of what the country takes in a day.</summary>
	private const long LargeShare = 100;

	/// <summary>And half where it is below this share.</summary>
	private const long SmallShare = 20;

	private const long Double = 2000;
	private const long Half = 500;

	private static readonly EGoods[] Weighed =
	{
		EGoods.Money, EGoods.CrudeOil, EGoods.Metal, EGoods.Energy, EGoods.RareMaterials,
	};

	public static void Distribute(CGameState state, CSupplyNetwork network, CCountry country)
	{
		var idle = new List<CConvoy>();
		var first = new LinkedList<CConvoy>();
		var last = new LinkedList<CConvoy>();
		int escortsOwned = country.Escorts;
		int escortsWanted = 0;
		int shortOf = 0;
		country.TransportsNeeded = 0;

		foreach (CConvoy convoy in country.Convoys)
		{
			escortsOwned += convoy.Escorts;
			if (convoy.Path.Count < 2 || !CConvoyRules.IsRouteUsable(state, network, convoy))
			{
				idle.Add(convoy);
				continue;
			}

			MeasureLoading(state, network, convoy);
			convoy.TransportsWanted = CConvoyRules.DesiredTransports(state, convoy);
			convoy.EscortsWanted = CConvoyRules.DesiredEscorts(state, convoy);
			if (convoy.IsSupply)
			{
				first.AddFirst(convoy);
				shortOf += Math.Max(convoy.TransportsWanted - convoy.Transports, 0);
			}
			else if (!CResourceConvoys.HoldsResources(convoy.LoadingProduction))
			{
				idle.Add(convoy);
				continue;
			}
			else
			{
				long worth = Worth(country, convoy);
				if (worth >= Rich)
				{
					first.AddFirst(convoy);
				}
				else if (worth > Worthwhile)
				{
					first.AddLast(convoy);
				}
				else if (worth > Something)
				{
					last.AddFirst(convoy);
				}
				else
				{
					last.AddLast(convoy);
				}
			}
			escortsWanted += convoy.EscortsWanted;
		}

		foreach (CConvoy convoy in idle)
		{
			country.Transports += convoy.Transports;
			country.Escorts += convoy.Escorts;
			convoy.Transports = 0;
			convoy.Escorts = 0;
		}

		shortOf -= country.Transports;
		for (LinkedListNode<CConvoy>? node = last.Last; node is not null; node = node.Previous)
		{
			CConvoy convoy = node.Value;
			int spare = convoy.Transports - convoy.TransportsWanted;
			if (shortOf > 0 || convoy.Transports < CConvoyRules.DesiredTransports(state, convoy))
			{
				spare = convoy.Transports;
			}
			if (spare > 0)
			{
				shortOf -= spare;
				GiveBack(country, convoy, spare);
			}
		}
		foreach (CConvoy convoy in first)
		{
			int spare = convoy.Transports - convoy.TransportsWanted;
			if (spare > 0)
			{
				GiveBack(country, convoy, spare);
			}
			// One under strength is emptied, to be filled again in its turn.
			if (convoy.Transports != 0 && convoy.Transports < CConvoyRules.DesiredTransports(state, convoy))
			{
				GiveBack(country, convoy, convoy.Transports);
			}
		}

		Fill(country, first);
		Fill(country, last);

		if (country.Convoys.Count == 0)
		{
			return;
		}
		long ratio = escortsWanted == 0
			? 0
			: Math.Min(escortsOwned * Scale * Scale / (escortsWanted * Scale), Scale);
		foreach (CConvoy convoy in country.Convoys)
		{
			int due = (int)((convoy.EscortsWanted * Scale * ratio / Scale - convoy.Escorts * Scale) / Scale);
			// More escorts go only to a convoy that was attacked in the last hundred days,
			// and nothing attacks one here yet.
			if (due < 0)
			{
				country.Escorts -= due;
				convoy.Escorts += due;
			}
			if (convoy.Transports == 0 && convoy.Escorts > 0)
			{
				country.Escorts += convoy.Escorts;
				convoy.Escorts = 0;
			}
		}
	}

	private static void Fill(CCountry country, LinkedList<CConvoy> convoys)
	{
		foreach (CConvoy convoy in convoys)
		{
			country.TransportsNeeded += convoy.TransportsWanted;
			int given = Math.Min(convoy.TransportsWanted - convoy.Transports, country.Transports);
			if (given > 0)
			{
				country.Transports -= given;
				convoy.Transports += given;
			}
		}
	}

	private static void GiveBack(CCountry country, CConvoy convoy, int transports)
	{
		country.Transports += transports;
		convoy.Transports -= transports;
	}

	/// <summary>What the land a convoy loads in yields in a day, all of it together.</summary>
	private static void MeasureLoading(CGameState state, CSupplyNetwork network, CConvoy convoy)
	{
		convoy.LoadingProduction.Clear();
		if (network.AreaOf(convoy.Start) is not { } area)
		{
			return;
		}
		foreach (int id in area.Provinces)
		{
			convoy.LoadingProduction.Add(state.Province(id).CurrentProducing);
		}
	}

	/// <summary>
	/// What a run for resources is worth: what it loads in a day, a good counting
	/// double where that is a large part of what the country takes in and half where it
	/// is next to nothing.
	/// </summary>
	private static long Worth(CCountry country, CConvoy convoy)
	{
		if (convoy.IsLendLease)
		{
			return 0;
		}
		long worth = 0;
		foreach (EGoods goods in Weighed)
		{
			long loads = convoy.LoadingProduction[goods].Raw;
			long compared = convoy.IsTrade ? convoy.Daily[goods].Raw : loads;
			long income = DailyIncome(country, goods);
			long weight = Scale;
			if (compared > income * LargeShare / Scale)
			{
				weight = Double;
			}
			if (compared < income * SmallShare / Scale)
			{
				weight = Half;
			}
			worth += loads * weight / Scale;
		}
		return worth;
	}

	/// <summary>
	/// What a country takes in of a good in a day: what it produces at home and what
	/// its convoys land. The original counts trade and what puppets send as well.
	/// </summary>
	private static long DailyIncome(CCountry country, EGoods goods)
	{
		return country.HomeProduced[goods].Raw + country.ConvoyedIn[goods].Raw + country.TradedFor[goods].Raw;
	}
}
