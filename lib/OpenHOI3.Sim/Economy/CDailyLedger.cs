using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Sim.Economy;

/// <summary>
/// A country's day of one good as the original sums it off fifteen of its pools: what came in, and what went out. The
/// original's <c>GetDailyIncome</c> and <c>GetDailyExpense</c>, which the energy conversion, the refining and the AI's
/// <c>GetDailyBalance</c> all read - each whatever the pools hold at the moment it asks (<c>docs/mechanics/ic.md</c>,
/// section 4; <c>lua.md</c>, section 4.2).
/// </summary>
public static class CDailyLedger
{
	/// <summary>
	/// Home production - and convoyed in, but not for supplies or fuel - with what came back, was handed over by
	/// governments in exile, was repaid to it, was made by turning, was traded for and came as tribute. Thousandths.
	/// </summary>
	public static long Income(CCountry country, EGoods goods)
	{
		long home = country.HomeProduced[goods].Raw;
		if (goods is not (EGoods.Fuel or EGoods.Supplies))
		{
			home += country.ConvoyedIn[goods].Raw;
		}
		return home + country.SentBack[goods].Raw + country.ReceivedFromExiles[goods].Raw
			+ Repaid(country.IncomeFromDebt, goods) + country.ConversionMade[goods].Raw
			+ country.TradedFor[goods].Raw + country.TributeReceived[goods].Raw;
	}

	/// <summary>
	/// Sent away from the capital, used, turned into another good, convoyed out and traded away - less what it repaid and
	/// the tribute it sent, which is held below nought, as the original writes it. Thousandths.
	/// </summary>
	public static long Expense(CCountry country, EGoods goods)
	{
		return country.SentTo[goods].Raw - Repaid(country.RepaidAway, goods) + country.Usage[goods].Raw
			+ country.ConversionUsed[goods].Raw + country.ConvoyedOut[goods].Raw + country.TradedAway[goods].Raw
			- country.TributeSent[goods].Raw;
	}

	/// <summary>What came in less what went out, thousandths.</summary>
	public static long Net(CCountry country, EGoods goods) => Income(country, goods) - Expense(country, goods);

	/// <summary>A debt's money, which is the only good it moves.</summary>
	private static long Repaid(CFixedPoint money, EGoods goods) => goods == EGoods.Money ? money.Raw : 0;
}
