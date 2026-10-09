using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// What a day's supply pass goes by, measured once before it begins: the network, the
/// order of the provinces, and for every province what it can pass, what its units ask
/// for and what a load loses on arriving. All in thousandths, by the province's number.
/// See <c>docs/mechanics/supply.md</c>, section 11.
/// </summary>
public sealed class CSupplyDay
{
	public const string PoolDays = "SUPPLYPOOL_DAYS";

	private CSupplyDay(CSupplyNetwork network, int provinces)
	{
		Network = network;
		Order = CSupplyOrder.FarthestFirst(network);
		Capacity = new long[provinces];
		Holders = new CCountry?[provinces];
		Supplies = new long[provinces];
		Fuel = new long[provinces];
		Loss = new long[provinces];
	}

	public CSupplyNetwork Network { get; }

	/// <summary>Every province's number, the furthest from its depot first.</summary>
	public int[] Order { get; }

	public long[] Capacity { get; }

	/// <summary>Who holds each province that takes part, and null for one that does not: the sea, and land nobody holds.</summary>
	public CCountry?[] Holders { get; }

	/// <summary>The supplies the units drawing on each province ask for in a day.</summary>
	public long[] Supplies { get; }

	public long[] Fuel { get; }

	/// <summary>What a load loses on arriving in each province: an amount, not a share.</summary>
	public long[] Loss { get; }

	/// <summary>How many days of what its units ask for a province tries to hold.</summary>
	public long Days { get; private init; }

	/// <summary>How many days' supplies a province holds for its units: the define, in whole days and never under one.</summary>
	public static long DaysHeld(CGameState state)
	{
		return Math.Max(1, state.Defines[CSupplyFigures.Military, PoolDays].Raw / CFixedPoint.Scale);
	}

	/// <summary>Whether the pass has anything to do with a province: land somebody holds.</summary>
	public static bool TakesPart(CProvince province)
	{
		return province.Id > 0 && !province.IsSea && province.Controller is not null;
	}

	public static CSupplyDay Measure(CGameState state, bool beforeGame = false)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var day = new CSupplyDay(network, state.Provinces.Count) { Days = DaysHeld(state) };
		foreach (CProvince province in state.Provinces)
		{
			if (TakesPart(province) && state.Country(province.Controller) is { } holder)
			{
				day.Holders[province.Id] = holder;
				day.Capacity[province.Id] = CSupplyFigures.Capacity(state, province).Raw;
				day.Loss[province.Id] = CSupplyFigures.Loss(state, province).Raw;
			}
		}
		CSupplyNeed.Count(state, beforeGame, day.Supplies, day.Fuel);
		return day;
	}
}
