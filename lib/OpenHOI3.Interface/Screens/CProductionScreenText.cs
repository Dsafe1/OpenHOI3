using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the production screen writes into its elements for a country, as the original
/// builds them. See <c>docs/mechanics/productionscreen.md</c>.
/// </summary>
public static class CProductionScreenText
{
	/// <summary>
	/// A resource: what the country holds and, in brackets, what the day did to it, both
	/// in whole units. A stock that is falling shows the fall in red with no sign; one
	/// that is rising shows the rise in green with a plus; one that is still, a yellow
	/// nought.
	/// </summary>
	public static string Goods(CGameState state, CCountry country, EGoods goods)
	{
		// The fifteen ledgers the top bar's colour reads, with what was traded for counted as coming in
		// (reversing/findings/FINDINGS-countryviews.md): the original's daily expense less its daily income.
		CFixedPoint goingOut = CTopBarText.DayOut(country, goods);
		CFixedPoint comingIn = CTopBarText.DayIn(country, goods) + country.TradedFor[goods];
		long change = (goingOut - comingIn).Raw;

		string colour = CColouredText.In(CColouredText.Yellow);
		if (change > 0)
		{
			colour = CColouredText.In(CColouredText.Red);
		}
		else if (change < 0)
		{
			colour = CColouredText.In(CColouredText.Green) + "+";
			change = -change;
		}

		long held = (state.Stockpile(country)?[goods] ?? CFixedPoint.Zero).Raw;
		return (held / CFixedPoint.Scale).ToString(CultureInfo.InvariantCulture)
			+ " (" + colour + (change / CFixedPoint.Scale).ToString(CultureInfo.InvariantCulture)
			+ CColouredText.In('W') + ")";
	}

	/// <summary>The country's IC, a whole number.</summary>
	public static string Ic(CCountry country)
	{
		return country.TotalIc.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>Nuclear bombs held, to two decimals.</summary>
	public static string Nukes(CFixedPoint nukes)
	{
		return CNumberText.Format(nukes, 2);
	}

	/// <summary>
	/// Convoys or escorts: how many there are, and in brackets with a plus how many are
	/// being built, when any are.
	/// </summary>
	public static string Convoys(int held, int beingBuilt)
	{
		string text = held.ToString(CultureInfo.InvariantCulture);
		if (beingBuilt != 0)
		{
			text += " (+" + beingBuilt.ToString(CultureInfo.InvariantCulture) + ")";
		}
		return text;
	}
}
