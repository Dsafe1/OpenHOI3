using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The <c>befriend</c> and <c>vassal</c> lists: whom a country courts - a counterweight to its threats, its own kind,
/// its faction - and which small neighbours it wants as puppets. See <c>docs/mechanics/aistrategy.md</c>, section 8.
/// </summary>
public static class CStrategyFriends
{
	public static void Make(CGameState state, CCountry country, CAiStrategy strategy)
	{
		COwnedCounts owned = COwnedCounts.Of(state);
		CCountryFacts facts = CCountryFacts.Of(state);
		int ownC = owned.Count(country.Tag);
		bool diplomat = strategy.Personality == EAiPersonality.Diplomacy;
		foreach (CCountry other in state.Countries)
		{
			if (!CStrategyFigures.IsCandidate(state, country, other)
				|| (other.Overlord is { } overlord && overlord != country.Tag))
			{
				continue;
			}
			int provinces = owned.Count(other.Tag);
			bool ours = other.Overlord == country.Tag;
			bool borders = facts.AreNeighbours(other.Tag, country.Tag);
			bool small = borders && provinces < SmallBelow(ownC);
			if (borders && (!CStrategyFigures.IsHistoricalFriend(country, other.Tag)
					|| strategy.Antagonize.WeightOf(other.Tag) > 0 || CCasusBellis.HasUsable(state, country, other))
				&& CStrategyFigures.OwnsAConquest(state, strategy, other.Tag) && !ours && !(diplomat && small))
			{
				continue;
			}
			long threat = strategy.Threat.WeightOf(other.Tag);
			long strength = CStrategyFigures.Strength(other);
			long acc = 0;
			foreach ((CCountryTag feared, int weight) in strategy.Threat.Entries)
			{
				if (owned.Count(feared) <= 0 || feared == other.Tag || !facts.AreNeighbours(other.Tag, feared) || threat > weight
					|| state.Country(feared) is not { } enemy)
				{
					continue;
				}
				long strengthRatio = CStrategyFigures.Ratio(strength, CStrategyFigures.Strength(enemy));
				long provinceRatio = CStrategyFigures.Ratio(provinces * 1000L, owned.Count(feared) * 1000L);
				long counterweight = Math.Min(CStrategyFigures.Times(500, provinceRatio + strengthRatio), 4000);
				acc += CStrategyFigures.Times(CStrategyFigures.Thousandths((float)weight), counterweight);
			}
			if (country.RulingIdeology.Equals(other.RulingIdeology, StringComparison.OrdinalIgnoreCase))
			{
				acc += 100000;
			}
			else if (CStrategyFigures.RulingGroup(state, country) == CStrategyFigures.RulingGroup(state, other))
			{
				acc += 50000;
			}
			else
			{
				acc -= 100000;
			}
			if (small)
			{
				int more = ownC - provinces;
				if (diplomat && more * 10 > 0)
				{
					CStrategyFigures.Add(country, strategy.Vassal, other.Tag, more * 10, CStrategyFigures.VassalCap);
				}
				if (ours)
				{
					acc += more * 10000L;
				}
			}
			if (diplomat)
			{
				acc += threat / 2 * 1000;
			}
			if (country.Faction.Length > 0)
			{
				if (country.Faction == other.Faction)
				{
					acc += 600000;
				}
				else if (other.Faction.Length > 0)
				{
					acc -= 200000;
				}
			}
			if (CStrategyFigures.IsHistoricalFriend(country, other.Tag) && strategy.Antagonize.WeightOf(other.Tag) < 1
				&& !CCasusBellis.HasUsable(state, country, other))
			{
				acc += 400000;
			}
			if (acc >= 1000)
			{
				CStrategyFigures.Add(country, strategy.Befriend, other.Tag, CStrategyFigures.Rounded(acc),
					CStrategyFigures.FriendCap(strategy));
			}
		}
	}

	/// <summary>
	/// How few provinces make a neighbour small: the country's times 1.5, a single-precision number made thousandths and
	/// rounded to whole, half to even.
	/// </summary>
	public static int SmallBelow(int ownC)
	{
		return CStrategyFigures.Rounded(CStrategyFigures.Thousandths((float)(ownC * 1.5)));
	}
}
