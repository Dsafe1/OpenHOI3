using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;

namespace OpenHOI3.Sim.Research;

/// <summary>
/// Research: what a level of a technology costs a country, what a day's leadership adds
/// to it, what finishing it does, and the monthly decay of what a country knows. See
/// <c>docs/mechanics/research.md</c>.
///
/// Every step is whole-number arithmetic in the original's own order, because progress
/// is the sum of hundreds of days of it and a different rounding shows.
/// </summary>
public static class CResearch
{
	/// <summary>The section of the defines the four research numbers are in, and their names.</summary>
	public const string CountrySection = "country";
	public const string YearImpact = "TECHNOLOGY_YEAR_IMPACT";
	public const string BaseDecay = "BASE_TECH_DECAY";
	public const string MostAbility = "MAX_TECH_ABILITY";
	public const string AbilityGainDivisor = "TECH_ABILITY_GAIN_DIVISOR";

	private const string Efficiency = "research_efficiency";
	private const string NuclearResearch = "nuke_research";

	/// <summary>What a technology of no difficulty costs, and what each point of difficulty adds, in thousandths.</summary>
	private const long CostPerLevel = 125000;
	private const long DifficultyStep = 100;

	/// <summary>A technology is never made more than this much cheaper.</summary>
	private const long MostBonus = 900;

	/// <summary>Research never runs at less than this, whatever the modifiers say.</summary>
	private const long LeastEfficiency = 100;

	/// <summary>An ability counts up to this towards a bonus, and counts from this.</summary>
	private const long AbilityCounted = 20000;
	private const long AbilityExpected = 5000;

	/// <summary>
	/// The original's square root of a thousandths value. It stops as soon as the square
	/// of its guess is within the number itself of the number, which is after a step or
	/// two, so it is rough: 5 gives 2.25. It has to be this one and not a better one.
	/// </summary>
	public static long RoughSquareRoot(long value)
	{
		if (value <= 0)
		{
			return 0;
		}
		long guess = value * CFixedPoint.Scale / (2 * CFixedPoint.Scale);
		long error;
		do
		{
			error = guess * guess / CFixedPoint.Scale - value;
			long twice = guess * (2 * CFixedPoint.Scale) / CFixedPoint.Scale;
			guess -= twice == 0 ? -1 : error * CFixedPoint.Scale / twice;
		}
		while (error > value || error < -value);
		return guess;
	}

	/// <summary>What an ability in a category is worth towards a technology's bonus, in thousandths.</summary>
	public static long AbilityTerm(CFixedPoint ability)
	{
		long counted = Math.Clamp(ability.Raw, 0, AbilityCounted) - AbilityExpected;
		if (counted > CFixedPoint.Scale)
		{
			counted = RoughSquareRoot(counted);
		}
		return counted * 100 / CFixedPoint.Scale;
	}

	/// <summary>What a level of a technology costs the country, in leadership-days.</summary>
	public static CFixedPoint Cost(CCountry country, CTechnology technology)
	{
		long cost = (technology.Difficulty * CFixedPoint.Scale * DifficultyStep / CFixedPoint.Scale + CFixedPoint.Scale)
			* CostPerLevel / CFixedPoint.Scale;

		long bonus = 0;
		foreach ((string category, CFixedPoint weight) in technology.ResearchBonusFrom)
		{
			bonus += AbilityTerm(country.Ability(category)) * weight.Raw / CFixedPoint.Scale;
		}
		if (technology.IsNuclear)
		{
			bonus += country.Modifier[NuclearResearch].Raw;
		}
		if (bonus > MostBonus)
		{
			bonus = MostBonus;
		}
		return new CFixedPoint(cost * (CFixedPoint.Scale - bonus) / CFixedPoint.Scale);
	}

	/// <summary>
	/// How far one day of that much leadership takes a level of a technology, where
	/// 32768 is the whole level.
	/// </summary>
	public static CFixed15 Gain(CGameState state, CCountry country, CTechnology technology, int level, CFixedPoint leadership)
	{
		CFixed15 cost = CFixed15.FromThousandths(Cost(country, technology));
		if (cost <= CFixed15.Zero)
		{
			return CFixed15.Zero;
		}

		long penalty = YearPenalty(state, technology, level, state.Date.Year);
		long gain = ResearchEfficiency(country) * leadership.Raw / CFixedPoint.Scale;
		gain = penalty == 0 ? -1 : gain * CFixedPoint.Scale / penalty;
		return new CFixed15((CFixed15.FromThousandths(new CFixedPoint(gain)).Raw << CFixed15.Places) / cost.Raw);
	}

