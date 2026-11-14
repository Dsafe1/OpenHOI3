using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Research;

/// <summary>
/// A new game's head start in technology: every country, the player's too, buys levels with a budget of its core
/// provinces' leadership, the cheapest level already due first, one at a time, until the budget is spent. The original's
/// <c>SetUpCountryLawsAndTechnologyForNewGame</c>, its second half. See <c>docs/mechanics/research.md</c>, section 9.
/// </summary>
public static class CStartTechnology
{
	/// <summary>The days of a year the budget counts, a built-in figure.</summary>
	private const long DaysAYear = 3650;

	/// <summary>The most core leadership the budget counts, in thousandths.</summary>
	private const long MostLeadership = 5000;

	/// <summary>What a level not yet due costs over its price, in thousandths.</summary>
	private const long NotYetDue = 2000;

	/// <summary>The price no candidate reaches, which a first candidate undercuts.</summary>
	private const long NoPrice = 99_999_000;

	/// <summary>The folder whose technologies are never bought, named in the original's code.</summary>
	public const string TheoryFolder = "theory_folder";

	/// <summary>
	/// The built-in null technology costs this, in thousandths, and stands first among the candidates: no file declares
	/// it, it has no level a game shows, and buying it is a charge on the budget and nothing else.
	/// </summary>
	public const long NullTechnologyPrice = 125_000;

	/// <summary>
	/// The budget, in thousandths of a leadership-day over a hundred: the years since the start, two more and a month's
	/// share less a twelfth - 1.917 on 1 January of the start year - times 3650, times the country's core provinces'
	/// leadership held to five; each step dropping its remainder.
	/// </summary>
	public static long Budget(CGameState state, CCountry country)
	{
		long years = state.Date.Year * 1000L + (state.Date.Month * 1000L - 2000) * 1000 / 12000 + 2000
			- state.StartDate.Year * 1000L;
		long leadership = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Cores.Contains(country.Tag))
			{
				leadership += province.Leadership.Raw;
			}
		}
		leadership = Math.Min(leadership, MostLeadership);
		return years * DaysAYear / 1000 * leadership / 1000;
	}

	/// <summary>
	/// Buys the country's levels: while the budget lasts, the cheapest candidate, the first of equals in the database's
	/// order, gains a level and its price over a hundred comes off. A candidate is the null technology while it is not
	/// bought, and a technology not in the theory folder that the country may research further, whose current level is
	/// due before this year, and which is not of one level and already had; one whose next level is not yet due costs
	/// double. The level is written alone - no progress, ability or message.
	/// </summary>
	/// <returns>The levels bought, the null technology among them.</returns>
	public static int Buy(CGameState state, CCountry country)
	{
		long budget = Budget(state, country);
		int year = state.Date.Year;
		bool nullBought = false;
		int bought = 0;
		while (budget > 0)
		{
			CTechnology? pick = null;
			long best = NoPrice;
			if (!nullBought && NullTechnologyPrice < best)
			{
				best = NullTechnologyPrice;
			}
			foreach (CTechnology technology in state.TechnologiesInOrder)
			{
				int level = country.TechnologyLevels.GetValueOrDefault(technology.Name);
				if (technology.IsSingleLevel && level >= 1)
				{
					continue;
				}
				if (string.Equals(technology.Folder, TheoryFolder, StringComparison.OrdinalIgnoreCase)
					|| !CResearch.MayResearchNext(state, country, technology)
					|| Due(technology, level) >= year)
				{
					continue;
				}
				long price = CResearch.Cost(country, technology).Raw;
				if (Due(technology, level + 1) >= year)
				{
					price = price * NotYetDue / 1000;
				}
				if (price < best)
				{
					best = price;
					pick = technology;
				}
			}
			if (best == NoPrice)
			{
				break;
			}
			if (pick is null)
			{
				nullBought = true;
				budget -= NullTechnologyPrice / 100;
				country.NullTechnologyLevel = 1;
			}
			else
			{
				budget -= CResearch.Cost(country, pick).Raw / 100;
				int level = country.TechnologyLevels.GetValueOrDefault(pick.Name);
				country.TechnologyLevels[pick.Name] = Math.Min(level + 1, pick.HighestLevel);
			}
			bought++;
		}
		return bought;
	}

	/// <summary>The year a level is due: the start year for the first, the first offset for the second, and so on.</summary>
	public static int Due(CTechnology technology, int level)
	{
		return level <= 1
			? technology.StartYear
			: technology.FirstOffset + (level - 2) * technology.AdditionalOffset;
	}
}
