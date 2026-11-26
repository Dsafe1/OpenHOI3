using OpenHOI3.Interface.Screens;
using OpenHOI3.Interface.Tooltips;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Sim;

/// <summary>
/// Which model a brigade is shown as: the model nearest the levels it carries, of its builder's models or its owner's,
/// never the model an order of battle raised it as - so a brigade loaded from a save, which names none, is shown as the
/// one it was (<c>docs/mechanics/units.md</c>, section 5).
/// </summary>
public class BrigadeModelTests
{
	private static readonly CCountryTag Ger = new("GER");
	private static readonly CCountryTag Ita = new("ITA");

	private static (CGameState State, CCountry Germany, CCountry Italy, CSubUnitType Type) World()
	{
		var germany = new CCountry { Tag = Ger, Name = "Germany", DefinitionFile = "" };
		var italy = new CCountry { Tag = Ita, Name = "Italy", DefinitionFile = "" };
		var defines = new CDefines();
		defines.Set(CHistoricalModels.MilitarySection, CHistoricalModels.MaxDefine, CFixedPoint.FromInt(4));
		var state = new CGameState(new CDate(1936, 1, 1), new[] { new CProvince(0, "", isSea: false) }, new[] { germany, italy },
			defines: defines);
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		type.Technologies.Add(new CTechnology("smallarms", 10, new OpenHOI3.Sim.Modifiers.CModifier("smallarms")));
		type.Technologies.Add(new CTechnology("radio", 10, new OpenHOI3.Sim.Modifiers.CModifier("radio")) { Change = false });
		return (state, germany, italy, type);
	}

	[Fact]
	public void ABrigadeIsTheModelItsOwnLevelsAreNotTheOneItWasRaisedAs()
	{
		(CGameState state, CCountry germany, _, CSubUnitType type) = World();
		var brigade = new CSubUnit(type) { HistoricalModel = 0 };
		brigade.TechnologyLevels["smallarms"] = 2;
		brigade.TechnologyLevels["radio"] = 3;      // changes nothing, so matches any model

		Assert.Equal(2, CHistoricalModels.OfBrigade(state, germany, brigade));
		Assert.False(CHistoricalModels.IsInexact(state, germany, brigade));
		Assert.Equal("III", CUnitPanelText.BrigadeLevel(state, germany, brigade));
	}

	[Fact]
	public void LevelsPastEveryModelAreTheNearestAndMarked()
	{
		(CGameState state, CCountry germany, _, CSubUnitType type) = World();
		var brigade = new CSubUnit(type);
		brigade.TechnologyLevels["smallarms"] = 7;

		// Four models, 0 to 3: the last is nearest, and none is exact.
		Assert.Equal(3, CHistoricalModels.OfBrigade(state, germany, brigade));
		Assert.True(CHistoricalModels.IsInexact(state, germany, brigade));
		Assert.Equal("Infantry (IV)+*", CBrigadeTooltips.ModelLabel(key => key == "infantry_brigade" ? "Infantry" : key, _ => null,
			state, germany, brigade));
	}

	[Fact]
	public void TheFirstOfTwoEquallyNearModelsWins()
	{
		(CGameState state, CCountry germany, _, CSubUnitType type) = World();
		germany.HistoricalModels["infantry_brigade"] = new() { [1] = new() { ["smallarms"] = 3 } };
		var brigade = new CSubUnit(type);
		brigade.TechnologyLevels["smallarms"] = 3;

		// Model 1 is the file's, at 3; model 3 the default's, at 3 too.
		Assert.Equal(1, CHistoricalModels.OfBrigade(state, germany, brigade));
	}

	[Fact]
	public void AnEnhancedGamesModelThatAsksForMoreThanIsHeldLosesToOneThatDoesNot()
	{
		(CGameState state, CCountry germany, _, CSubUnitType type) = World();
		germany.HistoricalModels["infantry_brigade"] = new()
		{
			[0] = new() { ["smallarms"] = 0 }, [1] = new() { ["smallarms"] = 4 },
			[2] = new() { ["smallarms"] = 8 }, [3] = new() { ["smallarms"] = 9 },
		};
		germany.TechnologyLevels["smallarms"] = 3;
		var brigade = new CSubUnit(type);
		brigade.TechnologyLevels["smallarms"] = 3;

		// The original's: a level too many is as near as a level too few, and model 1 is one away.
		Assert.Equal(1, CHistoricalModels.PickCurrent(state, germany, type));
		Assert.Equal(1, CHistoricalModels.OfBrigade(state, germany, brigade));

		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(System.Array.Empty<OpenHOI3.Sim.Rules.EFix>()));
		Assert.Equal(0, CHistoricalModels.PickCurrent(state, germany, type));
		Assert.Equal(0, CHistoricalModels.OfBrigade(state, germany, brigade));
		// Where every model asks more, the first of them: each such technology counts the same whatever the gap.
		germany.HistoricalModels["infantry_brigade"][0]["smallarms"] = 9;
		Assert.Equal(0, CHistoricalModels.PickCurrent(state, germany, type));
		// A game that goes without the fix keeps the original's.
		state.Rules.CopyFrom(new OpenHOI3.Sim.Rules.CRules(new[] { OpenHOI3.Sim.Rules.EFix.HistoricalModelChoice }));
		Assert.Equal(1, CHistoricalModels.PickCurrent(state, germany, type));
	}

	[Fact]
	public void ABrigadeBuiltForAnotherIsNamedByItsBuildersModelsAndItsNumeralByItsOwners()
	{
		(CGameState state, CCountry germany, CCountry italy, CSubUnitType type) = World();
		italy.HistoricalModels["infantry_brigade"] = new() { [0] = new() { ["smallarms"] = 2 } };
		var brigade = new CSubUnit(type) { Builder = Ita };
		brigade.TechnologyLevels["smallarms"] = 2;

		Assert.Same(italy, CHistoricalModels.BuilderOf(state, germany, brigade));
		// Italy's file model lists no radio, so it is never matched exactly and is marked, though it is the nearest.
		Assert.Equal("ITA 0+*", CBrigadeTooltips.ModelLabel(key => key, key => key.EndsWith("_0") ? key[..3] + " 0" : null,
			state, germany, brigade));
		Assert.Equal(new[] { "gfx/models/ITA_infantry_brigade_0.tga", "gfx/models/infantry_brigade_0.tga", "gfx/models/infantry_brigade_0.tga" },
			CBrigadeCardText.Pictures(state, germany, brigade));
		Assert.Equal("III", CUnitPanelText.BrigadeLevel(state, germany, brigade));
	}
}