	/// <summary>
	/// What researching a level in a year costs over its plain cost, in thousandths: a
	/// thousand, and more for every year it is early. Level 1 is due in the start year,
	/// level 2 at the first offset, and each after that so many years later again.
	/// </summary>
	public static long YearPenalty(CGameState state, CTechnology technology, int level, int year)
	{
		int due = level <= 1
			? technology.StartYear
			: technology.FirstOffset + Math.Max(level - 2, 0) * technology.AdditionalOffset;
		long early = due - year;
		long penalty = CFixedPoint.Scale;
		if (early > 0)
		{
			penalty += early * CFixedPoint.Scale * state.Defines[CountrySection, YearImpact].Raw / CFixedPoint.Scale;
		}
		return penalty;
	}

	/// <summary>How efficiently the country researches, in thousandths, never below a tenth.</summary>
	public static long ResearchEfficiency(CCountry country)
	{
		long efficiency = CFixedPoint.Scale + country.Modifier[Efficiency].Raw + country.Technology[Efficiency].Raw;
		return efficiency < LeastEfficiency ? LeastEfficiency : efficiency;
	}

	/// <summary>Whether the country may research that level of a technology.</summary>
	public static bool MayResearch(CGameState state, CCountry country, CTechnology technology, int level)
	{
		if (technology.IsSingleLevel && (level > 1 || level < 0))
		{
			return false;
		}
		if (level > technology.HighestLevel)
		{
			return false;
		}
		return technology.Allow?.Holds(state, country) ?? true;
	}

	/// <summary>Whether the country may research the next level of a technology.</summary>
	public static bool MayResearchNext(CGameState state, CCountry country, CTechnology technology)
	{
		return MayResearch(state, country, technology, country.TechnologyLevels.GetValueOrDefault(technology.Name) + 1);
	}

	/// <summary>
	/// What each technology on the queue would be given of a day's research leadership:
	/// a whole point, or what is left when less is, in the queue's order; nothing for one
	/// the country may not research, which keeps its place. A technology the leadership
	/// ran out before is not in the answer.
	/// </summary>
	/// <param name="allocation">The research share's leadership for the day.</param>
	public static Dictionary<string, CFixedPoint> ShareOut(CGameState state, CCountry country, CFixed15 allocation)
	{
		var given = new Dictionary<string, CFixedPoint>(StringComparer.OrdinalIgnoreCase);
		long remaining = allocation.Raw;
		foreach (string name in country.ResearchQueue)
		{
			if (remaining <= 0)
			{
				break;
			}
			if (!state.Technologies.TryGetValue(name, out CTechnology? technology))
			{
				continue;
			}
			if (!MayResearchNext(state, country, technology))
			{
				given[name] = CFixedPoint.Zero;
				continue;
			}
			long share = Math.Min(CFixed15.Scale, remaining);
			remaining -= share;
			given[name] = new CFixed15(share).ToThousandths();
		}
		return given;
	}

	/// <summary>
	/// How many days that much leadership a day takes to finish the level a country is
	/// on: what is left of the level over a day's gain, and one. -1 when a day gains nothing.
	/// </summary>
	public static int DaysToFinish(CGameState state, CCountry country, CTechnology technology, CFixedPoint leadership)
	{
		int level = country.TechnologyLevels.GetValueOrDefault(technology.Name) + 1;
		CFixed15 gain = Gain(state, country, technology, level, leadership);
		if (gain <= CFixed15.Zero)
		{
			return -1;
		}
		CFixed15 left = CFixed15.One - country.ResearchProgress.GetValueOrDefault(technology.Name);
		return (int)(left.Raw / gain.Raw) + 1;
	}

	/// <summary>
	/// One day of a country's research: its queue is worked through in order, each
	/// technology taking a point of leadership or what is left, until none is.
	/// </summary>
	/// <param name="allocation">The research share's leadership for the day.</param>
	public static void RunDaily(CGameState state, CCountry country, CFixed15 allocation)
	{
		country.ResearchLeadership.Clear();
		long remaining = allocation.Raw;
		List<CTechnology>? finished = null;

		foreach (string name in country.ResearchQueue.ToArray())
		{
			if (remaining <= 0)
			{
				break;
			}
			if (!state.Technologies.TryGetValue(name, out CTechnology? technology))
			{
				continue;
			}
			if (!MayResearchNext(state, country, technology))
			{
				// It keeps its place, and is given nothing.
				country.ResearchLeadership[name] = CFixedPoint.Zero;
				continue;
			}

			long given = Math.Min(CFixed15.Scale, remaining);
			remaining -= given;
			if (!Advance(state, country, technology, new CFixed15(given).ToThousandths()))
			{
				continue;
			}

			GainAbility(state, country, technology.OnCompletion, CFixedPoint.One);
			country.ResearchQueue.Remove(name);
			if (!technology.IsSingleLevel)
			{
				(finished ??= new List<CTechnology>()).Add(technology);
			}
		}

		// What has further levels goes back on at the end, once everything has had its day.
		foreach (CTechnology technology in finished ?? (IEnumerable<CTechnology>)Array.Empty<CTechnology>())
		{
			if (MayResearchNext(state, country, technology))
			{
				country.ResearchQueue.Add(technology.Name);
			}
		}
		// And a tech minister, told of each, takes it off again (ministers.md, section 2).
		if (finished is not null)
		{
			CResearchAi.LevelResearched(state, country, finished);
		}
	}

