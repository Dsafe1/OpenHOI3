using System.Collections.Generic;
using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>
/// What the technology screen writes into its elements, against
/// <c>docs/mechanics/technologyscreen.md</c>. The words are the original's own English.
/// </summary>
public class TechnologyScreenTextTests
{
	private static readonly Dictionary<string, string> Words = new()
	{
		["DISPOSAL_OF_LEADERSHIP"] = "Distribution of leadership",
		["CURR_RES_LABEL"] = "$CURR$ projects of §Y$MAX$§W possible.",
		["TECH_AT_MAX"] = "(Max)",
		["GER_small_arms_2"] = "MP 38",
		["small_arms_3"] = "Third pattern",
	};

	private static string Localise(string key)
	{
		return Words.GetValueOrDefault(key, key);
	}

	private static string? Find(string key)
	{
		return Words.GetValueOrDefault(key);
	}

	private static CTechnology Many(int levels = 12)
	{
		return new CTechnology("small_arms", levels, new CModifier("small_arms"))
		{
			Difficulty = 4, StartYear = 1918, FirstOffset = 1934, AdditionalOffset = 2,
		};
	}

	private static CTechnology One()
	{
		return new CTechnology("radio", 1, new CModifier("radio")) { Difficulty = 7, StartYear = 1936 };
	}

	[Fact]
	public void TheHeadingIsTheWordsAndTheLeadershipToTwoDecimals()
	{
		var country = new CCountry { Tag = new CCountryTag("GER"), Name = "Germany", DefinitionFile = "" };
		country.TotalLeadership = CFixedPoint.Parse("27.319");

		Assert.Equal("Distribution of leadership:§Y 27.31", CTechnologyScreenText.LeadershipLabel(Localise, country));
	}

	[Fact]
	public void TheProjectsLineCountsThemAgainstTheResearchLeadership()
	{
		Assert.Equal("§G7§W projects of §Y6.82§W possible.",
			CTechnologyScreenText.ProjectsLabel(Localise, 7, CFixedPoint.Parse("6.82")));
	}

	[Fact]
	public void TheCountTurnsYellowWhenItIsAWholeProjectOver()
	{
		Assert.StartsWith("§G", CTechnologyScreenText.ProjectsLabel(Localise, 7, CFixedPoint.Parse("6.001")));
		Assert.StartsWith("§Y", CTechnologyScreenText.ProjectsLabel(Localise, 7, CFixedPoint.FromInt(6)));
		Assert.StartsWith("§G", CTechnologyScreenText.ProjectsLabel(Localise, 0, CFixedPoint.FromInt(6)));
	}

	[Fact]
	public void ATechnologyOfOneLevelIsFinishedOrMayBeResearchedOrMayNot()
	{
		Assert.Equal(ETechBackground.Finished, CTechnologyScreenText.Background(One(), 1, mayResearch: false));
		Assert.Equal(ETechBackground.OneShot, CTechnologyScreenText.Background(One(), 0, mayResearch: true));
		Assert.Equal(ETechBackground.Cannot, CTechnologyScreenText.Background(One(), 0, mayResearch: false));
	}

	[Fact]
	public void ATechnologyOfManyLevelsIsNeverShownFinished()
	{
		Assert.Equal(ETechBackground.Endless, CTechnologyScreenText.Background(Many(), 12, mayResearch: true));
		Assert.Equal(ETechBackground.Cannot, CTechnologyScreenText.Background(Many(), 12, mayResearch: false));
	}

	[Fact]
	public void AnEntryNamesTheLevelBeingWorkedTowardsAndTheLast()
	{
		Assert.Equal("Small Arms: 3 / 12", CTechnologyScreenText.EntryName(Localise, "Small Arms", Many(), 2));
		Assert.Equal("Small Arms: 12 / 12", CTechnologyScreenText.EntryName(Localise, "Small Arms", Many(), 11));
		Assert.Equal("Small Arms: (Max)", CTechnologyScreenText.EntryName(Localise, "Small Arms", Many(), 12));
		Assert.Equal("Radio", CTechnologyScreenText.EntryName(Localise, "Radio", One(), 0));
	}

	[Fact]
	public void TheBarIsTheLevelsHadOfThoseThereAre()
	{
		Assert.Equal(25, CTechnologyScreenText.Progress(Many(), 3));
		Assert.Equal(16, CTechnologyScreenText.Progress(Many(), 2));        // cut off, not rounded
		Assert.Equal(0, CTechnologyScreenText.Progress(One(), 1));
	}

	[Theory]
	[InlineData(0, "§G0")]
	[InlineData(2, "§G2")]
	[InlineData(3, "§Y3")]
	[InlineData(5, "§Y5")]
	[InlineData(6, "§R6")]
	[InlineData(10, "§R10")]
	public void DifficultyIsGreenUnderThreeAndRedAboveFive(int difficulty, string text)
	{
		Assert.Equal(text, CTechnologyScreenText.Difficulty(difficulty));
	}

	[Theory]
	[InlineData(0, 1918)]          // level 1 is due in the start year
	[InlineData(1, 1934)]          // level 2 in the first offset
	[InlineData(2, 1936)]          // and each one after so many years later
	[InlineData(5, 1942)]
	public void TheYearIsWhenTheNextLevelIsDue(int level, int year)
	{
		Assert.Equal(year, CTechnologyScreenText.YearDue(Many(), level));
	}

