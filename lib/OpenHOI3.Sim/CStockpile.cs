using System.Collections.Generic;

namespace OpenHOI3.Sim;

/// <summary>
/// What countries hold: what a stockpile is filled with when a game begins, and what a
/// day adds to it. See <c>docs/mechanics/stockpile.md</c>.
///
/// The starting amounts were measured from a savegame of the original rather than read
/// out of it. Nothing is taken out of a stockpile yet: industry's usage, the conversions
/// and what the units draw arrive with the systems that cause them.
/// </summary>
public static class CStockpile
{
	/// <summary>How many days of its industry's needs a country at peace starts with.</summary>
	public const int DaysAtPeace = 90;

	/// <summary>And a country at war.</summary>
	public const int DaysAtWar = 75;

	/// <summary>Money per IC per day of the above, in ten-thousandths: 0.0025.</summary>
	private const long MoneyPerIcDay = 25;

	/// <summary>
	/// Fills every stockpile for the first day of a new game.
	/// </summary>
	/// <param name="fromHistory">What each country's history file gives it on the start date, by tag.</param>
	public static void FillAtStart(CGameState state, IReadOnlyDictionary<string, CGoodsPool>? fromHistory = null)
	{
		// It is the base that counts, which no law or minister changes.
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

		// In the order the countries are declared, which matters where two share a capital.
		foreach (CCountry country in state.Countries)
		{
			// The capital's pool, whoever holds the province: a country that does not exist
			// yet is given its share all the same, in somebody else's province.
			if (state.Stockpile(country) is not { } stockpile)
			{
				continue;
			}

			long icDays = (long)country.BaseIc * (state.IsAtWar(country.Tag) ? DaysAtWar : DaysAtPeace);
			foreach ((EGoods goods, CFixedPoint perIc) in CResources.NeededPerIc)
			{
				stockpile[goods] += new CFixedPoint(icDays * perIc.Raw);
			}
			stockpile[EGoods.CrudeOil] += new CFixedPoint(icDays * CFixedPoint.Scale);

			// The resources are added. Money is given only where there is none.
			if (stockpile[EGoods.Money] == CFixedPoint.Zero)
			{
				stockpile[EGoods.Money] = new CFixedPoint(icDays * MoneyPerIcDay / 10);
			}
		}
	}

	/// <summary>
	/// The day's income: what each country produced at home goes into its stockpile.
	/// A country that does not hold its capital produced nothing.
	/// </summary>
	public static void AddHomeProduction(CGameState state)
	{
		foreach (CCountry country in state.Countries)
		{
			state.Stockpile(country)?.Add(country.HomeProduced);
		}
	}
}
