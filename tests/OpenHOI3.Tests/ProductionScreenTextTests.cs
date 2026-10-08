using OpenHOI3.Interface;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>
/// What the production screen writes into its elements, against
/// <c>docs/mechanics/productionscreen.md</c>. The format of a resource is the
/// original's own: <c>%i (%s%i§W)</c>.
/// </summary>
public class ProductionScreenTextTests
{
	private readonly CCountry _country;
	private readonly CProvince _capital;
	private readonly CGameState _state;

	public ProductionScreenTextTests()
	{
		var tag = new CCountryTag("AAA");
		_country = new CCountry { Tag = tag, Name = "Aland", DefinitionFile = "", Capital = 1 };
		_capital = new CProvince(1, "Capital", isSea: false) { Owner = tag, Controller = tag };
		_state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false), _capital },
			new[] { _country });
	}

	private string Goods(EGoods goods, string held, string made, string used)
	{
		_capital.Pool[goods] = CFixedPoint.Parse(held);
		_country.HomeProduced[goods] = CFixedPoint.Parse(made);
		_country.Usage[goods] = CFixedPoint.Parse(used);
		return CProductionScreenText.Goods(_state, _country, goods);
	}

	[Fact]
	public void ARisingStockShowsTheRiseInGreenWithAPlus()
	{
		Assert.Equal("26820 (§G+459§W)", Goods(EGoods.Energy, "26820.5", "500", "40.2"));
	}

	[Fact]
	public void AFallingStockShowsTheFallInRedWithNoSign()
	{
		Assert.Equal("100 (§R12§W)", Goods(EGoods.Metal, "100", "3", "15.9"));
	}

	[Fact]
	public void AStockThatIsStillShowsAYellowNought()
	{
		Assert.Equal("7 (§Y0§W)", Goods(EGoods.Fuel, "7.999", "0", "0"));
	}

	[Fact]
	public void BothFiguresAreCutToWholeUnits()
	{
		// Less than one a day either way is a nought, in the colour of the way it goes.
		Assert.Equal("0 (§G+0§W)", Goods(EGoods.Supplies, "0.9", "0.5", "0"));
		Assert.Equal("0 (§R0§W)", Goods(EGoods.Supplies, "0.9", "0", "0.5"));
	}

	[Fact]
	public void IcIsAPlainWholeNumber()
	{
		_country.TotalIc = 129;

		Assert.Equal("129", CProductionScreenText.Ic(_country));
	}

	[Fact]
	public void ConvoysBeingBuiltFollowInBracketsOnlyWhenThereAreAny()
	{
		Assert.Equal("12", CProductionScreenText.Convoys(12, 0));
		Assert.Equal("12 (+3)", CProductionScreenText.Convoys(12, 3));
	}

	[Fact]
	public void NukesAreWrittenToTwoDecimals()
	{
		Assert.Equal("0.00", CProductionScreenText.Nukes(CFixedPoint.Zero));
		Assert.Equal("1.50", CProductionScreenText.Nukes(CFixedPoint.Parse("1.5")));
	}
}