	[Fact]
	public void ATechnologyOfOneLevelIsAlwaysDueInItsStartYearAndShowsNoCostOnceHad()
	{
		Assert.Equal(1936, CTechnologyScreenText.YearDue(One(), 0));
		Assert.True(CTechnologyScreenText.ShowsCost(One(), 0));
		Assert.False(CTechnologyScreenText.ShowsCost(One(), 1));
		Assert.True(CTechnologyScreenText.ShowsCost(Many(), 12));
	}

	[Theory]
	[InlineData("0", "-")]
	[InlineData("0.099", "-")]
	[InlineData("0.1", "0.1")]
	[InlineData("2.59", "2.5")]
	[InlineData("10", "10.0")]
	public void ASkillIsWrittenToOneDecimalOrAsADash(string level, string text)
	{
		Assert.Equal(text, CTechnologyScreenText.Skill(CFixedPoint.Parse(level), shared: false));
	}

	[Fact]
	public void ASharedSkillIsGreen()
	{
		Assert.Equal("§G7.5", CTechnologyScreenText.Skill(CFixedPoint.Parse("7.5"), shared: true));
	}

	[Theory]
	[InlineData("0", 0)]
	[InlineData("0.4", 1)]         // any skill at all lights the first step
	[InlineData("1.9", 1)]
	[InlineData("10", 10)]
	[InlineData("25", 20)]
	public void TheSkillBarHasTwentySteps(string level, int frame)
	{
		Assert.Equal(frame, CTechnologyScreenText.SkillFrame(CFixedPoint.Parse(level)));
	}

	[Fact]
	public void TheQueueWritesTheDayALevelIsDoneYearFirst()
	{
		Assert.Equal("1936 April 14", CTechnologyScreenText.QueueDate(Localise, new CDate(1936, 1, 1), 103));
	}

	[Fact]
	public void AProjectThatGainsNothingHasNoDate()
	{
		Assert.Equal("?", CTechnologyScreenText.QueueDate(Localise, new CDate(1936, 1, 1), -1));
	}

	[Fact]
	public void TheQueueWritesTheLevelInRomanNumerals()
	{
		Assert.Equal("II", CTechnologyScreenText.QueueLevel(1));
		Assert.Equal("V", CTechnologyScreenText.QueueLevel(4));
	}

	[Fact]
	public void TheQueueNamesTheLevelOnlyWhereThereIsMoreThanOne()
	{
		Assert.Equal("Small Arms: 2", CTechnologyScreenText.QueueName("Small Arms", Many(), 1));
		Assert.Equal("Radio", CTechnologyScreenText.QueueName("Radio", One(), 0));
	}

	[Theory]
	[InlineData("1", EQueueBackground.Green)]
	[InlineData("0.99", EQueueBackground.Green)]
	[InlineData("0.989", EQueueBackground.Orange)]
	[InlineData("0.001", EQueueBackground.Orange)]
	[InlineData("0", EQueueBackground.Red)]
	public void AProjectIsGreenWithAWholePointOrangeWithPartAndRedWithNone(string leadership, EQueueBackground background)
	{
		Assert.Equal(background, CTechnologyScreenText.QueueBackground(CFixedPoint.Parse(leadership)));
	}

	[Fact]
	public void ProgressInTheQueueIsInHundredths()
	{
		Assert.Equal(50, CTechnologyScreenText.QueueProgress(new CFixed15(CFixed15.Scale / 2)));
		Assert.Equal(99, CTechnologyScreenText.QueueProgress(new CFixed15(CFixed15.Scale - 1)));
	}

	[Fact]
	public void ThePanelNamesTheLevelItWouldBeInBrackets()
	{
		Assert.Equal("Small Arms (2)", CTechnologyScreenText.DetailName("Small Arms", Many(), 1));
		Assert.Equal("Radio (1)", CTechnologyScreenText.DetailName("Radio", One(), 0));
		Assert.Equal("Radio (1)", CTechnologyScreenText.DetailName("Radio", One(), 1));
	}

	[Fact]
	public void ALevelsNameIsTheCountrysOwnThenAnyonesThenTheTechnologys()
	{
		Assert.Equal("MP 38", CTechnologyScreenText.LevelName(Find, "GER", Many(), "Small Arms", 2));
		Assert.Equal("Third pattern", CTechnologyScreenText.LevelName(Find, "GER", Many(), "Small Arms", 3));
		Assert.Equal("Small Arms: 4", CTechnologyScreenText.LevelName(Find, "GER", Many(), "Small Arms", 4));
		Assert.Equal("Radio", CTechnologyScreenText.LevelName(Find, "GER", One(), "Radio", 1));
	}

	[Theory]
	[InlineData(1, "I")]
	[InlineData(4, "IV")]
	[InlineData(9, "IX")]
	[InlineData(12, "XII")]
	[InlineData(40, "XL")]
	[InlineData(50, "L")]
	[InlineData(0, "")]
	public void RomanNumeralsAreTheUsualOnes(int value, string text)
	{
		Assert.Equal(text, CRomanNumerals.Format(value));
	}
}
