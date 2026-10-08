using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// The research queue, what a level costs, what a day adds and what finishing does,
/// against <c>docs/mechanics/research.md</c>. Ethiopia's figure is the original's own,
/// out of its savegames.
/// </summary>
public class ResearchTests
{
	private static readonly CBuilding NavalBase = new(0, "naval_base", 10, "naval_capacity", CFixedPoint.One);
	private static readonly CBuilding Reactor = new(1, "nuclear_reactor", 10, "nuclear_production", CFixedPoint.One);

	private static CModifier Modifier(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}

	private static CCountry Country(string tag = "GER", params (string Kind, string Value)[] law)
	{
		var country = new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Capital = 1 };
		if (law.Length > 0)
		{
			country.Laws["a_law"] = new CLaw("a_law", Modifier("a_law", law));
		}
		return country;
	}

	/// <summary>A technology of difficulty 2 due in 1918, as most of the first ones are.</summary>
	private static CTechnology Technology(string name, int difficulty = 2, int levels = 1, int startYear = 1918,
		int firstOffset = 1932, CResearchCondition? allow = null, string onCompletion = "infantry_theory",
		bool nuclear = false, params (string Category, string Weight)[] bonus)
	{
		return new CTechnology(name, levels, Modifier(name, ("ic_modifier", "0.1")))
		{
			Difficulty = difficulty,
			StartYear = startYear,
			FirstOffset = firstOffset,
			AdditionalOffset = levels > 1 ? 2 : 0,
			OnCompletion = onCompletion,
			IsNuclear = nuclear,
			Allow = allow,
			ResearchBonusFrom = bonus.Select(entry => (entry.Category, CFixedPoint.Parse(entry.Weight))).ToList(),
		};
	}

	/// <summary>The original's own numbers for the four defines research reads.</summary>
	private static CGameState State(CCountry country, params CTechnology[] technologies)
	{
		var provinces = new List<CProvince>
		{
			new(0, "", isSea: false),
			new(1, "Capital", isSea: false) { Owner = country.Tag, Controller = country.Tag },
		};
		var defines = new CDefines();
		defines.Set("country", "TECHNOLOGY_YEAR_IMPACT", CFixedPoint.Parse("1.5"));
		defines.Set("country", "BASE_TECH_DECAY", CFixedPoint.Parse("-0.025"));
		defines.Set("country", "MAX_TECH_ABILITY", CFixedPoint.FromInt(99));
		defines.Set("country", "TECH_ABILITY_GAIN_DIVISOR", CFixedPoint.FromInt(10));
		var categories = new[]
		{
			new CTechnologyCategory("infantry_theory", true),
			new CTechnologyCategory("infantry_practical", false),
		};
		var state = new CGameState(new CDate(1936, 1, 1), provinces, new[] { country },
			new[] { NavalBase, Reactor }, technologies, null, defines, technologyCategories: categories);
		CTick.RunEntryPass(state);
		return state;
	}

	private static void Research(CGameState state, CCountry country, string leadership)
	{
		CResearch.RunDaily(state, country, CFixed15.FromThousandths(CFixedPoint.Parse(leadership)));
	}

	// --- the square root, and what an ability is worth ---

	[Theory]
	[InlineData(5000, 2250)]       // one step: the true root is 2.236
	[InlineData(10000, 3179)]      // two steps: 3.162
	[InlineData(15000, 3954)]      // two steps: 3.873
	[InlineData(2500, 1625)]       // the first guess is too low, and one step overshoots: 1.581
	[InlineData(1001, 1251)]       // just over one, where it is worst: the true root is 1.0005
	[InlineData(0, 0)]
	[InlineData(-4, 0)]
	public void TheSquareRootStopsAsSoonAsItIsRoughlyRight(long value, long root)
	{
		Assert.Equal(root, CResearch.RoughSquareRoot(value));
	}

	[Theory]
	[InlineData("0", -500)]        // nothing known makes a technology dearer
	[InlineData("2.5", -250)]
	[InlineData("5", 0)]
	[InlineData("6", 100)]         // up to one over, it is taken as it is
	[InlineData("10", 225)]        // above that, the rough root: 2.25
	[InlineData("20", 395)]
	[InlineData("99", 395)]        // nothing past twenty counts
	public void AnAbilityCountsFromFiveAndLessAndLessAboveSix(string ability, long term)
	{
		Assert.Equal(term, CResearch.AbilityTerm(CFixedPoint.Parse(ability)));
	}

	// --- the cost ---

	[Theory]
	[InlineData(0, "125")]
	[InlineData(2, "150")]
	[InlineData(3, "162.5")]
	[InlineData(14, "300")]
	public void ALevelCostsMoreForEachPointOfDifficulty(int difficulty, string cost)
	{
		Assert.Equal(CFixedPoint.Parse(cost), CResearch.Cost(Country(), Technology("t", difficulty)));
	}

	[Fact]
	public void TheCategoriesItNamesMakeItCheaperByTheirWeight()
	{
		CCountry country = Country();
		country.Abilities["infantry_theory"] = CFixedPoint.FromInt(10);
		country.Abilities["infantry_practical"] = CFixedPoint.FromInt(10);
		CTechnology smallArms = Technology("smallarms", difficulty: 3,
			bonus: new[] { ("infantry_theory", "0.3"), ("infantry_practical", "0.7") });

		// 0.225 x 0.3 and 0.225 x 0.7, each cut to a thousandth, are 0.067 and 0.157.
		Assert.Equal(CFixedPoint.Parse("126.1"), CResearch.Cost(country, smallArms));
	}

	[Fact]
	public void ACountryThatKnowsNothingPaysHalfAsMuchAgain()
	{
		CTechnology construction = Technology("construction", bonus: new[] { ("infantry_theory", "0.5"), ("infantry_practical", "0.5") });

		Assert.Equal(CFixedPoint.FromInt(225), CResearch.Cost(Country(), construction));   // Ethiopia's
	}

	[Fact]
	public void ATechnologyIsNeverMoreThanNineTenthsCheaper()
	{
		CCountry country = Country();
		country.Abilities["infantry_theory"] = CFixedPoint.FromInt(20);
		CTechnology easy = Technology("easy", bonus: new[] { ("infantry_theory", "5") });

		Assert.Equal(CFixedPoint.FromInt(15), CResearch.Cost(country, easy));
	}

	[Fact]
	public void NuclearResearchCountsOnlyForATechnologyMarkedNuclear()
	{
		CCountry country = Country("GER", ("nuke_research", "0.4"));
		CGameState state = State(country);

		Assert.Equal(CFixedPoint.FromInt(90), CResearch.Cost(country, Technology("bomb", nuclear: true)));
		Assert.Equal(CFixedPoint.FromInt(150), CResearch.Cost(country, Technology("plough")));
		_ = state;
	}

	// --- what a day adds ---

	[Fact]
	public void ADayOfOnePointOfLeadershipIsWhatTheSavegameSaysForEthiopia()
	{
		// No abilities, a technology of difficulty 2 and the 5% a country outside the
		// factions loses: 138 of 32768 a day.
		CCountry ethiopia = Country("ETH", ("research_efficiency", "-0.05"));
		CTechnology construction = Technology("construction", bonus: new[] { ("infantry_theory", "0.5"), ("infantry_practical", "0.5") });
		CGameState state = State(ethiopia, construction);

		Assert.Equal(138, CResearch.Gain(state, ethiopia, construction, 1, CFixedPoint.One).Raw);
	}

	[Fact]
	public void ResearchingAheadOfTheYearCostsMoreForEveryYearEarly()
	{
		CCountry country = Country();
		CTechnology late = Technology("late", difficulty: 0);
		CTechnology early = Technology("early", difficulty: 0, startYear: 1940);
		CGameState state = State(country, late, early);

		// 125 leadership-days for a level: 262 of 32768 a day. Four years early is seven times as long.
		Assert.Equal(262, CResearch.Gain(state, country, late, 1, CFixedPoint.One).Raw);
		Assert.Equal(37, CResearch.Gain(state, country, early, 1, CFixedPoint.One).Raw);
	}

	[Fact]
	public void LevelTwoIsDueInTheSecondYearGivenAndLaterLevelsSoManyYearsApart()
	{
		CCountry country = Country();
		CTechnology steel = Technology("steel", difficulty: 0, levels: 12, startYear: 1918, firstOffset: 1936);
		CGameState state = State(country, steel);

		Assert.Equal(262, CResearch.Gain(state, country, steel, 2, CFixedPoint.One).Raw);   // due this year
		Assert.Equal(65, CResearch.Gain(state, country, steel, 3, CFixedPoint.One).Raw);    // 1938: two early, four times
		Assert.Equal(37, CResearch.Gain(state, country, steel, 4, CFixedPoint.One).Raw);    // 1940
	}

	[Fact]
	public void TechnologyAndLawsAddToEfficiencyWhichIsNeverUnderATenth()
	{
		CCountry keen = Country("GER", ("research_efficiency", "0.1"));
		CCountry hopeless = Country("ITA", ("research_efficiency", "-5"));
		CTechnology plain = Technology("plain", difficulty: 0);

		Assert.Equal(288, CResearch.Gain(State(keen, plain), keen, plain, 1, CFixedPoint.One).Raw);
		Assert.Equal(26, CResearch.Gain(State(hopeless, plain), hopeless, plain, 1, CFixedPoint.One).Raw);
	}

	// --- the queue ---

	[Fact]
	public void EachTechnologyInTheQueueTakesAPointUntilTheLeadershipRunsOut()
	{
		CCountry country = Country();
		CGameState state = State(country, Technology("a"), Technology("b"), Technology("c"), Technology("d"));
		country.ResearchQueue.AddRange(new[] { "a", "b", "c", "d" });

		Research(state, country, "2.5");

		Assert.Equal(CFixedPoint.One, country.ResearchLeadership["a"]);
		Assert.Equal(CFixedPoint.One, country.ResearchLeadership["b"]);
		Assert.Equal(CFixedPoint.Parse("0.5"), country.ResearchLeadership["c"]);
		Assert.False(country.ResearchLeadership.ContainsKey("d"));
		Assert.Equal(218, country.ResearchProgress["a"].Raw);              // 150 leadership-days a level
		Assert.Equal(109, country.ResearchProgress["c"].Raw);
		Assert.False(country.ResearchProgress.ContainsKey("d"));
	}

	[Fact]
	public void ATechnologyThatMayNotBeResearchedKeepsItsPlaceAndTakesNothing()
	{
		CCountry country = Country();
		var needsOther = new CResearchCondition(EResearchCondition.All);
		needsOther.Children.Add(new CResearchCondition(EResearchCondition.TechnologyLevel, "b", CFixedPoint.One));
		CGameState state = State(country, Technology("a", allow: needsOther), Technology("b"));
		country.ResearchQueue.AddRange(new[] { "a", "b" });

		Research(state, country, "1");

		Assert.Equal(CFixedPoint.Zero, country.ResearchLeadership["a"]);
		Assert.Equal(CFixedPoint.One, country.ResearchLeadership["b"]);
		Assert.Equal(new[] { "a", "b" }, country.ResearchQueue);
		Assert.False(country.ResearchProgress.ContainsKey("a"));
	}

	// --- finishing a level ---

	[Fact]
	public void AFinishedSingleLevelTechnologyLeavesTheQueueAndKeepsNothingOver()
	{
		CCountry country = Country();
		CTechnology plain = Technology("plain", difficulty: 0);
		CGameState state = State(country, plain, Technology("other"));
		country.ResearchQueue.AddRange(new[] { "plain", "other" });
		country.ResearchProgress["plain"] = new CFixed15(32768 - 100);

		Research(state, country, "2");

		Assert.Equal(1, country.TechnologyLevels["plain"]);
		Assert.Equal(0, country.ResearchProgress["plain"].Raw);
		Assert.Equal(new[] { "other" }, country.ResearchQueue);
		Assert.Equal(CFixedPoint.Parse("0.1"), country.Technology["ic_modifier"]);   // what the level is worth, at once
	}

	[Fact]
	public void AFinishedLevelOfManyCarriesWhatWasOverAndGoesToTheBackOfTheQueue()
	{
		CCountry country = Country();
		CTechnology steel = Technology("steel", difficulty: 0, levels: 3, firstOffset: 1936);
		CGameState state = State(country, steel, Technology("other"));
		country.ResearchQueue.AddRange(new[] { "steel", "other" });
		country.ResearchProgress["steel"] = new CFixed15(32768 - 100);

		Research(state, country, "2");

		Assert.Equal(1, country.TechnologyLevels["steel"]);
		Assert.Equal(162, country.ResearchProgress["steel"].Raw);          // 262 gained, 100 of it needed
		Assert.Equal(new[] { "other", "steel" }, country.ResearchQueue);
	}

	[Fact]
	public void TheLastLevelOfManyDoesNotGoBackOn()
	{
		CCountry country = Country();
		CTechnology steel = Technology("steel", difficulty: 0, levels: 3, firstOffset: 1930);
		CGameState state = State(country, steel);
		country.TechnologyLevels["steel"] = 2;
		country.ResearchQueue.Add("steel");
		country.ResearchProgress["steel"] = new CFixed15(32768 - 1);

		Research(state, country, "1");

		Assert.Equal(3, country.TechnologyLevels["steel"]);
		Assert.Empty(country.ResearchQueue);
	}

	[Theory]
	[InlineData("0", "1")]         // a full point while the ability is under the divisor
	[InlineData("9", "10")]
	[InlineData("20", "20.5")]     // half a point at twice it
	[InlineData("98.5", "98.601")] // a tenth of a point, and a little, near the top
	public void FinishingALevelRaisesTheCategoryItNames(string before, string after)
	{
		CCountry country = Country();
		CTechnology plain = Technology("plain", difficulty: 0);
		CGameState state = State(country, plain);
		country.Abilities["infantry_theory"] = CFixedPoint.Parse(before);
		country.ResearchQueue.Add("plain");
		country.ResearchProgress["plain"] = new CFixed15(32768 - 1);

		Research(state, country, "1");

		Assert.Equal(CFixedPoint.Parse(after), country.Ability("infantry_theory"));
	}

	[Fact]
	public void AnAbilityIsNeverAboveTheMostTheDefinesAllow()
	{
		CCountry country = Country();
		CGameState state = State(country);

		CResearch.SetAbility(state, country, "infantry_theory", 150000);
		Assert.Equal(CFixedPoint.FromInt(99), country.Ability("infantry_theory"));
		CResearch.SetAbility(state, country, "infantry_theory", -5);
		Assert.Equal(CFixedPoint.Zero, country.Ability("infantry_theory"));
	}

	// --- what may be researched ---

	[Fact]
	public void ASingleLevelTechnologyHasOneLevelAndOthersStopAtTheirHighest()
	{
		CCountry country = Country();
		CTechnology plain = Technology("plain");
		CTechnology steel = Technology("steel", levels: 3);
		CGameState state = State(country, plain, steel);

		Assert.True(CResearch.MayResearch(state, country, plain, 1));
		Assert.False(CResearch.MayResearch(state, country, plain, 2));
		Assert.True(CResearch.MayResearch(state, country, steel, 3));
		Assert.False(CResearch.MayResearch(state, country, steel, 4));
	}

	[Fact]
	public void AnAllowBlockAsksForLevelsAbilitiesPortsAndBuildings()
	{
		CCountry country = Country();
		CGameState state = State(country, Technology("steel", levels: 3));
		var level = new CResearchCondition(EResearchCondition.TechnologyLevel, "steel", CFixedPoint.FromInt(2));
		var ability = new CResearchCondition(EResearchCondition.Ability, "infantry_practical", CFixedPoint.FromInt(10));
		var ports = new CResearchCondition(EResearchCondition.Ports, "", CFixedPoint.One);
		var reactor = new CResearchCondition(EResearchCondition.OwnsProvinceWithBuilding, "nuclear_reactor");

		Assert.False(level.Holds(state, country));
		country.TechnologyLevels["steel"] = 2;
		Assert.True(level.Holds(state, country));

		Assert.False(ability.Holds(state, country));
		country.Abilities["infantry_practical"] = CFixedPoint.FromInt(10);
		Assert.True(ability.Holds(state, country));

		Assert.False(ports.Holds(state, country));
		state.Province(1).SetBuildingLevel(NavalBase, CFixedPoint.One);
		Assert.True(ports.Holds(state, country));

		Assert.False(reactor.Holds(state, country));
		state.Province(1).SetBuildingLevel(Reactor, CFixedPoint.One);
		Assert.True(reactor.Holds(state, country));
	}

	[Fact]
	public void GroupsOfConditionsHoldTogetherOrSeparatelyOrNotAtAll()
	{
		CCountry country = Country();
		CGameState state = State(country, Technology("a"), Technology("b"));
		country.TechnologyLevels["a"] = 1;
		var hasA = new CResearchCondition(EResearchCondition.TechnologyLevel, "a", CFixedPoint.One);
		var hasB = new CResearchCondition(EResearchCondition.TechnologyLevel, "b", CFixedPoint.One);

		CResearchCondition Group(EResearchCondition kind)
		{
			var group = new CResearchCondition(kind);
			group.Children.Add(hasA);
			group.Children.Add(hasB);
			return group;
		}

		Assert.False(Group(EResearchCondition.All).Holds(state, country));
		Assert.True(Group(EResearchCondition.Any).Holds(state, country));
		Assert.False(Group(EResearchCondition.None).Holds(state, country));
		Assert.True(new CResearchCondition(EResearchCondition.All).Holds(state, country));   // nothing asked
	}

	// --- decay ---

	[Fact]
	public void EveryAbilityShrinksByTheSameFractionEachMonth()
	{
		CCountry country = Country();
		CGameState state = State(country);
		country.Abilities["infantry_theory"] = CFixedPoint.FromInt(10);
		country.Abilities["infantry_practical"] = CFixedPoint.Parse("2.5");

		CResearch.DecayMonthly(state);

		Assert.Equal(CFixedPoint.Parse("9.75"), country.Ability("infantry_theory"));
		Assert.Equal(CFixedPoint.Parse("2.437"), country.Ability("infantry_practical"));
	}

	[Fact]
	public void IcSetAsideForReinforcementAndUpgradesSlowsTheDecay()
	{
		CCountry country = Country();
		CGameState state = State(country);
		country.Abilities["infantry_theory"] = CFixedPoint.FromInt(10);
		country.ProductionShares[(int)EProductionCategory.Reinforcement] = new CFixed15(8192);   // a quarter
		country.ProductionShares[(int)EProductionCategory.Upgrades] = new CFixed15(8192);

		CResearch.DecayMonthly(state);

		Assert.Equal(CFixedPoint.Parse("9.88"), country.Ability("infantry_theory"));   // 1.25% and not 2.5%, less a rounding
	}

	[Fact]
	public void ATechnologysDecayEntryChangesItsCategorysDecayAndCannotTurnItIntoAGain()
	{
		CCountry country = Country();
		var school = new CTechnology("school", 12, new CModifier("school"))
		{
			Decay = new[] { ("infantry_theory", CFixedPoint.Parse("-0.1")) },
		};
		CGameState state = State(country, school);
		country.Abilities["infantry_theory"] = CFixedPoint.FromInt(10);
		country.Abilities["infantry_practical"] = CFixedPoint.FromInt(10);

		country.TechnologyLevels["school"] = 4;
		CResearch.DecayMonthly(state);
		Assert.Equal(CFixedPoint.Parse("9.85"), country.Ability("infantry_theory"));      // six tenths of 2.5%
		Assert.Equal(CFixedPoint.Parse("9.75"), country.Ability("infantry_practical"));   // the other category is untouched

		country.TechnologyLevels["school"] = 12;
		country.Abilities["infantry_theory"] = CFixedPoint.FromInt(10);
		CResearch.DecayMonthly(state);
		Assert.Equal(CFixedPoint.FromInt(10), country.Ability("infantry_theory"));
	}

	// --- in a game ---

	[Fact]
	public void ADayOfTheGameSpendsTheResearchShareOnTheQueue()
	{
		CCountry country = Country();
		CGameState state = State(country, Technology("a"), Technology("b"));
		CLeadership.SetAtStart(state);
		country.ResearchQueue.AddRange(new[] { "a", "b" });

		new CSimulation(state).RunHours(24);

		// No province gives leadership and nothing is in base_values, so there is the one
		// point every country has, and a quarter of it is the research share's.
		Assert.Equal(CFixedPoint.Parse("0.25"), country.ResearchLeadership["a"]);
		Assert.False(country.ResearchLeadership.ContainsKey("b"));
		Assert.Equal(54, country.ResearchProgress["a"].Raw);
	}

	[Fact]
	public void TheFirstOfTheMonthDecaysWhatACountryKnows()
	{
		CCountry country = Country();
		CGameState state = State(country);
		country.Abilities["infantry_theory"] = CFixedPoint.FromInt(10);

		new CSimulation(state).RunHours(24 * 31);

		Assert.Equal(CFixedPoint.Parse("9.75"), country.Ability("infantry_theory"));
	}

	[Fact]
	public void WhatEachProjectWouldBeGivenIsWhatADayGivesIt()
	{
		CCountry country = Country();
		var needsOther = new CResearchCondition(EResearchCondition.All);
		needsOther.Children.Add(new CResearchCondition(EResearchCondition.TechnologyLevel, "a", CFixedPoint.One));
		CGameState state = State(country, Technology("a"), Technology("blocked", allow: needsOther), Technology("c"),
			Technology("d"));
		country.ResearchQueue.AddRange(new[] { "a", "blocked", "c", "d" });

		Dictionary<string, CFixedPoint> given = CResearch.ShareOut(state, country,
			CFixed15.FromThousandths(CFixedPoint.Parse("1.5")));

		Assert.Equal(CFixedPoint.One, given["a"]);
		Assert.Equal(CFixedPoint.Zero, given["blocked"]);                 // keeps its place and takes nothing
		Assert.Equal(CFixedPoint.Parse("0.5"), given["c"]);
		Assert.False(given.ContainsKey("d"));                             // the leadership ran out before it
		Assert.Empty(country.ResearchLeadership);                         // asking changes nothing
	}

	[Fact]
	public void TheDaysALevelTakesAreWhatIsLeftOverADaysGainAndOne()
	{
		CCountry country = Country();
		CTechnology technology = Technology("a");
		CGameState state = State(country, technology);

		// 218 of 32768 a day: 150 whole days, and the one that finishes it.
		Assert.Equal(151, CResearch.DaysToFinish(state, country, technology, CFixedPoint.One));

		country.ResearchProgress["a"] = new CFixed15(CFixed15.Scale / 2);
		Assert.Equal(76, CResearch.DaysToFinish(state, country, technology, CFixedPoint.One));
		Assert.Equal(-1, CResearch.DaysToFinish(state, country, technology, CFixedPoint.Zero));
	}

	[Fact]
	public void TheQueueIsSetByACommandThatRefusesWhatCannotBeResearched()
	{
		CCountry country = Country();
		CGameState state = State(country, Technology("a"), Technology("b"), Technology("done"));
		country.TechnologyLevels["done"] = 1;
		var simulation = new CSimulation(state);

		simulation.Post(new CSetResearchQueueCommand(country.Tag, new[] { "b", "a" }));
		simulation.RunHours(1);
		Assert.Equal(new[] { "b", "a" }, country.ResearchQueue);

		simulation.Post(new CSetResearchQueueCommand(country.Tag, new[] { "a", "a" }));          // twice
		simulation.Post(new CSetResearchQueueCommand(country.Tag, new[] { "a", "nonsense" }));   // not a technology
		simulation.Post(new CSetResearchQueueCommand(country.Tag, new[] { "done" }));            // nothing left of it
		simulation.Post(new CSetResearchQueueCommand(new CCountryTag("XXX"), new[] { "a" }));
		simulation.RunHours(1);
		Assert.Equal(new[] { "b", "a" }, country.ResearchQueue);

		simulation.Post(new CSetResearchQueueCommand(country.Tag, System.Array.Empty<string>()));
		simulation.RunHours(1);
		Assert.Empty(country.ResearchQueue);
	}
}