	/// <summary>Gives a technology its day's leadership, and answers whether that finished a level.</summary>
	public static bool Advance(CGameState state, CCountry country, CTechnology technology, CFixedPoint leadership)
	{
		string name = technology.Name;
		int level = country.TechnologyLevels.GetValueOrDefault(name);
		country.ResearchLeadership[name] = leadership;

		CFixed15 progress = country.ResearchProgress.GetValueOrDefault(name)
			+ Gain(state, country, technology, level + 1, leadership);
		if (progress < CFixed15.One)
		{
			country.ResearchProgress[name] = progress;
			return false;
		}

		// What was over carries into the next level, unless there is no next level.
		progress -= CFixed15.One;
		country.ResearchProgress[name] = technology.IsSingleLevel || progress < CFixed15.Zero ? CFixed15.Zero : progress;
		country.TechnologyLevels[name] = level + 1;
		CCountryModifiers.RebuildTechnology(state, country);
		return true;
	}

	/// <summary>
	/// Raises a country's ability in a category. A gain counts in full while the ability
	/// is under the divisor the defines give, and for less and less above it.
	/// </summary>
	public static void GainAbility(CGameState state, CCountry country, string category, CFixedPoint gain)
	{
		if (category.Length == 0)
		{
			return;
		}
		long ability = country.Ability(category).Raw;
		long divisor = state.Defines[CountrySection, AbilityGainDivisor].Raw;
		long scale = divisor == 0 ? -1 : ability * CFixedPoint.Scale / divisor;
		if (scale < CFixedPoint.Scale)
		{
			scale = CFixedPoint.Scale;
		}
		SetAbility(state, country, category, ability + gain.Raw * CFixedPoint.Scale / scale);
	}

	/// <summary>Sets an ability, which is never below nothing or above the most the defines allow.</summary>
	public static void SetAbility(CGameState state, CCountry country, string category, long thousandths)
	{
		long most = state.Defines[CountrySection, MostAbility].Raw;
		country.Abilities[category] = new CFixedPoint(Math.Clamp(thousandths, 0, Math.Max(most, 0)));
	}

	/// <summary>
	/// The fraction of its IC a country has set aside for reinforcement and upgrades, in
	/// thousandths. It slows the decay of what the country knows.
	/// </summary>
	public static long DecaySlack(CCountry country)
	{
		long shares = country.ProductionShares[(int)EProductionCategory.Upgrades].Raw
			+ country.ProductionShares[(int)EProductionCategory.Reinforcement].Raw;
		return (shares * CFixedPoint.Scale) >> CFixed15.Places;
	}

	/// <summary>
	/// What a country's ministers and its technologies add up to for a category's decay,
	/// in thousandths.
	/// </summary>
	public static long DecayModifier(CGameState state, CCountry country, string category)
	{
		long total = CCountryModifiers.MinisterDecay(country, category);
		foreach ((string name, int level) in country.TechnologyLevels)
		{
			if (level <= 0 || !state.Technologies.TryGetValue(name, out CTechnology? technology))
			{
				continue;
			}
			foreach ((string decayed, CFixedPoint amount) in technology.Decay)
			{
				if (decayed.Equals(category, StringComparison.OrdinalIgnoreCase))
				{
					total += amount.Raw * level;
				}
			}
		}
		return total;
	}

	/// <summary>The month's decay: every ability of every country shrinks by a fraction.</summary>
	public static void DecayMonthly(CGameState state)
	{
		long baseDecay = state.Defines[CountrySection, BaseDecay].Raw;
		foreach (CCountry country in state.Countries)
		{
			long kept = CFixedPoint.Scale - DecaySlack(country);
			foreach (CTechnologyCategory category in state.TechnologyCategories)
			{
				if (!country.Abilities.TryGetValue(category.Name, out CFixedPoint ability))
				{
					continue;
				}
				long step = baseDecay * (CFixedPoint.Scale + DecayModifier(state, country, category.Name)) / CFixedPoint.Scale;
				step = step * kept / CFixedPoint.Scale;
				if (step > 0)
				{
					step = 0;                                  // a decay cannot be turned into a gain
				}
				country.Abilities[category.Name] = new CFixedPoint(ability.Raw * (CFixedPoint.Scale + step) / CFixedPoint.Scale);
			}
		}
	}
}
