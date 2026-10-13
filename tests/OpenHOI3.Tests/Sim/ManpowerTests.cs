using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A country's manpower, against <c>docs/mechanics/manpower.md</c>. The figures for East
/// Germany and for Germany are the original's own, out of its savegame.
/// </summary>
public class ManpowerTests
{
	private static readonly CBuilding Infra = new(0, "infra", 10, "infrastructure", CFixedPoint.Parse("0.1"));

	private static CModifier Modifier(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}

	private static CLaw Conscription(string manpower)
	{
		return new CLaw("conscription_law", Modifier("a_law", ("global_manpower_modifier", manpower)));
	}

	/// <summary>A country whose conscription law is worth the given share of its provinces' manpower.</summary>
	private static CCountry Country(string tag, int capital, string? law = null)
	{
		var country = new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Capital = capital };
		if (law is not null)
		{
			country.Laws["conscription_law"] = Conscription(law);
		}
		return country;
	}

	/// <summary>Land its holder owns, controls and has a claim to, unless told otherwise.</summary>
	private static CProvince Land(int id, string holder, string manpower, string continent = "west",
		string? owner = null, params int[] neighbours)
	{
		var owning = new CCountryTag(owner ?? holder);
		var province = new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = owning,
			Controller = new CCountryTag(holder),
			Continent = continent,
			Neighbours = neighbours,
			Manpower = CFixedPoint.Parse(manpower),
		};
		province.Cores.Add(owning);
		return province;
	}

	private static CWar War(string attacker, string defender, int year, int month)
	{
		var war = new CWar("a war", new CDate(year, month, 3));
		war.Attackers.Add(new CCountryTag(attacker));
		war.Defenders.Add(new CCountryTag(defender));
		return war;
	}

	/// <summary>
	/// The original's own numbers: four manpower a year for every country from
	/// <c>base_values</c>, three quarters lost overseas, and ten years' worth to start with.
	/// </summary>
	private static CGameState State(CCountry[] countries, CProvince[] provinces, CDate? date = null,
		string years = "10", params CWar[] wars)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);

		var statics = new[]
		{
			Modifier("base_values", ("global_manpower", "4"), ("global_leadership", "3.5"), ("ic", "5")),
			Modifier("overseas", ("local_leadership_modifier", "-0.9"), ("local_manpower_modifier", "-0.75")),
			Modifier("non_core", ("local_manpower_modifier", "-0.75")),
		};
		var defines = new CDefines();
		defines.Set("economy", "IC_TO_MONEY", CFixedPoint.Parse("0.05"));
		defines.Set("economy", "IC_TO_SUPPLIES", CFixedPoint.FromInt(7));
		defines.Set("military", "MAX_MANPOWER", CFixedPoint.Parse(years));
		var technologies = new[] { new CTechnology("agriculture", 12, Modifier("agriculture", ("manpower_gain", "0.1"))) };
		var resources = new[] { Modifier("black_soil", ("global_manpower_modifier", "0.1")) };
		return new CGameState(date ?? new CDate(1936, 1, 1), all, countries, new[] { Infra }, technologies,
			staticModifiers: statics, defines: defines, wars: wars, strategicResources: resources);
	}

	private static void RunDays(CGameState state, int days)
	{
		new CSimulation(state).RunHours(24 * days);
	}

	// --- what a year brings ---

	[Fact]
	public void ACountryThatHoldsNothingGetsWhatEveryCountryIsGiven()
	{
		// East Germany in 1936: no provinces. Four a year is a third of one a month.
		CCountry eastGermany = Country("DDR", 0, law: "1");
		CGameState state = State(new[] { eastGermany }, new CProvince[0]);
		CManpower.SetAtStart(state);

		Assert.Equal(CFixedPoint.Parse("0.333"), eastGermany.MonthlyManpower);
		Assert.Equal(CFixedPoint.FromInt(40), eastGermany.MaxManpower);
	}

	[Fact]
	public void TheLawScalesWhatTheProvincesGiveAndNotWhatEveryCountryIsGiven()
	{
		CCountry country = Country("LUX", 1, law: "-0.5");
		CGameState state = State(new[] { country }, new[] { Land(1, "LUX", "1") });
		CManpower.SetAtStart(state);

		// 1 x 0.5 + 4, where scaling the lot would give 2.5
		Assert.Equal(CFixedPoint.FromInt(45), country.MaxManpower);
		Assert.Equal(CFixedPoint.Parse("0.375"), country.MonthlyManpower);
	}

	[Fact]
	public void TechnologyScalesTheWholeOfIt()
	{
		// Germany in 1936: 59 from its provinces, four levels of agriculture.
		CCountry germany = Country("GER", 1, law: "0");
		germany.TechnologyLevels["agriculture"] = 4;
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", "59") });
		CManpower.SetAtStart(state);

		// (59 + 4) x 1.4 is 88.2 a year
		Assert.Equal(CFixedPoint.FromInt(882), germany.MaxManpower);
		Assert.Equal(CFixedPoint.Parse("7.35"), germany.MonthlyManpower);
	}

	[Fact]
	public void AProvincesOwnModifierIsAddedToTheLawAndNotMultipliedWithIt()
	{
		// The colony is across the sea and on another continent: three quarters off. With
		// half off for the law as well there is less than nothing left of it, which is nothing.
		CCountry france = Country("FRA", 1, law: "-0.5");
		CGameState state = State(new[] { france },
			new[] { Land(1, "FRA", "10"), Land(2, "FRA", "8", continent: "east") });
		CManpower.SetAtStart(state);

		// 10 x 0.5 + 8 x 0 + 4, where multiplying would have given the colony 1
		Assert.Equal(CFixedPoint.FromInt(90), france.MaxManpower);
	}

	[Fact]
	public void AProvinceGoesToWhoeverControlsItUnderItsOwnersLaw()
	{
		CCountry germany = Country("GER", 1, law: "1");
		CCountry poland = Country("POL", 2, law: "-0.5");
		CProvince taken = Land(3, "GER", "10", owner: "POL", neighbours: 1);
		CGameState state = State(new[] { germany, poland },
			new[] { Land(1, "GER", "0", neighbours: 3), Land(2, "POL", "0"), taken });
		CManpower.SetAtStart(state);

		// Poland's law, not Germany's: 10 x 0.5 + 4. Poland is left with what every country gets.
		Assert.Equal(CFixedPoint.FromInt(90), germany.MaxManpower);
		Assert.Equal(CFixedPoint.FromInt(40), poland.MaxManpower);
	}

	[Fact]
	public void NoCountrysMostIsLessThanOne()
	{
		CCountry tiny = Country("LUX", 0);
		CGameState state = State(new[] { tiny }, new CProvince[0], years: "0.1");
		CManpower.SetAtStart(state);

		Assert.Equal(CFixedPoint.One, tiny.MaxManpower);        // 0.4 as worked out
	}

	// --- what a day adds ---

	[Fact]
	public void ADayAddsTheMonthsFigureOverTheDaysTheMonthHas()
	{
		// East Germany two days in: 40.020 in the original's savegame.
		CCountry eastGermany = Country("DDR", 0);
		CGameState state = State(new[] { eastGermany }, new CProvince[0]);
		CManpower.SetAtStart(state);
		RunDays(state, 2);

		Assert.Equal(CFixedPoint.Parse("40.02"), eastGermany.Manpower);
	}

	[Fact]
	public void GermanysFirstTwoDaysAreTheOriginals()
	{
		CCountry germany = Country("GER", 1, law: "0");
		germany.TechnologyLevels["agriculture"] = 4;
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", "59") });
		CManpower.SetAtStart(state);
		RunDays(state, 2);

		Assert.Equal(CFixedPoint.Parse("882.474"), germany.Manpower);   // 7.35 over 31 days is 0.237
	}

	[Fact]
	public void AShortMonthGivesMoreADay()
	{
		CCountry eastGermany = Country("DDR", 0);
		CGameState state = State(new[] { eastGermany }, new CProvince[0], new CDate(1936, 2, 10));
		CManpower.SetAtStart(state);
		RunDays(state, 1);

		Assert.Equal(CFixedPoint.Parse("40.011"), eastGermany.Manpower);   // 0.333 over 28
	}

	[Fact]
	public void NothingHoldsThePoolToTheMost()
	{
		CCountry eastGermany = Country("DDR", 0);
		CGameState state = State(new[] { eastGermany }, new CProvince[0]);
		CManpower.SetAtStart(state);
		RunDays(state, 40);

		Assert.True(eastGermany.Manpower > eastGermany.MaxManpower);
		Assert.Equal(CFixedPoint.FromInt(40), eastGermany.MaxManpower);
	}

	[Fact]
	public void TheMostIsWorkedOutAgainOnlyWhenTheMonthTurns()
	{
		CCountry country = Country("LUX", 1, law: "-0.5");
		CGameState state = State(new[] { country }, new[] { Land(1, "LUX", "1") }, new CDate(1936, 1, 30));
		CManpower.SetAtStart(state);
		var simulation = new CSimulation(state);                             // entering a game works it out too

		country.Laws["conscription_law"] = Conscription("1");
		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.FromInt(45), country.MaxManpower);
		Assert.Equal(CFixedPoint.Parse("0.5"), country.MonthlyManpower);     // the income follows at once

		simulation.RunHours(24);                                             // into February
		Assert.Equal(CFixedPoint.FromInt(60), country.MaxManpower);
	}

	// --- what a new game starts with ---

	[Fact]
	public void ACountryAtPeaceStartsWithItsMost()
	{
		CCountry country = Country("LUX", 1, law: "-0.5");
		CGameState state = State(new[] { country }, new[] { Land(1, "LUX", "1") });
		CManpower.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(45), country.Manpower);
	}

	[Fact]
	public void ACountryAtWarStartsAYearShortAndAnEighthOfAYearMoreForEachMonthOfIt()
	{
		// October to January is three months: 1 + 3 x 0.12 years of the 10.
		CCountry italy = Country("ITA", 1, law: "0");
		CCountry ethiopia = Country("ETH", 2, law: "0");
		CGameState state = State(new[] { italy, ethiopia }, new[] { Land(1, "ITA", "46"), Land(2, "ETH", "6") },
			wars: War("ITA", "ETH", 1935, 10));
		CManpower.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(500), italy.MaxManpower);
		Assert.Equal(CFixedPoint.FromInt(432), italy.Manpower);              // 500 - 50 x 1.36
		Assert.Equal(CFixedPoint.Parse("86.4"), ethiopia.Manpower);          // 100 - 10 x 1.36
	}

	[Fact]
	public void ALongWarLeavesNothingAndNotLessThanNothing()
	{
		CCountry china = Country("CHI", 1, law: "0");
		CCountry japan = Country("JAP", 2, law: "0");
		CGameState state = State(new[] { china, japan }, new[] { Land(1, "CHI", "6"), Land(2, "JAP", "6") },
			wars: War("JAP", "CHI", 1929, 1));
		CManpower.SetAtStart(state);

		Assert.Equal(CFixedPoint.Zero, china.Manpower);                      // 84 months would cost 11 years
	}

	[Fact]
	public void ACountryAtWarThatOwnsNothingLosesNothing()
	{
		CCountry exile = Country("POL", 0);
		CCountry germany = Country("GER", 1, law: "0");
		CGameState state = State(new[] { exile, germany }, new[] { Land(1, "GER", "6") },
			wars: War("GER", "POL", 1935, 10));
		CManpower.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(40), exile.Manpower);
	}

	[Fact]
	public void AStrategicResourceCountsForTheIncomeAndNotForTheStart()
	{
		// The Soviet Union's black soil in the original's savegame: a tenth more from its
		// provinces every day, and none of it in what it began with.
		CCountry soviet = Country("SOV", 1, law: "-0.5");
		CProvince poltava = Land(1, "SOV", "100");
		poltava.StrategicResource = "black_soil";
		poltava.SetBuildingLevel(Infra, CFixedPoint.FromInt(6));
		CGameState state = State(new[] { soviet }, new[] { poltava });
		CManpower.SetAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(540), soviet.Manpower);             // 100 x 0.5 + 4
		Assert.Equal(CFixedPoint.Parse("4.5"), soviet.MonthlyManpower);

		// Nobody has the use of a resource until a day has been run.
		RunDays(state, 1);
		Assert.Equal(CFixedPoint.Parse("5.333"), soviet.MonthlyManpower);    // 100 x 0.6 + 4, from then on
	}
}
