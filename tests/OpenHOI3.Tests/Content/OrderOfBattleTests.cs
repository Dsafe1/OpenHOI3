using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.History;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The readers for unit types, historical models and orders of battle, against an
/// install the test writes itself. See <c>docs/mechanics/units.md</c>.
/// </summary>
public class OrderOfBattleTests : IDisposable
{
	private readonly string _root;
	private readonly List<string> _warnings = new();

	public OrderOfBattleTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-units-" + Guid.NewGuid().ToString("N"));
		Write("localisation/countries.csv", "AAA;Aland;;;;;;;;;;;;;x\nBBB;Bland;;;;;;;;;;;;;x\n");
		Write("common/countries.txt", "AAA = \"countries/A.txt\"\nBBB = \"countries/B.txt\"\n");
		Write("common/countries/A.txt", "color = { 1 2 3 }\n");
		Write("common/countries/B.txt", "color = { 4 5 6 }\n");
		Write("units/foot.txt", """
			foot_brigade = {
				type = land
				sprite = Infantry
				active = no
				unit_group = infantry_unit_type
				max_strength = 30
				default_organisation = 30
				officers = 100
				combat_width = 1
				on_completion = infantry_practical
				hills = { attack = -0.10 movement = -0.20 }
				river = { attack = -0.05 }
			}
			staff_brigade = {
				type = land
				max_strength = 10
				default_organisation = 30
				officers = 100
			}
			""");
		Write("units/sea.txt", """
			flat_top = { type = naval max_strength = 100 default_organisation = 30 capital = yes usable_by = { AAA BBB } }
			deck_wing = { type = air max_strength = 100 default_organisation = 30 is_cag = yes }
			""");
		Write("technologies/land.txt", """
			rifles = {
				foot_brigade = { soft_attack = 0.5 hills = { attack = 0.05 } }
				start_year = 1918
				first_offset = 1934
				additional_offset = 2
				max_level = 12
			}
			drill = {
				foot_brigade = { default_organisation = 5 }
				staff_brigade = { default_organisation = 5 }
				change = no
				additional_offset = 2
				max_level = 12
			}
			goggles = {
				foot_brigade = { soft_attack = 1 }
				allow = { rifles = 3 }
				start_year = 1940
			}
			""");
	}

	public void Dispose()
	{
		try
		{
			Directory.Delete(_root, recursive: true);
		}
		catch (IOException)
		{
		}
		GC.SuppressFinalize(this);
	}

	private void Write(string relative, string content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, Encoding.Latin1.GetBytes(content));
	}

	private CDirectorySettings Directories => CDirectorySettings.At(_root);

	private static readonly string[] Terrains = { "hills", "forest" };

	private Dictionary<string, CSubUnitType> Types()
	{
		var types = new Dictionary<string, CSubUnitType>(StringComparer.OrdinalIgnoreCase);
		foreach (CSubUnitType type in CSubUnitTypeReader.Read(Directories, Terrains, _warnings.Add))
		{
			types[type.Key] = type;
		}
		return types;
	}

	private Dictionary<string, CTechnology> Technologies(Dictionary<string, CSubUnitType> types)
	{
		var technologies = new Dictionary<string, CTechnology>(StringComparer.OrdinalIgnoreCase);
		foreach (CTechnology technology in CTechnologyReader.Read(Directories, _warnings.Add, null, types, Terrains))
		{
			technologies[technology.Name] = technology;
		}
		return technologies;
	}

	/// <summary>Reads country AAA's order of battle from the text given, and country BBB's if a second is.</summary>
	private (CCountry First, CCountry Second, int LastId) Read(string first, string? second = null)
	{
		Dictionary<string, CSubUnitType> types = Types();
		Dictionary<string, CTechnology> technologies = Technologies(types);
		CCountries countries = CCountries.Load(Directories, CLocalisation.Load(Directories), _warnings.Add);
		CCountry a = countries.Find("AAA")!;
		CCountry b = countries.Find("BBB")!;
		Write("history/units/a.txt", first);
		a.OrderOfBattleFile = "a.txt";
		if (second is not null)
		{
			Write("history/units/b.txt", second);
			b.OrderOfBattleFile = "b.txt";
		}
		int last = COrderOfBattleReader.Read(Directories, countries.Declared, types, technologies, provinceCount: 100, _warnings.Add);
		return (a, b, last);
	}

	// ---- unit types

	[Fact]
	public void AUnitTypeHasItsBranchItsFiguresAndItsFlags()
	{
		Dictionary<string, CSubUnitType> types = Types();

		CSubUnitType foot = types["foot_brigade"];
		Assert.Equal(EUnitBranch.Land, foot.Branch);
		Assert.Equal(CFixedPoint.FromInt(30), foot.Stats["max_strength"]);
		Assert.Equal(CFixedPoint.FromInt(100), foot.Stats["officers"]);
		Assert.Equal("Infantry", foot.Sprite);
		Assert.Equal("infantry_unit_type", foot.UnitGroup);
		Assert.Equal("infantry_practical", foot.OnCompletion);
		Assert.False(foot.Flag("active"));
		Assert.True(foot.Flags.ContainsKey("active"));

		Assert.Equal(EUnitBranch.Naval, types["flat_top"].Branch);
		Assert.True(types["flat_top"].Flag("capital"));
		Assert.Equal(new[] { "AAA", "BBB" }, types["flat_top"].UsableBy);
		Assert.Equal(EUnitBranch.Air, types["deck_wing"].Branch);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void WhatTerrainDoesToATypeIsKeptByTheTerrainsName()
	{
		CSubUnitType foot = Types()["foot_brigade"];

		Assert.Equal(CFixedPoint.Parse("-0.1"), foot.Stats.Adjusters["hills"]["attack"]);
		Assert.Equal(CFixedPoint.Parse("-0.2"), foot.Stats.Adjusters["hills"]["movement"]);
		Assert.Equal(CFixedPoint.Parse("-0.05"), foot.Stats.Adjusters["river"]["attack"]);
	}

	[Fact]
	public void AKeyNothingKnowsIsReported()
	{
		Write("units/odd.txt", "odd_brigade = { type = land max_strength = 30 shininess = 4 swamp = { attack = 1 } }\n");

		Types();

		string warning = Assert.Single(_warnings);
		Assert.Contains("shininess", warning);
		Assert.Contains("swamp", warning);
	}

	[Fact]
	public void ATypeWithNoBranchIsLeftOut()
	{
		Write("units/odd.txt", "odd_brigade = { type = submarine_land max_strength = 30 }\n");

		Assert.False(Types().ContainsKey("odd_brigade"));
		Assert.Contains(_warnings, warning => warning.Contains("odd_brigade"));
	}

	[Fact]
	public void TheModelsFolderIsNotReadAsUnitTypes()
	{
		Write("units/models/AAA - Land.txt", "foot_brigade.0 = { rifles = 1 }\n");

		Assert.Equal(4, Types().Count);
		Assert.Empty(_warnings);
	}

	// ---- what a technology gives a type

	[Fact]
	public void ABlockNamedAfterAUnitTypeIsWhatALevelAddsToIt()
	{
		Dictionary<string, CSubUnitType> types = Types();
		Dictionary<string, CTechnology> technologies = Technologies(types);

		CSubUnitStats gives = technologies["rifles"].UnitEffects["foot_brigade"];
		Assert.Equal(CFixedPoint.Parse("0.5"), gives["soft_attack"]);
		Assert.Equal(CFixedPoint.Parse("0.05"), gives.Adjusters["hills"]["attack"]);
		Assert.False(technologies["rifles"].UnitEffects.ContainsKey("staff_brigade"));
		// The block is the type's, not an effect on the country.
		Assert.Empty(technologies["rifles"].EffectPerLevel.Values);
	}

	[Fact]
	public void EachTypeIsToldWhichTechnologiesImproveItInTheOrderTheyAreDeclared()
	{
		Dictionary<string, CSubUnitType> types = Types();
		Technologies(types);

		Assert.Equal(new[] { "rifles", "drill", "goggles" }, types["foot_brigade"].Technologies.Select(t => t.Name));
		Assert.Equal(new[] { "drill" }, types["staff_brigade"].Technologies.Select(t => t.Name));
		Assert.Empty(types["flat_top"].Technologies);
	}

	[Fact]
	public void ADoctrineSaysItNeedsNoNewEquipment()
	{
		Dictionary<string, CTechnology> technologies = Technologies(Types());

		Assert.True(technologies["rifles"].Change);
		Assert.False(technologies["drill"].Change);
	}

	[Fact]
	public void ATechnologyKnowsWhetherItHasAPrecondition()
	{
		Dictionary<string, CTechnology> technologies = Technologies(Types());

		Assert.False(technologies["rifles"].HasPrecondition);
		Assert.True(technologies["goggles"].HasPrecondition);
		Assert.True(technologies["goggles"].IsSingleLevel);
	}

	// ---- historical models

	private CCountries ReadModels(Dictionary<string, CSubUnitType> types, Dictionary<string, CTechnology> technologies,
		int defaultCount = 10)
	{
		CCountries countries = CCountries.Load(Directories, CLocalisation.Load(Directories), _warnings.Add);
		CHistoricalModelReader.Read(Directories, countries, types, technologies, defaultCount, _warnings.Add);
		return countries;
	}

	[Fact]
	public void AModelFileBelongsToTheCountryItsNameStartsWith()
	{
		Write("units/models/aaa - land.txt", "foot_brigade.1 = { rifles = 3 drill = 1 }\n");
		Dictionary<string, CSubUnitType> types = Types();

		CCountries countries = ReadModels(types, Technologies(types));

		Dictionary<string, int> model = countries.Find("AAA")!.HistoricalModels["foot_brigade"][1];
		Assert.Equal(3, model["rifles"]);
		Assert.Equal(1, model["drill"]);
		Assert.False(model.ContainsKey("goggles"));
		Assert.Empty(countries.Find("BBB")!.HistoricalModels);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void AFileNamedForNoCountryIsSkipped()
	{
		Write("units/models/ZZZ - land.txt", "foot_brigade.1 = { rifles = 3 }\n");
		Dictionary<string, CSubUnitType> types = Types();

		CCountries countries = ReadModels(types, Technologies(types));

		Assert.Empty(countries.Find("AAA")!.HistoricalModels);
		Assert.Contains(_warnings, warning => warning.Contains("ZZZ - land.txt"));
	}

	[Fact]
	public void AModelHasToBeAUnitTypeAndANumber()
	{
		Write("units/models/AAA.txt", """
			foot_brigade = { rifles = 1 }
			horse_brigade.0 = { rifles = 1 }
			foot_brigade.0 = { rifles = 2 }
			""");
		Dictionary<string, CSubUnitType> types = Types();

		CCountries countries = ReadModels(types, Technologies(types));

		Assert.Equal(2, countries.Find("AAA")!.HistoricalModels["foot_brigade"][0]["rifles"]);
		Assert.Contains(_warnings, warning => warning.Contains("not a unit type and a number"));
		Assert.Contains(_warnings, warning => warning.Contains("horse_brigade"));
	}

	[Fact]
	public void AModelPastTheDefaultsHasToBeTheVeryNextOne()
	{
		Write("units/models/AAA.txt", """
			foot_brigade.4 = { rifles = 9 }
			foot_brigade.2 = { rifles = 5 }
			foot_brigade.3 = { rifles = 6 }
			""");
		Dictionary<string, CSubUnitType> types = Types();

		CCountries countries = ReadModels(types, Technologies(types), defaultCount: 2);

		SortedDictionary<int, Dictionary<string, int>> models = countries.Find("AAA")!.HistoricalModels["foot_brigade"];
		Assert.Equal(new[] { 2, 3 }, models.Keys);
		Assert.Contains(_warnings, warning => warning.Contains("foot_brigade.4"));
	}

	[Fact]
	public void ATechnologyThatDoesNothingForTheTypeIsLeftOutOfItsModel()
	{
		Write("units/models/AAA.txt", "staff_brigade.0 = { rifles = 1 drill = 2 nonsense = 1 }\n");
		Dictionary<string, CSubUnitType> types = Types();

		CCountries countries = ReadModels(types, Technologies(types));

		Dictionary<string, int> model = countries.Find("AAA")!.HistoricalModels["staff_brigade"][0];
		Assert.Equal(new[] { "drill" }, model.Keys);
		Assert.Contains(_warnings, warning => warning.Contains("rifles for staff_brigade") && warning.Contains("nonsense"));
	}

	// ---- the order of battle

	[Fact]
	public void UnitsAreMadeInTheShapeTheFileGives()
	{
		(CCountry country, _, _) = Read("""
			theatre = {
				name = "High Command"
				location = 10
				regiment = { type = staff_brigade name = "High Command HQ" historical_model = 0 }
				corps = {
					name = "I Corps"
					location = 11
					leader = 77
					regiment = { type = staff_brigade name = "I Corps HQ" historical_model = 0 }
					division = {
						name = "1st Division"
						location = 12
						regiment = { type = foot_brigade name = "1st Regiment" historical_model = 1 experience = 45 }
						regiment = { type = foot_brigade name = "2nd Regiment" historical_model = 1 }
					}
				}
			}
			navy = { name = "Fleet" base = 20 location = 21 ship = { type = flat_top name = "Ark" historical_model = 0 pride = yes } }
			""");

		Assert.Equal(new[] { "High Command", "Fleet" }, country.Units.Select(unit => unit.Name));
		CUnit theatre = country.Units[0];
		Assert.Equal(EUnitKind.Theatre, theatre.Kind);
		Assert.Equal(10, theatre.Location);
		Assert.Null(theatre.Parent);
		CUnit corps = Assert.Single(theatre.Children);
		Assert.Equal(EUnitKind.Corps, corps.Kind);
		Assert.Same(theatre, corps.Parent);
		Assert.Equal(77, corps.LeaderId);
		CUnit division = Assert.Single(corps.Children);
		Assert.Equal(12, division.Location);
		Assert.Equal(new[] { "1st Regiment", "2nd Regiment" }, division.SubUnits.Select(brigade => brigade.Name));
		Assert.Equal(CFixedPoint.FromInt(45), division.SubUnits[0].Experience);
		Assert.True(division.SubUnits[1].Experience < CFixedPoint.Zero);
		Assert.Equal(1, division.SubUnits[0].HistoricalModel);
		Assert.Same(division, division.SubUnits[0].Unit);

		CUnit fleet = country.Units[1];
		Assert.Equal(EUnitKind.Navy, fleet.Kind);
		Assert.Equal(20, fleet.Base);
		Assert.True(fleet.SubUnits[0].IsPride);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void EachBlockTakesTheNextNumberAsItCloses()
	{
		(CCountry country, _, int last) = Read("""
			corps = {
				name = "I Corps"
				location = 11
				regiment = { type = staff_brigade historical_model = 0 }
				division = {
					name = "1st Division"
					location = 12
					regiment = { type = foot_brigade historical_model = 0 }
					regiment = { type = foot_brigade historical_model = 0 }
				}
				division = { name = "2nd Division" location = 13 regiment = { type = foot_brigade historical_model = 0 } }
			}
			""");

		CUnit corps = country.Units[0];
		Assert.Equal(1, corps.SubUnits[0].Id);
		Assert.Equal(new[] { 2, 3 }, corps.Children[0].SubUnits.Select(brigade => brigade.Id));
		Assert.Equal(4, corps.Children[0].Id);
		Assert.Equal(5, corps.Children[1].SubUnits[0].Id);
		Assert.Equal(6, corps.Children[1].Id);
		Assert.Equal(7, corps.Id);
		Assert.Equal(7, last);
	}

	[Fact]
	public void TheCountGoesOnFromOneCountryToTheNext()
	{
		(CCountry first, CCountry second, int last) = Read(
			"division = { name = \"A\" location = 1 regiment = { type = foot_brigade historical_model = 0 } }\n",
			"division = { name = \"B\" location = 2 regiment = { type = foot_brigade historical_model = 0 } }\n");

		Assert.Equal(2, first.Units[0].Id);
		Assert.Equal(3, second.Units[0].SubUnits[0].Id);
		Assert.Equal(4, second.Units[0].Id);
		Assert.Equal(4, last);
		Assert.Equal(new CCountryTag("BBB"), second.Units[0].Owner);
	}

	[Fact]
	public void AUnitUnderConstructionIsNotMadeButTakesFiveNumbers()
	{
		(CCountry country, _, int last) = Read("""
			military_construction = {
				country = AAA
				flat_top = { name = "Next" historical_model = 1 }
				cost = 5
				progress = 0
				duration = 60
			}
			division = { name = "A" location = 1 regiment = { type = foot_brigade historical_model = 0 } }
			""");

		CUnit division = Assert.Single(country.Units);
		Assert.Equal(6, division.SubUnits[0].Id);
		Assert.Equal(7, last);
		Assert.Contains(_warnings, warning => warning.Contains("under construction"));
	}

	[Fact]
	public void ADivisionsReserveLineMakesReservesOfTheRegimentsAfterIt()
	{
		(CCountry country, _, _) = Read("""
			division = {
				name = "A"
				location = 1
				regiment = { type = foot_brigade name = "before" historical_model = 0 }
				is_reserve = yes
				regiment = { type = foot_brigade name = "after" historical_model = 0 }
			}
			division = { name = "B" location = 1 regiment = { type = foot_brigade name = "own" historical_model = 0 is_reserve = yes } }
			""");

		Assert.False(country.Units[0].SubUnits[0].IsReserve);
		Assert.True(country.Units[0].SubUnits[1].IsReserve);
		Assert.True(country.Units[0].IsReserve);
		Assert.True(country.Units[1].SubUnits[0].IsReserve);
		Assert.False(country.Units[1].IsReserve);
	}

	[Fact]
	public void AReserveUnitsWordOutweighsARegimentsOwn()
	{
		(CCountry country, _, _) = Read(
			"division = { name = \"A\" location = 1 is_reserve = yes regiment = { type = foot_brigade historical_model = 0 is_reserve = no } }\n");

		Assert.True(country.Units[0].SubUnits[0].IsReserve);
	}

	[Fact]
	public void OnlyARegimentTakesItsUnitsWordThatItIsAReserve()
	{
		(CCountry country, _, _) = Read(
			"air = { name = \"A\" location = 1 base = 1 is_reserve = yes wing = { type = deck_wing historical_model = 0 } }\n");

		Assert.False(country.Units[0].SubUnits[0].IsReserve);
	}

	[Fact]
	public void ARegimentNobodyNamedIsItsPlaceInItsUnitAndTheUnitsName()
	{
		(CCountry country, _, _) = Read("""
			division = {
				name = "1st Division"
				location = 1
				regiment = { type = foot_brigade name = "Guards" historical_model = 0 }
				regiment = { type = foot_brigade historical_model = 0 }
				regiment = { type = foot_brigade historical_model = 0 }
			}
			""");

		Assert.Equal(new[] { "Guards", "2. 1st Division", "3. 1st Division" },
			country.Units[0].SubUnits.Select(brigade => brigade.Name));
	}

	[Fact]
	public void AWingNobodyNamedIsLeftWithoutAName()
	{
		(CCountry country, _, _) = Read(
			"air = { name = \"A\" location = 1 base = 1 wing = { type = deck_wing historical_model = 0 } }\n");

		Assert.Equal("", country.Units[0].SubUnits[0].Name);
	}

	[Fact]
	public void ACarriersAirGroupsAreAboardTheShipAndNumberedBeforeIt()
	{
		(CCountry country, _, int last) = Read("""
			navy = {
				name = "Fleet"
				base = 20
				location = 21
				ship = { type = flat_top name = "Ark" historical_model = 0 air = { location = 21 wing = { type = deck_wing historical_model = 1 } } }
			}
			""");

		CSubUnit ship = country.Units[0].SubUnits[0];
		CUnit aboard = Assert.Single(ship.Carried);
		Assert.Equal(EUnitKind.Air, aboard.Kind);
		Assert.Same(ship, aboard.CarriedBy);
		Assert.Null(aboard.Parent);
		Assert.Equal(1, aboard.SubUnits[0].Id);
		Assert.Equal(2, aboard.Id);
		Assert.Equal(3, ship.Id);
		Assert.Equal(4, last);
		Assert.Equal(new[] { "Fleet", "" }, country.AllUnits().Select(unit => unit.Name));
	}

	[Fact]
	public void ABrigadeOfATypeNobodyDeclaresIsLeftOutButNumbered()
	{
		(CCountry country, _, _) = Read("""
			division = {
				name = "A"
				location = 1
				regiment = { type = horse_brigade historical_model = 0 }
				regiment = { type = foot_brigade historical_model = 0 }
			}
			""");

		CSubUnit brigade = Assert.Single(country.Units[0].SubUnits);
		Assert.Equal(2, brigade.Id);
		Assert.Contains(_warnings, warning => warning.Contains("horse_brigade"));
	}

	[Fact]
	public void AUnitAtTheTopWithNoLocationIsThrownAway()
	{
		(CCountry country, _, _) = Read("""
			division = { name = "Nowhere" regiment = { type = foot_brigade historical_model = 0 } }
			division = { name = "Somewhere" location = 5 regiment = { type = foot_brigade historical_model = 0 } }
			""");

		CUnit division = Assert.Single(country.Units);
		Assert.Equal("Somewhere", division.Name);
		Assert.Equal(4, division.Id);
		Assert.Contains(_warnings, warning => warning.Contains("Nowhere"));
	}

	[Fact]
	public void AUnitInAProvinceThatDoesNotExistIsPutInProvinceOne()
	{
		(CCountry country, _, _) = Read(
			"division = { name = \"A\" location = 4000 regiment = { type = foot_brigade historical_model = 0 } }\n");

		Assert.Equal(1, country.Units[0].Location);
		Assert.Contains(_warnings, warning => warning.Contains("4000"));
	}

	[Fact]
	public void ALineMayCarryALevelOfATechnologyTheWayASavegameDoes()
	{
		(CCountry country, _, _) = Read(
			"division = { name = \"A\" location = 1 regiment = { type = foot_brigade historical_model = 0 rifles = { 3 0.250 } drill = { 1 0.000 } } }\n");

		CSubUnit brigade = country.Units[0].SubUnits[0];
		Assert.Equal(3, brigade.TechnologyLevels["rifles"]);
		Assert.Equal(CFixedPoint.Parse("0.25"), brigade.TechnologyProgress["rifles"]);
		Assert.Equal(1, brigade.TechnologyLevels["drill"]);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void AKeyNothingKnowsInAnOrderOfBattleIsReported()
	{
		Read("""
			division = { name = "A" location = 1 colour = red regiment = { type = foot_brigade historical_model = 0 mood = sour } }
			weather = fine
			""");

		string warning = Assert.Single(_warnings);
		Assert.Contains("colour in a division", warning);
		Assert.Contains("mood in a regiment", warning);
		Assert.Contains("weather", warning);
	}

	[Fact]
	public void WhatASavegameKeepsForAUnitInTheFieldIsReadWithoutComment()
	{
		Read("division = { name = \"A\" location = 1 dig_in = 3 supplies = 1.000 regiment = { type = foot_brigade historical_model = 0 home = 5 } }\n");

		Assert.Empty(_warnings);
	}

	[Fact]
	public void ACountrysHistoryNamesItsOrderOfBattleAndItsOfficersRatio()
	{
		Write("history/countries/AAA - Aland.txt", """
			capital = 1
			oob = "a_1936.txt"
			officers_ratio = 1.5
			1938.9.1 = {
				oob = "a_1938.txt"
				officers_ratio = 1.25
			}
			""");
		CCountries countries = CCountries.Load(Directories, CLocalisation.Load(Directories), _warnings.Add);

		CCountryHistory.Apply(Directories, countries, new CDate(1936, 1, 1), _ => { });

		CCountry country = countries.Find("AAA")!;
		Assert.Equal("a_1936.txt", country.OrderOfBattleFile);
		Assert.Equal(CFixedPoint.Parse("1.5"), country.OfficersRatio);
		Assert.Equal("", countries.Find("BBB")!.OrderOfBattleFile);
		Assert.Equal(CFixedPoint.Zero, countries.Find("BBB")!.OfficersRatio);
	}

	[Fact]
	public void ACountryWithNoOrderOfBattleHasNoUnits()
	{
		(_, CCountry second, int last) = Read(
			"division = { name = \"A\" location = 1 regiment = { type = foot_brigade historical_model = 0 } }\n");

		Assert.Empty(second.Units);
		Assert.Equal(2, last);
	}
}
