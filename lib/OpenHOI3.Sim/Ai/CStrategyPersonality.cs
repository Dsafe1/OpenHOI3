using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Diplomacy;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// A strategy's personality, worked out from the ideologies the country's form of government accepts and where their
/// groups stand on the alignment triangle. See <c>docs/mechanics/aistrategy.md</c>, section 3.
/// </summary>
public static class CStrategyPersonality
{
	/// <summary>The share of the total a score must reach: 0.400.</summary>
	private const long Enough = 400;

	/// <summary>
	/// The three scores summed over the accepted ideologies, each weighed by its share - twice it for the ruling party's -
	/// and the first of military, diplomacy and capital to reach 0.4 of their total; balanced where none does.
	/// </summary>
	public static EAiPersonality Choose(CGameState state, CCountry country)
	{
		var ideologies = new System.Collections.Generic.List<Politics.CIdeology>();
		if (state.Governments.TryGetValue(country.GovernmentType, out Politics.CGovernment? government))
		{
			foreach (string name in government.Accepted)
			{
				if (state.Ideology(name) is { Group: not null } ideology)
				{
					ideologies.Add(ideology);
				}
			}
		}
		long share = ShareOf(Math.Max(ideologies.Count, 1));
		long military = 0;
		long capital = 0;
		long diplomacy = 0;
		foreach (Politics.CIdeology ideology in ideologies)
		{
			long weight = ideology.Name.Equals(country.RulingIdeology, StringComparison.OrdinalIgnoreCase) ? share * 2000 / 1000 : share;
			CIdeologyGroup group = ideology.Group!;
			military += CStrategyFigures.Times(Military(group.X, group.Y), weight);
			capital += CStrategyFigures.Times(Capital(group.X, group.Y), weight);
			diplomacy += CStrategyFigures.Times(Diplomacy(group.X, group.Y), weight);
		}
		long total = Math.Max(military + diplomacy + capital, 1000);
		if (military * 1000 / total >= Enough)
		{
			return EAiPersonality.Military;
		}
		if (diplomacy * 1000 / total >= Enough)
		{
			return EAiPersonality.Diplomacy;
		}
		return capital * 1000 / total >= Enough ? EAiPersonality.Capital : EAiPersonality.Balanced;
	}

	/// <summary>y / 2; where y is above nought, plus (y + |x|) x 0.5 for x of nought or more, or (y - x) x 0.33 below it.</summary>
	public static long Military(long x, long y)
	{
		long score = y * 1000 / 2000;
		if (y > 0)
		{
			score += x < 0 ? 330 * (y - x) / 1000 : 500 * (y + x) / 1000;
		}
		return score;
	}

	/// <summary>-x for x below nought; (x + y) / 2 for x of nought or more and y below it; otherwise nought.</summary>
	public static long Capital(long x, long y)
	{
		if (x < 0)
		{
			return -x;
		}
		return y < 0 ? (x + y) * 1000 / 2000 : 0;
	}

	/// <summary>(200 - |x| in whole units, truncated) / 2 - y / 2.</summary>
	public static long Diplomacy(long x, long y)
	{
		long whole = Math.Abs(x / 1000);
		return (200 - whole) * 1000 * 1000 / 2000 - y * 1000 / 2000;
	}

	/// <summary>One over the count as the original makes it: a single-precision quotient, then thousandths, floored.</summary>
	public static long ShareOf(int count)
	{
		float quotient = (float)(1.0 / (float)count);
		float lifted = (float)(quotient + 0.0005);
		return (long)MathF.Floor((float)(lifted * 1000.0));
	}
}
