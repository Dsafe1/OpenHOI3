using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The <c>protect</c> list: the weak neighbours a country of ten provinces or more would shield. See
/// <c>docs/mechanics/aistrategy.md</c>, section 9.1.
/// </summary>
public static class CStrategyProtection
{
	public static void Make(CGameState state, CCountry country, CAiStrategy strategy)
	{
		COwnedCounts owned = COwnedCounts.Of(state);
		int ownC = owned.Count(country.Tag);
		if (ownC <= 9)
		{
			return;
		}
		CCountryFacts facts = CCountryFacts.Of(state);
		long regiments = System.Math.Max(CStrategyFigures.Regiments(country), 1) * 1000L;
		foreach (CCountry other in state.Countries)
		{
			if (!CStrategyFigures.IsCandidate(state, country, other))
			{
				continue;
			}
			int provinces = owned.Count(other.Tag);
			long regimentRatio = CStrategyFigures.Ratio(CStrategyFigures.Regiments(other) * 1000L, regiments);
			long provinceRatio = CStrategyFigures.Ratio(provinces * 1000L, ownC * 1000L);
			long power = (regimentRatio + provinceRatio) * 1000 / 2000;
			long acc = 0;
			if (power < 660 && facts.AreNeighbours(country.Tag, other.Tag))
			{
				if (CCasusBellis.HasUsable(state, country, other))
				{
					acc += 25000;
				}
				if (provinceRatio <= 200)
				{
					acc += 25000;
				}
			}
			if (acc <= 0)
			{
				continue;
			}
			if (provinces == 1)
			{
				acc += 10000;
			}
			if (acc >= 1000)
			{
				CStrategyFigures.Add(country, strategy.Protect, other.Tag, CStrategyFigures.Rounded(acc),
					CStrategyFigures.ProtectCap(strategy));
			}
		}
	}
}
