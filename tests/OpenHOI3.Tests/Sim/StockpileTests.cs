using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// What a stockpile starts with and what a day adds to it, against
/// <c>docs/mechanics/stockpile.md</c>. The figures for Italy, Berlin, Madrid and the
/// United States are the original's own, out of its savegame.
/// </summary>
public class StockpileTests
{
	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);

	private static CCountry Country(string tag, int capital)
	{
		return new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Capital = capital };
	}

	/// <summary>A land province held by <paramref name="holder"/> with that many levels of industry.</summary>
	private static CProvince Land(int id, string? holder, int industry = 0)
	{
		CCountryTag? tag = holder is null ? null : new CCountryTag(holder);
		var province = new CProvince(id, $"P{id}", isSea: false) { Owner = tag, Controller = tag };
		province.SetBuildingLevel(Industry, CFixedPoint.FromInt(industry));
		return province;
	}

	/// <summary>Every country gets five IC from <c>base_values</c>, as in the original, and money at 0.05 an IC.</summary>
	private static CGameState State(CCountry[] countries, CProvince[] provinces, params CWar[] wars)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);

		var baseValues = new CModifier("base_values");
		baseValues.Set("ic", CFixedPoint.FromInt(5));
		var defines = new CDefines();
		defines.Set("economy", "IC_TO_MONEY", CFixedPoint.Parse("0.05"));
		TestRoads.Pave(all);
		return new CGameState(new CDate(1936, 1, 1), all, countries, new[] { Industry, TestRoads.Infra }, null,
			new[] { baseValues }, defines, wars);
	}

	/// <summary>A war begun on 3 October 1935 unless told otherwise, as Italy's on Ethiopia was.</summary>
	private static CWar War(string attacker, string defender, int year = 1935, int month = 10, int day = 3)
	{
		var war = new CWar("a war", new CDate(year, month, day));
		war.Attackers.Add(new CCountryTag(attacker));
		war.Defenders.Add(new CCountryTag(defender));
		return war;
	}

	private static CGoodsPool Goods(EGoods goods, string amount)
	{
		var pool = new CGoodsPool();
		pool[goods] = CFixedPoint.Parse(amount);
		return pool;
	}

	private static void AssertHolds(CGoodsPool pool, string metal, string energy, string rares, string crudeOil, string money)
	{
		Assert.Equal(CFixedPoint.Parse(metal), pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Parse(energy), pool[EGoods.Energy]);
		Assert.Equal(CFixedPoint.Parse(rares), pool[EGoods.RareMaterials]);
		Assert.Equal(CFixedPoint.Parse(crudeOil), pool[EGoods.CrudeOil]);
		Assert.Equal(CFixedPoint.Parse(money), pool[EGoods.Money]);
	}

	[Fact]
	public void ACountrysStockpileIsItsCapitalsPool()
	{
		CCountry germany = Country("GER", 2);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER"), Land(2, "GER") });

		Assert.Same(state.Province(2).Pool, state.Stockpile(germany));
		Assert.Null(state.Stockpile(Country("XXX", 0)));
	}

	[Fact]
	public void ACountryAtPeaceStartsWithNinetyDaysOfWhatItsBaseIcNeeds()
	{
		// Five from base_values and five built: a base IC of ten.
		CGameState state = State(new[] { Country("GER", 1) }, new[] { Land(1, "GER", industry: 5) });

		CStockpile.FillAtStart(state);

		AssertHolds(state.Province(1).Pool, metal: "900", energy: "1800", rares: "450", crudeOil: "900", money: "2.25");
		Assert.Equal(CFixedPoint.Zero, state.Province(1).Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Zero, state.Province(1).Pool[EGoods.Fuel]);
	}

	[Fact]
	public void ItIsTheBaseIcThatCountsAndNotWhatTheLawsLeave()
	{
		CCountry germany = Country("GER", 1);
		var law = new CModifier("full_civilian_economy");
		law.Set("global_ic", CFixedPoint.Parse("-0.5"));
		germany.Laws["economic_law"] = new CLaw("economic_law", law);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 5) });

		CStockpile.FillAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(900), state.Province(1).Pool[EGoods.Metal]);
	}

	[Theory]
	[InlineData(1936, 1, 1, 90)]     // begun today: nothing is lost yet
	[InlineData(1935, 12, 31, 85)]   // a day ago, but in last month
	[InlineData(1935, 10, 3, 75)]    // Italy in Ethiopia
	[InlineData(1935, 1, 1, 40)]     // a year: twelve months gone, and two are always counted
	[InlineData(1930, 1, 1, 40)]
	public void ACountryAtWarStartsWithFiveDaysFewerForEachMonthOfIt(int year, int month, int day, int days)
	{
		CCountry italy = Country("ITA", 1);
		CCountry germany = Country("GER", 3);
		CGameState state = State(new[] { italy, Country("ETH", 2), germany },
			new[] { Land(1, "ITA"), Land(2, "ETH"), Land(3, "GER") }, War("ITA", "ETH", year, month, day));

		Assert.Equal(days, CStockpile.DaysAtStart(state, italy));
		Assert.Equal(90, CStockpile.DaysAtStart(state, germany));
	}

	[Fact]
	public void ItIsTheOldestWarStillBeingFoughtThatCounts()
	{
		CCountry italy = Country("ITA", 1);
		CGameState state = State(new[] { italy, Country("ETH", 2), Country("GRE", 3) },
			new[] { Land(1, "ITA"), Land(2, "ETH"), Land(3, "GRE") },
			War("ITA", "GRE", 1935, 12, 1), War("ITA", "ETH", 1935, 6, 1));

		Assert.Equal(7, CStockpile.MonthsAtWar(state, italy));
		Assert.Equal(55, CStockpile.DaysAtStart(state, italy));
	}

	[Fact]
	public void TheMoneyAStockpileStartsWithFollowsTheRateTheDefinesGive()
	{
		// A twentieth of what ninety days of five IC would earn: at 0.05 an IC that is
		// 1.125, and at twice the rate twice as much.
		CGameState state = State(new[] { Country("GER", 1) }, new[] { Land(1, "GER") });
		state.Defines.Set("economy", "IC_TO_MONEY", CFixedPoint.Parse("0.1"));

		CStockpile.FillAtStart(state);

		Assert.Equal(CFixedPoint.Parse("2.25"), state.Province(1).Pool[EGoods.Money]);
	}

	[Fact]
	public void ACountryAtWarStartsWithSeventyFiveDaysAsItalyDoes()
	{
		// Italy's base IC is 69. The save has 5175 metal and 12.937 money: the money's
		// last half of a thousandth is dropped, not rounded.
		CGameState state = State(new[] { Country("ITA", 1), Country("ETH", 2) },
			new[] { Land(1, "ITA", industry: 64), Land(2, "ETH", industry: 2) }, War("ITA", "ETH"));

		CStockpile.FillAtStart(state);

		AssertHolds(state.Province(1).Pool, metal: "5175", energy: "10350", rares: "2587.5", crudeOil: "5175", money: "12.937");
		AssertHolds(state.Province(2).Pool, metal: "525", energy: "1050", rares: "262.5", crudeOil: "525", money: "1.312");
	}

	[Fact]
	public void ACountryThatDoesNotExistIsGivenItsShareInWhoeverHoldsItsCapital()
	{
		// Berlin: Germany's 144 and East Germany's five. The money is Germany's alone,
		// because Germany is declared first and East Germany finds money already there.
		CGameState state = State(new[] { Country("GER", 1), Country("DDR", 1) },
			new[] { Land(1, "GER", industry: 139) });

		CStockpile.FillAtStart(state);

		AssertHolds(state.Province(1).Pool, metal: "13410", energy: "26820", rares: "6705", crudeOil: "13410", money: "32.4");
	}

	[Fact]
	public void WhereTwoShareACapitalTheOneDeclaredFirstGetsTheMoneyEvenIfItHoldsNothing()
	{
		// Madrid: Nationalist Spain is declared first and does not exist; Republican
		// Spain holds the city and has 44 IC. The save has 1.125 money.
		CGameState state = State(new[] { Country("SPA", 1), Country("SPR", 1) },
			new[] { Land(1, "SPR", industry: 39) });

		CStockpile.FillAtStart(state);

		Assert.Equal(CFixedPoint.Parse("1.125"), state.Province(1).Pool[EGoods.Money]);
		Assert.Equal(CFixedPoint.FromInt(49 * 90), state.Province(1).Pool[EGoods.Metal]);
	}

	[Fact]
	public void WhatTheHistoryFileGivesIsAddedToTheResources()
	{
		// Germany's 40000 rare materials sit on top of what its IC gives it.
		CGameState state = State(new[] { Country("GER", 1) }, new[] { Land(1, "GER", industry: 5) });
		var given = new Dictionary<string, CGoodsPool> { ["GER"] = Goods(EGoods.RareMaterials, "40000") };

		CStockpile.FillAtStart(state, given);

		AssertHolds(state.Province(1).Pool, metal: "900", energy: "1800", rares: "40450", crudeOil: "900", money: "2.25");
	}

	[Fact]
	public void ACountryItsHistoryGivesMoneyGetsNoOtherAsTheUnitedStatesDoNot()
	{
		// 256 IC would be worth 57.6. The save has the history file's 1.
		CGameState state = State(new[] { Country("USA", 1) }, new[] { Land(1, "USA", industry: 251) });
		var given = new Dictionary<string, CGoodsPool> { ["USA"] = Goods(EGoods.Money, "1") };

		CStockpile.FillAtStart(state, given);

		AssertHolds(state.Province(1).Pool, metal: "23040", energy: "46080", rares: "11520", crudeOil: "23040", money: "1");
	}

	[Fact]
	public void ACountryWithNoCapitalIsGivenNothingAndBreaksNothing()
	{
		CGameState state = State(new[] { Country("REB", 0), Country("GER", 1) }, new[] { Land(1, "GER") });

		CStockpile.FillAtStart(state);

		Assert.Equal(CFixedPoint.FromInt(450), state.Province(1).Pool[EGoods.Metal]);
		Assert.Equal("", state.Province(0).Pool.ToString());
	}

	[Fact]
	public void MoneyInADayIsBaseIcAtTheDefinesRateScaledByTheCountrysModifier()
	{
		// Germany in the save: a base IC of 144, laws worth -50% money, 3.600 a day.
		CCountry germany = Country("GER", 1);
		var law = new CModifier("war_economy");
		law.Set("global_money", CFixedPoint.Parse("-0.5"));
		law.Set("global_ic", CFixedPoint.Parse("0.3"));       // more IC to spend is not more money
		germany.Laws["economic_law"] = new CLaw("economic_law", law);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 139) });

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("3.6"), germany.HomeProduced[EGoods.Money]);
	}

	[Fact]
	public void MoneyDropsWhatIsLeftBelowAThousandthAtEachStep()
	{
		// Afghanistan in the save: 5 IC at -15% is 0.2125, saved as 0.212.
		CCountry afghanistan = Country("AFG", 1);
		var law = new CModifier("a_law");
		law.Set("global_money", CFixedPoint.Parse("-0.15"));
		afghanistan.Laws["economic_law"] = new CLaw("economic_law", law);
		CGameState state = State(new[] { afghanistan }, new[] { Land(1, "AFG") });

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("0.212"), afghanistan.HomeProduced[EGoods.Money]);
	}

	[Fact]
	public void ACountryThatDoesNotExistStillEarnsIntoWhoeverHoldsItsCapital()
	{
		// East Germany in 1936: five IC from base_values, and its capital is Berlin.
		CCountry eastGermany = Country("DDR", 1);
		CGameState state = State(new[] { Country("GER", 1), eastGermany }, new[] { Land(1, "GER") });
		CStockpile.FillAtStart(state);
		var simulation = new CSimulation(state);

		Assert.Equal(CFixedPoint.Parse("0.25"), eastGermany.HomeProduced[EGoods.Money]);
		Assert.Equal(CFixedPoint.Zero, eastGermany.HomeProduced[EGoods.Metal]);   // but it produces nothing

		// Germany starts with 1.125 and earns a quarter a day; so does East Germany.
		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.Parse("1.625"), state.Province(1).Pool[EGoods.Money]);
	}

	[Fact]
	public void TheRebelsAndACountryWithNoCapitalHaveNoEconomy()
	{
		CCountry rebels = Country("REB", 1);
		CCountry nowhere = Country("XXX", 0);
		CGameState state = State(new[] { rebels, nowhere }, new[] { Land(1, null) });
		CStockpile.FillAtStart(state);
		var simulation = new CSimulation(state);
		simulation.RunHours(24);

		Assert.False(CProduction.TakesPart(state, rebels));
		Assert.False(CProduction.TakesPart(state, nowhere));
		Assert.Equal(CFixedPoint.Zero, rebels.HomeProduced[EGoods.Money]);
		Assert.Equal(CFixedPoint.Parse("1.125"), state.Province(1).Pool[EGoods.Money]);   // as it started
	}

	[Fact]
	public void WithoutTheDefineThereIsNoMoney()
	{
		var all = new List<CProvince> { new(0, "", isSea: false), Land(1, "GER", industry: 5) };
		CCountry germany = Country("GER", 1);
		TestRoads.Pave(all);
		var state = new CGameState(new CDate(1936, 1, 1), all, new[] { germany }, new[] { Industry, TestRoads.Infra });

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Zero, germany.HomeProduced[EGoods.Money]);
	}

	[Fact]
	public void EnteringAGameMovesNothingAndEachMidnightAddsADaysHomeProduction()
	{
		CProvince capital = Land(1, "GER", industry: 5);
		capital.MaxProducing[EGoods.Metal] = CFixedPoint.FromInt(12);
		capital.CurrentProducing[EGoods.Metal] = CFixedPoint.FromInt(12);
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, new[] { capital });
		CStockpile.FillAtStart(state);

		var simulation = new CSimulation(state);                // which runs the entry pass
		Assert.Equal(CFixedPoint.FromInt(900), capital.Pool[EGoods.Metal]);

		simulation.RunHours(23);
		Assert.Equal(CFixedPoint.FromInt(900), capital.Pool[EGoods.Metal]);

		// The province's 12 and the five that base_values' IC is worth; ten IC of money.
		simulation.RunHours(1);
		Assert.Equal(CFixedPoint.FromInt(917), capital.Pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Parse("2.75"), capital.Pool[EGoods.Money]);

		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.FromInt(934), capital.Pool[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Parse("3.25"), capital.Pool[EGoods.Money]);
	}

	[Fact]
	public void CountriesKeepTheOrderTheyWereDeclaredIn()
	{
		CGameState state = State(new[] { Country("SPR", 1), Country("GER", 1), Country("AFG", 1) }, new[] { Land(1, "GER") });

		Assert.Equal(new[] { "SPR", "GER", "AFG" }, state.Countries.Select(country => country.Tag.Value));
	}

	[Fact]
	public void ACountryIsAtWarWhileItIsOnEitherSideOfOne()
	{
		CGameState state = State(new[] { Country("ITA", 1), Country("ETH", 2), Country("GER", 3) },
			new[] { Land(1, "ITA"), Land(2, "ETH"), Land(3, "GER") }, War("ITA", "ETH"));

		Assert.True(state.IsAtWar(new CCountryTag("ITA")));
		Assert.True(state.IsAtWar(new CCountryTag("ETH")));
		Assert.False(state.IsAtWar(new CCountryTag("GER")));
	}
}
