using System;
using System.Globalization;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the technology screen writes into its elements, as the original builds them.
/// See <c>docs/mechanics/technologyscreen.md</c>.
/// </summary>
public static class CTechnologyScreenText
{
	public const string LeadershipKey = "DISPOSAL_OF_LEADERSHIP";
	public const string ProjectsKey = "CURR_RES_LABEL";
	public const string AtMaxKey = "TECH_AT_MAX";
	public const string StartKey = "ITEM_RESEARCH_START";
	public const string CancelKey = "cancel";

	/// <summary>What the queue shows when a level has no day it will be finished on.</summary>
	public const string NoDate = "?";

	/// <summary>What a skill shows when there is next to none of it.</summary>
	public const string NoSkill = "-";

	/// <summary>A skill at or under this, in thousandths, is not counted out.</summary>
	private const long LeastSkillShown = 99;

	/// <summary>The most frames the skill indicator has.</summary>
	private const long MostSkillFrames = 20;

	/// <summary>A project given at least this much of a point, in thousandths, is fully manned.</summary>
	private const long FullyManned = 990;

	/// <summary>The heading over the four sliders: the words and the country's leadership to two decimals.</summary>
	public static string LeadershipLabel(Func<string, string> localise, CCountry country)
	{
		return localise(LeadershipKey) + ":" + CColouredText.In(CColouredText.Yellow) + " "
			+ CNumberText.Format(country.TotalLeadership, 2);
	}

	/// <summary>
	/// The line over the queue: how many projects there are, of how many the research
	/// leadership mans. The count is green until it is a whole project or more over,
	/// and yellow after.
	/// </summary>
	public static string ProjectsLabel(Func<string, string> localise, int projects, CFixedPoint researchLeadership)
	{
		char colour = projects * CFixedPoint.Scale - researchLeadership.Raw >= CFixedPoint.Scale
			? CColouredText.Yellow
			: CColouredText.Green;
		return CTextTemplate.Fill(localise(ProjectsKey),
			("CURR", CColouredText.In(colour) + projects.ToString(CultureInfo.InvariantCulture) + CColouredText.In('W')),
			("MAX", CNumberText.Format(researchLeadership, 2)));
	}

	/// <summary>Which background a technology in the tree gets.</summary>
	public static ETechBackground Background(CTechnology technology, int level, bool mayResearch)
	{
		if (technology.IsSingleLevel)
		{
			return level >= 1 ? ETechBackground.Finished : mayResearch ? ETechBackground.OneShot : ETechBackground.Cannot;
		}
		return mayResearch ? ETechBackground.Endless : ETechBackground.Cannot;
	}

	/// <summary>
	/// A technology's line in the tree: its name alone when it has one level; otherwise
	/// the name, the level being worked towards and the last there is - or, with every
	/// level had, the words for that.
	/// </summary>
	public static string EntryName(Func<string, string> localise, string name, CTechnology technology, int level)
	{
		if (technology.IsSingleLevel)
		{
			return name;
		}
		int most = technology.HighestLevel;
		if (level < most)
		{
			return name + ": " + (level + 1).ToString(CultureInfo.InvariantCulture)
				+ " / " + most.ToString(CultureInfo.InvariantCulture);
		}
		return name + ": " + localise(AtMaxKey);
	}

	/// <summary>How much of a technology's bar is filled, in hundredths: the levels had of those there are.</summary>
	public static int Progress(CTechnology technology, int level)
	{
		return technology.IsSingleLevel ? 0 : level * 100 / technology.HighestLevel;
	}

	/// <summary>The difficulty: green under 3, yellow from 3 to 5, red above.</summary>
	public static string Difficulty(int difficulty)
	{
		char colour = difficulty < 3 ? CColouredText.Green : difficulty > 5 ? CColouredText.Red : CColouredText.Yellow;
		return CColouredText.In(colour) + difficulty.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The year the level after the one the country has is due.</summary>
	public static int YearDue(CTechnology technology, int level)
	{
		int next = level + 1;
		if (technology.IsSingleLevel || next <= 1)
		{
			return technology.StartYear;
		}
		return technology.FirstOffset + Math.Max(next - 2, 0) * technology.AdditionalOffset;
	}

	/// <summary>Whether an entry shows a difficulty and a year at all: not once a technology of one level is had.</summary>
	public static bool ShowsCost(CTechnology technology, int level)
	{
		return !(technology.IsSingleLevel && level >= 1);
	}

	/// <summary>
	/// A theory or practical as a number: to one decimal, green when it is another
	/// country's being shared, and a dash when there is next to none.
	/// </summary>
	public static string Skill(CFixedPoint level, bool shared)
	{
		if (level.Raw <= LeastSkillShown)
		{
			return NoSkill;
		}
		string text = CNumberText.Format(level, 1);
		return shared ? CColouredText.In(CColouredText.Green) + text : text;
	}

	/// <summary>The frame of the skill indicator: the whole part, at least one for any skill at all, at most twenty.</summary>
	public static int SkillFrame(CFixedPoint level)
	{
		long whole = level.Raw / CFixedPoint.Scale;
		if (whole == 0)
		{
			return level.Raw > 0 ? 1 : 0;
		}
		return (int)Math.Min(whole, MostSkillFrames);
	}

	/// <summary>The day a level will be finished, as the queue writes it: year, month, day.</summary>
	public static string QueueDate(Func<string, string> localise, CDate today, int days)
	{
		if (days < 0)
		{
			return NoDate;
		}
		CDate done = today.AddDays(days);
		return done.Year.ToString(CultureInfo.InvariantCulture) + " " + localise(done.MonthKey) + " "
			+ done.Day.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>The level being worked on, in roman numerals.</summary>
	public static string QueueLevel(int level)
	{
		return CRomanNumerals.Format(level + 1);
	}

	/// <summary>A project's name in the queue, with the level being worked on when it has more than one.</summary>
	public static string QueueName(string name, CTechnology technology, int level)
	{
		return technology.IsSingleLevel ? name : name + ": " + (level + 1).ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>How far a level has got, in hundredths.</summary>
	public static int QueueProgress(CFixed15 progress)
	{
		return (int)((progress.Raw * 100) >> CFixed15.Places);
	}

	/// <summary>Which background a project gets, from the leadership it is given a day.</summary>
	public static EQueueBackground QueueBackground(CFixedPoint leadership)
	{
		return leadership.Raw >= FullyManned ? EQueueBackground.Green
			: leadership.Raw > 0 ? EQueueBackground.Orange
			: EQueueBackground.Red;
	}

	/// <summary>The heading of the panel for one technology: its name and, in brackets, the level it would be.</summary>
	public static string DetailName(string name, CTechnology technology, int level)
	{
		int next = technology.IsSingleLevel ? 1 : level + 1;
		return name + " (" + next.ToString(CultureInfo.InvariantCulture) + ")";
	}

	/// <summary>
	/// The name of one level of a technology, which is where a model's name comes from:
	/// the text of <c>TAG_key_level</c> if there is one, else of <c>key_level</c>, else
	/// the technology's own name with the level after it when it has more than one.
	/// </summary>
	/// <param name="find">Answers the text of a key, or null when there is none.</param>
	public static string LevelName(Func<string, string?> find, string tag, CTechnology technology, string name, int level)
	{
		string number = level.ToString(CultureInfo.InvariantCulture);
		return find(tag + "_" + technology.Name + "_" + number)
			?? find(technology.Name + "_" + number)
			?? (technology.IsSingleLevel ? name : name + ": " + number);
	}
}
