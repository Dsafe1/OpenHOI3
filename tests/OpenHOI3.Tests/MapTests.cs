using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests;

/// <summary>Builds the bytes of an uncompressed bitmap, so no test needs one from an install.</summary>
internal static class TestBitmap
{
	/// <param name="storedRows">Rows in the order the file stores them, each pixel as red, green, blue.</param>
	public static byte[] Rgb24(int width, (byte R, byte G, byte B)[][] storedRows)
	{
		int stride = (width * 3 + 3) & ~3;
		int height = storedRows.Length;
		var bytes = new byte[54 + stride * height];
		using var writer = new BinaryWriter(new MemoryStream(bytes));
		writer.Write((byte)'B');
		writer.Write((byte)'M');
		writer.Write(bytes.Length);
		writer.Write(0);
		writer.Write(54);                                     // where the pixels start
		writer.Write(40);                                     // info header size
		writer.Write(width);
		writer.Write(height);
		writer.Write((ushort)1);
		writer.Write((ushort)24);
		writer.Write(0);                                      // no compression

		for (int row = 0; row < height; row++)
		{
			for (int x = 0; x < width; x++)
			{
				int at = 54 + row * stride + x * 3;
				bytes[at] = storedRows[row][x].B;
				bytes[at + 1] = storedRows[row][x].G;
				bytes[at + 2] = storedRows[row][x].R;
			}
		}
		return bytes;
	}

	/// <param name="storedRows">Rows in the order the file stores them, each pixel a palette index.</param>
	public static byte[] Indexed8(int width, byte[][] storedRows)
	{
		const int pixels = 54 + 256 * 4;                       // the palette sits between header and pixels
		int stride = (width + 3) & ~3;
		int height = storedRows.Length;
		var bytes = new byte[pixels + stride * height];
		using var writer = new BinaryWriter(new MemoryStream(bytes));
		writer.Write((byte)'B');
		writer.Write((byte)'M');
		writer.Write(bytes.Length);
		writer.Write(0);
		writer.Write(pixels);
		writer.Write(40);
		writer.Write(width);
		writer.Write(height);
		writer.Write((ushort)1);
		writer.Write((ushort)8);
		writer.Write(0);

		for (int row = 0; row < height; row++)
		{
			storedRows[row].CopyTo(bytes, pixels + row * stride);
		}
		return bytes;
	}
}

public class BitmapFileTests
{
	[Fact]
	public void ReadsRowsInStoredOrderAndSkipsPadding()
	{
		// Three pixels wide is nine bytes a row, padded to twelve.
		byte[] bytes = TestBitmap.Rgb24(3, new[]
		{
			new (byte, byte, byte)[] { (1, 2, 3), (4, 5, 6), (7, 8, 9) },
			new (byte, byte, byte)[] { (10, 20, 30), (40, 50, 60), (70, 80, 90) },
		});

		CBitmapFile bitmap = CBitmapFile.Parse(bytes);

		Assert.Equal(3, bitmap.Width);
		Assert.Equal(2, bitmap.Height);
		Assert.Equal(9, bitmap.StoredRow(0).Length);
		// Blue, green, red.
		Assert.Equal(new byte[] { 3, 2, 1, 6, 5, 4, 9, 8, 7 }, bitmap.StoredRow(0).ToArray());
		Assert.Equal(new byte[] { 30, 20, 10 }, bitmap.StoredRow(1)[..3].ToArray());
	}

	[Fact]
	public void RefusesWhatItCannotRead()
	{
		Assert.Throws<InvalidDataException>(() => CBitmapFile.Parse(new byte[10]));
		Assert.Throws<InvalidDataException>(() => CBitmapFile.Parse(Encoding.ASCII.GetBytes(new string('x', 80))));

		// A file cut short must not be read past its end.
		byte[] whole = TestBitmap.Rgb24(2, new[] { new (byte, byte, byte)[] { (1, 1, 1), (2, 2, 2) } });
		Assert.Throws<InvalidDataException>(() => CBitmapFile.Parse(whole[..^4]));
	}
}

public class HistoryFoldTests
{
	private static string Summary(string text, CDate date)
	{
		return string.Join(" ", CHistory.Fold(ClausewitzParser.Parse(text), date)
			.Select(entry => $"{entry.Key}={entry.Value}"));
	}

	[Fact]
	public void UndatedEntriesComeFirstThenDatedOnesInDateOrder()
	{
		// The 1935 block is written after the 1936 one and still applies before it.
		const string text = @"
			owner = GER
			1936.1.1 = { owner = FRA }
			1935.6.1 = { owner = ITA }
			controller = GER";

		Assert.Equal("owner=GER controller=GER owner=ITA owner=FRA", Summary(text, new CDate(1936, 1, 1)));
	}

	[Fact]
	public void ABlockOnTheDateCountsAndOneAfterItDoesNot()
	{
		const string text = "owner = GER  1936.1.1 = { owner = FRA }  1936.1.2 = { owner = ENG }";

		Assert.Equal("owner=GER owner=FRA", Summary(text, new CDate(1936, 1, 1)));
		Assert.Equal("owner=GER", Summary(text, new CDate(1935, 12, 31)));
		Assert.Equal("owner=GER owner=FRA owner=ENG", Summary(text, new CDate(1936, 1, 2)));
	}

