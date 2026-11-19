using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Sim.Countries;

namespace OpenHOI3.Tests.Content;

/// <summary>Loaders exercised against a fixture install.</summary>
public class GameDataLoadingTests : IDisposable
{
	private readonly string _root;

	public GameDataLoadingTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-data-" + Guid.NewGuid().ToString("N"));
		Write("localisation/countries.csv",
			"GER;Germany;;;;;;;;;;;;;x\nENG;United Kingdom;;;;;;;;;;;;;x\n");
		Write("common/countries.txt", """
			# Please keep tags sorted
			GER	= "countries/Germany.txt"
			ENG	= "countries/United Kingdom.txt"
			""");
		Write("common/countries/Germany.txt", "color = { 64 160 167 }\ngraphical_culture = Western\n");
		Write("common/countries/United Kingdom.txt", "color = { 200 10 10 }\ngraphical_culture = Western\n");
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

	private CLocalisation Localisation => CLocalisation.Load(Directories);

	/// <summary>
	/// Diagnostics are captured rather than left to the default sink, so a test can
	/// assert on what a loader reported.
	/// </summary>
	private readonly List<string> _warnings = new();

	[Fact]
	public void LoadsCountriesWithNamesAndColours()
	{
		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);

		Assert.Equal(2, countries.Count);
		CCountry germany = countries.Find("GER")!;
		Assert.Equal("Germany", germany.Name);
		Assert.Equal("Western", germany.GraphicalCulture);
		Assert.Equal(64 / 255f, germany.Color.R, 3);
		Assert.Equal("gfx/flags/GER.tga", germany.FlagFile);
	}

	[Fact]
	public void CountryLookupIsCaseInsensitiveAndSafe()
	{
		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);
		Assert.NotNull(countries.Find("ger"));
		Assert.Null(countries.Find("ZZZ"));
		Assert.Null(countries.Find(""));
	}

	[Fact]
	public void CountriesComeBackSortedByName()
	{
		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);
		Assert.Equal(new[] { "Germany", "United Kingdom" },
			countries.All.Select(c => c.Name).ToArray());
	}

	[Fact]
	public void AnUnlocalisedCountryKeepsItsTagAsAName()
	{
		Write("common/countries.txt", "ZZZ = \"countries/Nowhere.txt\"\n");
		Write("common/countries/Nowhere.txt", "color = { 1 2 3 }\n");

		CCountries countries = CCountries.Load(Directories, Localisation, _warnings.Add);
		Assert.Equal("ZZZ", countries.Find("ZZZ")!.Name);
	}

	[Fact]
	public void ACountrysFileGivesItsDivisionTemplatesAndTheNamesOfItsNewUnits()
	{
		Write("common/countries/Germany.txt", """
			color = { 64 160 167 }
			default_templates = {
				GER_panzer_1936 = {
					light_armor_brigade
					light_armor_brigade
					motorized_brigade
				}
				GER_motorised = { motorized_brigade motorized_brigade }
			}
			unit_names = {
				infantry_brigade = {
					"1. Infanterie-Division" "5. Infanterie-Division"
					"6. Infanterie-Division"
				}
				destroyer = { "1. Zerstörergeschwader" }
			}
			""");

		CCountry germany = CCountries.Load(Directories, Localisation, _warnings.Add).Find("GER")!;

		Assert.Equal(new[] { "GER_panzer_1936", "GER_motorised" }, germany.Templates.Select(template => template.Name));
		Assert.Equal(new[] { "light_armor_brigade", "light_armor_brigade", "motorized_brigade" }, germany.Templates[0].Brigades);
		Assert.Equal(new[] { "1. Infanterie-Division", "5. Infanterie-Division", "6. Infanterie-Division" },
			germany.UnitNames["infantry_brigade"]);
		Assert.Equal(new[] { "1. Zerstörergeschwader" }, germany.UnitNames["destroyer"]);
		Assert.Empty(CCountries.Load(Directories, Localisation, _warnings.Add).Find("ENG")!.Templates);
	}

	[Fact]
	public void CombinedArmsGroupsAreReadInTheirFilesOrderWithTheBaseOnesMarked()
	{
		Write("common/combined_arms.txt", """
			# combined arms spec
			combined_arms = {
				infantry_unit_type = {
					base = yes
					value = 0
				}
				armor_unit_type = {
					value = 0.05
					colour = red
				}
			}
			""");

		List<OpenHOI3.Sim.Units.CCombinedArmsGroup> groups = CCombinedArmsReader.Read(Directories, _warnings.Add);

		Assert.Equal(new[] { "infantry_unit_type", "armor_unit_type" }, groups.Select(group => group.Name));
		Assert.True(groups[0].IsBase);
		Assert.False(groups[1].IsBase);
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.Parse("0.05"), groups[1].Value);
		Assert.Contains(_warnings, warning => warning.Contains("colour"));
	}

	[Fact]
	public void ATechnologyNamesTheUnitTypesItGivesTheUseOf()
	{
		Write("technologies/land.txt", """
			combined_arms_warfare = {
				armor_unit_type = 0.1
				activate_unit = sp_rct_artillery_brigade
				activate_unit = engineer_brigade
				max_level = 1
			}
			superior_firepower = {
				division_size = 1
			}
			""");

		List<OpenHOI3.Sim.Research.CTechnology> technologies = CTechnologyReader.Read(Directories, _warnings.Add);

		OpenHOI3.Sim.Research.CTechnology warfare = technologies.Single(technology => technology.Name == "combined_arms_warfare");
		Assert.Equal(new[] { "sp_rct_artillery_brigade", "engineer_brigade" }, warfare.ActivatesUnits);
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.Parse("0.1"), warfare.EffectPerLevel["armor_unit_type"]);
		OpenHOI3.Sim.Research.CTechnology firepower = technologies.Single(technology => technology.Name == "superior_firepower");
		Assert.Empty(firepower.ActivatesUnits);
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.One, firepower.EffectPerLevel["division_size"]);
	}

	[Fact]
	public void A_technology_declared_twice_is_found_by_name_as_the_later_and_the_earlier_stays_its_unit_types()
	{
		// BlackICE's cag_design_hidden, in 00_unique_unit_techs.txt and again in unique_ships_techs.txt.
		Write("technologies/00_first.txt", """
			cag_design_hidden = { start_year = 1918 cag_wing = { air_attack = 0.3 } }
			first_after = { start_year = 1930 }
			""");
		Write("technologies/unique_second.txt", "cag_design_hidden = { start_year = 1960 carrier = { sea_attack = 0.7 } }\n");
		var wing = new OpenHOI3.Sim.Units.CSubUnitType("cag_wing", OpenHOI3.Sim.Units.EUnitBranch.Air);
		var carrier = new OpenHOI3.Sim.Units.CSubUnitType("carrier", OpenHOI3.Sim.Units.EUnitBranch.Naval);
		var types = new Dictionary<string, OpenHOI3.Sim.Units.CSubUnitType> { ["cag_wing"] = wing, ["carrier"] = carrier };

		List<OpenHOI3.Sim.Research.CTechnology> technologies = CTechnologyReader.Read(Directories, _warnings.Add, unitTypes: types);

		Assert.Equal(new[] { "first_after", "cag_design_hidden" }, technologies.Select(technology => technology.Name));
		Assert.Equal(1960, technologies[1].StartYear);
		// Each declaration is in its own unit types' lists, the earlier only there.
		Assert.Equal(1918, wing.Technologies.Single().StartYear);
		Assert.Same(technologies[1], carrier.Technologies.Single());
		Assert.Contains(_warnings, warning => warning.Contains("the last is the one a name finds"));
	}

	[Fact]
	public void ATechnologysBlockNamedAfterAMissionIsThatMissionsFigures()
	{
		Write("technologies/naval.txt", """
			trade_interdiction = {
				convoy_raid = {
					efficiency = 0.06
				}
				max_level = 12
			}
			unrestricted_warfare = {
				convoy_raid = {
					reduction_modifier = -0.50
				}
				sortie = {
					efficency = 0.1
				}
			}
			""");

		List<OpenHOI3.Sim.Research.CTechnology> technologies = CTechnologyReader.Read(Directories, _warnings.Add);

		OpenHOI3.Sim.Research.CTechnology interdiction = technologies.Single(technology => technology.Name == "trade_interdiction");
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.Parse("0.06"), interdiction.MissionEfficiency["convoy_raid"]);
		Assert.Empty(interdiction.MissionReduction);
		OpenHOI3.Sim.Research.CTechnology warfare = technologies.Single(technology => technology.Name == "unrestricted_warfare");
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.Parse("-0.5"), warfare.MissionReduction["convoy_raid"]);
		Assert.Empty(warfare.MissionEfficiency);
		// A key the block does not have is said, where the original would drop it.
		Assert.Contains(_warnings, warning => warning.Contains("sortie { efficency }"));
	}

	[Fact]
	public void OccupationPoliciesComeAfterNoOccupationInTheFilesOrderAndAllowIsNotAFigure()
	{
		Write("common/occupation_policies.txt", """
			collaboration_government = {
				local_partisan_support = 1.5
				local_ic = -0.95
			}
			civil_war = {
				allow = { tag = SPA }
			}
			""");

		List<OpenHOI3.Sim.Occupation.COccupationPolicy> policies = CModifierReader.ReadOccupationPolicies(Directories, _warnings.Add);

		Assert.Equal(new[] { "no_occupation", "collaboration_government", "civil_war" }, policies.Select(policy => policy.Name));
		Assert.Equal(new[] { 0, 1, 2 }, policies.Select(policy => policy.Index));
		Assert.False(policies[0].IsReal);
		Assert.Empty(policies[0].Modifier.Values);
		Assert.Equal(OpenHOI3.Sim.CFixedPoint.Parse("1.5"), policies[1].Modifier["local_partisan_support"]);
		Assert.Empty(policies[2].Modifier.Values);
		Assert.Empty(_warnings);
	}

}
