using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// What a new game does before its first hour so that no army starts hungry: the day's
/// supply pass is run over and over, as many times as the furthest province is far and
/// twice more, every unit eating after each - and each time the stockpiles are put back
/// as they were, so the provinces are filled for nothing. See
/// <c>docs/mechanics/supply.md</c>, section 16.
/// </summary>
public static class CSupplyStart
{
	/// <summary>The furthest province is never counted as further than this.</summary>
	public const int MostDistanceCounted = 500;

	/// <summary>How many times the pass is run over what the furthest province is far by.</summary>
	private const int PassesOver = 2;

	public static void Fill(CGameState state)
	{
		CSupplyDay day = CSupplyDay.Measure(state, beforeGame: true);
		int passes = Math.Min(day.Network.MostDistance, MostDistanceCounted) + PassesOver;

		// What each unit asks for does not change from one pass to the next.
		var units = new List<(CCountry Owner, List<(CUnit Unit, long Supplies, long Fuel)> Units)>();
		foreach (CCountry country in state.Countries)
		{
			var asking = new List<(CUnit, long, long)>();
			foreach (CUnit unit in country.UnitsInOrder())
			{
				asking.Add((unit, CUnitSupply.SupplyConsumption(country, unit).Raw,
					CUnitSupply.FuelConsumption(country, unit).Raw));
			}
			units.Add((country, asking));
		}

		var kept = new CGoodsPool[state.Countries.Count];
		for (int i = 0; i < kept.Length; i++)
		{
			kept[i] = new CGoodsPool();
		}

		for (int pass = 0; pass < passes; pass++)
		{
			for (int i = 0; i < kept.Length; i++)
			{
				if (state.Stockpile(state.Countries[i]) is { } stockpile)
				{
					kept[i].CopyFrom(stockpile);
				}
			}

			CSupplyPass.Run(state, day);
			CSupplyBySea.Deliver(state, day.Network);

			for (int i = 0; i < units.Count; i++)
			{
				CCountry country = units[i].Owner;
				foreach ((CUnit unit, long supplies, long fuel) in units[i].Units)
				{
					CSupplyDraw.Draw(state, country, unit, supplies, fuel);
				}
				// A stockpile that is its own depot is put back; one that draws on
				// another's depot is left as the pass and the units made it.
				if (country.Capital != 0 && day.Network.DepotOf(country.Capital) == country.Capital
					&& state.Stockpile(country) is { } stockpile)
				{
					stockpile.CopyFrom(kept[i]);
				}
			}
		}

		// A unit that all of that left with nothing is given its days out of the stockpile.
		foreach ((CCountry country, List<(CUnit Unit, long Supplies, long Fuel)> asking) in units)
		{
			foreach ((CUnit unit, long _, long _) in asking)
			{
				if (unit.SuppliesReceived == CFixedPoint.Zero && unit.Location != 0)
				{
					CSupplyCarry.Bring(state, country, unit, from: 0, unit.Location, beforeGame: true);
				}
			}
		}

		// The counting of a day starts with the first day.
		CSupplyPass.ClearCounts(state);
	}
}
