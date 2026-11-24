using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content.Settings;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Screens;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Intelligence;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Tests.Interface;

/// <summary>
/// An enhanced game's custom map modes, against <c>docs/mechanics/custommapmodes.md</c>: the sources of each kind and
/// what a province has of one, the scale, the shade, the colour by what the player sees, and the legend.
/// </summary>
public class CustomMapModeTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Pol = new("POL");

	private const int Berlin = 1;
	private const int Warszawa = 2;

	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);
	private static readonly CBuilding Fort = new(1, "land_fort", 10, "fort_level", CFixedPoint.One);

	private static string Localise(string key) => "[" + key + "]";

	private static CGameState State()
	{
		var state = new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(Berlin, "Berlin", isSea: false) { Owner = Ger, Controller = Ger },
				new CProvince(Warszawa, "Warszawa", isSea: false) { Owner = Pol, Controller = Pol },
			},
			new[]
			{
				new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "", Capital = Berlin },
				new CCountry { Tag = Pol, Name = "Poland", DefinitionFile = "", Capital = Warszawa },
			}, new[] { Industry, Fort });
		CProvinceIntel.Rebuild(state);
		return state;
	}

	private static CCustomMapSource Source(CGameState state, ECustomMapKind kind, string key) => CCustomMapSources.Find(state, kind, key)!;

	[Fact]
	public void EachKindListsItsSourcesInOrderAndTheBuildingsAreTheGamesOwn()
	{
		CGameState state = State();
		Assert.Equal(new[] { "industry", "land_fort" }, CCustomMapSources.Of(state, ECustomMapKind.Buildings).Select(source => source.Key));
		Assert.Equal(new[] { "energy", "metal", "rare_materials", "crude_oil", "manpower", "leadership" },
			CCustomMapSources.Of(state, ECustomMapKind.Resources).Select(source => source.Key));
		Assert.Equal(new[]
		{
			"days_of_supplies", "days_of_fuel", "depot_distance", "depot_detour", "supply_load", "fuel_load", "supply_tax",
			"supply_traffic", "fuel_traffic", "supply_stock", "fuel_stock",
		}, CCustomMapSources.Of(state, ECustomMapKind.Supply).Select(source => source.Key));

		// A building is named by its own key; the engine's sources by keys of the feature's.
		Assert.Equal("industry", Source(state, ECustomMapKind.Buildings, "INDUSTRY").Label);
		Assert.Equal("OPENHOI_MAPMODE_RARE_MATERIALS", Source(state, ECustomMapKind.Resources, "rare_materials").Label);
		Assert.Equal("OPENHOI_MAPMODE_SUPPLY_LOAD_DESC", Source(state, ECustomMapKind.Supply, "supply_load").Explanation);
		Assert.Null(CCustomMapSources.Find(state, ECustomMapKind.Supply, "energy"));
	}

	[Fact]
	public void ABuildingIsTheLevelThatWorksCutToWholeAndAResourceWhatIsMadeToday()
	{
		CGameState state = State();
		CProvince berlin = state.Province(Berlin);
		CCustomMapSource industry = Source(state, ECustomMapKind.Buildings, "industry");
		Assert.Null(industry.Value(state, berlin));
		berlin.SetBuildingMaxLevel(Industry, CFixedPoint.FromInt(8));
		berlin.SetBuildingLevel(Industry, CFixedPoint.Parse("5.9"));
		Assert.Equal(5, industry.Value(state, berlin));
		berlin.SetBuildingLevel(Industry, CFixedPoint.Parse("0.9"));
		Assert.Null(industry.Value(state, berlin));

		CCustomMapSource metal = Source(state, ECustomMapKind.Resources, "metal");
		berlin.MaxProducing[EGoods.Metal] = CFixedPoint.FromInt(9);
		Assert.Null(metal.Value(state, berlin));
		berlin.CurrentProducing[EGoods.Metal] = CFixedPoint.Parse("3.5");
		Assert.Equal(3500, metal.Value(state, berlin));
		berlin.Leadership = CFixedPoint.Parse("0.05");
		Assert.Equal(50, Source(state, ECustomMapKind.Resources, "leadership").Value(state, berlin));
	}

	[Fact]
	public void DaysOfSupplyAreTheStockOverTheDaysNeedHeldAtAYear()
	{
		CGameState state = State();
		CProvince berlin = state.Province(Berlin);
		CCustomMapSource days = Source(state, ECustomMapKind.Supply, "days_of_supplies");
		berlin.Pool[EGoods.Supplies] = CFixedPoint.FromInt(70);
		// Nobody draws here: nothing to show.
		Assert.Null(days.Value(state, berlin));
		berlin.SupplyNeed[EGoods.Supplies] = CFixedPoint.FromInt(4);
		Assert.Equal(17500, days.Value(state, berlin));
		berlin.Pool[EGoods.Supplies] = CFixedPoint.FromInt(9000);
		Assert.Equal(365000, days.Value(state, berlin));
		Assert.Equal(9000000, Source(state, ECustomMapKind.Supply, "supply_stock").Value(state, berlin));
		Assert.Null(Source(state, ECustomMapKind.Supply, "fuel_stock").Value(state, berlin));
	}

	[Fact]
	public void AMapScaleRunsEvenlyInStepsBetweenTheLeastAndTheMostHundredth()
	{
		var amounts = Enumerable.Range(1, 200).Select(index => (long)index * 1000).ToList();
		CCustomMapScale scale = CCustomMapScale.FromAmounts(amounts);
		Assert.Equal(200, scale.Count);
		// The third of two hundred and the hundred and ninety-ninth.
		Assert.Equal(3000, scale.Starts![0]);
		Assert.Equal(199000, scale.Starts[9]);
		// Each start the one before times the same ratio, rounded up.
		Assert.Equal(4782, scale.Starts[1]);
		Assert.True(scale.Starts.Zip(scale.Starts.Skip(1)).All(pair => pair.First < pair.Second));

		// One amount everywhere: every province that has it is the top shade.
		CCustomMapScale flat = CCustomMapScale.FromAmounts(new List<long> { 500, 500, 500 });
		Assert.All(flat.Starts!, start => Assert.Equal(500, start));
		Assert.Null(CCustomMapScale.FromAmounts(new List<long>()).Starts);
	}

	[Fact]
	public void AShadeIsALevelItselfOrTheLastStartReachedAndTurnedRoundWhereMoreIsWorse()
	{
		CGameState state = State();
		CCustomMapSource industry = Source(state, ECustomMapKind.Buildings, "industry");
		CCustomMapScale levels = CCustomMapScale.Of(state, industry);
		Assert.Equal((1, 7, 10), (levels.Shade(industry, 1), levels.Shade(industry, 7), levels.Shade(industry, 14)));

		CCustomMapSource days = Source(state, ECustomMapKind.Supply, "days_of_supplies");
		CCustomMapScale bands = CCustomMapScale.Of(state, days);
		// 0, 1, 2, 3, 5, 7, 10, 15, 21, 30 days.
		Assert.Equal((1, 2, 4, 5, 10), (bands.Shade(days, 500), bands.Shade(days, 1000), bands.Shade(days, 4999), bands.Shade(days, 5000),
			bands.Shade(days, 90000)));

		CCustomMapSource distance = Source(state, ECustomMapKind.Supply, "depot_distance");
		CCustomMapScale far = CCustomMapScale.Of(state, distance);
		// At the depot the top shade, cut off the bottom one.
		Assert.Equal((10, 7, 1), (far.Shade(distance, 0), far.Shade(distance, 5), far.Shade(distance, 100000)));
	}

	[Fact]
	public void TheGreenRampIsWorkedOutInWholeNumbersAndTheHeatRampIsTheInfrastructureModes()
	{
		static (int, int, int) Bytes(CColor colour) => ((int)System.MathF.Round(colour.R * 255), (int)System.MathF.Round(colour.G * 255),
			(int)System.MathF.Round(colour.B * 255));
		Assert.Equal((27, 106, 27), Bytes(CCustomMapColours.Shade("green", 1)));
		Assert.Equal((42, 172, 42), Bytes(CCustomMapColours.Shade("green", 5)));
		Assert.Equal((60, 255, 60), Bytes(CCustomMapColours.Shade("green", 10)));
		Assert.Equal((25, 0, 0), Bytes(CCustomMapColours.Shade("heat", 1)));
		Assert.Equal((255, 255, 0), Bytes(CCustomMapColours.Shade("HEAT", 5)));
		Assert.Equal((0, 255, 0), Bytes(CCustomMapColours.Shade("heat", 10)));
		// A name that is no palette is the green one.
		Assert.Equal(CCustomMapColours.Shade("green", 3), CCustomMapColours.Shade("purple", 3));
	}

	[Fact]
	public void AProvinceNotSeenIntoShowsThatThereIsSomeAndNotHowMuchAndOfSupplyNothing()
	{
		CGameState state = State();
		CProvince berlin = state.Province(Berlin);
		CProvince warszawa = state.Province(Warszawa);
		foreach (CProvince province in new[] { berlin, warszawa })
		{
			province.SetBuildingMaxLevel(Industry, CFixedPoint.FromInt(8));
			province.SetBuildingLevel(Industry, CFixedPoint.FromInt(8));
			province.Pool[EGoods.Supplies] = CFixedPoint.FromInt(50);
		}
		CCustomMapSource industry = Source(state, ECustomMapKind.Buildings, "industry");
		CCustomMapScale levels = CCustomMapScale.Of(state, industry);
		Assert.Equal(CCustomMapColours.Shade("green", 8), CCustomMapColours.Of(state, berlin, Ger, industry, levels, "green"));
		Assert.Equal(CCustomMapColours.Shade("green", 1), CCustomMapColours.Of(state, warszawa, Ger, industry, levels, "green"));

		// Nothing to show: light where seen, dark where not.
		CCustomMapSource fort = Source(state, ECustomMapKind.Buildings, "land_fort");
		Assert.Equal(CCustomMapColours.Light, CCustomMapColours.Of(state, berlin, Ger, fort, levels, "green"));
		Assert.Equal(CCustomMapColours.Dark, CCustomMapColours.Of(state, warszawa, Ger, fort, levels, "green"));

		CCustomMapSource stock = Source(state, ECustomMapKind.Supply, "supply_stock");
		CCustomMapScale scale = CCustomMapScale.Of(state, stock);
		Assert.Equal(CCustomMapColours.Shade("heat", 10), CCustomMapColours.Of(state, berlin, Ger, stock, scale, "heat"));
		Assert.Equal(CCustomMapColours.Dark, CCustomMapColours.Of(state, warszawa, Ger, stock, scale, "heat"));
		Assert.Equal(CCustomMapColours.Dark, CCustomMapColours.Of(state, berlin, null, stock, scale, "heat"));
	}

	[Fact]
	public void TheLegendWritesEachShadesStartByItsUnitAndTheLinesBeneath()
	{
		CGameState state = State();
		CCustomMapSource industry = Source(state, ECustomMapKind.Buildings, "industry");
		Assert.Equal("7", CCustomMapLegend.Under(Localise, industry, CCustomMapScale.Of(state, industry), 7));

		CCustomMapSource days = Source(state, ECustomMapKind.Supply, "days_of_supplies");
		CCustomMapScale bands = CCustomMapScale.Of(state, days);
		Assert.Equal(("0", "5", "30"), (CCustomMapLegend.Under(Localise, days, bands, 1), CCustomMapLegend.Under(Localise, days, bands, 5),
			CCustomMapLegend.Under(Localise, days, bands, 10)));

		// More is worse: the largest start under the first shade.
		CCustomMapSource distance = Source(state, ECustomMapKind.Supply, "depot_distance");
		CCustomMapScale far = CCustomMapScale.Of(state, distance);
		Assert.Equal(("[OPENHOI_MAPMODE_CUT_OFF]", "60", "0"), (CCustomMapLegend.Under(Localise, distance, far, 1),
			CCustomMapLegend.Under(Localise, distance, far, 2), CCustomMapLegend.Under(Localise, distance, far, 10)));
		CCustomMapSource load = Source(state, ECustomMapKind.Supply, "supply_load");
		Assert.Equal("85%", CCustomMapLegend.Under(Localise, load, CCustomMapScale.Of(state, load), 2));

		Assert.Equal(("123", "12.3", "1.5", "0.025", "2"), (CCustomMapLegend.Amount(123456), CCustomMapLegend.Amount(12340),
			CCustomMapLegend.Amount(1500), CCustomMapLegend.Amount(25), CCustomMapLegend.Amount(2000)));

		Assert.Equal("[OPENHOI_MAPMODE_LEVEL_NOTE]\n[OPENHOI_MAPMODE_GREY_NOTE]",
			CCustomMapLegend.Notes(Localise, industry, CCustomMapScale.Of(state, industry)));
		CCustomMapSource stock = Source(state, ECustomMapKind.Supply, "supply_stock");
		Assert.Equal("[OPENHOI_MAPMODE_SUPPLY_STOCK_DESC]\n[OPENHOI_MAPMODE_NONE_HAS_ANY]\n[OPENHOI_MAPMODE_GREY_SUPPLY_NOTE]",
			CCustomMapLegend.Notes(Localise, stock, CCustomMapScale.Of(state, stock)));
		Assert.Equal("OPENHOI_MAPMODE_PALETTE_HEAT", CCustomMapLegend.PaletteKey("heat"));
	}

	[Fact]
	public void ThePaletteIsKeptWithTheEnhancedSettingsAndTheResourcesTooltipGainsTwoLines()
	{
		var problems = new List<string>();
		var settings = new CEnhancedSettings(MapPalette: "heat");
		Assert.Equal(settings, CEnhancedSettingsFile.Parse(CEnhancedSettingsFile.Write(settings), "test", problems.Add));
		Assert.Equal("green", CEnhancedSettingsFile.Parse("mapmode_palette = purple", "test", problems.Add).MapPalette);
		Assert.Single(problems);

		CGameState state = State();
		CProvince berlin = state.Province(Berlin);
		berlin.Manpower = CFixedPoint.Parse("1.245");
		berlin.Leadership = CFixedPoint.Parse("0.05");
		// The faithful game says nothing of them.
		Assert.Equal("", CMapModeText.Resources(Localise, state, berlin, Ger));
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		// To two places, rounded half up.
		Assert.Equal("\n[MANPOWER_IRO]: 1.25\n[PV_LEADERSHIP]: 0.05", CMapModeText.Resources(Localise, state, berlin, Ger));
		Assert.Equal("", CMapModeText.Resources(Localise, state, state.Province(Warszawa), null));
	}
}
