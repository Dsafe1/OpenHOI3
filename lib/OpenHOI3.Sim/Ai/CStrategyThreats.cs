using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Sim.Ai;

/// <summary>
/// The <c>threat</c> list: whom a country fears, by casus belli, faction, border and strength. See
/// <c>docs/mechanics/aistrategy.md</c>, section 6.
/// </summary>
public static class CStrategyThreats
{
	public static void Make(CGameState state, CCountry country, CAiStrategy strategy)
	{
		COwnedCounts owned = COwnedCounts.Of(state);
		int ownC = owned.Count(country.Tag);
		if (ownC == 0)
		{
			return;
		}
		CCountryFacts facts = CCountryFacts.Of(state);
		Dictionary<CCountryTag, long> shares = CStrategyFigures.BorderShares(state, country);
		long strengthC = HalvedStrength(country);
		foreach (CCountry other in state.Countries)
		{
			if (!CStrategyFigures.IsCandidate(state, country, other) || CStrategyFigures.OneFaction(country, other)
				|| other.Overlord == country.Tag)
			{
				continue;
			}
			int provinces = owned.Count(other.Tag);
			long strength = HalvedStrength(other);
			long strengthRatio = CStrategyFigures.Ratio(strength, strengthC);
			long provinceRatio = CStrategyFigures.Ratio(ownC * 1000L, provinces * 1000L);
			if (provinceRatio > 3000 && strengthRatio < 500)
			{
				continue;
			}
			long score = 0;
			if (CCasusBellis.HasUsable(state, other, country))
			{
				score += 100000;
			}
			score += FactionTerm(state, country, other);
			if (ownC == 1 && provinces > 1)
			{
				score += Math.Min(provinces, 4) * 50000L;
			}
			if (!state.Played.Contains(other.Tag) && other.Strategy.IsMilitarist)
			{
				score = CStrategyFigures.Times(score, 1500);
			}
			bool borders = facts.AreNeighbours(other.Tag, country.Tag);
			if (borders)
			{
				long border = CStrategyFigures.Times(shares.GetValueOrDefault(other.Tag), 25000) + 100000;
				if (CStrategyFigures.CapitalAreaTouches(state, country, other.Tag))
				{
					border = CStrategyFigures.Times(border, 1500);
				}
				if (CStrategyFigures.CapitalBorders(state, country, other.Tag))
				{
					border = CStrategyFigures.Times(1500, border);
				}
				score += border;
			}
			long threat = CStrategyFigures.Times(score, Math.Min(strengthRatio, 4000));
			if (!borders)
			{
				long reach = CStrategyFigures.Reach(ownC);
				long distance = CStrategyFigures.Distance(state, country, other);
				long factor = 0;
				if (distance <= reach && ownC < provinces && strengthC < strength)
				{
					factor = CStrategyFigures.Ratio(reach - distance, reach);
				}
				threat = CStrategyFigures.Times(factor, threat);
			}
			if (threat > 1000)
			{
				CStrategyFigures.Add(country, strategy.Threat, other.Tag, CStrategyFigures.Rounded(threat), 0);
			}
		}
	}

	/// <summary>Brigades plus one, halved: the strength the threat weighs.</summary>
	public static long HalvedStrength(CCountry country)
	{
		return CStrategyFigures.Strength(country) * 1000 / 2000;
	}

	/// <summary>
	/// For another country in a faction other than the country's, by the faction's corner: fascism 400 for its leader and
	/// 200 for a member, communism 200 and 100, democracy only where the country is in a faction - 300 where that is
	/// fascist, 200 otherwise.
	/// </summary>
	public static long FactionTerm(CGameState state, CCountry country, CCountry other)
	{
		if (other.Faction.Length == 0 || other.Faction == country.Faction
			|| CStrategyFigures.GroupOfFaction(state, other.Faction) is not { } group)
		{
			return 0;
		}
		bool leads = CCountryLists.FactionMembers(state, other.Faction) is { Count: > 0 } members && members[0] == other;
		switch (group.Name)
		{
			case Fascism:
				return leads ? 400000 : 200000;
			case Communism:
				return leads ? 200000 : 100000;
			case Democracy when country.Faction.Length > 0:
				return CStrategyFigures.GroupOfFaction(state, country.Faction)?.Name == Fascism ? 300000 : 200000;
			default:
				return 0;
		}
	}

	/// <summary>The three corners the original keeps by name.</summary>
	public const string Fascism = "fascism";
	public const string Democracy = "democracy";
	public const string Communism = "communism";
}
