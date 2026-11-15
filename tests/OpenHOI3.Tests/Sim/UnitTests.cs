using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a brigade is made of and what it starts a new game with, against
/// <c>docs/mechanics/units.md</c>. The German figures are the original's own, out of its
/// savegame: reserves at half of 30 on a two-year draft, with 7.5 experience where the
/// training law gives 15.
/// </summary>
public class UnitTests
{
	private static readonly CCountryTag Ger = new("GER");

	private static CSubUnitStats Stats(params (string Statistic, string Value)[] values)
	{
		var stats = new CSubUnitStats();
		foreach ((string statistic, string value) in values)
		{
			stats.Values.Set(statistic, CFixedPoint.Parse(value));
		}
		return stats;
	}

	/// <summary>A technology that gives a unit type something a level.</summary>
	private static CTechnology Technology(string name, CSubUnitType type, bool change = true, int additionalOffset = 2,
		bool hasPrecondition = false, params (string Statistic, string Value)[] gives)
	{
		var technology = new CTechnology(name, 12, new CModifier(name))
		{
			Change = change,
			AdditionalOffset = additionalOffset,
			HasPrecondition = hasPrecondition,
		};
		technology.UnitEffects[type.Key] = Stats(gives);
		type.Technologies.Add(technology);
		return technology;
	}