	[Fact]
	public void BlocksSharingADateKeepFileOrder()
	{
		const string text = "1936.1.1 = { owner = FRA }  1936.1.1 = { owner = ENG }";

		Assert.Equal("owner=FRA owner=ENG", Summary(text, new CDate(1936, 1, 1)));
	}
}

/// <summary>
/// A map four pixels wide and three high, written out as an install would hold it.
/// North is at the top of this picture, and W is a colour no province has:
///
///     y=2   A A B E
///     y=1   A A B W
///     y=0   C C D D
///
/// A is province 1, B 2, C 3, D 4 (sea) and E 5. The terrain bitmap lies over the same
/// pixels, o for ocean, p for plains and f for forest:
///
///     y=2   p f f p
///     y=1   f f p o
///     y=0   p f o o
/// </summary>
public class MapLoadingTests : IDisposable
{
	private static readonly (byte, byte, byte) A = (10, 0, 0);
	private static readonly (byte, byte, byte) B = (20, 0, 0);
	private static readonly (byte, byte, byte) C = (30, 0, 0);
	private static readonly (byte, byte, byte) D = (40, 0, 0);
	private static readonly (byte, byte, byte) E = (50, 0, 0);
	private static readonly (byte, byte, byte) W = (255, 255, 255);

	// Palette indices, as terrain.txt below assigns them.
	private const byte Ocean = 254;
	private const byte Plains = 7;
	private const byte Forest = 12;

	private readonly string _root;
	private readonly List<string> _warnings = new();

