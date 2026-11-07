using System;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Interface;

/// <summary>The new map modes' colours, against <c>docs/mechanics/mapmodes.md</c>.</summary>
public class MapModeColoursTests
{
	[Fact]
	public void TheInfrastructureRampsLevelIsTheModifierOverAHundredRoundedDown()
	{
		Assert.Equal(0, CInfrastructureMapColours.Level(0));
		Assert.Equal(0, CInfrastructureMapColours.Level(99));
		Assert.Equal(1, CInfrastructureMapColours.Level(100));
		Assert.Equal(7, CInfrastructureMapColours.Level(700));
		Assert.Equal(7, CInfrastructureMapColours.Level(799));
		Assert.Equal(10, CInfrastructureMapColours.Level(1000));
		Assert.Equal(10, CInfrastructureMapColours.Level(1500));
		// Checked whole against the float arithmetic: every modifier up to 1000 gives its hundreds.
		for (long infrastructure = 0; infrastructure <= 1000; infrastructure++)
		{
			Assert.Equal((int)(infrastructure / 100), CInfrastructureMapColours.Level(infrastructure));
		}
	}

	[Fact]
	public void ARegionsColourComesInTurnAndTheRegionMetWhenTheListRunsOutKeepsWhite()
	{
		var colours = new[] { new CColor(1, 0, 0), new CColor(0, 1, 0) };
		var regions = new[]
		{
			new CRegion("a", new[] { 1 }, false), new CRegion("empty", Array.Empty<int>(), false),
			new CRegion("b", new[] { 2 }, false), new CRegion("c", new[] { 3 }, false), new CRegion("d", new[] { 4 }, false),
		};

		CColor[] assigned = CRegionMapColours.Assign(regions, colours);

		Assert.Equal(colours[0], assigned[0]);
		Assert.Equal(CRegionMapColours.White, assigned[1]);   // empty: passed over, no colour used
		Assert.Equal(colours[1], assigned[2]);
		Assert.Equal(CRegionMapColours.White, assigned[3]);   // the list ran out here
		Assert.Equal(colours[0], assigned[4]);                // and starts again
		// With no colours every region is white.
		Assert.All(CRegionMapColours.Assign(regions, Array.Empty<CColor>()), colour => Assert.Equal(CRegionMapColours.White, colour));
	}

	[Fact]
	public void ALandUnitWeighsByWhatIsKnownOfItsProvince()
	{
		var type = new CSubUnitType("infantry_brigade", EUnitBranch.Land);
		var unit = new CUnit(EUnitKind.Division, new CCountryTag("GER")) { Id = 40 };
		for (int index = 0; index < 3; index++)
		{
			unit.SubUnits.Add(new CSubUnit(type)
			{
				Unit = unit, Strength = CFixedPoint.FromInt(30), Organisation = CFixedPoint.FromInt(30),
				Experience = CFixedPoint.FromInt(10),
			});
		}

		Assert.Equal(1f, CStrengthMapColours.Weight(unit, 2));
		// 90 strength x 0.01 x (40 mod 37 + 15).
		Assert.Equal(0.9f * 18f, CStrengthMapColours.Weight(unit, 4), 3);
		Assert.Equal(0.9f * 30f, CStrengthMapColours.Weight(unit, 6), 3);
		Assert.Equal(0.9f * 30f * 11f, CStrengthMapColours.Weight(unit, 9), 3);
	}

	[Fact]
	public void AColourScaledByABrightnessIsHeldAtOne()
	{
		CColor scaled = CSimplifiedTerrainColours.Scaled(new CColor(0.8f, 0.4f, 1f, 0.5f), 0.5f);
		Assert.Equal(new CColor(0.4f, 0.2f, 0.5f, 0.5f), scaled);
		Assert.Equal(1f, CSimplifiedTerrainColours.Scaled(new CColor(0.8f, 0.4f, 1f), 2f).R);
	}

	[Fact]
	public void ATheatrePaintsItsFrontProvincesAsWellAsTheRest()
	{
		var ger = new CCountryTag("GER");
		var germany = new CCountry { Tag = ger, Name = "Germany", DefinitionFile = "" };
		var state = new CGameState(new CDate(1936, 1, 1),
			new[]
			{
				new CProvince(0, "", isSea: false),
				new CProvince(1, "Berlin", isSea: false) { Owner = ger, Controller = ger },
				new CProvince(2, "Breslau", isSea: false) { Owner = ger, Controller = ger },
			}, new[] { germany });
		var theatre = new OpenHOI3.Sim.Theatres.CTheatre(1, ger);
		// Berlin behind the front, Breslau on it: the saved list holds only the first, the whole list both.
		theatre.Provinces.Add(1);
		theatre.AllProvinces.AddRange(new[] { 1, 2 });
		germany.Strategy.Theatres.Add(theatre);
		var colour = new CColor(0.2f, 0.4f, 0.6f);

		var painted = CTheatreMapColours.Build(state, ger, new[] { colour });

		Assert.Equal(colour, painted[1]);
		Assert.Equal(colour, painted[2]);
	}

	[Fact]
	public void ContinentPaintingHandsTheColoursOutFromTheFirstAgainInTheFilesOrder()
	{
		var colours = new[] { new CColor(1, 0, 0), new CColor(0, 1, 0) };

		var assigned = CRegionMapColours.AssignContinents(new[] { "europe", "asia", "africa" }, colours);

		Assert.Equal(colours[0], assigned["europe"]);
		Assert.Equal(colours[1], assigned["asia"]);
		Assert.Equal(CRegionMapColours.White, assigned["africa"]);
	}
}
