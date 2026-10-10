using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;

namespace OpenHOI3.Sim.Convoys;

/// <summary>
/// One sailing of a convoy: what it loads, the limits on that, and where it lands. See
/// <c>docs/mechanics/convoys.md</c>, section 6.
/// </summary>
public static class CConvoyDelivery
{
	private const long Scale = CFixedPoint.Scale;

	/// <summary>What a puppet keeps of each good when its master's convoy loads from its stockpile.</summary>
	private const long PuppetKeeps = 1000 * Scale;

	/// <summary>No more of a good than this is loaded from a puppet.</summary>
	private const long MostFromPuppet = 99999 * Scale;

	/// <summary>
	/// Sails a convoy once. Nothing moves where it has no way, no transports, or its
	/// route is not usable.
	/// </summary>
	public static void Run(CGameState state, CSupplyNetwork network, CConvoy convoy)
	{
		Sail(state, network, convoy);
		// The day after a raid sank a ship of it, a convoy looks for its way afresh - and
		// the water it was attacked in is dear now.
		if (convoy.LastAttackHour != CConvoy.NeverAttacked
			&& state.TotalHours / CGameState.HoursPerDay - convoy.LastAttackHour / CGameState.HoursPerDay == 1)
		{
			CConvoyMaker.FindPath(state, convoy);
		}
	}

	private static void Sail(CGameState state, CSupplyNetwork network, CConvoy convoy)
	{
		convoy.Daily.Clear();
		if (convoy.Path.Count < 2 || convoy.Transports == 0 || convoy.IsLendLease
			|| !CConvoyRules.IsRouteUsable(state, network, convoy))
		{
			return;
		}

		CProvince start = state.Province(convoy.Start);
		CProvince end = state.Province(convoy.End);
		if (state.Country(start.Controller) is not { } loader)
		{
			return;
		}
		bool fromPuppet = loader.Overlord == convoy.Owner;
		CSupplyArea? home = network.AreaOf(loader.Capital);
		bool fromHome = network.AreaOf(convoy.Start) == home;

		// From the land its capital stands on a country loads out of its stockpile; from
		// anywhere else, out of what lies in the harbour.
		CGoodsPool? source = fromHome ? state.Stockpile(loader) : start.Pool;
		if (source is null)
		{
			return;
		}
		var load = new long[CGoodsPool.Count];
		for (int goods = 0; goods < load.Length; goods++)
		{
			load[goods] = source[(EGoods)goods].Raw;
			if (fromHome && fromPuppet)
			{
				load[goods] = Math.Clamp(load[goods] - PuppetKeeps, 0, MostFromPuppet);
			}
		}

		long efficiency = CConvoyRules.Efficiency(state, convoy);
		for (int goods = 0; goods < load.Length; goods++)
		{
			load[goods] = load[goods] * efficiency / Scale;
		}
		if (fromHome)
		{
			long landed = CConvoyRules.PortCapacity(state, end);
			for (int goods = 0; goods < load.Length; goods++)
			{
				load[goods] = Math.Clamp(load[goods], 0, landed);
			}
		}

		if (start.Controller != convoy.Owner && !fromPuppet)
		{
			return;
		}
		for (int goods = 0; goods < load.Length; goods++)
		{
			if (!convoy.Carries[goods])
			{
				load[goods] = 0;
			}
		}

		// Supplies and fuel only as far as the land they go to has room for them.
		(long roomForSupplies, long roomForFuel) = Room(state, network, loader, end);
		if (convoy.Carries[(int)EGoods.Supplies])
		{
			load[(int)EGoods.Supplies] = Math.Min(load[(int)EGoods.Supplies], Math.Max(roomForSupplies, 0));
		}
		if (convoy.Carries[(int)EGoods.Fuel])
		{
			load[(int)EGoods.Fuel] = Math.Min(load[(int)EGoods.Fuel], Math.Max(roomForFuel, 0));
		}

		bool crossing = network.AreaOf(convoy.Start) != network.AreaOf(convoy.End);
		CCountry? receiver = state.Country(end.Controller);
		// What is loaded at home lands in the harbour; what is loaded across the sea
		// lands in the stockpile of whoever holds the harbour it comes to.
		CGoodsPool? landing = fromHome ? end.Pool : receiver is null ? null : state.Stockpile(receiver);
		for (int goods = 0; goods < load.Length; goods++)
		{
			long amount = load[goods];
			var kind = (EGoods)goods;
			Add(source, kind, -amount);
			if (landing is not null)
			{
				Add(landing, kind, amount);
			}
			if (fromHome && crossing)
			{
				Add(loader.ConvoyedOut, kind, amount);
			}
			if (receiver is not null && (!fromHome || crossing))
			{
				Add(receiver.ConvoyedIn, kind, amount);
			}
			Add(end.SupplyThroughput, kind, amount);
			convoy.Daily[kind] = new CFixedPoint(amount);
		}
	}

	/// <summary>
	/// How much supplies and fuel the land around a harbour has room for: so many days
	/// of what was asked of its harbours today, less what lies in them - the harbours of
	/// every block of land that shares its depot. A harbour of the loading country's own
	/// home land that is no depot takes what it can land instead.
	/// </summary>
	public static (long Supplies, long Fuel) Room(CGameState state, CSupplyNetwork network, CCountry loader,
		CProvince end)
	{
		CSupplyArea? area = network.AreaOf(end.Id);
		long supplies = 0;
		long fuel = 0;
		if (area?.Group is { } group)
		{
			long askedSupplies = 0;
			long askedFuel = 0;
			long lyingSupplies = 0;
			long lyingFuel = 0;
			foreach (CSupplyArea sharing in group.Areas)
			{
				foreach (int id in sharing.Ports)
				{
					CProvince port = state.Province(id);
					askedSupplies += port.SupplyDrawn[EGoods.Supplies].Raw;
					askedFuel += port.SupplyDrawn[EGoods.Fuel].Raw;
					lyingSupplies += port.Pool[EGoods.Supplies].Raw;
					lyingFuel += port.Pool[EGoods.Fuel].Raw;
				}
			}
			long days = CSupplyDay.DaysHeld(state) * Scale;
			supplies = askedSupplies * days / Scale - lyingSupplies;
			fuel = askedFuel * days / Scale - lyingFuel;
		}
		if (area is not null && end.Id != network.DepotOf(end.Id) && area == network.AreaOf(loader.Capital))
		{
			supplies = fuel = CConvoyRules.PortCapacity(state, end);
		}
		return (supplies, fuel);
	}

	private static void Add(CGoodsPool pool, EGoods goods, long amount)
	{
		pool[goods] = new CFixedPoint(pool[goods].Raw + amount);
	}
}