	public MapLoadingTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-map-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);

		Write("map/default.map", @"
			max_provinces = 6
			sea_starts = { 4 }
			definitions = ""definition.csv""
			provinces = ""provinces.bmp""
			adjacencies = ""adjacencies.csv""");
		Write("map/definition.csv",
			"province;red;green;blue;x;x\n" +
			"1;10;0;0;Alpha;x\n2;20;0;0;Beta;x\n3;30;0;0;Gamma;x\n4;40;0;0;Delta;x\n5;50;0;0;Epsilon;x\n" +
			";99;99;99;x;x\n");
		Write("map/adjacencies.csv",
			"From;To;Type;Through;Comment\n3;2;sea;4;a strait\n-1;-1;;-1;-1\n");

		// The file stores the northernmost row first.
		WriteBytes("map/provinces.bmp", TestBitmap.Rgb24(4, new[]
		{
			new[] { A, A, B, E },
			new[] { A, A, B, W },
			new[] { C, C, D, D },
		}));

		// Declared in this order, which is what settles a tie.
		Write("map/terrain.txt", @"
			terrain = 256
			categories = {
				ocean = { is_water = yes color = { 0 0 255 } }
				plains = { movement_cost = 1.0 color = { 241 221 184 } }
				forest = { movement_cost = 1.5 color = { 91 123 45 } }
			}
			ocean1 = { type = ocean color = { 254 } has_texture = no }
			plains1 = { type = plains color = { 7 } priority = 3 }
			forest1 = { type = forest color = { 12 } priority = 4 }");
		WriteTerrain(new[]
		{
			new[] { Plains, Forest, Forest, Plains },
			new[] { Forest, Forest, Plains, Ocean },
			new[] { Plains, Forest, Ocean, Ocean },
		});
	}

	private void WriteTerrain(byte[][] storedRows)
	{
		WriteBytes("map/terrain.bmp", TestBitmap.Indexed8(4, storedRows));
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private void Write(string relative, string content)
	{
		WriteBytes(relative, Encoding.Latin1.GetBytes(content));
	}

	private void WriteBytes(string relative, byte[] content)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllBytes(path, content);
	}

	private CMap Load() => CMap.Load(CDirectorySettings.At(_root), _warnings.Add);

	[Fact]
	public void YCountsNorthAndTheFilesFirstRowIsTheNorthernmost()
	{
		CMap map = Load();

		Assert.Equal(4, map.Width);
		Assert.Equal(3, map.Height);
		Assert.Equal(1, map.ProvinceAt(0, 2));               // north-west corner is A
		Assert.Equal(5, map.ProvinceAt(3, 2));               // north-east corner is E
		Assert.Equal(3, map.ProvinceAt(0, 0));               // south-west corner is C
		Assert.Equal(4, map.ProvinceAt(3, 0));
		Assert.Equal(0, map.DisplayRow(2));                  // the north row is drawn first
		Assert.Equal(2, map.DisplayRow(0));
	}

	[Fact]
	public void XWrapsAndYDoesNot()
	{
		CMap map = Load();

		Assert.Equal(5, map.ProvinceAt(-1, 2));
		Assert.Equal(1, map.ProvinceAt(4, 2));
		Assert.Equal(1, map.ProvinceAt(8, 2));
		Assert.Equal(0, map.ProvinceAt(0, 3));
		Assert.Equal(0, map.ProvinceAt(0, -1));
	}

	[Fact]
	public void AColourNoProvinceHasBelongsToNothingAndIsReportedOnce()
	{
		CMap map = Load();

		Assert.Equal(0, map.ProvinceAt(3, 1));
		string warning = Assert.Single(_warnings);
		Assert.Contains("1 pixels in 1 colours", warning);
	}

	[Fact]
	public void IdZeroIsAnEntryAndRowsWithoutAnIdAreNotProvinces()
	{
		CMap map = Load();

		Assert.Equal(6, map.ProvinceCount);                  // ids 0 to 5
		Assert.True(map.Shapes[0].PixelCount == 1);          // the one unmatched pixel
	}

	[Fact]
	public void SeaIsWhatSeaStartsSays()
	{
		CMap map = Load();

		Assert.True(map.IsSea(4));
		Assert.False(map.IsSea(1));
		Assert.False(map.IsSea(0));
		Assert.False(map.IsSea(99));
	}

	[Fact]
	public void MeasuresEachProvincesBoundingBox()
	{
		CMap map = Load();

		Assert.Equal(new CProvinceShape(0, 1, 1, 2, 4), map.Shapes[1]);
		Assert.Equal(new CProvinceShape(2, 1, 2, 2, 2), map.Shapes[2]);
		Assert.Equal(new CProvinceShape(2, 0, 3, 0, 2), map.Shapes[4]);
	}

	[Fact]
	public void ProvincesTouchAcrossAnEdgeButNotACorner()
	{
		CMap map = Load();

		Assert.Contains(2, map.Neighbours[1]);               // A beside B
		Assert.Contains(3, map.Neighbours[1]);               // A above C
		Assert.Contains(4, map.Neighbours[2]);               // B above D
		Assert.DoesNotContain(4, map.Neighbours[1]);         // A and D meet only at a corner
	}

	[Fact]
	public void TheLastColumnIsBesideTheFirst()
	{
		CMap map = Load();

		// E is in the last column and A in the first; nothing else joins them.
		Assert.Contains(1, map.Neighbours[5]);
		Assert.Contains(5, map.Neighbours[1]);
	}

	[Fact]
	public void ExplicitAdjacenciesJoinWhatTheRasterDoesNot()
	{
		CMap map = Load();

		// C and B meet only at a corner; the strait is what connects them.
		Assert.Equal(new CExplicitAdjacency(3, 2, "sea", 4), Assert.Single(map.ExplicitAdjacencies));
		Assert.Contains(2, map.Neighbours[3]);
		Assert.Contains(3, map.Neighbours[2]);
	}

	[Fact]
	public void NeighboursAreSortedAndNeverIncludeNothingOrThemselves()
	{
		CMap map = Load();

		for (int id = 0; id < map.ProvinceCount; id++)
		{
			Assert.Equal(map.Neighbours[id].OrderBy(n => n), map.Neighbours[id]);
			Assert.DoesNotContain(0, map.Neighbours[id]);
			Assert.DoesNotContain(id, map.Neighbours[id]);
		}
		Assert.Empty(map.Neighbours[0]);
	}

	// --- terrain ---

	[Fact]
	public void ReadsTheTerrainCategoriesInFileOrder()
	{
		CMap map = Load();

		Assert.Equal(new[] { "ocean", "plains", "forest" }, map.Terrains.Select(terrain => terrain.Name));
		Assert.True(map.Terrains[0].IsWater);
		Assert.False(map.Terrains[1].IsWater);
		Assert.Equal(91 / 255f, map.Terrains[2].Color.R, 3);
	}

	[Fact]
	public void AProvinceIsTheTerrainCoveringMostOfIt()
	{
		CMap map = Load();

		Assert.Equal("forest", map.MajorityTerrain[1]?.Name);   // A: three forest, one plains
		Assert.Equal("ocean", map.MajorityTerrain[4]?.Name);    // D: all ocean
		Assert.Equal("plains", map.MajorityTerrain[5]?.Name);   // E: its one pixel
	}

	[Fact]
	public void ATieGoesToTheCategoryDeclaredFirst()
	{
		CMap map = Load();

		// B and C are each one plains and one forest; plains is declared before forest.
		Assert.Equal("plains", map.MajorityTerrain[2]?.Name);
		Assert.Equal("plains", map.MajorityTerrain[3]?.Name);
	}

	[Fact]
	public void PixelsPaintedWithAnIndexNoTerrainUsesAreNotCountedAndAreReported()
	{
		// Province A again, but its three forest pixels now use an index nothing declares.
		WriteTerrain(new[]
		{
			new byte[] { Plains, 99, Forest, Plains },
			new byte[] { 99, 99, Plains, Ocean },
			new byte[] { Plains, Forest, Ocean, Ocean },
		});
		CMap map = Load();

		Assert.Equal("plains", map.MajorityTerrain[1]?.Name);
		Assert.Contains(_warnings, warning => warning.Contains("3 pixels use a palette index"));
	}

	[Fact]
	public void ATerrainBitmapOfTheWrongSizeGivesNoProvinceATerrain()
	{
		WriteBytes("map/terrain.bmp", TestBitmap.Indexed8(2, new[] { new byte[] { Plains, Plains } }));
		CMap map = Load();

		Assert.All(map.MajorityTerrain, terrain => Assert.Null(terrain));
		Assert.Contains(_warnings, warning => warning.Contains("no province was given a terrain"));
	}

	[Fact]
	public void AProvinceFileOverridesTheTerrainTheBitmapGave()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", "owner = GER\nterrain = plains");
		Write("history/provinces/west/3 - Gamma.txt", "owner = GER\nterrain = swamp");
		CGameState state = LoadStart();

		Assert.Equal("plains", state.Province(1).Terrain?.Name);   // the bitmap said forest
		Assert.Equal("plains", state.Province(2).Terrain?.Name);   // no file, so the bitmap stands
		Assert.Equal("plains", state.Province(3).Terrain?.Name);   // an unknown name changes nothing
		Assert.Contains(_warnings, warning => warning.Contains("terrains that are not defined") && warning.Contains("swamp"));
	}

	// --- the start state ---

	private void WriteStartFiles()
	{
		Write("common/defines.lua",
			"NDefines = {\n\tstart_date = '1936.1.1',\n\tend_date = '1948.1.1',\n\teconomy = {\n\t\tIC_TO_MONEY = 0.05, -- per IC\n\t\tIC_TO_SUPPLIES = 7.0,\n\t},\n}\n");
		Write("common/countries.txt",
			"GER = \"countries/Germany.txt\"\nFRA = \"countries/France.txt\"\n" +
			"ITA = \"countries/Italy.txt\"\nENG = \"countries/Britain.txt\"\n");
		foreach (string name in new[] { "Germany", "France", "Italy", "Britain" })
		{
			Write($"common/countries/{name}.txt", "color = { 1 2 3 }\n");
		}
		Write("localisation/names.csv", "PROV1;Alphaville;x\nPROV2;Betaburg;x\n");
		Write("common/buildings.txt", @"
			air_base = { on_completion = construction_practical air_capacity = 1 capital = yes cost = 4 time = 90 max_level = 10 }
			industry = { completion_size = 0.67 ic = 1 capital = yes onmap = no cost = 5 time = 365 max_level = 10 }
			infra = { infrastructure = 0.1 onmap = no cost = 0.5 time = 180 max_level = 10 damage_factor = 0.25 }");

		Write("common/laws.txt", @"
			economic_law = {
				full_civilian_economy = { cost = 0.5 allow = { } global_ic = -0.5 global_money = 0.15 }
				war_economy = { cost = 0.5 allow = { war = yes } global_ic = 0.25 }
			}
			press_laws = {
				free_press = { cost = 0.5 allow = { } drift_speed = 0.1 }
			}");
		Write("common/minister_types.txt", @"
			administrative_genius = { global_ic = 0.1 }
			silent_workhorse = { global_money = 0.05 decay = 1 }");
		Write("common/static_modifiers.txt", @"
			hard_player = { global_ic = -0.1 icon = 3 }
			base_values = { peace_consumer_goods_demand = 0.25 global_manpower = 4 ic = 5 }");
		Write("technologies/industry.txt", @"
			steel_production = {
				metal_production = 0.05
				research_bonus_from = { mechanicalengineering_theory = 0.5 }
				on_completion = mechanicalengineering_theory
				difficulty = 2
				start_year = 1918
				first_offset = 1932
				additional_offset = 2
				max_level = 12
				folder = industry_folder
			}
			industral_production = { ic_modifier = 0.025 difficulty = 2 max_level = 12 folder = industry_folder }
			lighttank_brigade = { activate_unit = light_armor_brigade difficulty = 3 start_year = 1918 }");
		Write("common/government_positions.txt",
			"head_of_state = { changeable = no }\narmament_minister = { }\n");
		Write("common/countries/Germany.txt", @"
			color = { 1 2 3 }
			ministers = {
				1 = {
					name = ""The Leader""
					head_of_state = silent_workhorse
					armament_minister = administrative_genius
					start_date = 1936.1.1
				}
				2 = { name = ""The Other"" armament_minister = administrative_genius }
				3 = { name = ""The Clerk"" head_of_state = silent_workhorse }
			}");
	}

	private CGameState LoadStart()
	{
		CDirectorySettings directories = CDirectorySettings.At(_root);
		CLocalisation localisation = CLocalisation.Load(directories, warn: _warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, _warnings.Add);
		return CGameStateLoader.LoadStart(directories, Load(), localisation, countries, _warnings.Add);
	}

	[Fact]
	public void ReadsTheStartDateOutOfTheDefines()
	{
		WriteStartFiles();

		Assert.Equal(new CDate(1936, 1, 1), CGameStateLoader.ReadStartDate(CDirectorySettings.At(_root)));
	}

	[Fact]
	public void ProvincesAreNamedFromLocalisationAndKnowTheirNeighbours()
	{
		WriteStartFiles();
		CGameState state = LoadStart();

		Assert.Equal(6, state.Provinces.Count);
		Assert.Equal("Alphaville", state.Province(1).Name);
		Assert.Equal("PROV3", state.Province(3).Name);        // no string, so the key shows
		Assert.True(state.Province(4).IsSea);
		Assert.Contains(2, state.Province(1).Neighbours);
		Assert.Same(state.Provinces[0], state.Province(77));  // out of range is "no province"
	}

	[Fact]
	public void FoldsProvinceHistoryToTheStartDate()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", @"
			owner = GER
			controller = GER
			add_core = GER
			add_core = FRA
			1936.1.1 = { owner = FRA controller = FRA remove_core = GER }
			1935.6.1 = { owner = ITA }
			1936.1.2 = { owner = ENG }");
		CGameState state = LoadStart();

		CProvince alpha = state.Province(1);
		Assert.Equal("FRA", alpha.Owner?.Value);              // on the date counts; the day after does not
		Assert.Equal("FRA", alpha.Controller?.Value);
		Assert.Equal(new[] { "FRA" }, alpha.Cores.Select(core => core.Value));
		Assert.Null(state.Province(2).Owner);                 // no file, no owner
		Assert.Equal("countries/France.txt", state.Country(alpha.Owner)?.DefinitionFile);
	}

	// --- laws and governments ---

	private static CCountry Germany(CGameState state) => state.Country(new CCountryTag("GER"))!;

	[Fact]
	public void ACountrysHistoryPutsItsLawsAndItsGovernmentInPlace()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt", @"
			economic_law = full_civilian_economy
			press_laws = free_press
			head_of_state = 1
			armament_minister = 2
			1936.1.1 = { economic_law = war_economy }
			1939.9.1 = { economic_law = full_civilian_economy head_of_state = 3 }");
		CCountry germany = Germany(LoadStart());

		Assert.Equal(3, germany.Ministers.Count);
		Assert.Equal("war_economy", germany.Laws["economic_law"].Name);   // dated on the start date
		Assert.Equal("free_press", germany.Laws["press_laws"].Name);
		Assert.Equal("The Leader", germany.Government["head_of_state"].Name);
		Assert.Equal("The Other", germany.Government["armament_minister"].Name);
	}

	[Fact]
	public void ACountrysModifierIsItsLawsAndItsMinistersAddedUp()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt",
			"economic_law = war_economy\npress_laws = free_press\nhead_of_state = 1\narmament_minister = 2");
		CGameState state = LoadStart();
		_ = new CSimulation(state);                             // entering a game rebuilds it

		CModifier modifier = Germany(state).Modifier;
		Assert.Equal(CFixedPoint.Parse("0.35"), modifier["global_ic"]);       // the law's 0.25 and the minister's 0.1
		Assert.Equal(CFixedPoint.Parse("0.05"), modifier["global_money"]);
		Assert.Equal(CFixedPoint.Parse("0.1"), modifier["drift_speed"]);

		// A block's own keys are not modifiers.
		Assert.Equal(CFixedPoint.Zero, modifier["cost"]);
		Assert.Equal(CFixedPoint.Zero, modifier["decay"]);
		Assert.Equal(CFixedPoint.Zero, modifier["war"]);
	}

	[Fact]
	public void AMinisterIsWorthWhatHisTypeForTheSeatHeHoldsIsWorth()
	{
		WriteStartFiles();
		// The same man: a workhorse as head of state, a genius at armaments.
		Write("history/countries/GER - Germany.txt", "armament_minister = 1");
		CGameState state = LoadStart();
		_ = new CSimulation(state);

		Assert.Equal(CFixedPoint.Parse("0.1"), Germany(state).Modifier["global_ic"]);
		Assert.Equal(CFixedPoint.Zero, Germany(state).Modifier["global_money"]);
	}

	[Fact]
	public void ReportsLawsAndMinistersThatDoNotExistAndSeatsThatAddNothing()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt", @"
			economic_law = planned_economy
			head_of_state = 99
			armament_minister = 3");
		Write("history/countries/XYZ - Nowhere.txt", "economic_law = war_economy");
		Write("history/countries/notes.txt", "economic_law = war_economy");
		CCountry germany = Germany(LoadStart());

		Assert.Empty(germany.Laws);
		Assert.False(germany.Government.ContainsKey("head_of_state"));
		Assert.Contains(_warnings, warning => warning.Contains("economic_law = planned_economy"));
		Assert.Contains(_warnings, warning => warning.Contains("1 positions with ministers the country does not declare"));
		Assert.Contains(_warnings, warning => warning.Contains("1 ministers hold a position"));   // The Clerk at armaments
		Assert.Contains(_warnings, warning => warning.Contains("there is no country 'XYZ'"));
		Assert.Contains(_warnings, warning => warning.Contains("does not start with a country tag"));
	}

	[Fact]
	public void TheExpansionReplacesACountrysHistoryFile()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt", "economic_law = full_civilian_economy");
		Write("tfh/history/countries/GER - Germany.txt", "economic_law = war_economy");
		CCountry germany = Germany(LoadStart());

		Assert.Equal("war_economy", germany.Laws["economic_law"].Name);
	}

	// --- resources, technologies and the static modifiers ---

	[Fact]
	public void AProvinceFileGivesItsResourcesManpowerLeadershipAndVictoryPoints()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", @"
			owner = GER
			points = 30
			metal = 12.00
			energy = 25.00
			crude_oil = 0.5
			manpower = 10.00
			leadership = 3.00");
		CProvince alpha = LoadStart().Province(1);

		Assert.Equal(CFixedPoint.FromInt(12), alpha.MaxProducing[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(12), alpha.CurrentProducing[EGoods.Metal]);   // it starts at all it can
		Assert.Equal(CFixedPoint.FromInt(25), alpha.CurrentProducing[EGoods.Energy]);
		Assert.Equal(CFixedPoint.Parse("0.5"), alpha.CurrentProducing[EGoods.CrudeOil]);
		Assert.Equal(CFixedPoint.Zero, alpha.CurrentProducing[EGoods.RareMaterials]);
		Assert.Equal(CFixedPoint.FromInt(10), alpha.Manpower);
		Assert.Equal(CFixedPoint.FromInt(3), alpha.Leadership);
		Assert.Equal(30, alpha.Points);
	}

	[Fact]
	public void ATechnologyIsReadForWhatALevelIsWorthAndNothingElse()
	{
		WriteStartFiles();
		CGameState state = LoadStart();

		Assert.Equal(3, state.Technologies.Count);
		CTechnology steel = state.Technologies["steel_production"];
		Assert.Equal(12, steel.MaxLevel);
		Assert.Equal(CFixedPoint.Parse("0.05"), steel.EffectPerLevel["metal_production"]);

		// How hard it is and when it appears describe the technology, not its effect.
		Assert.Equal(new[] { "metal_production" }, steel.EffectPerLevel.Values.Keys);
		Assert.Empty(state.Technologies["lighttank_brigade"].EffectPerLevel.Values);
	}

	[Fact]
	public void ACountrysHistoryGivesItsCapitalAndItsTechnologyLevels()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt", @"
			capital = 1
			steel_production = 2
			industral_production = 4
			infantry_theory = 10
			1936.1.1 = { steel_production = 3 }
			1937.1.1 = { steel_production = 9 }");
		CGameState state = LoadStart();
		_ = new CSimulation(state);
		CCountry germany = Germany(state);

		Assert.Equal(1, germany.Capital);
		Assert.Equal(3, germany.TechnologyLevels["steel_production"]);
		Assert.Equal(4, germany.TechnologyLevels["industral_production"]);
		Assert.False(germany.TechnologyLevels.ContainsKey("infantry_theory"));   // not a technology

		Assert.Equal(CFixedPoint.Parse("0.15"), germany.Technology["metal_production"]);
		Assert.Equal(CFixedPoint.Parse("0.1"), germany.Technology["ic_modifier"]);
	}

	[Fact]
	public void StaticModifiersAreKnownByName()
	{
		WriteStartFiles();
		CGameState state = LoadStart();

		Assert.Equal(CFixedPoint.FromInt(5), state.StaticModifier("base_values", "ic"));
		Assert.Equal(CFixedPoint.Parse("-0.1"), state.StaticModifier("hard_player", "global_ic"));
		Assert.Equal(CFixedPoint.Zero, state.StaticModifier("hard_player", "icon"));      // its own key, not a modifier
		Assert.Equal(CFixedPoint.Zero, state.StaticModifier("no_such_modifier", "ic"));
	}

	// --- buildings ---

	[Fact]
	public void ABuildingsOneUnknownKeyIsItsModifier()
	{
		WriteStartFiles();
		CGameState state = LoadStart();

		Assert.Equal(new[] { "air_base", "industry", "infra" }, state.Buildings.Select(building => building.Key));
		Assert.Equal(new[] { 0, 1, 2 }, state.Buildings.Select(building => building.Index));

		CBuilding industry = state.Buildings[1];
		Assert.Equal("ic", industry.ModifierKey);                 // not cost, time or any other field
		Assert.Equal(CFixedPoint.One, industry.ModifierValue);
		Assert.Equal(10, industry.MaxLevel);
		Assert.Equal("infrastructure", state.Buildings[2].ModifierKey);
		Assert.Equal(CFixedPoint.Parse("0.1"), state.Buildings[2].ModifierValue);
	}

	[Fact]
	public void ABuildingHoldsOneModifierSoASecondReplacesTheFirstAndIsReported()
	{
		WriteStartFiles();
		Write("common/buildings.txt", "factory = { ic = 1 max_level = 5 local_ic = 0.5 }\nfactory = { ic = 9 }");
		CGameState state = LoadStart();

		CBuilding factory = Assert.Single(state.Buildings);
		Assert.Equal("local_ic", factory.ModifierKey);
		Assert.Contains(_warnings, warning => warning.Contains("has two modifiers"));
		Assert.Contains(_warnings, warning => warning.Contains("declared twice"));
	}

	[Fact]
	public void AProvinceFileSetsBuildingLevelsAndALaterLineReplacesAnEarlierOne()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", @"
			owner = GER
			industry = 3
			infra = 4
			infra = 6
			1936.1.1 = { industry = 5 }
			1937.1.1 = { industry = 9 }");
		CGameState state = LoadStart();

		CProvince alpha = state.Province(1);
		Assert.Equal(CFixedPoint.FromInt(5), alpha.BuildingLevel(state.Buildings[1]));   // set, not added
		Assert.Equal(CFixedPoint.FromInt(6), alpha.BuildingLevel(state.Buildings[2]));
		Assert.Equal(CFixedPoint.Zero, alpha.BuildingLevel(state.Buildings[0]));
		Assert.Equal(CFixedPoint.Zero, state.Province(2).BuildingLevel(state.Buildings[1]));

		// Five levels of industry at one IC each, and six of infrastructure at a tenth.
		Assert.Equal(CFixedPoint.FromInt(5), alpha.ModifierValue(state.Buildings, "ic"));
		Assert.Equal(CFixedPoint.Parse("0.6"), alpha.ModifierValue(state.Buildings, "infrastructure"));
	}

	[Fact]
	public void ABuildingLevelThatIsNotANumberIsIgnoredAndCounted()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", "owner = GER\nindustry = lots");
		CGameState state = LoadStart();

		Assert.Equal(CFixedPoint.Zero, state.Province(1).BuildingLevel(state.Buildings[1]));
		Assert.Contains(_warnings, warning => warning.Contains("1 building levels and amounts"));
	}

	[Fact]
	public void TheExpansionReplacesAProvinceFileAtTheSamePath()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", "owner = GER");
		Write("tfh/history/provinces/west/1 - Alpha.txt", "owner = FRA");
		CGameState state = LoadStart();

		Assert.Equal("FRA", state.Province(1).Owner?.Value);
		Assert.DoesNotContain(_warnings, warning => warning.Contains("more than one history file"));
	}

	[Fact]
	public void TwoFilesForOneProvinceApplyInPathOrderAndAreReported()
	{
		WriteStartFiles();
		Write("history/provinces/east/2 - Later.txt", "owner = GER");
		Write("history/provinces/asia/2 - Earlier.txt", "owner = FRA\ncontroller = FRA");
		CGameState state = LoadStart();

		Assert.Equal("GER", state.Province(2).Owner?.Value);  // east/ sorts after asia/
		Assert.Equal("FRA", state.Province(2).Controller?.Value);
		Assert.Contains(_warnings, warning => warning.Contains("1 provinces have more than one history file"));
	}

	[Fact]
	public void ReportsCountriesNobodyDefinedAndFilesThatNameNoProvince()
	{
		WriteStartFiles();
		Write("history/provinces/west/3 - Gamma.txt", "owner = XXX");
		Write("history/provinces/west/notes.txt", "owner = GER");
		Write("history/provinces/west/900 - Nowhere.txt", "owner = GER");
		LoadStart();

		Assert.Contains(_warnings, warning => warning.Contains("not defined") && warning.Contains("XXX"));
		Assert.Equal(2, _warnings.Count(warning => warning.Contains("does not start with a province id")));
	}

	// --- the defines ---

	[Fact]
	public void ReadsTheDefinesBySectionPastCommentsCommasAndQuotes()
	{
		Write("common/defines.lua", @"
			defines = {
			start_date = '1936.1.1',   -- when it begins
			base_militia = ""militia_brigade"",
			economy = {
				IC_TO_MONEY 			= 0.05,
				IC_TO_SUPPLIES = 7.0,	-- supplies per IC, = 7
				LAST_ONE = 96  -- no comma after the last
			},
			weather = {
				COLD = -0.75,
				IC_TO_MONEY = 3,
				deeper = { HIDDEN = 1 },
			},
			}");
		// IC_TO_SUPPLIES is there, so nothing this project reads is missing.
		CDefines defines = CDefinesReader.Read(CDirectorySettings.At(_root), _warnings.Add);

		Assert.Equal(CFixedPoint.Parse("0.05"), defines["economy", "IC_TO_MONEY"]);
		Assert.Equal(CFixedPoint.FromInt(7), defines["economy", "IC_TO_SUPPLIES"]);
		Assert.Equal(CFixedPoint.FromInt(96), defines["economy", "LAST_ONE"]);
		Assert.Equal(CFixedPoint.Parse("-0.75"), defines["weather", "COLD"]);
		Assert.Equal(CFixedPoint.FromInt(3), defines["weather", "IC_TO_MONEY"]);   // a name belongs to its section
		Assert.False(defines.Has("weather", "HIDDEN"));
		Assert.False(defines.Has("deeper", "HIDDEN"));
		Assert.Equal(CFixedPoint.Zero, defines["economy", "NOT_THERE"]);
		Assert.Equal(5, defines.Count);                        // the two outside any section are not numbers to ask for
		Assert.Empty(_warnings);
	}

	[Fact]
	public void ReportsADefineThisProjectReadsAndTheFileDoesNotGive()
	{
		Write("common/defines.lua", "defines = {\n\tstart_date = '1936.1.1',\n\teconomy = { IC_TO_SUPPLIES = 7 },\n}\n");
		CDefinesReader.Read(CDirectorySettings.At(_root), _warnings.Add);

		string warning = Assert.Single(_warnings);
		Assert.Contains("economy.IC_TO_MONEY", warning);
		Assert.Contains("zero", warning);
		Assert.DoesNotContain("IC_TO_SUPPLIES", warning);
	}

	// --- wars ---

	private List<CWar> Wars(CDate date)
	{
		CDirectorySettings directories = CDirectorySettings.At(_root);
		CLocalisation localisation = CLocalisation.Load(directories, warn: _warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, _warnings.Add);
		return CWarReader.Read(directories, countries, date, _warnings.Add);
	}

	[Fact]
	public void AWarIsWhoHasJoinedAndNotLeftByTheDate()
	{
		WriteStartFiles();
		Write("history/wars/First.txt", @"
			name = ""The First War""
			limited_war = yes
			1935.10.3 = {
				add_attacker = ITA
				add_defender = ENG
				add_defender = FRA
				war_goal = { casus_belli = puppet actor = ""ITA"" receiver = ""ENG"" }
			}
			1935.12.1 = { rem_defender = FRA }
			1936.5.9 = { rem_attacker = ITA rem_defender = ENG }");
		Write("history/wars/Later.txt", "name = \"Later\"\n1937.7.7 = { add_attacker = GER add_defender = FRA }");
		Write("history/wars/Over.txt",
			"name = \"Over\"\n1930.1.1 = { add_attacker = GER add_defender = FRA }\n1931.1.1 = { rem_attacker = GER rem_defender = FRA }");

		CWar war = Assert.Single(Wars(new CDate(1936, 1, 1)));
		Assert.Equal("The First War", war.Name);
		Assert.Equal(new[] { "ITA" }, war.Attackers.Select(tag => tag.Value));
		Assert.Equal(new[] { "ENG" }, war.Defenders.Select(tag => tag.Value));
		Assert.Empty(_warnings);

		Assert.Equal(new[] { "Later" }, Wars(new CDate(1937, 7, 7)).Select(later => later.Name));
	}

	[Fact]
	public void AWarFileIsReplacedByTheExpansionsAndWhatItDoesNotUnderstandIsReported()
	{
		WriteStartFiles();
		Write("history/wars/First.txt", "name = \"Base\"\n1935.1.1 = { add_attacker = ITA add_defender = ENG }");
		Write("tfh/history/wars/First.txt", @"
			name = ""Expansion""
			1935.1.1 = { add_attacker = GER add_defender = FRA add_defender = XYZ add_neutral = ENG }");

		CWar war = Assert.Single(Wars(new CDate(1936, 1, 1)));
		Assert.Equal("Expansion", war.Name);
		Assert.Equal(new[] { "FRA" }, war.Defenders.Select(tag => tag.Value));
		Assert.Contains(_warnings, warning => warning.Contains("not understood") && warning.Contains("add_neutral"));
		Assert.Contains(_warnings, warning => warning.Contains("not defined") && warning.Contains("XYZ"));
	}

	// --- what a country holds at the start ---

	[Fact]
	public void ANewGameFillsEveryStockpile()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", "owner = GER\ncontroller = GER\nindustry = 5");
		Write("history/provinces/west/3 - Gamma.txt", "owner = ITA\ncontroller = ITA");
		Write("history/countries/GER - Germany.txt", @"
			capital = 1
			rare_materials = 100
			rare_materials = 50
			1937.1.1 = { rare_materials = 999 }");
		Write("history/countries/ITA - Italy.txt", "capital = 3");
		Write("history/wars/First.txt", "name = \"A War\"\n1935.10.3 = { add_attacker = ITA add_defender = ENG }");
		CGameState state = LoadStart();

		// Germany: ten IC at peace, and what its history gives by the start date on top.
		CGoodsPool berlin = state.Stockpile(Germany(state))!;
		Assert.Same(state.Province(1).Pool, berlin);
		Assert.Equal(CFixedPoint.FromInt(900), berlin[EGoods.Metal]);
		Assert.Equal(CFixedPoint.FromInt(1800), berlin[EGoods.Energy]);
		Assert.Equal(CFixedPoint.FromInt(600), berlin[EGoods.RareMaterials]);
		Assert.Equal(CFixedPoint.Parse("2.25"), berlin[EGoods.Money]);

		// Italy: the five IC every country has, and seventy-five days of it for being at war.
		Assert.True(state.IsAtWar(new CCountryTag("ITA")));
		Assert.Equal(CFixedPoint.FromInt(375), state.Province(3).Pool[EGoods.Metal]);

		// Britain is at war too, but has no capital to keep anything in.
		Assert.Null(state.Stockpile(state.Country(new CCountryTag("ENG"))!));
		Assert.DoesNotContain(_warnings, warning => warning.Contains("history") || warning.Contains("defines"));
	}

	[Fact]
	public void TheSimulationWorksThroughCountriesInTheOrderTheyAreDeclared()
	{
		WriteStartFiles();
		CGameState state = LoadStart();

		Assert.Equal(new[] { "GER", "FRA", "ITA", "ENG" }, state.Countries.Select(country => country.Tag.Value));
	}

	[Fact]
	public void MoneyInADayUsesTheRateTheDefinesGive()
	{
		WriteStartFiles();
		Write("history/provinces/west/1 - Alpha.txt", "owner = GER\ncontroller = GER\nindustry = 5");
		Write("history/countries/GER - Germany.txt", "capital = 1\neconomic_law = full_civilian_economy");
		CGameState state = LoadStart();
		_ = new CSimulation(state);

		// Ten IC at 0.05, and the law's +15%.
		Assert.Equal(CFixedPoint.Parse("0.575"), Germany(state).HomeProduced[EGoods.Money]);
	}

	[Fact]
	public void AnAmountOfGoodsThatIsNotANumberIsReported()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt", "capital = 1\nmoney = plenty");
		LoadStart();

		Assert.Contains(_warnings, warning => warning.Contains("1 amounts of goods in country history"));
	}
}

public class RecursiveLayeringTests : IDisposable
{
	private readonly string _root;

	public RecursiveLayeringTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "openhoi3-layers-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_root);
	}

	public void Dispose()
	{
		Directory.Delete(_root, recursive: true);
	}

	private void Write(string relative)
	{
		string path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, relative);
	}

	[Fact]
	public void IdentifiesAFileByItsPathBelowTheDirectory()
	{
		Write("history/provinces/asia/7 - Same.txt");
		Write("history/provinces/soviet/7 - Same.txt");       // same name, different folder: both stay
		Write("history/provinces/asia/9 - Base.txt");
		Write("tfh/history/provinces/asia/9 - Base.txt");     // replaces the base copy
		Write("tfh/history/provinces/asia/3 - New.txt");

		CDirectorySettings.LayeredFile[] files =
			CDirectorySettings.At(_root).ListFilesRecursive("history/provinces", "*.txt");

		Assert.Equal(
			new[] { "asia/3 - New.txt", "asia/7 - Same.txt", "asia/9 - Base.txt", "soviet/7 - Same.txt" },
			files.Select(file => file.RelativePath));
		Assert.Contains("tfh", files[2].AbsolutePath);
		Assert.DoesNotContain("tfh", files[1].AbsolutePath);
	}

	[Fact]
	public void AMissingDirectoryIsSimplyEmpty()
	{
		Assert.Empty(CDirectorySettings.At(_root).ListFilesRecursive("history/provinces", "*.txt"));
	}
}
