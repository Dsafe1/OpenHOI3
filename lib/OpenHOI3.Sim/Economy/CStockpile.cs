using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

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

	/// <summary>The most IC-days a subject country is given at the start, whole. It is built into the original.</summary>
	public const long SubjectAmount = 1000;

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
	/// The pool of the province a country's government sits in, its acting capital's, whoever holds it - an exile's
	/// included, where its stockpile is its own pool: what a new game starts a country with goes there, as the
	/// original's set-up adds it to the acting capital's pool without asking about exile. Null for a country with no
	/// capital.
	/// </summary>
	public static CGoodsPool? SeatPool(CGameState state, CCountry country)
	{
		Provinces.CProvince seat = state.Province(country.ActingCapital);
		return seat.Id == 0 ? null : seat.Pool;
	}

	/// <summary>
	/// What the history files give, as the world is made at its date: a line sets the good of the country's stockpile -
	/// an exile's own pool - rather than adding to it, in the countries' order, so where two countries share a capital
	/// the later's line stands (stockpile.md, section 2). It comes before <see cref="FillAtStart"/>, and decides who is
	/// given money there.
	/// </summary>
	/// <param name="fromHistory">
	/// What each country's history file sets of each good by the start date, by tag - the last line of a good.
	/// </param>
	public static void SetFromHistory(CGameState state, IReadOnlyDictionary<string, Dictionary<EGoods, CFixedPoint>> fromHistory)
	{
		foreach (CCountry country in state.Countries)
		{
			if (fromHistory.TryGetValue(country.Tag.Value, out Dictionary<EGoods, CFixedPoint>? given)
				&& state.Stockpile(country) is { } pool)
			{
				foreach ((EGoods goods, CFixedPoint amount) in given)
				{
					pool[goods] = amount;
				}
			}
		}
	}

	/// <summary>
	/// Fills every stockpile for the first day of a new game: what the history gives, where given, then the resources
	/// and money of so many days of base IC. A new game does the second once the effects of the decisions a country's
	/// history has taken have run, as the original's set-up does - so France, whose decisions give it 35 money, is
	/// given none for its IC (stockpile.md, section 2).
	/// </summary>
	/// <param name="fromHistory">
	/// What each country's history file sets of each good by the start date, by tag - the last line of a good.
	/// </param>
	public static void FillAtStart(CGameState state, IReadOnlyDictionary<string, Dictionary<EGoods, CFixedPoint>>? fromHistory = null)
	{
		// It is the base that counts, which no law or minister changes.
		CProvinceModifiers.Rebuild(state);
		CIndustrialCapacity.Update(state);

		// What the history files give comes first, because it decides who gets money.
		if (fromHistory is not null)
		{
			SetFromHistory(state, fromHistory);
		}

		long icToMoney = state.Defines[CResources.EconomySection, CResources.IcToMoney].Raw;

		// In the order the countries are declared, which matters where two share a capital.
		foreach (CCountry country in state.Countries)
		{
			// The pool of the province its government sits in, whoever holds it - an exile's too, which is not its
			// stockpile: a country that does not exist yet is given its share all the same, in somebody else's
			// province, and an exile in its host's capital (stockpile.md, section 2).
			if (SeatPool(state, country) is not { } stockpile)
			{
				continue;
			}

			// Its base IC for that many days, in thousandths; a subject country's held to a
			// thousand (stockpile.md, section 2) - Manchukuo's 1890 to 1000.
			long icDays = (long)country.BaseIc * DaysAtStart(state, country) * CFixedPoint.Scale;
			if (country.Overlord is not null)
			{
				icDays = Math.Min(icDays, SubjectAmount * CFixedPoint.Scale);
			}

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
			// Into the seat's pool, as the resources are (FillAtStart).
			if (SeatPool(state, country) is not { } stockpile)
			{
				continue;
			}
			int days = DaysAtStart(state, country);
			(CFixedPoint supplies, CFixedPoint fuel) = CUnitSupply.DailyUse(state, country);
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

	/// <summary>The most of any good a land province holds at the end of a day, in thousandths.</summary>
	public const long MostHeld = 99999 * CFixedPoint.Scale;

	/// <summary>
	/// The last thing a day does to a land province: what lies in it of each good is
	/// held between nothing and 99999. So a stockpile that consumer goods or supplies
	/// were charged to beyond what it held is empty and not in debt, and no stockpile
	/// grows past that figure. See <c>docs/mechanics/stockpile.md</c>, section 4.
	/// </summary>
	public static void HoldWithinBounds(CGameState state)
	{
		foreach (Provinces.CProvince province in state.Provinces)
		{
			if (province.Id == 0 || province.IsSea)
			{
				continue;
			}
			for (int goods = 0; goods < CGoodsPool.Count; goods++)
			{
				long held = province.Pool[(EGoods)goods].Raw;
				if (held < 0 || held > MostHeld)
				{
					province.Pool[(EGoods)goods] = new CFixedPoint(Math.Clamp(held, 0, MostHeld));
				}
			}
		}
	}
}
