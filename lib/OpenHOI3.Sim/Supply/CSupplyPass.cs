using System;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// The day's carrying of supplies and fuel: out from each depot province by province to
/// where units ask for them, and back in from where more is lying than is asked for.
/// See <c>docs/mechanics/supply.md</c>, sections 11 and 12.
///
/// It is written to do what the original does step for step, its oddities with it,
/// because who goes short when a road is full is decided by nothing else.
/// </summary>
public static class CSupplyPass
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>A need under this is taken for this much where the province holds little.</summary>
	private const long LeastNeed = 1000;

	/// <summary>What "holds little" is.</summary>
	private const long ThinPool = 5000;

	/// <summary>A province asks for no more than this many days of its need in a day.</summary>
	private const long MostWanted = 2000;

	/// <summary>
	/// The supply part of a day, which comes before any country's own day: the day's
	/// counts start from nothing and the provinces pass supplies on. What a depot across
	/// the sea is short of comes to its harbours by convoy, later in the day.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		ClearCounts(state);
		CSupplyDay day = CSupplyDay.Measure(state);
		Run(state, day);
	}

	/// <summary>What every country counts afresh each day.</summary>
	public static void ClearCounts(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			country.UnitDemand.Clear();
			country.UnitDrawn.Clear();
			country.SentTo.Clear();
			country.SentBack.Clear();
			country.TradedAway.Clear();
			country.TradedFor.Clear();
			country.TradedAwaySansAlliedSupply.Clear();
			country.TradedForSansAlliedSupply.Clear();
			// Ours: whether the original clears these with the rest was not established (aid.md).
			country.RepaidAway = CFixedPoint.Zero;
			country.IncomeFromDebt = CFixedPoint.Zero;
		}
	}

	/// <summary>One pass over the provinces, by figures measured beforehand.</summary>
	public static void Run(CGameState state, CSupplyDay day)
	{
		foreach (CProvince province in state.Provinces)
		{
			province.BeginSupplyDay();
		}
		Pull(state, day);
		SendBack(state, day);
	}

	/// <summary>
	/// The furthest first: each province works out what it is short of, takes what the
	/// provinces nearer its depot will give, and leaves what it could not get as a
	/// demand on them - so that by the time the pass reaches a depot's neighbours,
	/// every want in the network has been passed in to them.
	/// </summary>
	private static void Pull(CGameState state, CSupplyDay day)
	{
		CSupplyNetwork network = day.Network;
		foreach (int id in day.Order)
		{
			CProvince province = state.Provinces[id];
			if (day.Holders[id] is not { } holder)
			{
				continue;
			}

			// A stockpile is where supplies come from, not somewhere they are sent.
			if (holder.Capital == id)
			{
				province.LastPool.CopyFrom(province.Pool);
				continue;
			}

			long supplies = day.Supplies[id];
			long fuel = day.Fuel[id];
			if (supplies > 0 && supplies < LeastNeed && Held(province.Pool, EGoods.Supplies) < ThinPool)
			{
				supplies = LeastNeed;
			}
			if (fuel > 0 && fuel < LeastNeed && Held(province.Pool, EGoods.Fuel) < ThinPool)
			{
				fuel = LeastNeed;
			}
			province.SupplyNeed[EGoods.Supplies] = new CFixedPoint(supplies);
			province.SupplyNeed[EGoods.Fuel] = new CFixedPoint(fuel);

			Add(province.SupplyDrawn, EGoods.Supplies, Wanted(day.Days, supplies, province, EGoods.Supplies));
			Add(province.SupplyDrawn, EGoods.Fuel, Wanted(day.Days, fuel, province, EGoods.Fuel));

			// What is asked of it by now: its own want and all that was passed in to it.
			long wantedSupplies = Held(province.SupplyDrawn, EGoods.Supplies);
			long wantedFuel = Held(province.SupplyDrawn, EGoods.Fuel);
			if (wantedSupplies < 1 && wantedFuel < 1)
			{
				continue;
			}

			// What a load loses on arriving is asked for on top.
			long loss = day.Loss[id];
			if (wantedSupplies > 0)
			{
				wantedSupplies += loss;
			}
			if (wantedFuel > 0)
			{
				wantedFuel += loss;
			}

			int depot = network.DepotOf(id);
			int distance = network.DistanceOf(id);

			// First, goods: from each province nearer the depot in turn.
			int nearer = 0;
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (!IsNearer(network, edge.To, depot, distance))
				{
					continue;
				}
				nearer++;
				if (wantedSupplies < 1 && wantedFuel < 1)
				{
					break;
				}
				CProvince from = state.Provinces[edge.To];
				CCountry? stockpileOf = day.Holders[edge.To] is { } fromHolder && fromHolder.Capital == edge.To
					? fromHolder
					: null;
				long capacity = day.Capacity[edge.To];
				wantedSupplies -= Take(province, from, stockpileOf, wantedSupplies, capacity, loss, EGoods.Supplies);
				wantedFuel -= Take(province, from, stockpileOf, wantedFuel, capacity, loss, EGoods.Fuel);
			}

			// Then what is still wanted is asked of them in equal shares, each as far as
			// it has room to pass more today. A share once cut stays cut for the
			// provinces after, and a province asked for more than it can pass throws the
			// difference back: both are the original's.
			long divisor = nearer * Scale;
			long shareOfSupplies = divisor == 0 ? -1 : wantedSupplies * Scale / divisor;
			long shareOfFuel = divisor == 0 ? -1 : wantedFuel * Scale / divisor;
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (!IsNearer(network, edge.To, depot, distance))
				{
					continue;
				}
				CGoodsPool asked = state.Provinces[edge.To].SupplyDrawn;
				shareOfFuel = Math.Min(shareOfFuel, day.Capacity[edge.To] - Held(asked, EGoods.Fuel));
				shareOfSupplies = Math.Min(shareOfSupplies, day.Capacity[edge.To] - Held(asked, EGoods.Supplies));
				if (shareOfSupplies > 0)
				{
					Add(asked, EGoods.Supplies, shareOfSupplies);
				}
				if (shareOfFuel > 0)
				{
					Add(asked, EGoods.Fuel, shareOfFuel);
				}
				wantedFuel -= shareOfFuel;
				wantedSupplies -= shareOfSupplies;
			}

			// And what is left after that is asked of them one after another, each up to
			// all it can pass in a day.
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (network.DepotOf(edge.To) != depot)
				{
					continue;
				}
				if (wantedSupplies < 1 && wantedFuel < 1)
				{
					break;
				}
				if (network.DistanceOf(edge.To) >= distance)
				{
					continue;
				}
				CGoodsPool asked = state.Provinces[edge.To].SupplyDrawn;
				long ofSupplies = Math.Min(wantedSupplies, day.Capacity[edge.To]);
				long ofFuel = Math.Min(wantedFuel, day.Capacity[edge.To]);
				if (ofSupplies > 0)
				{
					Add(asked, EGoods.Supplies, ofSupplies);
				}
				if (ofFuel > 0)
				{
					Add(asked, EGoods.Fuel, ofFuel);
				}
				wantedSupplies -= ofSupplies;
				wantedFuel -= ofFuel;
			}

			province.LastPool.CopyFrom(province.Pool);
		}
	}

	/// <summary>
	/// What a province wants today of one of the two: enough to hold so many days of
	/// what its units ask for, less what it holds and what it makes itself, and never
	/// more than two days' worth at once.
	/// </summary>
	private static long Wanted(long days, long need, CProvince province, EGoods goods)
	{
		long made = Math.Min(need, Held(province.CurrentProducing, goods));
		long wanted = days * Scale * need / Scale - Held(province.Pool, goods) - made;
		return Math.Min(Math.Max(wanted, 0), MostWanted * need / Scale);
	}

	private static bool IsNearer(CSupplyNetwork network, int province, int depot, int distance)
	{
		return network.DepotOf(province) == depot && network.DistanceOf(province) < distance;
	}

	/// <summary>
	/// Takes what one nearer province gives of one of the two, and answers how much of
	/// what was wanted that was. It gives of what it held when the pass last came to it
	/// - not of what has reached it since - up to what it can pass in a day; less, by
	/// this province's share, where more was asked of it yesterday than it passed; and
	/// never more than it holds now. What arrives is less by the loss.
	/// </summary>
	private static long Take(CProvince into, CProvince from, CCountry? stockpileOf, long wanted, long capacity,
		long loss, EGoods goods)
	{
		long offered = Math.Min(Held(from.LastPool, goods), capacity);
		long askedYesterday = Held(from.LastSupplyDrawn, goods);
		long passedYesterday = Held(from.LastSupplyThroughput, goods);
		// For fuel the original asks whether anything is asked of it today, where for
		// supplies it asks about yesterday. It is kept.
		long asked = goods == EGoods.Fuel ? Held(from.SupplyDrawn, goods) : askedYesterday;
		if (asked > 0 && passedYesterday > 0 && passedYesterday < askedYesterday)
		{
			long share = Math.Min(wanted * Scale / askedYesterday, Scale);
			offered = share * offered / Scale;
		}
		offered = Math.Min(offered, Held(from.Pool, goods));

		long taken = Math.Min(wanted, offered);
		if (wanted <= 0 || taken <= 0)
		{
			return 0;
		}
		if (stockpileOf is not null)
		{
			Add(stockpileOf.UnitDrawn, goods, taken);
		}
		Add(from.SupplyDrawn, goods, taken);
		Add(from.SupplyThroughput, goods, taken);
		Add(into.Pool, goods, Math.Max(taken - loss, 0));
		Add(from.Pool, goods, -taken);
		return taken;
	}

	/// <summary>
	/// The nearest first: a province holding more than its days' worth and what was
	/// asked of it today sends all of the rest one province in, with no limit - and a
	/// depot that is no capital sends it home over the sea, as far as its convoys carry.
	/// </summary>
	private static void SendBack(CGameState state, CSupplyDay day)
	{
		CSupplyNetwork network = day.Network;
		for (int i = day.Order.Length - 1; i >= 0; i--)
		{
			int id = day.Order[i];
			CProvince province = state.Provinces[id];
			if (day.Holders[id] is not { } holder)
			{
				continue;
			}
			int distance = network.DistanceOf(id);
			if (distance == 0 && holder.Capital == id)
			{
				continue;
			}

			long heldSupplies = Held(province.Pool, EGoods.Supplies);
			long heldFuel = Held(province.Pool, EGoods.Fuel);
			long askedSupplies = Held(province.SupplyDrawn, EGoods.Supplies);
			long askedFuel = Held(province.SupplyDrawn, EGoods.Fuel);
			// Short of both, or empty of both, it keeps what it has.
			if ((askedSupplies > 0 && heldSupplies < askedSupplies && askedFuel > 0 && heldFuel < askedFuel)
				|| (heldSupplies < 1 && heldFuel < 1))
			{
				continue;
			}

			long spareSupplies = heldSupplies - (askedSupplies + day.Days * Scale * day.Supplies[id] / Scale);
			long spareFuel = heldFuel - (askedFuel + day.Days * Scale * day.Fuel[id] / Scale);
			if (spareSupplies < 1 && spareFuel < 1)
			{
				continue;
			}

			if (distance == 0)
			{
				// A depot that is no capital sends it home over the sea, as far as the
				// convoys that end at it can carry.
				if (network.IsDepot(id) && holder.Capital != 0 && state.Stockpile(holder) is not null)
				{
					long carried = CConvoyRules.HomewardCapacity(state, network, holder, province);
					CProvince capital = state.Provinces[holder.Capital];
					Move(province, capital, holder, Math.Min(spareSupplies, carried), EGoods.Supplies);
					Move(province, capital, holder, Math.Min(spareFuel, carried), EGoods.Fuel);
				}
				continue;
			}

			int depot = network.DepotOf(id);
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (!IsNearer(network, edge.To, depot, distance))
				{
					continue;
				}
				CProvince inward = state.Provinces[edge.To];
				CCountry? home = day.Holders[edge.To] is { } inwardHolder && inwardHolder.Capital == edge.To
					? inwardHolder
					: null;
				Move(province, inward, home, spareSupplies, EGoods.Supplies);
				Move(province, inward, home, spareFuel, EGoods.Fuel);
				break;
			}
		}
	}

	private static void Move(CProvince from, CProvince to, CCountry? home, long amount, EGoods goods)
	{
		if (amount <= 0)
		{
			return;
		}
		Add(from.Pool, goods, -amount);
		Add(to.Pool, goods, amount);
		if (home is not null)
		{
			Add(home.SentBack, goods, amount);
		}
	}

	internal static long Held(CGoodsPool pool, EGoods goods)
	{
		return pool[goods].Raw;
	}

	internal static void Add(CGoodsPool pool, EGoods goods, long amount)
	{
		pool[goods] = new CFixedPoint(pool[goods].Raw + amount);
	}
}
