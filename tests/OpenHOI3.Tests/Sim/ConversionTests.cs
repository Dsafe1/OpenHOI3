using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Sim;

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
	public void TheDaysFlowsOfEveryPoolCountTowardsTheBalance()
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

		// Convoyed in and traded for count as home production does, convoyed out and traded away as usage does.
		germany.HomeProduced.Clear();
		germany.ConvoyedIn[EGoods.CrudeOil] = CFixedPoint.FromInt(3);
		Assert.Equal(CFixedPoint.FromInt(5), CConversions.EnergyToConvert(germany, pool));
		germany.TradedAway[EGoods.CrudeOil] = CFixedPoint.FromInt(3);
		Assert.Equal(CFixedPoint.FromInt(10), CConversions.EnergyToConvert(germany, pool));
		germany.TradedFor[EGoods.Energy] = CFixedPoint.FromInt(12);
		Assert.Equal(CFixedPoint.FromInt(20), CConversions.EnergyToConvert(germany, pool));
	}

	[Fact]
	public void TheDayClearsItsUsageBeforeTheConversionReadsItSoYesterdaysIndustryHoldsNothingBack()
	{
		// Bhutan's 900 energy against 400 crude oil, and yesterday's industry having used more than the hundred over:
		// had the usage still stood, nothing would be converted.
		CCountry bhutan = Country("BHU", 1);
		CGameState state = State(new[] { bhutan }, Land(1, "BHU", industry: 2));
		Stock(state.Province(1).Pool, energy: "900", crudeOil: "400");
		bhutan.Usage[EGoods.Energy] = CFixedPoint.FromInt(200);

		RunADay(state);

		Assert.Equal(CFixedPoint.FromInt(10), bhutan.ConversionUsed[EGoods.Energy]);
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

		// What comes in is the day's whole income of crude oil: convoyed in counts beside home production.
		germany.HomeProduced[EGoods.CrudeOil] = CFixedPoint.One;
		germany.ConvoyedIn[EGoods.CrudeOil] = CFixedPoint.One;
		Stock(pool, energy: "0", crudeOil: "300", fuel: "731");
		Assert.Equal(CFixedPoint.Zero, CConversions.CrudeOilToRefine(germany, pool));
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

	// --- a country's own setting, an enhanced game's (ic.md, section 4.1) ---

	private static void MakeEnhanced(CGameState state)
	{
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
	}

	private static void Set(CConversionSetting setting, string perDay, string keep = "0", string target = "0")
	{
		setting.Set(true, CFixedPoint.Parse(perDay), CFixedPoint.Parse(keep), CFixedPoint.Parse(target));
	}

	[Fact]
	public void ACountrysOwnSettingDecidesOnlyInAnEnhancedGameAndForACountrySomebodyPlays()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		Set(germany.EnergyConversion, "30");
		Set(germany.CrudeOilConversion, "20");

		// A faithful game: the setting is there and nothing asks it.
		state.Played.Add(germany.Tag);
		Assert.False(CConversions.IsOwn(state, germany, EConversion.EnergyIntoCrudeOil));
		// An enhanced game, the country nobody's: the original's rule.
		MakeEnhanced(state);
		state.Played.Clear();
		Assert.False(CConversions.IsOwn(state, germany, EConversion.CrudeOilIntoFuel));
		// Played: its own - and only the conversion it took into its own hands.
		state.Played.Add(germany.Tag);
		Assert.True(CConversions.IsOwn(state, germany, EConversion.EnergyIntoCrudeOil));
		germany.CrudeOilConversion.Set(false, CFixedPoint.FromInt(20), CFixedPoint.Zero, CFixedPoint.Zero);
		Assert.False(CConversions.IsOwn(state, germany, EConversion.CrudeOilIntoFuel));
	}

	[Fact]
	public void ItsOwnEnergyIsWhatItChoseCutToItsCapacityAndToWhatItHoldsAboveTheFloor()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		CGoodsPool pool = state.Province(1).Pool;
		CTick.RunEntryPass(state);
		// A hundred IC and a rate of a tenth: ten energy a crude oil, at most five crude oil a day, 200 energy for industry.
		Assert.Equal(CFixedPoint.FromInt(50), CConversions.EnergyCapacity(germany));
		Assert.Equal(CFixedPoint.FromInt(200), CConversions.EnergyFloor(germany));

		// Where the original converts nothing - less than twice as much energy as crude oil - its own choice stands.
		Stock(pool, energy: "1000", crudeOil: "600");
		Assert.Equal(CFixedPoint.Zero, CConversions.EnergyToConvert(germany, pool));
		Set(germany.EnergyConversion, "30");
		Assert.Equal(CFixedPoint.FromInt(30), CConversions.OwnEnergyToConvert(germany, pool));
		// More than its capacity is cut to it.
		Set(germany.EnergyConversion, "500");
		Assert.Equal(CFixedPoint.FromInt(50), CConversions.OwnEnergyToConvert(germany, pool));
		// Nothing a day is nothing.
		Set(germany.EnergyConversion, "0");
		Assert.Equal(CFixedPoint.Zero, CConversions.OwnEnergyToConvert(germany, pool));

		// What it chose to keep, where that is above industry's need; and industry's need where it is not.
		Set(germany.EnergyConversion, "500", keep: "980");
		Assert.Equal(CFixedPoint.FromInt(20), CConversions.OwnEnergyToConvert(germany, pool));
		Stock(pool, energy: "215", crudeOil: "600");
		Set(germany.EnergyConversion, "500", keep: "100");
		Assert.Equal(CFixedPoint.FromInt(15), CConversions.OwnEnergyToConvert(germany, pool));
		Stock(pool, energy: "200", crudeOil: "0");
		Assert.Equal(CFixedPoint.Zero, CConversions.OwnEnergyToConvert(germany, pool));

		// The crude oil to stop at: nothing from there on.
		Stock(pool, energy: "1000", crudeOil: "600");
		Set(germany.EnergyConversion, "30", target: "600");
		Assert.Equal(CFixedPoint.Zero, CConversions.OwnEnergyToConvert(germany, pool));
		Set(germany.EnergyConversion, "30", target: "601");
		Assert.Equal(CFixedPoint.FromInt(30), CConversions.OwnEnergyToConvert(germany, pool));
	}

	[Fact]
	public void ARateOfNoughtOrBelowConvertsNothingWhateverItChose()
	{
		// As BlackICE's countries start: a modifier taking the whole rate away until a synthetic industry gives it back.
		CCountry germany = Country("GER", 1, ("energy_to_oil_conversion", "0"));
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		CTick.RunEntryPass(state);
		germany.Technology.Set("energy_to_oil_conversion", CFixedPoint.Parse("-1"));
		Stock(state.Province(1).Pool, energy: "5000", crudeOil: "0");
		Set(germany.EnergyConversion, "30");
		Assert.Equal(CFixedPoint.Zero, CConversions.EnergyCapacity(germany));
		Assert.Equal(CFixedPoint.Zero, CConversions.OwnEnergyToConvert(germany, state.Province(1).Pool));
	}

	[Fact]
	public void ItsOwnCrudeOilIsWhatItChoseCutToItsCapacityAndToWhatItKeeps()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		CGoodsPool pool = state.Province(1).Pool;
		CTick.RunEntryPass(state);
		Assert.Equal(CFixedPoint.FromInt(50), CConversions.CrudeOilCapacity(germany));

		// Where the original refines nothing - fuel beyond a year's crude oil and twice what is held - its own choice stands.
		Stock(pool, energy: "0", crudeOil: "300", fuel: "731");
		Set(germany.CrudeOilConversion, "20");
		Assert.Equal(CFixedPoint.FromInt(20), CConversions.OwnCrudeOilToRefine(germany, pool));
		Set(germany.CrudeOilConversion, "500");
		Assert.Equal(CFixedPoint.FromInt(50), CConversions.OwnCrudeOilToRefine(germany, pool));
		// What it keeps, and the fuel it stops at.
		Set(germany.CrudeOilConversion, "500", keep: "290");
		Assert.Equal(CFixedPoint.FromInt(10), CConversions.OwnCrudeOilToRefine(germany, pool));
		Set(germany.CrudeOilConversion, "500", keep: "300");
		Assert.Equal(CFixedPoint.Zero, CConversions.OwnCrudeOilToRefine(germany, pool));
		Set(germany.CrudeOilConversion, "20", target: "731");
		Assert.Equal(CFixedPoint.Zero, CConversions.OwnCrudeOilToRefine(germany, pool));
		// The original's stop at 99,000 stays whatever it chose.
		Stock(pool, energy: "0", crudeOil: "300", fuel: "99000");
		Set(germany.CrudeOilConversion, "20");
		Assert.Equal(CFixedPoint.Zero, CConversions.OwnCrudeOilToRefine(germany, pool));
	}

	[Fact]
	public void ADayGoesByTheSettingAndWritesTheSameTwoLedgers()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		MakeEnhanced(state);
		state.Played.Add(germany.Tag);
		// Stocks at which the original converts nothing of either.
		Stock(state.Province(1).Pool, energy: "1000", crudeOil: "600", fuel: "5000");
		Set(germany.EnergyConversion, "30");
		Set(germany.CrudeOilConversion, "20");

		RunADay(state);

		Assert.Equal(CFixedPoint.FromInt(30), germany.ConversionUsed[EGoods.Energy]);
		Assert.Equal(CFixedPoint.FromInt(3), germany.ConversionMade[EGoods.CrudeOil]);
		Assert.Equal(CFixedPoint.FromInt(20), germany.ConversionUsed[EGoods.CrudeOil]);
		Assert.Equal(CFixedPoint.FromInt(20), germany.ConversionMade[EGoods.Fuel]);
	}

	[Fact]
	public void TheSettingIsChangedByItsCommandWhichAFaithfulGameRefuses()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		var command = new OpenHOI3.Sim.Commands.CSetConversionCommand(germany.Tag, EConversion.CrudeOilIntoFuel, true,
			CFixedPoint.FromInt(20), CFixedPoint.FromInt(100), CFixedPoint.FromInt(5000));

		Assert.False(command.MayExecute(state));
		command.Execute(state);
		Assert.True(germany.CrudeOilConversion.IsUntouched);

		MakeEnhanced(state);
		Assert.True(command.MayExecute(state));
		command.Execute(state);
		Assert.True(germany.CrudeOilConversion.Own);
		Assert.Equal(CFixedPoint.FromInt(20), germany.CrudeOilConversion.PerDay);
		Assert.Equal(CFixedPoint.FromInt(100), germany.CrudeOilConversion.Keep);
		Assert.Equal(CFixedPoint.FromInt(5000), germany.CrudeOilConversion.Target);
		Assert.True(germany.EnergyConversion.IsUntouched);

		// A country that is none, and a figure below nought, are refused.
		Assert.False(new OpenHOI3.Sim.Commands.CSetConversionCommand(new CCountryTag("XXX"), EConversion.CrudeOilIntoFuel, true,
			CFixedPoint.Zero, CFixedPoint.Zero, CFixedPoint.Zero).MayExecute(state));
		Assert.False(new OpenHOI3.Sim.Commands.CSetConversionCommand(germany.Tag, EConversion.CrudeOilIntoFuel, true,
			new CFixedPoint(-1), CFixedPoint.Zero, CFixedPoint.Zero).MayExecute(state));
	}

	[Fact]
	public void AnEnhancedSaveHoldsASettingThatWasTouchedAndAFaithfulOneNone()
	{
		CCountry germany = Country("GER", 1);
		CGameState state = State(new[] { germany }, Land(1, "GER", industry: 100));
		Set(germany.EnergyConversion, "30", keep: "250.5", target: "1000");

		var writer = new OpenHOI3.Content.Saves.CSaveTextWriter();
		OpenHOI3.Content.Saves.CSaveConversions.Write(writer, state, germany);
		Assert.Equal("", writer.Text);

		MakeEnhanced(state);
		OpenHOI3.Content.Saves.CSaveConversions.Write(writer, state, germany);
		// The energy's, in thousandths; the crude oil's, never touched, not at all.
		Assert.Contains("openhoi_energy_conversion", writer.Text);
		Assert.Contains("1 30000 250500 1000000", writer.Text);
		Assert.DoesNotContain("openhoi_crude_oil_conversion", writer.Text);

		CCountry loaded = Country("GER", 1);
		Set(loaded.CrudeOilConversion, "99");
		OpenHOI3.Content.Saves.CSaveConversions.Read(loaded, OpenHOI3.Content.Clausewitz.ClausewitzParser.Parse(writer.Text));
		Assert.True(loaded.EnergyConversion.Own);
		Assert.Equal(CFixedPoint.FromInt(30), loaded.EnergyConversion.PerDay);
		Assert.Equal(CFixedPoint.Parse("250.5"), loaded.EnergyConversion.Keep);
		Assert.Equal(CFixedPoint.FromInt(1000), loaded.EnergyConversion.Target);
		// A setting the save has not is as never touched, whatever the country held before.
		Assert.True(loaded.CrudeOilConversion.IsUntouched);
	}
}
