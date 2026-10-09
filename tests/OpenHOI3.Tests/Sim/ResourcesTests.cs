using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Home production and the parts of IC that came with it, against
/// <c>docs/mechanics/resources.md</c> and <c>docs/mechanics/ic.md</c>. The numbers in
/// <see cref="ReproducesGermanysMetalFromTheOriginalsSavegame"/> are the original's own.
/// </summary>
public class ResourcesTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Fra = new("FRA");
	private static readonly CBuilding Industry = new(0, "industry", 10, "ic", CFixedPoint.One);

	private static CCountry Country(string tag, int capital)
	{
		return new CCountry { Tag = new CCountryTag(tag), Name = tag, DefinitionFile = "", Capital = capital };
	}

	private static CProvince Land(int id, CCountryTag? controller, params int[] neighbours)
	{
		return new CProvince(id, $"P{id}", isSea: false)
		{
			Owner = controller,
			Controller = controller,
			Neighbours = neighbours,
		};
	}

	private static CProvince Yielding(CProvince province, EGoods goods, string amount)
	{
		province.MaxProducing[goods] = CFixedPoint.Parse(amount);
		province.CurrentProducing[goods] = CFixedPoint.Parse(amount);
		return province;
	}

	/// <param name="baseIc">What <c>base_values</c> gives every country, or null for no such modifier.</param>
	private static CGameState State(string? baseIc, IEnumerable<CTechnology>? technologies, params CProvince[] provinces)
	{
		var all = new List<CProvince> { new(0, "", isSea: false) };
		all.AddRange(provinces);

		var statics = new List<CModifier>();
		if (baseIc is not null)
		{
			var baseValues = new CModifier("base_values");
			baseValues.Set("ic", CFixedPoint.Parse(baseIc));
			statics.Add(baseValues);
		}
		TestRoads.Pave(all);
		return new CGameState(new CDate(1936, 1, 1), all, new[] { Country("GER", 1), Country("FRA", 9) },
			new[] { Industry, TestRoads.Infra }, technologies, statics);
	}

	private static CModifier Modifier(string name, string kind, string value)
	{
		var modifier = new CModifier(name);
		modifier.Set(kind, CFixedPoint.Parse(value));
		return modifier;
	}

	private static CTechnology Technology(string name, string kind, string perLevel)
	{
		return new CTechnology(name, 12, Modifier(name, kind, perLevel));
	}

	private static CFixedPoint Home(CGameState state, EGoods goods) => state.Country(Ger)!.HomeProduced[goods];

	[Fact]
	public void HomeProductionIsWhatTheProvincesJoinedToTheCapitalYield()
	{
		CGameState state = State(null, null,
			Yielding(Land(1, Ger, 2), EGoods.Metal, "10"),
			Yielding(Land(2, Ger, 1, 3), EGoods.Energy, "20"),
			Yielding(Land(3, Ger, 2), EGoods.Metal, "4"),
			Yielding(Land(4, Ger), EGoods.Metal, "100"));         // an island: it touches nothing

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.FromInt(14), Home(state, EGoods.Metal));    // the island's hundred has to be shipped
		Assert.Equal(CFixedPoint.FromInt(20), Home(state, EGoods.Energy));
		Assert.Equal(new[] { 1, 2, 3 }, CResources.ProvincesJoinedToCapital(state, state.Country(Ger)!));
	}

	[Fact]
	public void LandSomeoneElseHoldsDoesNotJoinAProvinceToTheCapital()
	{
		CGameState state = State(null, null,
			Yielding(Land(1, Ger, 2), EGoods.Metal, "10"),
			Land(2, Fra, 1, 3),
			Yielding(Land(3, Ger, 2), EGoods.Metal, "4"));

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.FromInt(10), Home(state, EGoods.Metal));
	}

	[Fact]
	public void SeaDoesNotJoinProvincesEither()
	{
		var sea = new CProvince(2, "Sea", isSea: true) { Neighbours = new[] { 1, 3 } };
		CGameState state = State(null, null,
			Yielding(Land(1, Ger, 2), EGoods.Metal, "10"),
			sea,
			Yielding(Land(3, Ger, 2), EGoods.Metal, "4"));

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.FromInt(10), Home(state, EGoods.Metal));
	}

	[Fact]
	public void ACountrysLawsScaleWhatItsProvincesYield()
	{
		CGameState state = State(null, null, Yielding(Land(1, Ger), EGoods.CrudeOil, "40"));
		state.Country(Ger)!.Laws["economic_law"] = new CLaw("economic_law",
			Modifier("full_civilian_economy", "global_resources", "-0.45"));

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.FromInt(22), Home(state, EGoods.CrudeOil));
	}

	[Fact]
	public void ReproducesGermanysMetalFromTheOriginalsSavegame()
	{
		// 134 metal in the provinces, laws worth +10%, two levels of steel production at
		// 5% each, and five IC of base values: the save says 167.140.
		CGameState state = State("5", new[] { Technology("steel_production", "metal_production", "0.05") },
			Yielding(Land(1, Ger, 2), EGoods.Metal, "100"),
			Yielding(Land(2, Ger, 1), EGoods.Metal, "34"));
		CCountry germany = state.Country(Ger)!;
		germany.Laws["economic_law"] = new CLaw("economic_law", Modifier("war_economy", "global_resources", "0.1"));
		germany.TechnologyLevels["steel_production"] = 2;

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("167.140"), Home(state, EGoods.Metal));
	}

	[Fact]
	public void TechnologyMultipliesWhatTheLawsLeftRatherThanAddingToThem()
	{
		CGameState state = State(null, new[] { Technology("steel_production", "metal_production", "0.05") },
			Yielding(Land(1, Ger), EGoods.Metal, "100"));
		CCountry germany = state.Country(Ger)!;
		germany.Laws["economic_law"] = new CLaw("economic_law", Modifier("law", "global_resources", "-0.5"));
		germany.TechnologyLevels["steel_production"] = 2;

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.FromInt(55), Home(state, EGoods.Metal));    // 100 * 0.5 * 1.1, not 100 * 0.6
	}

	[Fact]
	public void EachProvinceIsScaledAndTruncatedOnItsOwn()
	{
		// 3 * 0.55 is 1.65, and 1.65 * 1.05 is 1.7325, which is 1.732. Twice that is 3.464;
		// scaling the six together would give 3.465.
		CGameState state = State(null, new[] { Technology("steel_production", "metal_production", "0.05") },
			Yielding(Land(1, Ger, 2), EGoods.Metal, "3"),
			Yielding(Land(2, Ger, 1), EGoods.Metal, "3"));
		CCountry germany = state.Country(Ger)!;
		germany.Laws["economic_law"] = new CLaw("economic_law", Modifier("law", "global_resources", "-0.45"));
		germany.TechnologyLevels["steel_production"] = 1;

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("3.464"), Home(state, EGoods.Metal));
	}

	[Fact]
	public void EachResourceHasItsOwnTechnologyAndCrudeOilHasNone()
	{
		var technologies = new[]
		{
			Technology("steel_production", "metal_production", "0.05"),
			Technology("coal_processing_technologies", "energy_production", "0.05"),
			Technology("raremetal_refinning_techniques", "rares_production", "0.05"),
			Technology("oil_refinning", "refinery_efficiency", "0.1"),
		};
		CProvince capital = Land(1, Ger);
		Yielding(capital, EGoods.Metal, "100");
		Yielding(capital, EGoods.Energy, "100");
		Yielding(capital, EGoods.RareMaterials, "100");
		Yielding(capital, EGoods.CrudeOil, "100");
		CGameState state = State(null, technologies, capital);
		CCountry germany = state.Country(Ger)!;
		germany.TechnologyLevels["steel_production"] = 1;
		germany.TechnologyLevels["coal_processing_technologies"] = 2;
		germany.TechnologyLevels["raremetal_refinning_techniques"] = 3;
		germany.TechnologyLevels["oil_refinning"] = 4;

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.FromInt(105), Home(state, EGoods.Metal));
		Assert.Equal(CFixedPoint.FromInt(110), Home(state, EGoods.Energy));
		Assert.Equal(CFixedPoint.FromInt(115), Home(state, EGoods.RareMaterials));
		Assert.Equal(CFixedPoint.FromInt(100), Home(state, EGoods.CrudeOil));   // refining is not drilling
	}

	[Fact]
	public void EveryCountryThatHoldsItsCapitalProducesWhatBaseValuesIcNeeds()
	{
		CGameState state = State("5", null, Land(1, Ger));     // a capital that yields nothing at all
		state.Country(Ger)!.Laws["economic_law"] = new CLaw("economic_law", Modifier("law", "global_resources", "-0.5"));

		CTick.RunEntryPass(state);

		// 1 metal, 2 energy and half a rare material for each of the five IC, and the laws do not touch it.
		Assert.Equal(CFixedPoint.FromInt(5), Home(state, EGoods.Metal));
		Assert.Equal(CFixedPoint.FromInt(10), Home(state, EGoods.Energy));
		Assert.Equal(CFixedPoint.Parse("2.5"), Home(state, EGoods.RareMaterials));
		Assert.Equal(CFixedPoint.Zero, Home(state, EGoods.CrudeOil));
		Assert.Equal(CFixedPoint.Zero, Home(state, EGoods.Supplies));
	}

	[Fact]
	public void ACountryThatDoesNotHoldItsCapitalProducesNothingAtHome()
	{
		CGameState state = State("5", null,
			Yielding(Land(1, Fra), EGoods.Metal, "10"),            // Germany's capital, in French hands
			Yielding(Land(2, Ger, 1), EGoods.Metal, "10"));

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Zero, Home(state, EGoods.Metal));
		Assert.Equal(CFixedPoint.Zero, Home(state, EGoods.Energy));
		Assert.Empty(CResources.ProvincesJoinedToCapital(state, state.Country(Ger)!));
	}

	[Fact]
	public void IsRebuiltEachDayNotAddedTo()
	{
		CProvince mine = Yielding(Land(2, Ger, 1), EGoods.Metal, "7");
		CGameState state = State(null, null, Yielding(Land(1, Ger, 2), EGoods.Metal, "10"), mine);
		var simulation = new CSimulation(state);
		Assert.Equal(CFixedPoint.FromInt(17), Home(state, EGoods.Metal));

		simulation.RunHours(48);
		Assert.Equal(CFixedPoint.FromInt(17), Home(state, EGoods.Metal));    // two days on, still one day's worth

		mine.Controller = Fra;
		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.FromInt(10), Home(state, EGoods.Metal));
	}

	// --- the parts of IC that arrived with this

	private static CProvince WithIndustry(CProvince province, int level)
	{
		province.SetBuildingLevel(Industry, CFixedPoint.FromInt(level));
		return province;
	}

	[Fact]
	public void EveryCountrysIcStartsFromBaseValues()
	{
		CGameState state = State("5", null, WithIndustry(Land(1, Ger), 139));

		CTick.RunEntryPass(state);

		Assert.Equal(144, state.Country(Ger)!.BaseIc);          // its provinces' 139 and the 5 everyone has
		Assert.Equal(5, state.Country(Fra)!.BaseIc);            // a country that holds nothing still has the 5
		Assert.Equal(5, state.Country(Fra)!.TotalIc);
	}

	[Fact]
	public void TheBaseValuesIcIsScaledByTheCountrysBonusLikeTheRest()
	{
		CGameState state = State("5", null, WithIndustry(Land(1, Ger), 13));
		state.Country(Ger)!.Laws["economic_law"] = new CLaw("economic_law", Modifier("law", "global_ic", "-0.45"));

		CTick.RunEntryPass(state);

		Assert.Equal(9, state.Country(Ger)!.TotalIc);           // (13 + 5) * 0.55 is 9.9
	}

	[Fact]
	public void TechnologyAddsToTheIcBonusBesideTheCountrysModifier()
	{
		CGameState state = State(null, new[] { Technology("industral_production", "ic_modifier", "0.025") },
			WithIndustry(Land(1, Ger), 100));
		CCountry germany = state.Country(Ger)!;
		germany.Laws["economic_law"] = new CLaw("economic_law", Modifier("law", "global_ic", "-0.25"));
		germany.TechnologyLevels["industral_production"] = 4;

		CTick.RunEntryPass(state);

		Assert.Equal(CFixedPoint.Parse("0.1"), germany.Technology["ic_modifier"]);
		Assert.Equal(CFixedPoint.Zero, germany.Modifier["ic_modifier"]);    // kept apart from the laws
		Assert.Equal(85, germany.TotalIc);                      // 100 * (1 - 0.25 + 0.1)
	}

	[Fact]
	public void ATechnologyTheGameDoesNotKnowOrALevelOfNoneIsWorthNothing()
	{
		CGameState state = State(null, new[] { Technology("industral_production", "ic_modifier", "0.025") },
			WithIndustry(Land(1, Ger), 100));
		CCountry germany = state.Country(Ger)!;
		germany.TechnologyLevels["industral_production"] = 0;
		germany.TechnologyLevels["perpetual_motion"] = 9;

		CTick.RunEntryPass(state);

		Assert.Empty(germany.Technology.Values);
		Assert.Equal(100, germany.TotalIc);
	}

	[Fact]
	public void GoodsAreKnownByTheNamesTheFilesUse()
	{
		Assert.True(CGoodsPool.TryParseKey("rare_materials", out EGoods rares));
		Assert.Equal(EGoods.RareMaterials, rares);
		Assert.True(CGoodsPool.TryParseKey("Crude_Oil", out EGoods oil));
		Assert.Equal(EGoods.CrudeOil, oil);
		Assert.False(CGoodsPool.TryParseKey("gold", out _));
		Assert.Equal("energy", CGoodsPool.KeyOf(EGoods.Energy));
	}
}
