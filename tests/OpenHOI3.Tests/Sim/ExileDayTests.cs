using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// A government in exile's day, against <c>docs/mechanics/war.md</c>, section 14, and <c>stockpile.md</c>: what it makes
/// into its own pool, what it hands its host and when, and the bounds its pool is held to. The figures are the 1941.6.22
/// start save's for Denmark, Norway and Poland in London.
/// </summary>
public class ExileDayTests
{
	private static readonly CCountryTag Eng = new("ENG");
	private static readonly CCountryTag Nor = new("NOR");
	private static readonly CCountryTag Ger = new("GER");

	private static CCountry Country(CCountryTag tag, int capital) =>
		new() { Tag = tag, Name = tag.Value, DefinitionFile = "", Capital = capital };

	/// <summary>London England's, Oslo Norway's and held by Germany, Berlin Germany's; IC five from <c>base_values</c>.</summary>
	private static (CGameState State, CCountry England, CCountry Norway, CCountry Israel) World()
	{
		CCountry england = Country(Eng, 1);
		CCountry norway = Country(Nor, 2);
		CCountry germany = Country(Ger, 3);
		CCountry israel = Country(new CCountryTag("ISR"), 4);
		var provinces = new List<CProvince>
		{
			new(0, "", isSea: false),
			new(1, "London", isSea: false) { Owner = Eng, Controller = Eng },
			new(2, "Oslo", isSea: false) { Owner = Nor, Controller = Ger },
			new(3, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
			new(4, "Jerusalem", isSea: false) { Owner = Eng, Controller = Eng },
		};
		var baseValues = new CModifier("base_values");
		baseValues.Set("ic", CFixedPoint.FromInt(5));
		var defines = new CDefines();
		defines.Set("economy", "IC_TO_MONEY", CFixedPoint.Parse("0.05"));
		TestRoads.Pave(provinces);
		var state = new CGameState(new CDate(1941, 6, 22), provinces, new[] { england, norway, germany, israel },
			new[] { TestRoads.Infra }, null, new[] { baseValues }, defines, System.Array.Empty<CWar>());
		norway.InExile = true;
		norway.ActingCapital = 1;
		return (state, england, norway, israel);
	}

	[Fact]
	public void AnExileSeatedInItsHostsCapitalMakesWhatItsBaseIcNeedsIntoItsOwnPool()
	{
		(CGameState state, CCountry _, CCountry norway, CCountry israel) = World();

		CResources.UpdateHomeProduction(state);
		CStockpile.AddHomeProduction(state);

		// The save's home figure for each of the seven exiles: 5 metal, 10 energy, 2.5 rare materials.
		Assert.Equal(CFixedPoint.FromInt(5), norway.HomeProduced[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(10), norway.HomeProduced[EGoods.Energy]);
		Assert.Equal(CFixedPoint.Parse("2.5"), norway.HomeProduced[EGoods.RareMaterials]);
		Assert.Equal(CFixedPoint.FromInt(5), norway.PoolInExile[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(5), state.Province(1).Pool[EGoods.Metal]);          // London: England's own five
		// A country that owns nothing and is in no exile has no day of its own and makes none.
		Assert.Equal(CFixedPoint.Zero, israel.HomeProduced[EGoods.Metal]);
	}

	[Fact]
	public void AnExileHandsItsHostTheSuppliesAndFuelInItsPoolBeforeItsSharesMakeTheDays()
	{
		(CGameState state, CCountry england, CCountry norway, CCountry _) = World();
		norway.PoolInExile[EGoods.Supplies] = CFixedPoint.Parse("9.147");
		norway.PoolInExile[EGoods.Fuel] = CFixedPoint.Parse("0.593");
		norway.PoolInExile[EGoods.Metal] = CFixedPoint.FromInt(10);
		CExile.HandOverDaily(state);

		CProduction.RunDaily(state);

		// Into London's pool and England's ledger, whoever's they were; nothing of either left with Norway.
		Assert.Equal(CFixedPoint.Parse("9.147"), state.Province(1).Pool[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Parse("0.593"), state.Province(1).Pool[EGoods.Fuel]);
		Assert.Equal(CFixedPoint.Parse("9.147"), england.ReceivedFromExiles[EGoods.Supplies]);
		Assert.Equal(CFixedPoint.Zero, norway.PoolInExile[EGoods.Fuel]);
		Assert.Equal(CFixedPoint.Zero, norway.PoolInExile[EGoods.Supplies]);
	}

	[Fact]
	public void AnExilesOwnPoolIsHeldBetweenNothingAndNineHundredAndNinetyNineThousandAndNinetyNine()
	{
		(CGameState state, CCountry _, CCountry norway, CCountry _) = World();
		norway.PoolInExile[EGoods.Metal] = CFixedPoint.Parse("-1.241");
		norway.PoolInExile[EGoods.Money] = CFixedPoint.FromInt(1_000_500);
		state.Province(1).Pool[EGoods.Metal] = CFixedPoint.Parse("-3");

		CExile.HoldPoolsDaily(state);

		Assert.Equal(CFixedPoint.Zero, norway.PoolInExile[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(999_999), norway.PoolInExile[EGoods.Money]);
		// A province's pool is the province day's to hold.
		Assert.Equal(CFixedPoint.Parse("-3"), state.Province(1).Pool[EGoods.Metal]);
	}
}
