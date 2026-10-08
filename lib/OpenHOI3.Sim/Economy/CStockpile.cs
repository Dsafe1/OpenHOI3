using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// What countries hold: what a stockpile is filled with when a game begins, and what a
/// day adds to it. See <c>docs/mechanics/stockpile.md</c>.
///
/// Supplies and fuel start at nothing here: the original gives each country as many
/// days of what its units use, and there are no units yet.
/// </summary>
public static class CStockpile
{
	/// <summary>The days every country starts with, whatever else is true of it.</summary>
	public const int DaysAlways = 30;

	/// <summary>The days added for each month of the year a country has not spent at war.</summary>
	public const int DaysPerMonthOfPeace = 5;

	/// <summary>The months counted: a year, and never fewer than two however long the war.</summary>
	public const int MonthsCounted = 12;
	public const int LeastMonthsCounted = 2;

	/// <summary>
	/// The fraction of what the stockpile's IC-days would earn that is given as money, in
	/// thousandths. It is built into the original.
	/// </summary>
	private const long MoneyShare = 50;

	/// <summary>
	/// How many whole months the country has been in the oldest war it is still
	/// fighting, by the calendar: October to January is three. Nothing for a country at peace.
	/// </summary>
	public static int MonthsAtWar(CGameState state, CCountry country)
	{
		CDate now = state.Date;
		CDate earliest = now;
		foreach (CWar war in state.Wars)
		{
			// A war nobody dated is taken to have just begun.
			if (war.Involves(country.Tag) && war.Started.IsValid && war.Started.CompareTo(earliest) < 0)
			{
				earliest = war.Started;
			}
		}
		return (now.Year * 12 + now.Month) - (earliest.Year * 12 + earliest.Month);
	}

	/// <summary>
	/// How many days of its industry's needs a country starts with: ninety at peace, five
	/// fewer for each month it has been at war, and never fewer than forty.
	/// </summary>
	public static int DaysAtStart(CGameState state, CCountry country)
	{
		int months = Math.Max(LeastMonthsCounted, MonthsCounted - MonthsAtWar(state, country));
		return DaysAlways + DaysPerMonthOfPeace * months;
	}

	/// <summary>
	/// Fills every stockpile for the first day of a new game.
	/// </summary>
	/// <param name="fromHistory">What each country's history file gives it on the start date, by tag.</param>
	public static void FillAtStart(CGameState state, IReadOnlyDictionary<string, CGoodsPool>? fromHistory = null)
	{
		// It is the base that counts, which no law or minister changes.
		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		// What the history files give comes first, because it decides who gets money.
		if (fromHistory is not null)
		{
			foreach (CCountry country in state.Countries)
			{
				if (fromHistory.TryGetValue(country.Tag.Value, out CGoodsPool? given))
				{
					state.Stockpile(country)?.Add(given);
				}
			}
		}

		long icToMoney = state.Defines[CResources.EconomySection, CResources.IcToMoney].Raw;

		// In the order the countries are declared, which matters where two share a capital.
		foreach (CCountry country in state.Countries)
		{
			// The capital's pool, whoever holds the province: a country that does not exist
			// yet is given its share all the same, in somebody else's province.
			if (state.Stockpile(country) is not { } stockpile)
			{
				continue;
			}

			// Its base IC for that many days, in thousandths. A subject country's is held to
			// a thousand in the original; there are no subjects here yet.
			long icDays = (long)country.BaseIc * DaysAtStart(state, country) * CFixedPoint.Scale;

			stockpile[EGoods.CrudeOil] += new CFixedPoint(icDays);
			stockpile[EGoods.Metal] += new CFixedPoint(icDays);
			stockpile[EGoods.Energy] += new CFixedPoint(icDays * 2);
			stockpile[EGoods.RareMaterials] += new CFixedPoint(icDays / 2);

			// The resources are added. Money is given only where there is none: a twentieth
			// of what that much IC would earn in a day.
			if (stockpile[EGoods.Money] == CFixedPoint.Zero)
			{
				long money = icDays * icToMoney / CFixedPoint.Scale;
				stockpile[EGoods.Money] = new CFixedPoint(money * MoneyShare / CFixedPoint.Scale);
			}
		}
	}

	/// <summary>
	/// The day's income: what each country produced at home goes into its stockpile.
	/// A country that does not hold its capital produced nothing.
	/// </summary>
	/// <summary>
	/// The other half of what a new game starts a country with: as many days of what
	/// its units use, in supplies and in fuel, as it was given of resources. It can only
	/// be done once the units exist, so it comes after <see cref="FillAtStart"/>.
	/// </summary>
	public static void AddUnitGoodsAtStart(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			if (state.Stockpile(country) is not { } stockpile)
			{
				continue;
			}
			int days = DaysAtStart(state, country);
			(CFixedPoint supplies, CFixedPoint fuel) = CUnitSupply.DailyUse(country);
			stockpile[EGoods.Supplies] += supplies * days;
			stockpile[EGoods.Fuel] += fuel * days;
		}
	}

	public static void AddHomeProduction(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			state.Stockpile(country)?.Add(country.HomeProduced);
		}
	}
}
