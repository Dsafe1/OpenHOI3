using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Revolts;

/// <summary>
/// The original's <c>RunDailyRevoltRoll</c>: once a day in each country's day, two draws for each province it holds,
/// and a rising of three where both pass. See <c>docs/mechanics/rebels.md</c>, section 3.
/// </summary>
public static class CRevoltRoll
{
	/// <summary>The first draw's range for a country somebody plays, and for one nobody does.</summary>
	public const int PlayedRange = 10000;
	public const int AiRange = 20000;

	/// <summary>The second draw's range: one in it passes.</summary>
	public const int DaysInYear = 365;

	/// <summary>Thousandths of revolt risk to a point of the first draw's threshold, rounded.</summary>
	public const long RiskPerPoint = 50;

	/// <summary>The brigades a roll's rising asks for.</summary>
	public const int Brigades = 3;

	/// <summary>
	/// Every country that owns a province, in the game's order, while a game runs - none while one is being made.
	/// </summary>
	public static void RunDaily(CGameState state)
	{
		if (state.IsBeingMade)
		{
			return;
		}
		foreach (CCountry country in state.Countries)
		{
			Roll(state, country);
		}
	}

	/// <summary>One country's roll, over the provinces it holds that are in a real area, in their order.</summary>
	public static void Roll(CGameState state, CCountry country)
	{
		if (CCountryLists.OwnedCount(state, country.Tag) <= 0)
		{
			return;
		}
		int range = state.Played.Contains(country.Tag) ? PlayedRange : AiRange;
		// The provinces it holds, in the map's order - as a walk of the whole map gives them, a rising that changes hands
		// on the way among them (CHeldProvinces.Walk).
		foreach (CProvince province in CHeldProvinces.Walk(state, country.Tag))
		{
			if (province.Controller != country.Tag || CRebelFactions.AreaOf(state, province.Id) is null)
			{
				continue;
			}
			long risk = CRevoltRisk.Shown(state, province);
			long threshold = Threshold(risk);
			if (state.Random.Next() % range >= threshold)
			{
				continue;
			}
			if (state.Random.Next() % DaysInYear != 0)
			{
				continue;
			}
			CRisings.Rise(state, province, CRebelChoice.TypeFor(state, province), Brigades);
		}
	}

	/// <summary>
	/// The first draw's threshold: the risk in thousandths over 50, rounded to nearest as the floating point unit
	/// rounds - a half to the even neighbour. Never below nought, the figure shown never being.
	/// </summary>
	public static long Threshold(long risk)
	{
		long scaled = risk * (1000 / RiskPerPoint);
		long whole = scaled / 1000;
		long over = scaled % 1000;
		if (over > 500 || (over == 500 && whole % 2 != 0))
		{
			whole++;
		}
		return whole;
	}
}
