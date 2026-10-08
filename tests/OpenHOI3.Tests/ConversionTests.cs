using System.Collections.Generic;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>
/// Energy into crude oil and crude oil into fuel, against <c>docs/mechanics/ic.md</c>,
/// section 4. The daily figures for Bhutan, the United States and Germany are what the
/// original's savegame shows them losing in energy.
/// </summary>
public class ConversionTests
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

	/// <summary>No <c>base_values</c>, so a country's IC is what is built and it produces nothing at home.</summary>
	private static CGameState State(CCountry[] countries, params CProvince[] provinces)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);
		var technologies = new[]
		{
			new CTechnology("oil_to_coal_conversion", 12, Modifier("oil_to_coal_conversion", ("energy_to_oil_conversion", "0.1"))),
			new CTechnology("oil_refinning", 12, Modifier("oil_refinning", ("refinery_efficiency", "0.1"))),
		};
		return new CGameState(new CDate(1936, 1, 1), all, countries, new[] { Industry }, technologies);
	}

	private static void Stock(CGoodsPool pool, string energy, string crudeOil, string fuel = "0")
	{
		pool[EGoods.Metal] = CFixedPoint.FromInt(100000);
		pool[EGoods.RareMaterials] = CFixedPoint.FromInt(100000);
		pool[EGoods.Energy] = CFixedPoint.Parse(energy);
		pool[EGoods.CrudeOil] = CFixedPoint.Parse(crudeOil);
		pool[EGoods.Fuel] = CFixedPoint.Parse(fuel);
	}

	private static void RunADay(CGameState state)
	{
		new CSimulation(state).RunHours(24);
	}

	// --- energy into crude oil ---

	[Fact]
	public void ASmallCountryTurnsTenEnergyIntoOneCrudeOilADayAsBhutanDoes()
	{
		// Two IC and no technology: a tenth of a unit of oil per unit of energy, and
		// never less than one unit of oil a day.
		CCountry bhutan = Country("BHU", 1);
		CGameState state = State(new[] { bhutan }, Land(1, "BHU", industry: 2));
		Stock(state.Province(1).Pool, energy: "900", crudeOil: "400");

		RunADay(state);

		Assert.Equal(CFixedPoint.FromInt(10), bhutan.ConversionUsed[EGoods.Energy]);
		Assert.Equal(CFixedPoint.One, bhutan.ConversionMade[EGoods.CrudeOil]);
		Assert.Equal(CFixedPoint.FromInt(890), state.Province(1).Pool[EGoods.Energy]);
	}

	[Fact]
	public void IndustryMakesAtMostATwentiethOfAUnitOfOilPerIcAsTheUnitedStatesDo()
	{
		// 153 IC and one level of the technology: 7.65 crude oil at five energy each. The
		// save shows them 38.25 energy down a day.
		CCountry america = Country("USA", 1);
		america.TechnologyLevels["oil_to_coal_conversion"] = 1;
		CGameState state = State(new[] { america }, Land(1, "USA", industry: 153));
		Stock(state.Province(1).Pool, energy: "47000", crudeOil: "20000");

		RunADay(state);

		Assert.Equal(CFixedPoint.Parse("0.2"), CConversions.OilRate(america));
		Assert.Equal(CFixedPoint.Parse("38.25"), america.ConversionUsed[EGoods.Energy]);
		Assert.Equal(CFixedPoint.Parse("7.65"), america.ConversionMade[EGoods.CrudeOil]);
	}

	[Fact]
	public void TheEnergyAUnitOfOilCostsIsCutShortOfTheThousandthAsGermanysIs()
	{
		// 201 IC and two levels: 10.05 crude oil at 3.333 energy each is 33.496, and that
		// much energy makes 10.048.
		CCountry germany = Country("GER", 1);
		germany.TechnologyLevels["oil_to_coal_conversion"] = 2;
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 201));
		Stock(state.Province(1).Pool, energy: "27000", crudeOil: "10000");

		RunADay(state);

		Assert.Equal(CFixedPoint.Parse("33.496"), germany.ConversionUsed[EGoods.Energy]);
		Assert.Equal(CFixedPoint.Parse("10.048"), germany.ConversionMade[EGoods.CrudeOil]);
	}

	[Fact]
	public void ItConvertsOnlyUntilItHoldsTwiceAsMuchEnergyAsCrudeOil()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		CGoodsPool pool = state.Province(1).Pool;
		CTick.RunEntryPass(state);

		// Less than twice as much: nothing.
		Stock(pool, energy: "1000", crudeOil: "600");
		Assert.Equal(CFixedPoint.Zero, CConversions.EnergyToConvert(germany, pool));

		// 12 over: converting 10 of it makes 1 oil and leaves 990 against 2 x 495.
		Stock(pool, energy: "1000", crudeOil: "494");
		Assert.Equal(CFixedPoint.FromInt(10), CConversions.EnergyToConvert(germany, pool));

		// Far over: the day's limit, five crude oil at ten energy each.
		Stock(pool, energy: "5000", crudeOil: "0");
		Assert.Equal(CFixedPoint.FromInt(50), CConversions.EnergyToConvert(germany, pool));
	}

	[Fact]
	public void ItNeverConvertsWhatIndustryNeedsToday()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		CGoodsPool pool = state.Province(1).Pool;
		CTick.RunEntryPass(state);

		Stock(pool, energy: "230", crudeOil: "0");               // a hundred IC want two hundred
		Assert.Equal(CFixedPoint.FromInt(30), CConversions.EnergyToConvert(germany, pool));

		Stock(pool, energy: "200", crudeOil: "0");
		Assert.Equal(CFixedPoint.Zero, CConversions.EnergyToConvert(germany, pool));
	}

	[Fact]
	public void YesterdaysUsageAndTodaysProductionCountTowardsTheBalance()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		CGoodsPool pool = state.Province(1).Pool;
		CTick.RunEntryPass(state);
		Stock(pool, energy: "1000", crudeOil: "494");            // 12 over, as above

		germany.Usage[EGoods.Energy] = CFixedPoint.FromInt(12);
		Assert.Equal(CFixedPoint.Zero, CConversions.EnergyToConvert(germany, pool));

		germany.Usage.Clear();
		germany.HomeProduced[EGoods.CrudeOil] = CFixedPoint.FromInt(3);   // oil coming in counts double against it
		Assert.Equal(CFixedPoint.FromInt(5), CConversions.EnergyToConvert(germany, pool));
	}

	// --- crude oil into fuel ---

	[Fact]
	public void ACountryRefinesHalfAUnitOfCrudeOilForEachWholeIc()
	{
		CCountry honduras = Country("HON", 1);
		CGameState state = State(new[] { honduras }, Land(1, "HON", industry: 5));
		Stock(state.Province(1).Pool, energy: "10", crudeOil: "450");   // too little energy to convert any

		RunADay(state);

		// Five halves is two, not two and a half.
		Assert.Equal(CFixedPoint.FromInt(2), honduras.ConversionUsed[EGoods.CrudeOil]);
		Assert.Equal(CFixedPoint.FromInt(2), honduras.ConversionMade[EGoods.Fuel]);
		Assert.Equal(CFixedPoint.FromInt(448), state.Province(1).Pool[EGoods.CrudeOil]);
		Assert.Equal(CFixedPoint.FromInt(2), state.Province(1).Pool[EGoods.Fuel]);
	}

	[Fact]
	public void ItRefinesNoMoreCrudeOilThanItHas()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		Stock(state.Province(1).Pool, energy: "200", crudeOil: "7.5");   // energy for its industry and none to spare

		RunADay(state);

		Assert.Equal(CFixedPoint.Parse("7.5"), germany.ConversionMade[EGoods.Fuel]);
		Assert.Equal(CFixedPoint.Zero, state.Province(1).Pool[EGoods.CrudeOil]);
	}

	[Fact]
	public void TheTechnologyAndTheModifierMultiplyEachOther()
	{
		CCountry germany = Country("GER", 1, ("fuel_conversion", "0.5"));
		germany.TechnologyLevels["oil_refinning"] = 2;
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 20));
		Stock(state.Province(1).Pool, energy: "40", crudeOil: "450");

		RunADay(state);

		// Ten crude oil at 1.2 x 1.5. Added, the two would give 17.
		Assert.Equal(CFixedPoint.FromInt(18), germany.ConversionMade[EGoods.Fuel]);
	}

	[Fact]
	public void ACountrySwimmingInFuelStopsRefining()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		CGoodsPool pool = state.Province(1).Pool;
		CTick.RunEntryPass(state);
		germany.HomeProduced[EGoods.CrudeOil] = CFixedPoint.FromInt(2);   // a year of it is 730

		Stock(pool, energy: "0", crudeOil: "100000", fuel: "99000");
		Assert.Equal(CFixedPoint.Zero, CConversions.CrudeOilToRefine(germany, pool));

		// More than a year of what comes in, and more than twice the crude oil: stop.
		Stock(pool, energy: "0", crudeOil: "300", fuel: "731");
		Assert.Equal(CFixedPoint.Zero, CConversions.CrudeOilToRefine(germany, pool));

		// Either one alone is not enough to stop it.
		Stock(pool, energy: "0", crudeOil: "400", fuel: "731");
		Assert.Equal(CFixedPoint.FromInt(50), CConversions.CrudeOilToRefine(germany, pool));
		Stock(pool, energy: "0", crudeOil: "300", fuel: "730");
		Assert.Equal(CFixedPoint.FromInt(50), CConversions.CrudeOilToRefine(germany, pool));
	}

	[Fact]
	public void TheOilMadeFromEnergyTodayIsRefinedToday()
	{
		// It starts with no crude oil at all: the ten energy make one, and one is refined.
		CCountry bhutan = Country("BHU", 1);
		CGameState state = State(new[] { bhutan }, Land(1, "BHU", industry: 2));
		Stock(state.Province(1).Pool, energy: "900", crudeOil: "0");

		RunADay(state);

		Assert.Equal(CFixedPoint.One, state.Province(1).Pool[EGoods.Fuel]);
		Assert.Equal(CFixedPoint.Zero, state.Province(1).Pool[EGoods.CrudeOil]);
	}

	[Fact]
	public void ACountryThatDoesNotExistConvertsNothing()
	{
		CCountry germany = Country("GER", 1);
		CCountry eastGermany = Country("DDR", 1);
		eastGermany.Laws["economic_law"] = new CLaw("economic_law", Modifier("a_law", ("ic", "0")));
		CGameState state = State(new[] { germany, eastGermany }, Land(1, "GER", industry: 4));
		Stock(state.Province(1).Pool, energy: "900", crudeOil: "400");

		RunADay(state);

		Assert.Equal("", eastGermany.ConversionMade.ToString());
		Assert.Equal(CFixedPoint.FromInt(2), germany.ConversionMade[EGoods.Fuel]);
		Assert.Equal(CFixedPoint.One, germany.ConversionMade[EGoods.CrudeOil]);
	}

	[Fact]
	public void WhatWasConvertedIsOneDaysAndNotARunningTotal()
	{
		CCountry bhutan = Country("BHU", 1);
		CGameState state = State(new[] { bhutan }, Land(1, "BHU", industry: 2));
		Stock(state.Province(1).Pool, energy: "900", crudeOil: "400");
		var simulation = new CSimulation(state);

		simulation.RunHours(48);

		Assert.Equal(CFixedPoint.FromInt(10), bhutan.ConversionUsed[EGoods.Energy]);
		Assert.Equal(CFixedPoint.FromInt(880), state.Province(1).Pool[EGoods.Energy]);
	}
}
