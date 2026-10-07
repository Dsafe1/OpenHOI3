using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>
/// The six shares of IC and what spending it costs, against
/// <c>docs/mechanics/production.md</c>. The figures for Germany, East Germany and
/// Croatia are the original's own, out of its savegame.
/// </summary>
public class ProductionTests
{
	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);

	private static CModifier Modifier(string name, params (string Kind, string Value)[] values)
	{
		var modifier = new CModifier(name);
		foreach ((string kind, string value) in values)
		{
			modifier.Set(kind, CFixedPoint.Parse(value));
		}
		return modifier;
	}

	/// <summary>A country whose one law holds the given modifier values.</summary>
	private static CCountry Country(string tag, int capital, params (string Kind, string Value)[] law)
	{
		var country = new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Capital = capital };
		if (law.Length > 0)
		{
			country.Laws["economic_law"] = new CLaw("economic_law", Modifier("a_law", law));
		}
		return country;
	}

	private static CProvince Land(int id, string holder, int industry = 0)
	{
		var tag = new CCountryTag(holder);
		var province = new CProvince(id, $"P{id}", isSea: false) { Owner = tag, Controller = tag };
		province.SetBuildingLevel(Industry, CFixedPoint.FromInt(industry));
		return province;
	}

	/// <summary>
	/// The original's own numbers: five IC and a quarter of it wanted on consumer goods
	/// from <c>base_values</c>, 0.05 money and 7 supplies an IC.
	/// </summary>
	private static CGameState State(CCountry[] countries, CProvince[] provinces, params CWar[] wars)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);

		CModifier baseValues = Modifier("base_values", ("ic", "5"),
			("peace_consumer_goods_demand", "0.25"), ("war_consumer_goods_demand", "0.15"));
		var defines = new CDefines();
		defines.Set("economy", "IC_TO_MONEY", CFixedPoint.Parse("0.05"));
		defines.Set("economy", "IC_TO_SUPPLIES", CFixedPoint.FromInt(7));
		var technologies = new[]
		{
			new CTechnology("supply_production", 12, Modifier("supply_production", ("ic_to_supplies", "0.05"))),
			new CTechnology("industral_production", 12, Modifier("industral_production", ("ic_modifier", "0.025"))),
		};
		return new CGameState(new CDate(1936, 1, 1), all, countries, new[] { Industry }, technologies,
			new[] { baseValues }, defines, wars);
	}

	private static void SetShares(CCountry country, params long[] raw)
	{
		for (int i = 0; i < raw.Length; i++)
		{
			country.ProductionShares[i] = new CFixed15(raw[i]);
		}
	}

	/// <summary>Plenty of everything, so that nothing here is about running short unless it says so.</summary>
	private static void Stock(CGoodsPool pool, string metal = "10000", string energy = "20000", string rares = "5000")
	{
		pool[EGoods.Metal] = CFixedPoint.Parse(metal);
		pool[EGoods.Energy] = CFixedPoint.Parse(energy);
		pool[EGoods.RareMaterials] = CFixedPoint.Parse(rares);
	}

	private static void RunADay(CGameState state)
	{
		new CSimulation(state).RunHours(24);
	}

	// --- the second kind of number ---

	[Fact]
	public void AThousandthsValueLosesWhatDoesNotFitInFifteenPlaces()
	{
		Assert.Equal(3276, CFixed15.FromThousandths(CFixedPoint.Parse("0.1")).Raw);     // 3276.8
		Assert.Equal(7340, CFixed15.FromThousandths(CFixedPoint.Parse("0.224")).Raw);
		Assert.Equal(CFixed15.Scale, CFixed15.FromThousandths(CFixedPoint.One).Raw);
		Assert.Equal(CFixed15.One, CFixed15.FromInt(1));
	}

	[Fact]
	public void TurningItBackRoundsDownAndSoDoesAProduct()
	{
		Assert.Equal(CFixedPoint.Parse("0.099"), new CFixed15(3276).ToThousandths());   // 0.09997...
		Assert.Equal(CFixedPoint.Parse("-0.001"), new CFixed15(-1).ToThousandths());    // down, not towards nothing
		Assert.Equal(new CFixed15(11796), new CFixed15(5898) * CFixed15.FromInt(2));
		Assert.Equal(new CFixed15(1638), new CFixed15(3277) * new CFixed15(16384));     // half of 3277, less the half
	}

	[Fact]
	public void AShareIsWrittenAsTheOriginalsSavegameWritesIt()
	{
		Assert.Equal("0.09998", new CFixed15(3276).ToString());
		Assert.Equal("0.17999", new CFixed15(5898).ToString());
		Assert.Equal("0.22400", new CFixed15(7340).ToString());
		Assert.Equal("0.15518", new CFixed15(5085).ToString());
		Assert.Equal("1.00000", CFixed15.One.ToString());
	}

	// --- the shares ---

	[Fact]
	public void ANewGameSetsConsumerGoodsToTheDemandAndSplitsTheRestFiveWays()
	{
		// East Germany in the savegame: laws that take 0.15 off the 0.25, and shares
		// written 0.09998 and five of 0.17999.
		CCountry eastGermany = Country("DDR", 1, ("peace_consumer_goods_demand", "-0.15"));
		CGameState state = State(new[] { eastGermany }, new[] { Land(1, "GER") });

		CProduction.SetStartingShares(state);

		Assert.Equal(new[] { "0.17999", "0.09998", "0.17999", "0.17999", "0.17999", "0.17999" },
			eastGermany.ProductionShares.Select(share => share.ToString()));
		Assert.True(CProduction.AreValidShares(eastGermany.ProductionShares));
	}

	[Fact]
	public void BaseValuesReachEveryCountrysModifier()
	{
		CCountry germany = Country("GER", 1, ("peace_consumer_goods_demand", "-0.1"));
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER") });

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("0.15"), germany.Modifier["peace_consumer_goods_demand"]);
		Assert.Equal(CFixedPoint.Parse("0.15"), germany.Modifier["war_consumer_goods_demand"]);
	}

	[Fact]
	public void TheDemandIsThePeaceFigureUntilTheCountryIsAtWar()
	{
		CCountry italy = Country("ITA", 1, ("peace_consumer_goods_demand", "-0.05"), ("war_consumer_goods_demand", "-0.11"));
		CCountry germany = Country("GER", 2, ("peace_consumer_goods_demand", "-0.05"), ("war_consumer_goods_demand", "-0.11"));
		var war = new CWar("a war");
		war.Attackers.Add(italy.Tag);
		war.Defenders.Add(new CCountryTag("ETH"));
		CGameState state = State(new[] { italy, germany }, new[] { Land(1, "ITA"), Land(2, "GER") }, war);
		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("0.04"), CProduction.ConsumerGoodsDemand(state, italy));
		Assert.Equal(CFixedPoint.Parse("0.2"), CProduction.ConsumerGoodsDemand(state, germany));
	}

	[Fact]
	public void TheDemandIsNeverBelowOnePercentOrAboveNinetyNine()
	{
		CCountry low = Country("GER", 1, ("peace_consumer_goods_demand", "-0.9"));
		CCountry high = Country("FRA", 2, ("peace_consumer_goods_demand", "0.9"));
		CGameState state = State(new[] { low, high }, new[] { Land(1, "GER"), Land(2, "FRA") });
		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("0.01"), CProduction.ConsumerGoodsDemand(state, low));
		Assert.Equal(CFixedPoint.Parse("0.99"), CProduction.ConsumerGoodsDemand(state, high));
	}

	// --- supplies ---

	[Fact]
	public void ReproducesGermanysSuppliesFromTheOriginalsSavegame()
	{
		// A share of 5085/32768, 201 IC, 7 supplies an IC, a law worth 10% and four
		// levels of supply production at 5% each: the save says 283.840.
		CCountry germany = Country("GER", 1, ("global_ic", "0.3"), ("global_supplies", "0.1"));
		germany.TechnologyLevels["supply_production"] = 4;
		germany.TechnologyLevels["industral_production"] = 4;
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 139) });
		SetShares(germany, 5085, 7340, 5085, 5085, 5085, 5085);

		CTick.RunEntryPass(state);

		Assert.Equal(201, germany.TotalIc);
		Assert.Equal(CFixedPoint.Parse("283.84"), germany.HomeProduced[EGoods.Supplies]);
	}

	[Fact]
	public void TheLawsBonusToSuppliesAndTheTechnologysAreAddedNotMultiplied()
	{
		CCountry germany = Country("GER", 1, ("global_supplies", "0.5"));
		germany.TechnologyLevels["supply_production"] = 10;
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 5) });
		SetShares(germany, 0, 0, 0, CFixed15.Scale, 0, 0);

		CTick.RunEntryPass(state);

		// Ten IC at 7 each, doubled: 140. Multiplied it would be 157.5.
		Assert.Equal(CFixedPoint.FromInt(140), germany.HomeProduced[EGoods.Supplies]);
	}

	[Fact]
	public void SuppliesGoIntoTheStockpileEachDay()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 5) });
		SetShares(germany, 0, 0, 0, CFixed15.Scale / 2, 0, 0);
		Stock(state.Province(1).Pool);

		RunADay(state);

		Assert.Equal(CFixedPoint.FromInt(35), state.Province(1).Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.FromInt(35), germany.HomeProduced[EGoods.Supplies]);
	}

	// --- what spending IC costs ---

	[Fact]
	public void ReproducesWhatEastGermanyUsesInADayFromTheOriginalsSavegame()
	{
		// It does not exist, has two IC, and its shares are as a new game left them. The
		// save says usage = { metal=0.557 energy=1.114 rare_materials=0.278 }: consumer
		// goods 0.198 and supplies 0.359, out of Berlin.
		CCountry germany = Country("GER", 1);
		CCountry eastGermany = Country("DDR", 1, ("global_ic", "-0.5"));
		CGameState state = State(new[] { germany, eastGermany }, new[] { Land(1, "GER") });
		SetShares(eastGermany, 5898, 3276, 5898, 5898, 5898, 5898);
		Stock(state.Province(1).Pool);

		RunADay(state);

		Assert.Equal(2, eastGermany.TotalIc);
		Assert.Equal(CFixedPoint.Parse("0.557"), eastGermany.Usage[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Parse("1.114"), eastGermany.Usage[EGoods.Energy]);
		Assert.Equal(CFixedPoint.Parse("0.278"), eastGermany.Usage[EGoods.RareMaterials]);

		// Berlin gained the five metal Germany produces at home and lost what East Germany used.
		Assert.Equal(CFixedPoint.Parse("10004.443"), state.Province(1).Pool[EGoods.Metal]);
	}

	[Fact]
	public void ReproducesWhatCroatiaUsesInADayFromTheOriginalsSavegame()
	{
		// Five IC and shares of 0.19998 and 0.16000: the save says metal=1.795.
		CCountry croatia = Country("CRO", 1);
		CGameState state = State(new[] { croatia }, new[] { Land(1, "YUG") });
		SetShares(croatia, 5243, 6553, 5243, 5243, 5243, 5243);
		Stock(state.Province(1).Pool);

		RunADay(state);

		Assert.Equal(CFixedPoint.Parse("1.795"), croatia.Usage[EGoods.Metal]);
		Assert.Equal(CFixedPoint.Parse("3.59"), croatia.Usage[EGoods.Energy]);
		Assert.Equal(CFixedPoint.Parse("0.897"), croatia.Usage[EGoods.RareMaterials]);
	}

	[Fact]
	public void IcInTheSharesNothingSpendsYetCostsNothing()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 95) });
		SetShares(germany, 8192, 0, 8192, 0, 8192, 8192);      // lend-lease, production, reinforcement, upgrades
		Stock(state.Province(1).Pool);

		RunADay(state);

		Assert.Equal("", germany.Usage.ToString());
		Assert.Equal(CFixedPoint.FromInt(10005), state.Province(1).Pool[EGoods.Metal]);   // and the day's five came in
	}

	[Fact]
	public void UsageIsOneDaysAndNotARunningTotal()
	{
		CCountry croatia = Country("CRO", 1);
		CGameState state = State(new[] { croatia }, new[] { Land(1, "CRO") });
		SetShares(croatia, 5243, 6553, 5243, 5243, 5243, 5243);
		Stock(state.Province(1).Pool);
		var simulation = new CSimulation(state);

		simulation.RunHours(24);
		simulation.RunHours(24);

		Assert.Equal(CFixedPoint.Parse("1.795"), croatia.Usage[EGoods.Metal]);
	}

	// --- running short ---

	[Fact]
	public void AStockpileSupportsTheLeastOfItsMetalHalfItsEnergyAndTwiceItsRares()
	{
		var pool = new CGoodsPool();
		Stock(pool, metal: "50", energy: "60", rares: "40");
		Assert.Equal(CFixedPoint.FromInt(30), CProduction.SupportedIc(pool));           // energy

		Stock(pool, metal: "50", energy: "600", rares: "10");
		Assert.Equal(CFixedPoint.FromInt(20), CProduction.SupportedIc(pool));           // rares

		Stock(pool, metal: "-5", energy: "600", rares: "10");
		Assert.Equal(CFixedPoint.Zero, CProduction.SupportedIc(pool));                  // never less than nothing
	}

	[Fact]
	public void AShortageCutsTheDaysIcToWhatTheStockpileCanPayFor()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER", industry: 95) });
		SetShares(germany, 0, 0, 0, CFixed15.Scale, 0, 0);
		Stock(state.Province(1).Pool, metal: "1000", energy: "60.5", rares: "1000");

		RunADay(state);

		// The day's own ten energy arrive first, so there is energy for thirty-five and a bit.
		Assert.Equal(100, germany.BaseIc);
		Assert.Equal(35, germany.TotalIc);
		Assert.Equal(CFixedPoint.FromInt(245), germany.HomeProduced[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("0.5"), state.Province(1).Pool[EGoods.Energy]);
	}

	[Fact]
	public void ACountryWithNothingInStockSpendsNothingAndEarnsNothing()
	{
		// It holds nothing, so nothing is produced at home either, and the pool stays empty.
		CCountry croatia = Country("CRO", 1);
		CGameState state = State(new[] { croatia }, new[] { Land(1, "YUG") });
		CProduction.SetStartingShares(state);

		RunADay(state);

		Assert.Equal(5, croatia.BaseIc);
		Assert.Equal(0, croatia.TotalIc);
		Assert.Equal("", croatia.Usage.ToString());
		Assert.Equal("", state.Province(1).Pool.ToString());
		Assert.Equal(CFixedPoint.Zero, croatia.HomeProduced[EGoods.Money]);
	}

	// --- dissent ---

	[Fact]
	public void DissentRisesOneForOneWithWhatTheShareIsShortOfTheDemand()
	{
		CCountry germany = Country("GER", 1);                                           // demands a quarter
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER") });
		SetShares(germany, 0, CFixed15.FromThousandths(CFixedPoint.Parse("0.2")).Raw, 0, 0, 0, 0);
		Stock(state.Province(1).Pool);
		var simulation = new CSimulation(state);

		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.Parse("0.05"), germany.Dissent);

		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.Parse("0.1"), germany.Dissent);
	}

	[Fact]
	public void AShareOverTheDemandTakesDissentOffAndNeverBelowNothing()
	{
		CCountry germany = Country("GER", 1);
		germany.Dissent = CFixedPoint.Parse("0.3");
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER") });
		SetShares(germany, 0, CFixed15.FromThousandths(CFixedPoint.Parse("0.45")).Raw, 0, 0, 0, 0);
		Stock(state.Province(1).Pool);
		var simulation = new CSimulation(state);

		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.Parse("0.1"), germany.Dissent);

		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.Zero, germany.Dissent);
	}

	[Fact]
	public void AShareAtTheDemandLeavesDissentAloneAndTheModifierMovesIt()
	{
		CCountry calm = Country("GER", 1);
		CCountry restless = Country("FRA", 2, ("dissent", "0.02"));
		CGameState state = State(new[] { calm, restless }, new[] { Land(1, "GER"), Land(2, "FRA") });
		CProduction.SetStartingShares(state);
		Stock(state.Province(1).Pool);
		Stock(state.Province(2).Pool);

		RunADay(state);

		Assert.Equal(CFixedPoint.Zero, calm.Dissent);
		Assert.Equal(CFixedPoint.Parse("0.019"), restless.Dissent);                     // 0.02 does not fit the fifteen places exactly
	}

	// --- the command ---

	[Fact]
	public void SharesChangeThroughACommand()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER") });
		var simulation = new CSimulation(state);
		var wanted = new[] { 0L, 16384, 8192, 8192, 0, 0 }.Select(raw => new CFixed15(raw)).ToArray();

		simulation.Post(new CSetProductionSharesCommand(germany.Tag, wanted));
		simulation.Pump();

		Assert.Equal(wanted, germany.ProductionShares);
	}

	[Fact]
	public void SharesThatAreNotSharesAreRefused()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, new[] { Land(1, "GER") });
		var simulation = new CSimulation(state);
		SetShares(germany, 0, 16384, 16384, 0, 0, 0);
		CFixed15[] before = germany.ProductionShares.ToArray();

		CFixed15[] tooMuch = new[] { 0L, 16384, 16384, 1, 0, 0 }.Select(raw => new CFixed15(raw)).ToArray();
		CFixed15[] negative = new[] { -1L, 16384, 16384, 0, 0, 0 }.Select(raw => new CFixed15(raw)).ToArray();
		CFixed15[] five = new CFixed15[5];
		simulation.Post(new CSetProductionSharesCommand(germany.Tag, tooMuch));
		simulation.Post(new CSetProductionSharesCommand(germany.Tag, negative));
		simulation.Post(new CSetProductionSharesCommand(germany.Tag, five));
		simulation.Post(new CSetProductionSharesCommand(new CCountryTag("XXX"), before));
		simulation.Pump();

		Assert.Equal(before, germany.ProductionShares);
	}
}
