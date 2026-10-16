using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The <c>antagonize</c> and <c>rival</c> lists, made together in one walk of the countries as the original makes them.
/// See <c>docs/mechanics/aistrategy.md</c>, section 7.
/// </summary>
public static class CStrategyAntagonism
{
	public static void Make(CGameState state, CCountry country, CAiStrategy strategy)
	{
		COwnedCounts owned = COwnedCounts.Of(state);
		CCountryFacts facts = CCountryFacts.Of(state);
		Dictionary<CCountryTag, long> shares = CStrategyFigures.BorderShares(state, country);
		long strengthC = CStrategyFigures.Strength(country);
		int ownC = owned.Count(country.Tag);
		foreach (CCountry other in state.Countries)
		{
			if (!CStrategyFigures.IsCandidate(state, country, other) || CStrategyFigures.OneFaction(country, other)
				|| other.Overlord == country.Tag)
			{
				continue;
			}
			long conquests = 0;
			foreach ((int province, int weight) in strategy.ConquerProvinces.Entries)
			{
				if (state.Province(province).Owner == other.Tag)
				{
					conquests += weight * 1000L;
				}
			}
			if (other.Overlord is not null && TopOverlord(state, other) is { } top && top != other && conquests > 0
				&& (!CStrategyFigures.IsHistoricalFriend(country, top.Tag) || CCasusBellis.HasUsable(state, country, top)
					|| strategy.Antagonize.WeightOf(top.Tag) > 0))
			{
				CStrategyFigures.Add(country, strategy.Antagonize, top.Tag, CStrategyFigures.Rounded(conquests),
					CStrategyFigures.AntagonizeCap);
				continue;
			}
			int provinces = owned.Count(other.Tag);
			long acc = Thousandths(CStrategyFigures.Times(6000000 / Math.Min(provinces * 1000L, 6000), conquests));
			long strengthRatio = CStrategyFigures.Ratio(CStrategyFigures.Strength(other), strengthC);
			long provinceRatio = CStrategyFigures.Ratio(provinces * 1000L, ownC * 1000L);
			long power = (provinceRatio + strengthRatio) * 1000 / 2000;
			bool matched = power <= 3000 && power >= 340;
			bool borders = facts.AreNeighbours(other.Tag, country.Tag);
			bool touches = CStrategyFigures.CapitalAreaTouches(state, country, other.Tag);
			bool hasCasusBelli = CCasusBellis.HasUsable(state, country, other);
			if (borders && strategy.IsMilitarist)
			{
				long border = CStrategyFigures.Times(shares.GetValueOrDefault(other.Tag), 100000) + 25000;
				if (touches)
				{
					border = CStrategyFigures.Times(1250, border);
				}
				if (CStrategyFigures.CapitalBorders(state, country, other.Tag))
				{
					border = CStrategyFigures.Times(1250, border);
				}
				acc += border;
			}
			if (hasCasusBelli)
			{
				acc += 50000;
			}
			if (country.Overlord == other.Tag && power < 2000)
			{
				acc += CStrategyFigures.Times(300000, 2100 - power);
			}
			if (CStrategyFigures.IsHistoricalFriend(country, other.Tag) && strategy.Antagonize.WeightOf(other.Tag) < 1
				&& !hasCasusBelli)
			{
				acc = 0;
			}
			if (CCountryLists.HasAlliance(state, country.Tag, other.Tag))
			{
				acc -= 400000;
			}
			else if (country.RulingIdeology.Equals(other.RulingIdeology, StringComparison.OrdinalIgnoreCase))
			{
				acc -= strategy.Personality switch
				{
					EAiPersonality.Military => 200000,
					EAiPersonality.Diplomacy => 400000,
					_ => 300000,
				};
			}
			if (CStrategyFigures.RulingGroup(state, country) != CStrategyFigures.RulingGroup(state, other))
			{
				acc += 50000;
				if (touches || (borders && strategy.IsMilitarist))
				{
					acc += 150000;
				}
			}
			if (other.Faction.Length > 0)
			{
				acc += country.Faction.Length > 0 ? 400000 : -400000;
			}
			if (acc > 0)
			{
				if (provinceRatio < 1000)
				{
					long smaller = CStrategyFigures.Times(100000, 1000 - provinceRatio);
					if (strategy.Personality == EAiPersonality.Balanced)
					{
						smaller = CStrategyFigures.Times(750, smaller);
					}
					else if (!strategy.IsMilitarist)
					{
						smaller = CStrategyFigures.Times(500, smaller);
					}
					acc += smaller;
				}
				if (acc >= 1000)
				{
					CStrategyFigures.Add(country, strategy.Antagonize, other.Tag, CStrategyFigures.Rounded(acc),
						CStrategyFigures.AntagonizeCap);
				}
			}
			Rival(state, country, strategy, other, matched, borders, hasCasusBelli, power, ownC);
		}
	}

	/// <summary>
	/// The rival, for a country evenly matched with it: 100 for a neighbour, else half of what is left of the reach - not
	/// over the reach, as the threat has it; then war, casus belli and two capital personalities; plus 25, times or over the
	/// power held between 0.34 and 3.
	/// </summary>
	private static void Rival(CGameState state, CCountry country, CAiStrategy strategy, CCountry other, bool matched,
		bool borders, bool hasCasusBelli, long power, int ownC)
	{
		long rival = 0;
		if (matched)
		{
			if (borders)
			{
				rival = 100000;
			}
			else
			{
				long reach = CStrategyFigures.Reach(ownC);
				long distance = CStrategyFigures.Distance(state, country, other);
				if (distance < reach)
				{
					rival = (reach - distance) * 1000 / 2000;
				}
			}
		}
		if (rival <= 0)
		{
			return;
		}
		if (CCountryLists.AtWar(state, country.Tag, other.Tag))
		{
			rival += 50000;
		}
		if (hasCasusBelli)
		{
			rival += 50000;
		}
		if (strategy.Personality == EAiPersonality.Capital && other.Strategy.Personality == EAiPersonality.Capital)
		{
			rival += 100000;
		}
		long based = rival + 25000;
		float held = (float)(power / 1000.0);
		if (3.0f <= held)
		{
			held = 3.0f;
		}
		long first = CStrategyFigures.Thousandths(held);
		float floor = (float)(first / 1000.0);
		if (floor <= 0.34f)
		{
			floor = 0.34f;
		}
		long factor = CStrategyFigures.Thousandths(floor);
		long value = power < 1000 ? factor * based / 1000 : based * 1000 / factor;
		if (value >= 1000)
		{
			CStrategyFigures.Add(country, strategy.Rival, other.Tag, CStrategyFigures.Rounded(value), CStrategyFigures.RivalCap);
		}
	}

	/// <summary>The conquests' weight made a single-precision number of whole units, at most 800, and thousandths again.</summary>
	private static long Thousandths(long weighed)
	{
		float whole = (float)(weighed / 1000.0);
		if (800.0 <= whole)
		{
			whole = 800.0f;
		}
		return CStrategyFigures.Thousandths(whole);
	}

	/// <summary>The country's overlord's overlord and so on, to one that is nobody's subject.</summary>
	private static CCountry? TopOverlord(CGameState state, CCountry country)
	{
		CCountry current = country;
		for (int guard = 0; guard < state.Countries.Count && current.Overlord is { } overlord; guard++)
		{
			if (state.Country(overlord) is not { } next)
			{
				return null;
			}
			current = next;
		}
		return current;
	}
}
