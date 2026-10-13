using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenHOI3.Interface.Modifiers;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agreements;
using OpenHOI3.Sim.Aid;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Interface.Tooltips;

/// <summary>
/// What the technology screen's elements say in a tooltip, as the original's screen writes
/// it. See <c>docs/mechanics/technologyscreen.md</c>, section 9. Its sliders' are
/// <see cref="CShareTooltips"/>.
/// </summary>
public static class CTechnologyTooltips
{
	private const string White = "§W";
	private const string GreenPlus = "§G+";
	private const string LocalLeadership = "local_leadership";
	private const string LocalLeadershipModifier = "local_leadership_modifier";
	private const string GlobalLeadership = "global_leadership";
	private const string GlobalLeadershipModifier = "global_leadership_modifier";
	private const string TechnologyLeadership = "leadership_gain";
	private const string ResearchEfficiency = "research_efficiency";

	/// <summary>A year's days, in thousandths, and what dividing a year by a penalty is done with.</summary>
	private const long Year = CDate.DaysPerYear * CFixedPoint.Scale;

	/// <summary>
	/// Where the country's leadership comes from: the provinces it controls, the sources of
	/// the two global kinds, what technology adds, and a line for each theory it shares,
	/// <c>SHARE_TECH_TOOLTIP</c> with what it costs to one place (<c>aid.md</c>, section 2).
	/// </summary>
	public static string LeadershipLabel(Func<string, string> localise, CGameState state, CCountry country)
	{
		long sum = 0;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Controller != country.Tag)
			{
				continue;
			}
			long leadership = province.Leadership.Raw + province.Modifier[LocalLeadership].Raw;
			long factor = Math.Max(0, CFixedPoint.Scale + province.Modifier[LocalLeadershipModifier].Raw);
			sum += Math.Max(0, leadership * factor / CFixedPoint.Scale);
		}

		var text = new StringBuilder();
		text.Append(localise("BASE")).Append(" §Y").Append(CNumberText.Format(new CFixedPoint(sum), 2)).Append(White).Append('\n');
		text.Append(CModifierText.Sources(localise, country.ModifierSources, GlobalLeadership));
		text.Append(CModifierText.Sources(localise, country.ModifierSources, GlobalLeadershipModifier));
		CFixedPoint technology = country.Technology[TechnologyLeadership];
		if (technology.Raw != 0)
		{
			text.Append('\n').Append(FromTechnology(localise, technology));
		}
		CFixedPoint cost = state.Defines[COfferRules.Section, CTechnologySharing.LeadershipCostDefine];
		foreach ((CCountry receiver, string category) in country.TechnologyShares)
		{
			text.Append(CTextTemplate.Fill(localise("SHARE_TECH_TOOLTIP"), ("TECH", localise(category)),
				("COUNTRY", receiver.Name), ("COST", CNumberText.Format(cost, 1)))).Append('\n');
		}
		return text.ToString();
	}

	/// <summary>What technology adds, as the technology screen writes it: cut to two places, a line break after.</summary>
	private static string FromTechnology(Func<string, string> localise, CFixedPoint value)
	{
		return localise("FROM_TECHNOLOGY") + ": " + GreenPlus + CNumberText.Format(new CFixedPoint(value.Raw * 100), 2) + "%" + White + "\n";
	}

	/// <summary>The level a tree entry speaks of: the next the country would research, or 1 for a technology of one level.</summary>
	public static int EntryLevel(CCountry country, CTechnology technology)
	{
		return technology.AdditionalOffset == 0 ? 1 : country.TechnologyLevels.GetValueOrDefault(technology.Name) + 1;
	}

	/// <summary>
	/// A tree entry: the name and the level, why it may not be researched where it may not,
	/// what as many levels as that do, and a double click starting or stopping it.
	/// </summary>
	public static CTooltip TreeEntry(Func<string, string> localise, CGameState state, CCountry country, CTechnology technology)
	{
		var text = new StringBuilder();
		int level = EntryLevel(country, technology);
		text.Append(localise(technology.Name)).Append(" (").Append(level.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");
		bool queued = country.ResearchQueue.Contains(technology.Name, StringComparer.OrdinalIgnoreCase);
		if (!CResearch.MayResearchNext(state, country, technology))
		{
			text.Append(queued ? localise("TTE_ALREADY") : WhyNot(localise, state, country, technology)).Append('\n');
		}
		text.Append(CTechnologyEffectsText.For(localise, state, country, technology, level));
		return new CTooltip
		{
			Text = text.ToString(),
			DelayedExtra = localise(queued ? "TECH_DC_TO_STOP" : "TECH_DC_TO_START"),
		};
	}

	/// <summary>
	/// Why a technology may not be researched: had already, for one of one level; otherwise its
	/// <c>allow</c> block's account of itself - every condition, met or not.
	/// </summary>
	public static string WhyNot(Func<string, string> localise, CGameState state, CCountry country, CTechnology technology)
	{
		int next = country.TechnologyLevels.GetValueOrDefault(technology.Name) + 1;
		if (technology.AdditionalOffset == 0 && (next > 1 || next < 0))
		{
			return localise("ALREADY_RESEARCHED");
		}
		return technology.Allow is { } allow ? CRequirementText.For(localise, state, country, allow) : "";
	}

	/// <summary>A theory or practical: its name.</summary>
	public static string Skill(Func<string, string> localise, CTechnologyCategory category)
	{
		return localise(category.Name);
	}

	/// <summary>
	/// A theory's delayed half where another country shares it: <c>TW_SKILL_SHARE</c>, the sharer, the higher
	/// of the two levels and the country's own, each to one place; nothing where none does.
	/// </summary>
	public static string SkillShared(Func<string, string> localise, CCountry country, CTechnologyCategory category)
	{
		if (!country.TechnologySharedFrom.TryGetValue(category.Name, out CCountry? sharer))
		{
			return "";
		}
		return CTextTemplate.Fill(localise("TW_SKILL_SHARE"), ("COUNTRY", sharer.Name),
			("THEIR", CNumberText.Format(country.Ability(category.Name), 1)),
			("OUR", CNumberText.Format(country.OwnAbility(category.Name), 1)));
	}

	/// <summary>
	/// A line of the queue, by which of its elements is under the mouse - what its buttons do,
	/// or the level being researched - and then what research efficiency is made of.
	/// </summary>
	public static string QueueLine(Func<string, string> localise, CCountry country, CTechnology technology, string element)
	{
		var text = new StringBuilder();
		switch (element.ToLowerInvariant())
		{
			case "tech_prio_maxup":
				text.Append(localise("PEN_MAX_PRIO"));
				break;
			case "tech_prio_up":
				text.Append(localise("PEN_PRIO"));
				break;
			case "tech_prio_down":
				text.Append(localise("PEN_NON_PRIO"));
				break;
			case "tech_prio_maxdown":
				text.Append(localise("PEN_MAX_NON_PRIO"));
				break;
			case "cancel":
				text.Append(localise("TECH_SUSPEND"));
				break;
			default:
				if (technology.AdditionalOffset != 0)
				{
					text.Append(CTextTemplate.Fill(localise("TECH_TREE_INFO"),
						("NAME", localise(technology.Name)),
						("LEVEL", (country.TechnologyLevels.GetValueOrDefault(technology.Name) + 1).ToString(CultureInfo.InvariantCulture))));
				}
				break;
		}

		string sources = CModifierText.Sources(localise, country.ModifierSources, ResearchEfficiency);
		CFixedPoint fromTechnology = country.Technology[ResearchEfficiency];
		if (sources.Length > 1 || fromTechnology.Raw > 0)
		{
			text.Append('\n').Append(localise("MODIFIER_RESEARCH_EFFICIENCY")).Append(":\n");
			if (fromTechnology.Raw > 0)
			{
				text.Append(FromTechnology(localise, fromTechnology));
			}
			if (sources.Length > 1)
			{
				text.Append(sources);
			}
		}
		return text.ToString();
	}

	/// <summary>
	/// A category icon of the panel for one technology: the country's skill in it, its weight
	/// for this technology, and what it does to the research's speed.
	/// </summary>
	public static string CategoryIcon(Func<string, string> localise, CCountry country, string category, CFixedPoint weight)
	{
		CFixedPoint skill = country.Ability(category);
		long speed = CResearch.AbilityTerm(skill);
		string sign = speed < 0 ? CColouredText.In(CColouredText.Red) : speed > 0 ? GreenPlus : "";
		return CTextTemplate.Fill(localise("TECH_ICON_SPEED_MOD"),
			("NAME", localise(category)),
			("SKILL", CNumberText.Format(skill, 1)),
			("PERCENT", CNumberText.Format(new CFixedPoint(weight.Raw * 100), 0)),
			("SPEED", sign + CNumberText.Trimmed(new CFixedPoint(speed * 100))));
	}

	/// <summary>
	/// The panel's start button: already on the queue, or how many days a level would take at
	/// full funding, or why it may not be researched.
	/// </summary>
	public static string Start(Func<string, string> localise, CGameState state, CCountry country, CTechnology technology)
	{
		if (country.ResearchQueue.Contains(technology.Name, StringComparer.OrdinalIgnoreCase))
		{
			return localise("TTE_ALREADY");
		}
		if (!CResearch.MayResearchNext(state, country, technology))
		{
			return WhyNot(localise, state, country, technology);
		}
		return CTextTemplate.Fill(localise("START_RESEARCH"),
			("DAYS", CNumberText.Format(new CFixedPoint(EstimatedDays(state, country, technology)), 0)));
	}

	/// <summary>
	/// The days, in thousandths, a level would take from nothing at one leadership a day, as the
	/// start button works it out: the cost over the efficiency, made dearer by the year's penalty,
	/// and where that runs past the year's end, the rest at each next year's penalty.
	/// </summary>
	public static long EstimatedDays(CGameState state, CCountry country, CTechnology technology)
	{
		int level = country.TechnologyLevels.GetValueOrDefault(technology.Name) + 1;
		long efficiency = CResearch.ResearchEfficiency(country);
		long cost = CResearch.Cost(country, technology).Raw;
		long work = efficiency == 0 ? -1 : cost * CFixedPoint.Scale / efficiency;

		int year = state.Date.Year;
		long penalty = CResearch.YearPenalty(state, technology, level, year);
		int dayOfYear = state.Date.DayNumber - year * CDate.DaysPerYear;
		long left = (CDate.DaysPerYear - dayOfYear) * CFixedPoint.Scale;
		long days = work * penalty / CFixedPoint.Scale;
		if (days <= left)
		{
			return days;
		}

		days = left;
		work -= penalty == 0 ? -1 : left * CFixedPoint.Scale / penalty;
		while (work > 0)
		{
			year++;
			penalty = CResearch.YearPenalty(state, technology, level, year);
			long thisYear = penalty * work / CFixedPoint.Scale;
			if (thisYear <= Year)
			{
				days += thisYear;
				work = 0;
			}
			else
			{
				work -= penalty == 0 ? -1 : Year * CFixedPoint.Scale / penalty;
				days += Year;
			}
		}
		return days;
	}

	/// <summary>The panel's picture of what a level adds to: that category, by one.</summary>
	public static string IconFinish(Func<string, string> localise, CTechnology technology)
	{
		return CTextTemplate.Fill(localise("ICON_FINISH"), ("NAME", localise(technology.OnCompletion)));
	}
}
