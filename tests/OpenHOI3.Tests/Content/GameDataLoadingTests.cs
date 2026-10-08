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

}