	private static CSubUnitType Infantry()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Stats.CopyFrom(Stats(("max_strength", "30"), ("default_organisation", "30"), ("officers", "100"),
			("combat_width", "1"), ("build_cost_ic", "2.33"), ("build_cost_manpower", "3.33"), ("build_time", "95"),
			("toughness", "3"), ("repair_cost_multiplier", "0.05")));
		return type;
	}

	private static CSubUnitType Headquarters()
	{
		var type = new CSubUnitType("hq_brigade", EUnitBranch.Land);
		type.Stats.CopyFrom(Stats(("max_strength", "10"), ("default_organisation", "30"), ("officers", "100")));
		return type;
	}

	private static CCountry Country(string conscription = "-0.5", string training = "15")
	{
		var country = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		country.Modifier.Set(CUnits.ReservesPenaltySize, CFixedPoint.Parse(conscription));
		country.Modifier.Set(CUnits.UnitStartExperience, CFixedPoint.Parse(training));
		return country;
	}

	private static CGameState State(CCountry country, IEnumerable<CTechnology>? technologies = null, CWar? war = null,
		int models = 10)
	{
		var defines = new CDefines();
		defines.Set(CHistoricalModels.MilitarySection, CHistoricalModels.MaxDefine, CFixedPoint.FromInt(models));
		return new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { country },
			technologies: technologies, defines: defines, wars: war is null ? null : new[] { war });
	}

	private static CSubUnit Brigade(CSubUnitType type, CUnit unit, int model = 0, bool reserve = false)
	{
		var brigade = new CSubUnit(type) { Unit = unit, HistoricalModel = model, IsReserve = reserve };
		unit.SubUnits.Add(brigade);
		return brigade;
	}

	// ---- what a brigade is made of

	[Fact]
	public void ABrigadeIsMadeWithMoreThanAnyTypeHasAndNoExperienceGiven()
	{
		var brigade = new CSubUnit(Infantry());

		Assert.Equal(CFixedPoint.FromInt(100), brigade.Strength);
		Assert.Equal(CFixedPoint.FromInt(100), brigade.Organisation);
		Assert.True(brigade.Experience < CFixedPoint.Zero);
		Assert.Equal(CSubUnit.NoModel, brigade.HistoricalModel);
		Assert.Equal(CFixedPoint.Zero, brigade.Highest);
	}

	[Fact]
	public void ABrigadeListsEveryTechnologyOfItsTypeAtNothing()
	{
		CSubUnitType type = Infantry();
		Technology("smallarms", type);
		Technology("infantry_warfare", type, change: false);

		var brigade = new CSubUnit(type);

		Assert.Equal(2, brigade.TechnologyLevels.Count);
		Assert.Equal(0, brigade.TechnologyLevels["smallarms"]);
	}

	[Fact]
	public void ItsFiguresAreItsTypesWithEachTechnologyAddedOnceALevel()
	{
		CSubUnitType type = Infantry();
		Technology("smallarms", type, gives: ("default_organisation", "5"));
		Technology("support", type, gives: ("max_strength", "1.5"));
		var brigade = new CSubUnit(type);
		brigade.TechnologyLevels["smallarms"] = 2;
		brigade.TechnologyLevels["support"] = 3;

		CUnits.RebuildStats(brigade);

		Assert.Equal(CFixedPoint.FromInt(40), brigade.Stats["default_organisation"]);
		Assert.Equal(CFixedPoint.Parse("34.5"), brigade.Stats["max_strength"]);
		Assert.Equal(CFixedPoint.FromInt(100), brigade.Stats["officers"]);
	}

	[Fact]
	public void RebuildingTwiceGivesTheSameFigures()
	{
		CSubUnitType type = Infantry();
		Technology("smallarms", type, gives: ("default_organisation", "5"));
		var brigade = new CSubUnit(type);
		brigade.TechnologyLevels["smallarms"] = 2;

		CUnits.RebuildStats(brigade);
		CUnits.RebuildStats(brigade);

		Assert.Equal(CFixedPoint.FromInt(40), brigade.Stats["default_organisation"]);
	}

	[Fact]
	public void ToughnessIsAddedTwiceAndTheRepairCostNotAtAll()
	{
		CSubUnitStats stats = Stats(("toughness", "3"), ("repair_cost_multiplier", "0.05"));

		stats.Add(Stats(("toughness", "0.4"), ("repair_cost_multiplier", "0.01")), times: 2);

		Assert.Equal(CFixedPoint.Parse("4.6"), stats["toughness"]);
		Assert.Equal(CFixedPoint.Parse("0.05"), stats["repair_cost_multiplier"]);
	}

	[Fact]
	public void ATechnologyCannotTakeTheCostsUnderATenthOrTheTimeUnderADay()
	{
		CSubUnitStats stats = Stats(("build_cost_ic", "1"), ("build_cost_manpower", "1"), ("build_time", "10"));

		stats.Add(Stats(("build_cost_ic", "-0.6"), ("build_cost_manpower", "-0.3"), ("build_time", "-20")), times: 2);

		Assert.Equal(CFixedPoint.Parse("0.1"), stats["build_cost_ic"]);
		Assert.Equal(CFixedPoint.Parse("0.4"), stats["build_cost_manpower"]);
		Assert.Equal(CFixedPoint.One, stats["build_time"]);
	}

	[Fact]
	public void WhatTerrainDoesIsAddedALevelAtATimeToo()
	{
		var stats = new CSubUnitStats();
		var gives = new CSubUnitStats();
		var hills = new CModifier("hills");
		hills.Set("attack", CFixedPoint.Parse("0.05"));
		gives.Adjusters["hills"] = hills;

		stats.Add(gives, times: 3);

		Assert.Equal(CFixedPoint.Parse("0.15"), stats.Adjusters["hills"]["attack"]);
	}

	// ---- historical models

	[Fact]
	public void ADefaultModelGivesEveryTechnologyItsOwnNumber()
	{
		CSubUnitType type = Infantry();
		CTechnology smallarms = Technology("smallarms", type);
		CCountry country = Country();
		CGameState state = State(country);

		Assert.Equal(0, CHistoricalModels.Level(state, country, type, 0, smallarms));
		Assert.Equal(3, CHistoricalModels.Level(state, country, type, 3, smallarms));
	}

	[Fact]
	public void ADefaultModelLeavesAOneLevelTechnologyWithAPreconditionAtNothing()
	{
		CSubUnitType type = Infantry();
		CTechnology guarded = Technology("night_goggles", type, additionalOffset: 0, hasPrecondition: true);
		CTechnology open = Technology("odd_one", type, additionalOffset: 0, hasPrecondition: false);
		CTechnology many = Technology("smallarms", type, hasPrecondition: true);
		CCountry country = Country();
		CGameState state = State(country);

		Assert.Equal(0, CHistoricalModels.Level(state, country, type, 2, guarded));
		Assert.Equal(2, CHistoricalModels.Level(state, country, type, 2, open));
		Assert.Equal(2, CHistoricalModels.Level(state, country, type, 2, many));
	}

	[Fact]
	public void ThereAreAsManyDefaultModelsAsTheDefineSays()
	{
		CSubUnitType type = Infantry();
		CTechnology smallarms = Technology("smallarms", type);
		CCountry country = Country();
		CGameState state = State(country, models: 10);

		Assert.Equal(9, CHistoricalModels.Level(state, country, type, 9, smallarms));
		Assert.Null(CHistoricalModels.Level(state, country, type, 10, smallarms));
		Assert.Null(CHistoricalModels.Level(state, country, type, -1, smallarms));
	}

	[Fact]
	public void AModelOutOfAFileHoldsWhatTheFileListsAndNothingElse()
	{
		CSubUnitType type = Infantry();
		CTechnology smallarms = Technology("smallarms", type);
		CTechnology support = Technology("support", type);
		CCountry country = Country();
		country.HistoricalModels[type.Key] = new SortedDictionary<int, Dictionary<string, int>>
		{
			[1] = new() { ["smallarms"] = 4 },
		};
		CGameState state = State(country);

		Assert.Equal(4, CHistoricalModels.Level(state, country, type, 1, smallarms));
		Assert.Null(CHistoricalModels.Level(state, country, type, 1, support));
		Assert.Equal(2, CHistoricalModels.Level(state, country, type, 2, support));
	}

	[Fact]
	public void AFileCanAddAModelPastTheDefaults()
	{
		CSubUnitType type = Infantry();
		CTechnology smallarms = Technology("smallarms", type);
		CCountry country = Country();
		country.HistoricalModels[type.Key] = new SortedDictionary<int, Dictionary<string, int>>
		{
			[10] = new() { ["smallarms"] = 12 },
		};
		CGameState state = State(country, models: 10);

		Assert.Equal(11, CHistoricalModels.Count(state, country, type));
		Assert.Equal(12, CHistoricalModels.Level(state, country, type, 10, smallarms));
	}

	// ---- what a brigade becomes once its line is read

	[Fact]
	public void ItTakesItsModelsLevelsWhateverItsCountryHasReached()
	{
		CSubUnitType type = Infantry();
		CTechnology smallarms = Technology("smallarms", type);
		CCountry country = Country();
		country.TechnologyLevels["smallarms"] = 0;
		CGameState state = State(country, new[] { smallarms });
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), model: 2);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(2, brigade.TechnologyLevels["smallarms"]);
	}

	[Fact]
	public void ADoctrineTheModelLeavesAtNothingIsTheCountrys()
	{
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("infantry_warfare", type, change: false);
		CTechnology equipment = Technology("smallarms", type);
		CCountry country = Country();
		country.TechnologyLevels["infantry_warfare"] = 2;
		country.TechnologyLevels["smallarms"] = 2;
		CGameState state = State(country, new[] { doctrine, equipment });
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), model: 0);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(2, brigade.TechnologyLevels["infantry_warfare"]);
		Assert.Equal(0, brigade.TechnologyLevels["smallarms"]);
	}

	[Fact]
	public void ADoctrineTheModelGivesALevelOfKeepsThatLevel()
	{
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("infantry_warfare", type, change: false);
		CCountry country = Country();
		country.TechnologyLevels["infantry_warfare"] = 2;
		CGameState state = State(country, new[] { doctrine });
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), model: 1);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(1, brigade.TechnologyLevels["infantry_warfare"]);
	}

	[Fact]
	public void ARegularStartsWithAllItsTypeHasAndThatIsItsHighest()
	{
		CSubUnitType type = Infantry();
		CCountry country = Country();
		CGameState state = State(country);
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger));

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(CFixedPoint.FromInt(30), brigade.Strength);
		Assert.Equal(CFixedPoint.FromInt(30), brigade.Highest);
		Assert.Equal(CFixedPoint.FromInt(30), brigade.Organisation);
	}

	[Fact]
	public void AReserveStartsWithTheShareItsConscriptionLawLeaves()
	{
		CSubUnitType type = Infantry();
		CCountry country = Country(conscription: "-0.5");
		CGameState state = State(country);
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), reserve: true);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(CFixedPoint.FromInt(15), brigade.Strength);
		Assert.Equal(CFixedPoint.FromInt(15), brigade.Highest);
		// Its organisation is not cut: no game is running when it is made.
		Assert.Equal(CFixedPoint.FromInt(30), brigade.Organisation);
	}

	[Theory]
	[InlineData("-0.75", "7.5")]
	[InlineData("-0.66", "10.2")]
	[InlineData("-0.25", "22.5")]
	public void EachConscriptionLawLeavesItsOwnShare(string penalty, string strength)
	{
		CSubUnitType type = Infantry();
		CCountry country = Country(conscription: penalty);
		CGameState state = State(country);
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), reserve: true);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(CFixedPoint.Parse(strength), brigade.Strength);
	}

	[Fact]
	public void AMobilisedCountrysReservesStartWhole()
	{
		CSubUnitType type = Infantry();
		CCountry country = Country(conscription: "-0.75");
		country.Mobilised = true;
		CGameState state = State(country);
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), reserve: true);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(CFixedPoint.FromInt(30), brigade.Strength);
	}

	[Fact]
	public void ACountryAtWarWhenTheGameStartsHasItsReservesWhole()
	{
		CSubUnitType type = Infantry();
		CCountry country = Country(conscription: "-0.75");
		var war = new CWar("a war", new CDate(1935, 10, 3));
		war.Attackers.Add(Ger);
		war.Defenders.Add(new CCountryTag("ETH"));
		CGameState state = State(country, war: war);
		var division = new CUnit(EUnitKind.Division, Ger);
		country.Units.Add(division);
		CSubUnit brigade = Brigade(type, division, reserve: true);

		CUnits.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(30), brigade.Strength);
	}

	[Fact]
	public void ABrigadeOfAUnitTheFileSaysWasLentTakesItsLendersDoctrines()
	{
		// Germany's corps lent by Romania at 1941.12.11: the start save's headquarters has Romania's mobile_warfare of
		// nought, not Germany's five (expeditions.md, section 4).
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("mobile_warfare", type, change: false);
		CCountry germany = Country();
		germany.TechnologyLevels["mobile_warfare"] = 5;
		var romania = new CCountry { Tag = new CCountryTag("ROM"), Name = "Romania", DefinitionFile = "" };
		romania.TechnologyLevels["mobile_warfare"] = 1;
		var defines = new CDefines();
		defines.Set(CHistoricalModels.MilitarySection, CHistoricalModels.MaxDefine, CFixedPoint.FromInt(10));
		var state = new CGameState(new CDate(1941, 12, 11), new[] { new CProvince(0, "", isSea: false) },
			new[] { germany, romania }, technologies: new[] { doctrine }, defines: defines);
		var lent = new CUnit(EUnitKind.Corps, Ger) { Lender = romania.Tag };
		var own = new CUnit(EUnitKind.Corps, Ger);
		germany.Units.Add(lent);
		germany.Units.Add(own);
		CSubUnit lentBrigade = Brigade(type, lent);
		CSubUnit ownBrigade = Brigade(type, own);

		CUnits.SetAtStart(state);

		Assert.Equal(1, lentBrigade.TechnologyLevels["mobile_warfare"]);
		Assert.Equal(5, ownBrigade.TechnologyLevels["mobile_warfare"]);
	}

	[Fact]
	public void AStrengthTheLineGaveIsKeptWhereItIsLess()
	{
		CSubUnitType type = Infantry();
		CCountry country = Country();
		CGameState state = State(country);
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger));
		brigade.Strength = CFixedPoint.FromInt(12);
		brigade.Organisation = CFixedPoint.FromInt(5);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(CFixedPoint.FromInt(12), brigade.Strength);
		Assert.Equal(CFixedPoint.FromInt(12), brigade.Highest);
		Assert.Equal(CFixedPoint.FromInt(5), brigade.Organisation);
	}

	[Fact]
	public void ItsOrganisationIsItsTypesWithItsTechnologies()
	{
		// The original's headquarters: 30, and 5 a level of a doctrine the country has two of.
		CSubUnitType type = Headquarters();
		CTechnology doctrine = Technology("elastic_defence", type, change: false, gives: ("default_organisation", "5"));
		CCountry country = Country();
		country.TechnologyLevels["elastic_defence"] = 2;
		CGameState state = State(country, new[] { doctrine });
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Theatre, Ger));

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(CFixedPoint.FromInt(40), brigade.Organisation);
		Assert.Equal(CFixedPoint.FromInt(10), brigade.Strength);
	}

	[Fact]
	public void ABrigadeWithNoModelIsLeftAsItWasMade()
	{
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("infantry_warfare", type, change: false);
		CCountry country = Country();
		country.TechnologyLevels["infantry_warfare"] = 2;
		CGameState state = State(country, new[] { doctrine });
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), model: CSubUnit.NoModel, reserve: true);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(0, brigade.TechnologyLevels["infantry_warfare"]);
		Assert.Equal(CFixedPoint.FromInt(100), brigade.Strength);
		Assert.Equal(CFixedPoint.FromInt(100), brigade.Organisation);
		Assert.Equal(CFixedPoint.Zero, brigade.Highest);
	}

	[Fact]
	public void AModelNumberTheCountryHasNoneOfGivesNoLevelsButStillHoldsTheBrigadeToItsType()
	{
		CSubUnitType type = Infantry();
		CTechnology doctrine = Technology("infantry_warfare", type, change: false);
		CCountry country = Country();
		country.TechnologyLevels["infantry_warfare"] = 2;
		CGameState state = State(country, new[] { doctrine }, models: 10);
		CSubUnit brigade = Brigade(type, new CUnit(EUnitKind.Division, Ger), model: 25);

		CUnits.AfterLoad(state, country, brigade);

		Assert.Equal(0, brigade.TechnologyLevels["infantry_warfare"]);
		Assert.Equal(CFixedPoint.FromInt(30), brigade.Strength);
	}

	// ---- experience

	[Fact]
	public void ABrigadeNobodyGaveAnExperienceTakesItsCountrysLaw()
	{
		Assert.Equal(CFixedPoint.FromInt(15), CUnits.StartingExperience(Country(training: "15"), isReserve: false));
		Assert.Equal(CFixedPoint.Zero, CUnits.StartingExperience(Country(training: "0"), isReserve: false));
	}

	[Theory]
	[InlineData("-0.5", "15", "7.5")]
	[InlineData("-0.75", "15", "3.75")]
	[InlineData("-0.75", "10", "2.5")]
	[InlineData("-0.25", "15", "11.25")]
	public void AReservesIsTheShareItsConscriptionLawLeaves(string penalty, string training, string expected)
	{
		Assert.Equal(CFixedPoint.Parse(expected),
			CUnits.StartingExperience(Country(penalty, training), isReserve: true));
	}

	[Fact]
	public void MobilisingDoesNotSpareAReservesExperience()
	{
		CCountry country = Country(conscription: "-0.5", training: "15");
		country.Mobilised = true;

		Assert.Equal(CFixedPoint.Parse("7.5"), CUnits.StartingExperience(country, isReserve: true));
	}

	[Fact]
	public void ExperienceIsNeverMoreThanAHundred()
	{
		Assert.Equal(CFixedPoint.FromInt(100), CUnits.StartingExperience(Country(training: "250"), isReserve: false));
	}

	[Fact]
	public void AnExperienceTheLineGaveIsKeptEvenForAReserve()
	{
		CSubUnitType type = Infantry();
		CCountry country = Country(conscription: "-0.5", training: "15");
		CGameState state = State(country);
		var division = new CUnit(EUnitKind.Division, Ger);
		country.Units.Add(division);
		CSubUnit given = Brigade(type, division, reserve: true);
		given.Experience = CFixedPoint.FromInt(45);
		CSubUnit notGiven = Brigade(type, division, reserve: true);

		CUnits.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(45), given.Experience);
		Assert.Equal(CFixedPoint.Parse("7.5"), notGiven.Experience);
	}

	// ---- officers

	[Fact]
	public void ACountryStartsWithTheOfficersItsUnitsCallFor()
	{
		CSubUnitType infantry = Infantry();
		CSubUnitType headquarters = Headquarters();
		CCountry country = Country();
		CGameState state = State(country);
		var corps = new CUnit(EUnitKind.Corps, Ger);
		var division = new CUnit(EUnitKind.Division, Ger) { Parent = corps };
		corps.Children.Add(division);
		country.Units.Add(corps);
		Brigade(headquarters, corps);
		Brigade(infantry, division);
		Brigade(infantry, division);

		CUnits.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(300), country.Officers);
	}

	[Fact]
	public void TheHistorysRatioIsAppliedUnitByUnitAndRoundedDown()
	{
		CSubUnitType type = Infantry();
		type.Stats.Values.Set("officers", CFixedPoint.Parse("0.333"));
		CCountry country = Country();
		country.OfficersRatio = CFixedPoint.Parse("1.5");
		CGameState state = State(country);
		for (int i = 0; i < 3; i++)
		{
			var division = new CUnit(EUnitKind.Division, Ger);
			country.Units.Add(division);
			Brigade(type, division);
		}

		CUnits.SetAtStart(state);

		// 0.333 * 1.5 is 0.4995, which is 0.499 a unit; three of them are not 1.498.
		Assert.Equal(CFixedPoint.Parse("1.497"), country.Officers);
	}

	[Fact]
	public void AnAirGroupAboardACarrierIsOneOfTheCountrysUnits()
	{
		var carrier = new CSubUnitType("carrier", EUnitBranch.Naval);
		carrier.Stats.CopyFrom(Stats(("max_strength", "100"), ("default_organisation", "30"), ("officers", "0")));
		var cag = new CSubUnitType("cag", EUnitBranch.Air);
		cag.Stats.CopyFrom(Stats(("max_strength", "100"), ("default_organisation", "30"), ("officers", "50")));
		CCountry country = Country();
		CGameState state = State(country);
		var fleet = new CUnit(EUnitKind.Navy, Ger);
		country.Units.Add(fleet);
		CSubUnit ship = Brigade(carrier, fleet);
		var aboard = new CUnit(EUnitKind.Air, Ger) { CarriedBy = ship };
		ship.Carried.Add(aboard);
		CSubUnit wing = Brigade(cag, aboard);

		CUnits.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(50), country.Officers);
		Assert.Equal(CFixedPoint.FromInt(15), wing.Experience);
		Assert.Equal(CFixedPoint.FromInt(100), wing.Strength);
	}

	[Fact]
	public void ASupportBrigadeThatIsAllButGoneAddsNothingToItsUnit()
	{
		CSubUnitType headquarters = Headquarters();
		var corps = new CUnit(EUnitKind.Corps, Ger);
		var whole = new CSubUnit(headquarters) { Unit = corps, Strength = CFixedPoint.FromInt(10), Organisation = CFixedPoint.FromInt(30) };
		var broken = new CSubUnit(headquarters) { Unit = corps, Strength = CFixedPoint.FromInt(10), Organisation = CFixedPoint.Parse("0.5") };
		var destroyed = new CSubUnit(headquarters) { Unit = corps, Strength = CFixedPoint.Parse("0.05"), Organisation = CFixedPoint.FromInt(30) };
		corps.SubUnits.AddRange(new[] { whole, broken, destroyed });

		Assert.Equal(CFixedPoint.FromInt(100), CUnits.OfficersOf(corps));
	}

	[Fact]
	public void ABrigadeThatHoldsFrontageAlwaysCounts()
	{
		CSubUnitType infantry = Infantry();
		var division = new CUnit(EUnitKind.Division, Ger);
		var beaten = new CSubUnit(infantry) { Unit = division, Strength = CFixedPoint.Parse("0.01"), Organisation = CFixedPoint.Zero };
		division.SubUnits.Add(beaten);

		Assert.Equal(CFixedPoint.FromInt(100), CUnits.OfficersOf(division));
	}

	// ---- the order of battle as a tree

	[Fact]
	public void ACountrysUnitsAreListedEachBeforeTheUnitsUnderIt()
	{
		CCountry country = Country();
		var theatre = new CUnit(EUnitKind.Theatre, Ger) { Name = "theatre" };
		var corps = new CUnit(EUnitKind.Corps, Ger) { Name = "corps", Parent = theatre };
		var division = new CUnit(EUnitKind.Division, Ger) { Name = "division", Parent = corps };
		theatre.Children.Add(corps);
		corps.Children.Add(division);
		country.Units.Add(theatre);
		country.Units.Add(new CUnit(EUnitKind.Navy, Ger) { Name = "fleet" });

		var names = new List<string>();
		foreach (CUnit unit in country.AllUnits())
		{
			names.Add(unit.Name);
		}

		Assert.Equal(new[] { "theatre", "corps", "division", "fleet" }, names);
	}
}
