
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// What the top bar writes into its elements, against <c>docs/mechanics/topbar.md</c>.
/// The strings quoted as the original's are what its own top bar held in a running
/// game, as the fact base recorded them.
/// </summary>
public class TopBarTextTests
{
	private const string Red = "§R";
	private const string Green = "§G";
	private const string Yellow = "§Y";

	private readonly CCountry _country;
	private readonly CProvince _capital;
	private readonly CGameState _state;

	public TopBarTextTests()
	{
		var tag = new CCountryTag("AAA");
		_country = new CCountry { Tag = tag, Name = "Aland", DefinitionFile = "", Capital = 1 };
		_capital = new CProvince(1, "Capital", isSea: false) { Owner = tag, Controller = tag };
		_state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), _capital },
			new[] { _country });
	}

	private void Hold(EGoods goods, string amount)
	{
		_capital.Pool[goods] = CFixedPoint.Parse(amount);
	}

	private void Day(EGoods goods, string made, string used)
	{
		_country.HomeProduced[goods] = CFixedPoint.Parse(made);
		_country.Usage[goods] = CFixedPoint.Parse(used);
	}

	[Fact]
	public void AResourceIsTheStockpileInWholeUnits()
	{
		Hold(EGoods.Energy, "3804.900");
		Day(EGoods.Energy, "10", "5");

		Assert.Equal(Green + "3804", CTopBarText.Goods(_state, _country, EGoods.Energy));
	}

	[Fact]
	public void AResourceIsGreenWhenTheDayBringsMoreThanItTakes()
	{
		Hold(EGoods.Metal, "100");
		Day(EGoods.Metal, "10", "9.999");

		Assert.StartsWith(Green, CTopBarText.Goods(_state, _country, EGoods.Metal));
	}

	[Fact]
	public void AResourceIsRedWhenTheDayTakesMoreThanItBrings()
	{
		// The original's oil figure for a country importing none and burning it.
		Hold(EGoods.CrudeOil, "1870");
		Day(EGoods.CrudeOil, "0", "2");

		Assert.Equal(Red + "1870", CTopBarText.Goods(_state, _country, EGoods.CrudeOil));
	}

	[Fact]
	public void AResourceIsYellowWhenTheTwoAreLevel()
	{
		Hold(EGoods.Fuel, "462");

		Assert.Equal(Yellow + "462", CTopBarText.Goods(_state, _country, EGoods.Fuel));
	}

	[Fact]
	public void WhatConvertingMakesAndUsesCountsOnEachSide()
	{
		Hold(EGoods.CrudeOil, "50");
		_country.ConversionUsed[EGoods.CrudeOil] = CFixedPoint.Parse("3");
		Assert.StartsWith(Red, CTopBarText.Goods(_state, _country, EGoods.CrudeOil));

		Hold(EGoods.Fuel, "50");
		_country.ConversionMade[EGoods.Fuel] = CFixedPoint.Parse("3");
		Assert.StartsWith(Green, CTopBarText.Goods(_state, _country, EGoods.Fuel));
	}

	[Theory]
	[InlineData("99999.000", "99999")]
	[InlineData("99999.001", "99K")]                                 // one thousandth over, and it is thousands
	[InlineData("123456.789", "123K")]
	[InlineData("2000000", "2000K")]
	public void AStockpileOverNinetyNineThousandIsShownInThousands(string held, string expected)
	{
		Hold(EGoods.Money, held);

		Assert.Equal(Yellow + expected, CTopBarText.Goods(_state, _country, EGoods.Money));
	}

	[Fact]
	public void ACountryWithNoCapitalHoldsNothing()
	{
		_country.Capital = 0;

		Assert.Equal(Yellow + "0", CTopBarText.Goods(_state, _country, EGoods.Supplies));
	}

	[Fact]
	public void IcIsWastedThenUsedThenWhatThereWouldBe()
	{
		// The original's string for a country wasting 10 of 21: half its IC on a queue
		// with nothing in it, the rest on consumer goods and supplies, which use all of it.
		_country.TotalIc = 21;
		_country.MaxIc = 21;
		_country.ProductionShares[(int)EProductionCategory.ConsumerGoods] = new CFixed15(CFixed15.Scale / 4);
		_country.ProductionShares[(int)EProductionCategory.Supplies] = new CFixed15(CFixed15.Scale / 4);
		_country.ProductionShares[(int)EProductionCategory.Production] = new CFixed15(CFixed15.Scale / 2);

		Assert.Equal(Red + "10/" + Green + "21/21", CTopBarText.Ic(_country));
	}

	[Fact]
	public void NothingWastedIsGreenAndAShortageOfResourcesIsRed()
	{
		_country.TotalIc = 15;
		_country.MaxIc = 21;
		_country.ProductionShares[(int)EProductionCategory.ConsumerGoods] = new CFixed15(CFixed15.Scale / 2);
		_country.ProductionShares[(int)EProductionCategory.Supplies] = new CFixed15(CFixed15.Scale / 2);

		Assert.Equal(Green + "0/" + Red + "15/21", CTopBarText.Ic(_country));
	}

	[Fact]
	public void LendLeaseIsNotCountedAsWaste()
	{
		_country.TotalIc = 20;
		_country.MaxIc = 20;
		_country.ProductionShares[(int)EProductionCategory.LendLease] = new CFixed15(CFixed15.Scale);

		Assert.Equal(0, CProduction.Wasted(_country));
	}

	[Fact]
	public void TheDateIsAsTheOriginalWritesIt()
	{
		Assert.Equal("0:00 1, January 1936", CTopBarText.Date(_state, key => key));

		_state.AdvanceOneHour();
		Assert.Equal("1:00 1, Januar 1936", CTopBarText.Date(_state, key => key == "January" ? "Januar" : key));
	}

	[Fact]
	public void DissentIsColouredByWhichWayItIsGoing()
	{
		_country.Modifier.Set("peace_consumer_goods_demand", CFixedPoint.Parse("0.250"));
		_country.Dissent = CFixedPoint.Parse("0.500");

		_country.ProductionShares[(int)EProductionCategory.ConsumerGoods] = new CFixed15(CFixed15.Scale / 8);
		Assert.Equal(Red + "0.50", CTopBarText.Dissent(_state, _country));         // too little: it rises

		_country.ProductionShares[(int)EProductionCategory.ConsumerGoods] = new CFixed15(CFixed15.Scale / 2);
		Assert.Equal(Green + "0.50", CTopBarText.Dissent(_state, _country));       // more than wanted: it falls

		_country.ProductionShares[(int)EProductionCategory.ConsumerGoods] = CFixed15.FromThousandths(CFixedPoint.Parse("0.250"));
		Assert.Equal(Yellow + "0.50", CTopBarText.Dissent(_state, _country));
	}

	[Theory]
	[InlineData("0.000", "0.00")]                                    // the original's own, for a country with none
	[InlineData("1.000", "1.00")]                                    // two decimals up to one
	[InlineData("1.001", "1.0")]                                     // and one above it
	[InlineData("12.345", "12.3")]
	public void DissentLosesADecimalAboveOne(string dissent, string expected)
	{
		_country.Modifier.Set("peace_consumer_goods_demand", CFixedPoint.Parse("0.250"));
		_country.ProductionShares[(int)EProductionCategory.ConsumerGoods] = new CFixed15(CFixed15.Scale / 2);
		_country.Dissent = CFixedPoint.Parse(dissent);

		Assert.Equal(Green + expected, CTopBarText.Dissent(_state, _country));
	}

	[Fact]
	public void NationalUnityIsAWholePercentageColouredByWhatPushesIt()
	{
		_country.NationalUnity = CFixedPoint.Parse("80.900");
		Assert.Equal(Yellow + "80%", CTopBarText.NationalUnity(_country));         // the original's own

		_country.Modifier.Set("national_unity", CFixedPoint.Parse("0.100"));
		Assert.Equal(Green + "80%", CTopBarText.NationalUnity(_country));

		_country.Modifier.Set("national_unity", CFixedPoint.Parse("-0.100"));
		Assert.Equal(Red + "80%", CTopBarText.NationalUnity(_country));
	}

	[Fact]
	public void InfluenceAndManpowerAreWholeNumbersWithNoColour()
	{
		_country.DiplomaticInfluence = CFixedPoint.Parse("9.800");
		_country.Manpower = CFixedPoint.Parse("115.700");

		Assert.Equal("9", CTopBarText.Diplomacy(_country));
		Assert.Equal("115", CTopBarText.Manpower(_country));
	}

	[Theory]
	[InlineData("0.999", "0")]
	[InlineData("7.500", "7")]
	[InlineData("99.999", "99")]
	[InlineData("100.000", "99+")]
	public void SpiesAreCountedOutUpToNinetyNine(string spies, string expected)
	{
		_country.FreeSpies = CFixedPoint.Parse(spies);

		Assert.Equal(expected, CTopBarText.Spies(_country));
	}

	[Theory]
	[InlineData("1.080", "§G108%")]                             // the original's own
	[InlineData("0.900", "§G90%")]
	[InlineData("0.899", "§Y89%")]
	[InlineData("0.500", "§Y50%")]
	[InlineData("0.499", "§R49%")]
	public void TheOfficerRatioIsRedUnderAHalfAndYellowUnderNineTenths(string ratio, string expected)
	{
		Assert.Equal(expected, CTopBarText.OfficerRatio(CFixedPoint.Parse(ratio)));
	}

	[Fact]
	public void ACountryWithNoUnitsHasAllTheOfficersItWants()
	{
		Assert.Equal("§G100%", CTopBarText.OfficerRatio(_country));
	}

	[Fact]
	public void TheBoxShowsTheCountrysOwnRatio()
	{
		_country.OfficerRatio = CFixedPoint.Parse("1.4");
		Assert.Equal("§G140%", CTopBarText.OfficerRatio(_country));
	}

	[Fact]
	public void TheSpeedIndicatorHasAFrameForPausedAndOneForEachSpeed()
	{
		Assert.Equal(1, CTopBarText.SpeedFrame(paused: true, speed: 3));
		Assert.Equal(2, CTopBarText.SpeedFrame(paused: false, speed: 0));
		Assert.Equal(6, CTopBarText.SpeedFrame(paused: false, speed: CClock.SpeedCount - 1));

		Assert.Equal(1, CTopBarText.PauseFrame(paused: true));
		Assert.Equal(2, CTopBarText.PauseFrame(paused: false));
	}
}
