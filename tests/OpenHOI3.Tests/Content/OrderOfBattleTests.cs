using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.History;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Construction;
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
	private (CCountry First, CCountry Second, int LastId) Read(string first, string? second = null,
		IReadOnlyDictionary<int, OpenHOI3.Sim.Leaders.CLeader>? leaders = null)
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
		int last = COrderOfBattleReader.Read(Directories, countries.Declared, types, technologies, provinceCount: 100, _warnings.Add,
			leaders: leaders);
		return (a, b, last);
	}

	[Fact]
	public void ALeaderNoFileDeclaresIsPassedOverAndReportedLeavingWhatAnEarlierLineGave()
	{
		var leaders = new Dictionary<int, OpenHOI3.Sim.Leaders.CLeader> { [77] = new(77) };
		(CCountry country, _, _) = Read("""
			division = { name = "Known" location = 12 leader = 77 regiment = { type = foot_brigade historical_model = 1 } }
			division = { name = "Unknown" location = 12 leader = 99 regiment = { type = foot_brigade historical_model = 1 } }
			division = { name = "Both" location = 12 leader = 77 leader = 98 regiment = { type = foot_brigade historical_model = 1 } }
			""", leaders: leaders);

		Assert.Equal(new[] { 77, 0, 77 }, country.Units.Select(unit => unit.LeaderId));
		// Each at its own line (logs.md, section 4).
		Assert.Contains(_warnings, warning => warning.EndsWith("a.txt:2: AAA's leader 99 is declared by no file and was passed " +
			"over, as the original passes him over"));
		Assert.Contains(_warnings, warning => warning.Contains("a.txt:3: AAA's leader 98"));
	}

	[Fact]
	public void AnExtraAmphibiousDefenceIsKeptByTheKindOfBrigadeAndOneThatIsNoTypeIsReported()
	{
		Write("units/assault.txt", """
			assault_ship = {
				type = naval
				max_strength = 100
				amphibious_invasion_defence = 0.25
				extra_amphibious_defence = {
					foot_brigade = 0.25
					tank_brigade = 0.5
					staff_brigade = lots
				}
			}
			""");

		CSubUnitType assault = Types()["assault_ship"];

		Assert.Equal(CFixedPoint.Parse("0.25"), assault.ExtraAmphibiousDefence["foot_brigade"]);
		Assert.Equal(CFixedPoint.Parse("0.5"), assault.ExtraAmphibiousDefence["tank_brigade"]);
		Assert.False(assault.ExtraAmphibiousDefence.ContainsKey("staff_brigade"));
		Assert.Contains(_warnings, warning => warning.Contains("assault_ship: tank_brigade"));
		Assert.Contains(_warnings, warning => warning.Contains("assault_ship.extra_amphibious_defence.staff_brigade"));
		Assert.DoesNotContain(_warnings, warning => warning.Contains("not understood"));
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
	public void ABlockUnderAnotherCountrysTagIsWhatTheCountryHoldsOfIt()
	{
		Dictionary<string, CSubUnitType> types = Types();
		Dictionary<string, CTechnology> technologies = Technologies(types);
		CCountries countries = CCountries.Load(Directories, CLocalisation.Load(Directories), _warnings.Add);
		CCountry a = countries.Find("AAA")!;
		CCountry b = countries.Find("BBB")!;
		var state = new CGameState(new CDate(1941, 6, 22), new[] { new OpenHOI3.Sim.Provinces.CProvince(0, "", isSea: false) },
			new[] { a, b });
		CGameState.Status(a, b).Relation = CFixedPoint.FromInt(10);
		Write("history/units/a.txt", """
			BBB = { military_access = yes threat = 4 shininess = 1 }
			division = { name = "Known" location = 12 regiment = { type = foot_brigade historical_model = 1 } }
			""");
		a.OrderOfBattleFile = "a.txt";

		COrderOfBattleReader.Read(Directories, countries.Declared, types, technologies, provinceCount: 100, _warnings.Add,
			state: state);

		// As the original's country loader reads a save's pair block, key by key: Their Finest Hour's German files of 1941
		// and after open with SWE = { military_access = yes }. What the block does not name is left as it was.
		Assert.True(CGameState.Status(a, b).HasMilitaryAccess);
		Assert.False(CGameState.Status(b, a).HasMilitaryAccess);
		Assert.Equal(CFixedPoint.FromInt(4), CGameState.Status(a, b).Threat);
		Assert.Equal(CFixedPoint.FromInt(10), CGameState.Status(a, b).Relation);
		Assert.Single(a.Units);
		string warning = Assert.Single(_warnings, w => w.Contains("not understood"));
		Assert.Contains("BBB/shininess", warning);
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
	public void AUnitsBrigadesAreInTheOrderOfTheirPriorityAndNotTheFiles()
	{
		// BlackICE's division headquarters first in the file and of priority 1, its infantry 83: the original's start save
		// has the infantry first, and so does its counter.
		Write("units/ranked.txt", """
			hq_division = { type = land max_strength = 10 default_organisation = 30 officers = 100 priority = 1 }
			rifle_brigade = { type = land max_strength = 30 default_organisation = 30 officers = 100 priority = 83 }
			""");
		(CCountry country, _, _) = Read("""
			division = {
				name = "1. Division"
				location = 12
				regiment = { type = hq_division name = "Stab" historical_model = 0 }
				regiment = { type = foot_brigade name = "Unranked" historical_model = 0 }
				regiment = { type = rifle_brigade name = "IR 1" historical_model = 0 }
				regiment = { type = rifle_brigade name = "IR 2" historical_model = 0 }
			}
			""");

		Assert.Equal(new[] { "IR 1", "IR 2", "Stab", "Unranked" }, country.Units[0].SubUnits.Select(brigade => brigade.Name));
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
	public void AUnitTheFileSaysAnotherCountryLentIsItsLendersAndOnItsLendersList()
	{
		(CCountry first, CCountry second, _) = Read("""
			corps = {
				name = "Corps" location = 1
				regiment = { type = foot_brigade historical_model = 0 }
				division = { expeditionary_owner = BBB name = "Lent" location = 1 regiment = { type = foot_brigade historical_model = 0 } }
				division = { name = "Own" location = 1 regiment = { type = foot_brigade historical_model = 0 } }
			}
			""");

		CUnit corps = Assert.Single(first.Units);
		Assert.Null(corps.Lender);
		Assert.Equal(new CCountryTag("BBB"), corps.Children[0].Lender);
		Assert.Equal(new CCountryTag("BBB"), corps.Children[0].FiguresOwner);
		Assert.Equal(new CCountryTag("AAA"), corps.Children[0].Owner);
		Assert.Null(corps.Children[1].Lender);
		Assert.Same(corps.Children[0], Assert.Single(second.LentUnits));
		Assert.DoesNotContain(_warnings, warning => warning.Contains("expeditionary_owner"));
	}

	[Fact]
	public void AUnitUnderConstructionIsALineOfTheQueueAndTakesFiveNumbers()
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

		// The line, the unit it will make, two passed over, and its one brigade.
		CUnit division = Assert.Single(country.Units);
		Assert.Equal(6, division.SubUnits[0].Id);
		Assert.Equal(7, last);

		CConstruction construction = Assert.Single(country.Constructions);
		Assert.Equal(1, construction.Id);
		Assert.Equal(2, construction.UnitId);
		Assert.Equal(CFixedPoint.FromInt(60), construction.Duration);       // which a new game takes as the days to go
		Assert.Equal(CFixedPoint.FromInt(5), construction.Cost);
		Assert.Equal(1, construction.Size);
		CBrigadeOrder order = Assert.Single(construction.Brigades);
		Assert.Equal(5, order.Id);
		Assert.Equal("flat_top", order.Type.Key);
		Assert.Equal("Next", order.Name);
		Assert.Equal(1, order.HistoricalModel);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void AConstructionOfSeveralBrigadesTakesANumberForEach()
	{
		(CCountry country, _, int last) = Read("""
			military_construction = {
				country = AAA
				is_reserve = yes
				size = 3
				manpower = 0.56
				foot_brigade = { name = "One" historical_model = 0 }
				foot_brigade = { name = "Two" experience = 20 model = { 2 1 } }
				duration = 30
				nonsense = 4
			}
			""");

		CConstruction construction = Assert.Single(country.Constructions);
		Assert.Equal(new[] { 5, 6 }, construction.Brigades.ConvertAll(order => order.Id));
		Assert.Equal(6, last);
		Assert.True(construction.IsReserve);
		Assert.Equal(CFixedPoint.One, construction.ReservesFactor);         // a file's line is not given its law's share
		Assert.Equal(CFixedPoint.Parse("0.56"), construction.Manpower);
		Assert.Equal(1, construction.Size);                                 // a file's line builds one, whatever it says
		Assert.Equal(CFixedPoint.FromInt(20), construction.Brigades[1].Experience);
		Assert.Equal(new[] { 2, 1 }, construction.Brigades[1].Model);
		Assert.Contains(_warnings, warning => warning.Contains("'nonsense' in a military_construction"));
	}

	[Fact]
	public void ALineInAFileReadAsAGameRunsGoesOnTheQueuesEndAsTheFileHasItWithItsModelTurned()
	{
		Write("units/models/aaa - land.txt", "foot_brigade.1 = { drill = 2 }\n");
		Dictionary<string, CSubUnitType> types = Types();
		Dictionary<string, CTechnology> technologies = Technologies(types);
		CCountries countries = ReadModels(types, technologies);
		CCountry a = countries.Find("AAA")!;
		var earlier = new CConstruction(a.Tag) { Id = 3 };
		a.Constructions.Add(earlier);
		var provinces = new List<OpenHOI3.Sim.Provinces.CProvince>();
		for (int id = 0; id < 10; id++)
		{
			provinces.Add(new OpenHOI3.Sim.Provinces.CProvince(id, $"p{id}", isSea: false));
		}
		var state = new CGameState(new CDate(1943, 2, 1), provinces, countries.Declared, technologies: technologies.Values)
		{
			SubUnitTypes = types,
			LastUnitId = 40,
		};
		Write("history/units/late.txt", """
			military_construction = {
				country = AAA
				name = "Late"
				foot_brigade = { name = "Late Foot" historical_model = 1 }
				cost = 32.5
				duration = 157
				progress = 86.35
			}
			""");

		COrderOfBattleReader.ReadInto(Directories, state, a, "late.txt", _warnings.Add);

		Assert.Equal(2, a.Constructions.Count);
		Assert.Same(earlier, a.Constructions[0]);
		CConstruction line = a.Constructions[1];
		// Numbered after the game's last: the line, its unit, two passed over and its brigade.
		Assert.Equal(41, line.Id);
		Assert.Equal(42, line.UnitId);
		Assert.Equal(45, line.Brigades[0].Id);
		Assert.Equal(45, state.LastUnitId);
		// Its cost, duration and progress the file's: nothing is priced again in a game under way.
		Assert.Equal(CFixedPoint.Parse("32.5"), line.Cost);
		Assert.Equal(CFixedPoint.FromInt(157), line.Duration);
		Assert.Equal(CFixedPoint.Parse("86.35"), line.Progress);
		// The model names drill alone, so its level takes rifles' place, the first of the type's technologies.
		Assert.Equal(new[] { 2 }, line.Brigades[0].Model);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void AShipNobodyNamedInAFileReadAsAGameRunsTakesTheNextFreeNameOfItsCountrysList()
	{
		Dictionary<string, CSubUnitType> types = Types();
		Dictionary<string, CTechnology> technologies = Technologies(types);
		CCountries countries = ReadModels(types, technologies);
		CCountry a = countries.Find("AAA")!;
		a.UnitNames["flat_top"] = new List<string> { "Taken", "Hessen", "Next" };
		a.UnitNamesInUse.Add("Taken");
		var provinces = new List<OpenHOI3.Sim.Provinces.CProvince>();
		for (int id = 0; id < 10; id++)
		{
			provinces.Add(new OpenHOI3.Sim.Provinces.CProvince(id, $"p{id}", isSea: false));
		}
		var state = new CGameState(new CDate(1939, 2, 1), provinces, countries.Declared, technologies: technologies.Values)
		{
			SubUnitTypes = types,
			LastUnitId = 40,
		};
		Write("history/units/hessen.txt", """
			navy = {
				name = "Hessen"
				base = 1
				location = 1
				ship = { type = flat_top name = "Hessen" historical_model = 0 }
				ship = { type = flat_top historical_model = 0 }
			}
			""");

		COrderOfBattleReader.ReadInto(Directories, state, a, "hessen.txt", _warnings.Add);

		// "Taken" is in use and "Hessen" was registered as the first ship was read, so the second takes "Next".
		CUnit fleet = a.Units.Single();
		Assert.Equal(new[] { "Hessen", "Next" }, fleet.SubUnits.Select(ship => ship.Name));
		Assert.Contains("Next", a.UnitNamesInUse);
		Assert.Empty(_warnings);
	}

	[Fact]
	public void AUnitWaitingToBePlacedInAFileReadAsAGameRunsGoesOnTheDeployList()
	{
		Dictionary<string, CSubUnitType> types = Types();
		Dictionary<string, CTechnology> technologies = Technologies(types);
		CCountries countries = ReadModels(types, technologies);
		CCountry a = countries.Find("AAA")!;
		var provinces = new List<OpenHOI3.Sim.Provinces.CProvince>();
		for (int id = 0; id < 10; id++)
		{
			provinces.Add(new OpenHOI3.Sim.Provinces.CProvince(id, $"p{id}", isSea: false));
		}
		var state = new CGameState(new CDate(1939, 1, 1), provinces, countries.Declared, technologies: technologies.Values)
		{
			SubUnitTypes = types,
			LastUnitId = 40,
		};
		// BlackICE's AI_CAG_buildup files: a save's own form, its numbers the save's pairs.
		Write("history/units/cag.txt", """
			unit_deployment=
			{
				id=
				{
					id=2353
					type=4713
				}
				air=
				{
					id=
					{
						id=2352
						type=4713
					}
					name="2nd CAG"
					wing=
					{
						id=
						{
							id=2355
							type=4713
						}
						name="2nd CAG"
						type="deck_wing"
						organisation=73.987
						strength=100
						experience=20
					}
				}
			}
			""");

		COrderOfBattleReader.ReadInto(Directories, state, a, "cag.txt", _warnings.Add);

		CUnit waiting = Assert.Single(a.Deployments);
		Assert.Equal("2nd CAG", waiting.Name);
		Assert.Equal(EUnitKind.Air, waiting.Kind);
		Assert.Equal("deck_wing", Assert.Single(waiting.SubUnits).Type.Key);
		// The file's pairs take the numbers after the game's last, in the order the save's reader asks for them: the
		// group, its wing, and then the deployment's own.
		Assert.Equal(41, waiting.Id);
		Assert.Equal(42, waiting.SubUnits[0].Id);
		Assert.Equal(43, waiting.DeploymentId);
		Assert.Equal(43, state.LastUnitId);
		Assert.Empty(a.Units);
	}

	[Fact]
	public void ABuildingsLineAndAConvoysInAFileReadAsAGameRunsGoOnTheQueuesEndAsTheFileHasThem()
	{
		Dictionary<string, CSubUnitType> types = Types();
		Dictionary<string, CTechnology> technologies = Technologies(types);
		CCountries countries = ReadModels(types, technologies);
		CCountry a = countries.Find("AAA")!;
		var provinces = new List<OpenHOI3.Sim.Provinces.CProvince>();
		for (int id = 0; id < 10; id++)
		{
			provinces.Add(new OpenHOI3.Sim.Provinces.CProvince(id, $"p{id}", isSea: false));
		}
		var buildings = new[]
		{
			new OpenHOI3.Sim.Provinces.CBuilding(0, "industry", 10, "industry", CFixedPoint.One),
			new OpenHOI3.Sim.Provinces.CBuilding(1, "rocket_test", 10, "", CFixedPoint.Zero),
		};
		var state = new CGameState(new CDate(1936, 1, 1), provinces, countries.Declared, technologies: technologies.Values,
			buildings: buildings)
		{
			SubUnitTypes = types,
			LastUnitId = 40,
		};
		// BlackICE's Peenemünde file, the building by its place in buildings.txt counted from one, and a convoy's line.
		Write("history/units/late.txt", """
			building_construction = {
				id = { type = 4713 }
				size = 1
				cost = 23
				progress = 179
				duration = 180
				location = 7
				country = "AAA"
				building = 2
			}
			convoy_construction = { size = 3 cost = 1.5 duration = 40 progress = 4 status = 1.000 status = yes }
			nonsense = { }
			""");

		COrderOfBattleReader.ReadInto(Directories, state, a, "late.txt", _warnings.Add);

		Assert.Equal(2, a.Constructions.Count);
		CConstruction building = a.Constructions[0];
		Assert.Same(buildings[1], building.Building);
		Assert.Equal(7, building.Location);
		Assert.Equal(CFixedPoint.FromInt(23), building.Cost);
		Assert.Equal(CFixedPoint.FromInt(180), building.Duration);
		Assert.Equal(CFixedPoint.FromInt(179), building.Progress);
		Assert.Equal(41, building.Id);
		CConstruction convoy = a.Constructions[1];
		Assert.True(convoy.IsConvoy);
		Assert.True(convoy.IsEscorts);
		Assert.Equal(3, convoy.Size);
		Assert.Equal(42, convoy.Id);
		Assert.Equal(42, state.LastUnitId);
		// A block nothing reads is said, where the original drops it.
		Assert.Contains(_warnings, warning => warning.Contains("nonsense") && warning.Contains("late.txt"));
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
	public void ACarriersAirGroupsAreAboardTheShipUnderItsFleetAndNumberedBeforeIt()
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
		// The original's CShip reading hangs it under the fleet (transport.md, section 13.1), and it is met once.
		Assert.Same(country.Units[0], aboard.Parent);
		Assert.Same(aboard, Assert.Single(country.Units[0].Children));
		Assert.Single(country.Units);
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

		// One line each, at its file and line (logs.md, section 4).
		Assert.Equal(3, _warnings.Count);
		Assert.EndsWith("a.txt:1: 'colour' in a division is not understood", _warnings[0]);
		Assert.EndsWith("a.txt:1: 'mood' in a regiment is not understood", _warnings[1]);
		Assert.EndsWith("a.txt:2: 'weather' is a key that is not understood", _warnings[2]);
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
	public void AHistorysCapitalSetsBothCapitalsAndNoOriginalCapital()
	{
		// China's: Nanjing at the top, Chongqing from 1937.11.21. The original keeps the history's entries apart from the
		// country's own record of its changes, which is where its original capital is looked for.
		Write("history/countries/AAA - Aland.txt", """
			capital = 1
			1937.11.21 = { capital = 2 }
			""");
		CCountries countries = CCountries.Load(Directories, CLocalisation.Load(Directories), _warnings.Add);

		CCountryHistory.Apply(Directories, countries, new CDate(1939, 9, 1), _ => { });

		CCountry country = countries.Find("AAA")!;
		Assert.Equal(2, country.Capital);
		Assert.Equal(2, country.ActingCapital);
		Assert.Equal(0, country.OriginalCapital);
		Assert.Equal(2, country.OriginalCapitalOrDeclared);
	}

	[Fact]
	public void ACountryWithNoOrderOfBattleHasNoUnits()
	{
		(_, CCountry second, int last) = Read(
			"division = { name = \"A\" location = 1 regiment = { type = foot_brigade historical_model = 0 } }\n");

		Assert.Empty(second.Units);
		Assert.Equal(2, last);
	}

	[Fact]
	public void AUnitWrittenWithoutItsEqualsSignIsReadAsThoughItHadOne()
	{
		// Their Finest Hour's SWE_DayOfInfamy.txt opens its army so, and JAP_DayOfInfamy.txt two theatres: the original reads
		// the key, throws the token after it away for not being "=", and the unit's block is read whole all the same.
		(CCountry country, _, int last) = Read("""
			last_election = 1936.9.15
			army {
				name = "Field Army"
				location = 12
				regiment = { type = staff_brigade historical_model = 0 }
				corps = { name = "I Corps" location = 12 regiment = { type = staff_brigade historical_model = 0 } }
			}
			division = { name = "After" location = 13 regiment = { type = foot_brigade historical_model = 0 } }
			""");

		Assert.Equal(new[] { "Field Army", "After" }, country.Units.Select(unit => unit.Name));
		CUnit army = country.Units[0];
		Assert.Equal(EUnitKind.Army, army.Kind);
		Assert.Single(army.Children);
		Assert.Equal(4, army.Id);
		Assert.Equal(6, last);
		Assert.DoesNotContain(_warnings, warning => warning.Contains("not understood"));
	}
}
