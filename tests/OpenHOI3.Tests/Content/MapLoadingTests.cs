using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenHOI3.Content;
using OpenHOI3.Content.Definitions;
using OpenHOI3.Content.History;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Modifiers;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Research;
using OpenHOI3.Sim.Wars;

namespace OpenHOI3.Tests.Content;

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
			adjacencies = ""adjacencies.csv""
			continent = ""continent.txt""
			region = ""region.txt""
			climate = ""climate.bmp""
			high_pressure_zones = { 1 }
			low_pressure_zones = { 4 = 31 5 = 16 }");

		// Alpha, Beta and Gamma on one continent and Epsilon on another.
		Write("map/continent.txt", "west = {\n\t1 2 3\n}\neast = { 5 }\n");
		// Beta in two regions, the coast's first.
		Write("map/region.txt", "coast_region = { 2 4 }\nland_region = { peace 1 2 3 }\n");
		Write("map/definition.csv",
			"province;red;green;blue;x;x\n" +
			"1;10;0;0;Alpha;x\n2;20;0;0;Beta;x\n3;30;0;0;Gamma;x\n4;40;0;0;Delta;x\n5;50;0;0;Epsilon;x\n" +
			";99;99;99;x;x\n");
		Write("map/adjacencies.csv",
			"From;To;Type;Through;Comment\n3;2;sea;4;a strait\n-1;-1;;-1;-1\n");

		// Alpha is placed by hand and its text's place is not a place for it; Gamma is
		// placed twice; the sea zone has only a place for its text.
		Write("map/positions.txt",
			"1 = {\n\tunit = { x=1.900000 y=2.700000 }\n\ttext_position = { x=0.000000 y=1.000000 }\n}\n" +
			"# a comment, and a block with points that are not the province's\n" +
			"3 = { unit = { x=0.4 y=0.2 } building_position = { naval_base = { x=3.0 y=2.0 } } }\n" +
			"4 = { text_position = { x=3.6 y=0.4 } text_rotation = 0.5 }\n" +
			"3 = { unit = { x=1.7 y=0.9 } }\n");

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

		// One pixel of river, in the middle of A's east column.
		WriteBytes("map/rivers.bmp", TestBitmap.Indexed8(4, new[]
		{
			new byte[] { NoRiver, NoRiver, NoRiver, NoRiver },
			new byte[] { NoRiver, River, NoRiver, NoRiver },
			new byte[] { NoRiver, NoRiver, NoRiver, NoRiver },
		}));

		// A palette index for every pixel, northernmost row first, so that which pixel a province samples shows.
		WriteBytes("map/climate.bmp", TestBitmap.Indexed8(4, new[]
		{
			new byte[] { 100, 110, 120, 130 },
			new byte[] { 101, 111, 121, 131 },
			new byte[] { 102, 112, 122, 132 },
		}));
	}

	[Fact]
	public void AProvincesClimateIsThePaletteIndexUnderItsUnitPointAndTheZonesAreReadInOrder()
	{
		CMap map = Load();

		// Alpha placed at (1, 2); Beta at its box's centre (2, 2); Gamma by its last place, (1, 0); the sea zone by its
		// text's place, (3, 0); Epsilon at (3, 2).
		// Province 0 has no point of its own and reads the first pixel.
		Assert.Equal(new byte[] { 102, 110, 120, 112, 132, 130 }, map.ClimateIndex.ToArray());
		Assert.Equal(new[] { (4, 31), (5, 16) }, map.LowPressureZones);
		Assert.Equal(new[] { 1 }, map.HighPressureZones);
	}

	private const byte NoRiver = 255;
	private const byte River = 3;

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
	public void AProvinceIsAtItsCentreUnlessThePositionsFilePlacesIt()
	{
		CMap map = Load();

		Assert.Equal(new CMapPosition(1, 2), map.Positions[1]);   // unit, cut to whole pixels; text_position is not for land
		Assert.Equal(new CMapPosition(2, 2), map.Positions[2]);   // nothing said: the centre of its bounding box, rounded up
		Assert.Equal(new CMapPosition(1, 0), map.Positions[3]);   // the last one in the file
		Assert.Equal(new CMapPosition(3, 0), map.Positions[4]);   // a sea zone takes its text's place
		Assert.Equal(new CMapPosition(3, 2), map.Positions[5]);
	}

	[Fact]
	public void AProvincesNameIsWrittenWhereTheFileSaysUncutTurnedAndScaled()
	{
		CMap map = Load();

		// text_position as written, not cut; rotation 0.5 where given; scale 1 where not.
		Assert.Equal(new CProvinceLabel(3.6f, 0.4f, true, 0.5f, 1f), map.ProvinceLabels[4]);
		// Nothing said: the centre of its bounding box, and not given.
		Assert.Equal(new CProvinceLabel(2, 2, false, 0, 1), map.ProvinceLabels[2]);
	}

	[Fact]
	public void AProvinceAlsoHasAPlaceForItsTextAndOneForItsPort()
	{
		CMap map = Load();

		Assert.Equal(new CMapPosition(0, 1), map.TextPositions[1]);        // text_position, on land too
		Assert.Equal(new CMapPosition(2, 2), map.TextPositions[2]);        // nothing said: the centre
		Assert.Equal(new CMapPosition(3, 2), map.NavalBasePositions[3]);   // naval_base inside building_position
		Assert.Equal(new CMapPosition(1, 2), map.NavalBasePositions[1]);   // nothing said: the centre
	}

	[Fact]
	public void TheBuildingsAProvinceIsGivenPlacesForAreKeptAsTheFileHasThem()
	{
		CMap map = Load();

		Assert.Equal(new[] { new CBuildingPosition("naval_base", 3.0f, 2.0f) }, map.BuildingPositions[3]);
		Assert.Empty(map.BuildingPositions[1]);
		// That naval base lies on a pixel of Epsilon's, which is land, and the one sea zone
		// touches no other: no port, and nothing in the table of distances by sea.
		Assert.Equal(0, map.PortSea[3]);
		Assert.Equal(-1, map.SeaIndex[4]);
		Assert.Equal(0, map.SeaIndexCount);
	}

	[Fact]
	public void APositionForNoProvinceIsReportedAndAMissingFileLeavesTheCentres()
	{
		Write("map/positions.txt", "9 = { unit = { x=1 y=1 } }\n2 = { unit = { x=2 y=2 } }\n");
		CMap map = Load();
		Assert.Equal(new CMapPosition(2, 2), map.Positions[2]);
		Assert.Contains(_warnings, warning => warning.Contains("1 blocks are not for a province"));

		File.Delete(Path.Combine(_root, "map", "positions.txt"));
		_warnings.Clear();
		map = Load();
		Assert.Equal(new CMapPosition(2, 2), map.Positions[2]);
		Assert.Contains(_warnings, warning => warning.Contains("no positions"));
	}

	// --- the ways between provinces ---

	private static int[] To(CProvinceEdge[] edges) => edges.Select(edge => edge.To).ToArray();

	[Fact]
	public void AProvincesEdgesGoToLowerNumbersFirstAndThenAsItsOwnPixelsMeetTheRest()
	{
		CMap map = Load();

		// The map is gone through from the south, row by row, and each pixel looks east,
		// west, north and south. A meets C under its south-west pixel before B beside its
		// south-east one, and E only in its north row, round the edge of the map.
		Assert.Equal(new[] { 3, 2, 5 }, To(map.Edges[1]));
		Assert.Equal(new[] { 1, 4, 5, 3 }, To(map.Edges[2]));   // the adjacency file's row comes last
		Assert.Equal(new[] { 1, 4, 2 }, To(map.Edges[3]));
		Assert.Equal(new[] { 2, 3 }, To(map.Edges[4]));
		Assert.Equal(new[] { 1, 2 }, To(map.Edges[5]));
		Assert.Empty(map.Edges[0]);
	}

	[Fact]
	public void TheAdjacencyFileNamesAnEdgesKindAndWhatItGoesThrough()
	{
		CMap map = Load();

		CProvinceEdge strait = map.Edges[3].Single(edge => edge.To == 2);
		Assert.Equal(EProvinceEdgeKind.Crossing, strait.Kind);
		Assert.Equal(4, strait.Through);
		Assert.Equal(EProvinceEdgeKind.Crossing, map.Edges[2].Single(edge => edge.To == 3).Kind);

		Assert.Equal(EProvinceEdgeKind.River, CMapGraph.KindOf("river"));
		Assert.Equal(EProvinceEdgeKind.Impassable, CMapGraph.KindOf("impassable"));
		Assert.Equal(EProvinceEdgeKind.Plain, CMapGraph.KindOf("canal"));
	}

	[Fact]
	public void ARowForTwoProvincesThatTouchWritesOverTheEdgeTheyHave()
	{
		Write("map/adjacencies.csv", "From;To;Type;Through;Comment\n1;2;impassable;0;a wall\n-1;-1;;-1;-1\n");
		CMap map = Load();

		Assert.Equal(new[] { 3, 2, 5 }, To(map.Edges[1]));                     // nothing added
		Assert.Equal(EProvinceEdgeKind.Impassable, map.Edges[1].Single(edge => edge.To == 2).Kind);
		Assert.Equal(EProvinceEdgeKind.Impassable, map.Edges[2].Single(edge => edge.To == 1).Kind);
	}

	private static CFixedPoint Distance(CMap map, int from, int to)
	{
		return map.Edges[from].Single(edge => edge.To == to).Distance;
	}

	[Fact]
	public void ADistanceIsTheCostOfTheTerrainOnAWalkFromOnePointToTheOther()
	{
		CMap map = Load();

		// A is at (1,2), a forest pixel, and B beside it: one pixel walked, the one left.
		Assert.Equal(CFixedPoint.Parse("1.5"), Distance(map, 1, 2));
		// A to C is two pixels south, both forest.
		Assert.Equal(CFixedPoint.FromInt(3), Distance(map, 1, 3));
		// A walk steps east or west and north or south at once: C at (1,0) to B at (2,2)
		// goes by (2,1), which is plains.
		Assert.Equal(CFixedPoint.Parse("2.5"), Distance(map, 3, 2));
	}

	[Fact]
	public void TheWayBackIsNotTheSameDistance()
	{
		CMap map = Load();

		// The pixel a walk starts on is counted and the one it ends on is not: A to E
		// crosses two forest pixels, E to A one plains and one forest.
		Assert.Equal(CFixedPoint.FromInt(3), Distance(map, 1, 5));
		Assert.Equal(CFixedPoint.Parse("2.5"), Distance(map, 5, 1));
	}

	[Fact]
	public void BetweenLandAndSeaTheLandEndIsThePort()
	{
		CMap map = Load();

		// C's unit point is (1,0) and its port is placed at (3,2): the way to the sea zone
		// at (3,0) is measured from the port, a plains pixel and an ocean one.
		Assert.Equal(CFixedPoint.One, Distance(map, 3, 4));
		// And from the sea it is measured to B's port, at (2,2): the ocean it starts on
		// costs nothing here, and (2,1) is plains.
		Assert.Equal(CFixedPoint.One, Distance(map, 4, 2));
	}

	[Fact]
	public void AnEdgeWhoseWalkTouchesTheRiverBitmapIsARiver()
	{
		CMap map = Load();

		Assert.Equal(EProvinceEdgeKind.River, map.Edges[1].Single(edge => edge.To == 3).Kind);   // by (1,1)
		Assert.Equal(EProvinceEdgeKind.River, map.Edges[3].Single(edge => edge.To == 1).Kind);
		Assert.Equal(EProvinceEdgeKind.Plain, map.Edges[1].Single(edge => edge.To == 2).Kind);
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
	public void TheMapsRegionsAreReadInTheirFilesOrder()
	{
		CMap map = Load();

		Assert.Equal(new[] { "coast_region", "land_region" }, map.Regions.Select(region => region.Name));
		Assert.Equal(new[] { 1, 2, 3 }, map.Regions[1].Provinces);
		Assert.True(map.Regions[1].UseForPeace);
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
	public void AProvinceIsTheTerrainPaintedMostOnItAroundItsText()
	{
		// The whole of this map is inside the square that is counted, so it is every pixel
		// of a province that counts here.
		CMap map = Load();

		Assert.Equal("forest", map.Terrain[1]?.Name);   // A: three forest, one plains
		Assert.Equal("ocean", map.Terrain[4]?.Name);    // D, the sea: what is painted at its point
		Assert.Equal("plains", map.Terrain[5]?.Name);   // E: its one pixel
	}

	[Fact]
	public void ATieGoesToTheCategoryDeclaredFirst()
	{
		CMap map = Load();

		// B and C are each one plains and one forest; plains is declared before forest.
		Assert.Equal("plains", map.Terrain[2]?.Name);
		Assert.Equal("plains", map.Terrain[3]?.Name);
	}

	/// <summary>A strip thirty pixels long and one high, all of it one land province.</summary>
	private static CTerrain?[] TerrainOfAStrip(int province, int textX, byte[] painted)
	{
		var categories = new[]
		{
			new CTerrain("ocean", isWater: true, default),
			new CTerrain("plains", isWater: false, default),
			new CTerrain("forest", isWater: false, default),
		};
		var categoryOfIndex = new int[256];
		Array.Fill(categoryOfIndex, -1);
		categoryOfIndex[Ocean] = 0;
		categoryOfIndex[Plains] = 1;
		categoryOfIndex[Forest] = 2;

		var raster = new ushort[painted.Length];
		Array.Fill(raster, (ushort)province);
		var unit = new CMapPosition[province + 1];
		var text = new CMapPosition[province + 1];
		text[province] = new CMapPosition(textX, 0);
		CBitmapFile bitmap = CBitmapFile.Parse(TestBitmap.Indexed8(painted.Length, new[] { painted }));
		return CTerrainReader.FindTerrain(bitmap, raster, new CTerrainReader.Definition(categories, categoryOfIndex),
			new CMapPositions(unit, text, unit, new CBuildingPosition[province + 1][], Array.Empty<CPortPlacement>()),
			id => false);
	}

	[Fact]
	public void OnlyTheTwentyPixelsAroundAProvincesTextAreCounted()
	{
		// Twenty plains and then ten forest. Most of the province is plains; of the pixels
		// from ten west of its text at 25, five are plains and ten forest.
		var painted = new byte[30];
		Array.Fill(painted, Plains, 0, 20);
		Array.Fill(painted, Forest, 20, 10);

		Assert.Equal("forest", TerrainOfAStrip(1, 25, painted)[1]?.Name);
		Assert.Equal("plains", TerrainOfAStrip(1, 12, painted)[1]?.Name);   // 2 to 21: eighteen plains, two forest
	}

	[Fact]
	public void LandPaintedAsWaterTakesTheFirstTerrainThatIsNotWater()
	{
		var painted = new byte[30];
		Array.Fill(painted, Ocean);

		Assert.Equal("plains", TerrainOfAStrip(1, 15, painted)[1]?.Name);
	}

	[Fact]
	public void OneProvinceIsNamedByNumberAndTakesWhatIsPaintedAtItsText()
	{
		// All plains but the one pixel its text stands on. Any other province would count
		// fourteen plains against one forest.
		var painted = new byte[30];
		Array.Fill(painted, Plains);
		painted[25] = Forest;

		Assert.Equal("forest", TerrainOfAStrip(CTerrainReader.ProvinceTakenAtItsText, 25, painted)[CTerrainReader.ProvinceTakenAtItsText]?.Name);
		Assert.Equal("plains", TerrainOfAStrip(1, 25, painted)[1]?.Name);
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

		Assert.Equal("plains", map.Terrain[1]?.Name);
		Assert.Contains(_warnings, warning => warning.Contains("3 pixels use a palette index"));
	}

	[Fact]
	public void ATerrainBitmapOfTheWrongSizeGivesNoProvinceATerrain()
	{
		WriteBytes("map/terrain.bmp", TestBitmap.Indexed8(2, new[] { new byte[] { Plains, Plains } }));
		CMap map = Load();

		Assert.All(map.Terrain, terrain => Assert.Null(terrain));
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
			"NDefines = {\n\tstart_date = '1936.1.1',\n\tend_date = '1948.1.1',\n\teconomy = {\n\t\tIC_TO_MONEY = 0.05, -- per IC\n\t\tIC_TO_SUPPLIES = 7.0,\n" +
			"\t\tLEADERSHIP_TO_OFFICERS = 4,\n\t\tLEADERSHIP_TO_DIPLOMACY = 0.5,\n\t\tLEADERSHIP_TO_SPIES = 0.15,\n\t},\n" +
			"\tcountry = {\n\t\tBASE_TECH_DECAY = -0.025,\n\t\tMAX_TECH_ABILITY = 99,\n\t\tTECH_ABILITY_GAIN_DIVISOR = 10,\n" +
			"\t\tTECHNOLOGY_YEAR_IMPACT = 1.5,\n\t\tPEACETIME_MANPOWER_ROTATION_LOW_SUPPLY_FACTOR = 1.25,\n\t},\n" +
			"\tmilitary = {\n\t\tMAX_MANPOWER = 10,\n\t\tPRIDE_BONUS_EXP = 10, PRIDE_BASE_REDESIGNATION_DISSENT = 1,\n\t\tSHIP_UPGRADE_SPEED_MOD = 4.0,\n" +
			"\t\tAIR_UPGRADE_SPEED_MOD = 1.0,\n\t\tUNIT_UPGRADE_COST = 0.4, UNIT_UPGRADE_TIME = 0.7, UNIT_UPGRADE_PRACTICAL_MOD = 0.9, LAND_UPGRADE_SPEED_MOD = 1.0,\n" +
			"\t\tMAX_OFFICERS = 1.4,\n\t\tLOW_ORG_REGAIN_BONUS = 0.3,\n\t\tNAVAL_BASE_EFFICIENCY = 6,\n\t},\n}\n");
		Write("common/technology.txt",
			"theoretical = {\n\tmechanicalengineering_theory\n\tinfantry_theory\n}\npractical = {\n\tinfantry_practical\n}\nfolders = {\n\tindustry_folder\n}\n");
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
		// What the files give a game: the two days a game then runs before its first hour
		// are not what these tests are about.
		return CGameStateLoader.LoadStart(directories, Load(), localisation, countries, _warnings.Add, firstDays: false);
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

		// The empty head of state's post is filled with him too, the first who may take it (politics.md, section 7):
		// he counts once as the genius and once as the workhorse - beside full_civilian_economy's -0.5 and 0.15, the law
		// a new game gives a country with none in the group (politics.md, section 13).
		Assert.Same(Germany(state).Government["armament_minister"], Germany(state).Government["head_of_state"]);
		Assert.Equal(CFixedPoint.Parse("-0.4"), Germany(state).Modifier["global_ic"]);
		Assert.Equal(CFixedPoint.Parse("0.2"), Germany(state).Modifier["global_money"]);
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

		// Not the history's planned_economy, which is no law: only what a new game gives a country with none in a group,
		// the last law of it that it is allowed (politics.md, section 13).
		Assert.Equal(new[] { "free_press", "full_civilian_economy" }, germany.Laws.Values.Select(law => law.Name).OrderBy(name => name, StringComparer.Ordinal));
		// The history's 99 is nobody, and the post is filled from the pool as an empty one (politics.md, section 7).
		Assert.NotEqual(99, germany.Government["head_of_state"].Id);
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
	public void ABuildingSaysWhetherItShowsOnTheMap()
	{
		WriteStartFiles();
		Write("common/buildings.txt",
			"air_base = { air_capacity = 1 onmap = yes }\nindustry = { ic = 1 onmap = no }\ninfra = { infrastructure = 0.1 }");
		CGameState state = LoadStart();

		Assert.Equal(new[] { true, false, false }, state.Buildings.Select(building => building.IsOnMap));
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
		Assert.Equal(CFixedPoint.FromInt(5), alpha.Modifier["ic"]);
		Assert.Equal(CFixedPoint.Parse("0.6"), alpha.Modifier["infrastructure"]);
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
				LEADERSHIP_TO_OFFICERS = 4, LEADERSHIP_TO_DIPLOMACY = 0.5, LEADERSHIP_TO_SPIES = 0.15,
				LAST_ONE = 96  -- no comma after the last
			},
			country = { BASE_TECH_DECAY = -0.025, MAX_TECH_ABILITY = 99, TECH_ABILITY_GAIN_DIVISOR = 10, TECHNOLOGY_YEAR_IMPACT = 1.5, PEACETIME_MANPOWER_ROTATION_LOW_SUPPLY_FACTOR = 1.25 },
			military = { MAX_MANPOWER = 10, PRIDE_BONUS_EXP = 10, PRIDE_BASE_REDESIGNATION_DISSENT = 1, SHIP_UPGRADE_SPEED_MOD = 4.0, AIR_UPGRADE_SPEED_MOD = 1.0, UNIT_UPGRADE_COST = 0.4, UNIT_UPGRADE_TIME = 0.7, UNIT_UPGRADE_PRACTICAL_MOD = 0.9, LAND_UPGRADE_SPEED_MOD = 1.0, MAX_OFFICERS = 1.4, LOW_ORG_REGAIN_BONUS = 0.3, NAVAL_BASE_EFFICIENCY = 6 },
			weather = {
				COLD = -0.75,
				IC_TO_MONEY = 3,
				deeper = { HIDDEN = 1 },
			},
			}");
		// Everything this project reads is there, so nothing is reported missing.
		CDefines defines = CDefinesReader.Read(CDirectorySettings.At(_root), _warnings.Add);

		Assert.Equal(CFixedPoint.Parse("0.05"), defines["economy", "IC_TO_MONEY"]);
		Assert.Equal(CFixedPoint.FromInt(7), defines["economy", "IC_TO_SUPPLIES"]);
		Assert.Equal(CFixedPoint.FromInt(96), defines["economy", "LAST_ONE"]);
		Assert.Equal(CFixedPoint.Parse("-0.75"), defines["weather", "COLD"]);
		Assert.Equal(CFixedPoint.FromInt(3), defines["weather", "IC_TO_MONEY"]);   // a name belongs to its section
		Assert.False(defines.Has("weather", "HIDDEN"));
		Assert.False(defines.Has("deeper", "HIDDEN"));
		Assert.Equal(CFixedPoint.Zero, defines["economy", "NOT_THERE"]);
		Assert.Equal(22 + 3, defines.Count);                       // the two outside any section are not numbers to ask for
		Assert.Empty(_warnings);
	}

	[Fact]
	public void ReportsADefineThisProjectReadsAndTheFileDoesNotGive()
	{
		Write("common/defines.lua", "defines = {\n\tstart_date = '1936.1.1',\n\teconomy = { IC_TO_SUPPLIES = 7,\n" +
			"\t\tLEADERSHIP_TO_OFFICERS = 4, LEADERSHIP_TO_DIPLOMACY = 0.5, LEADERSHIP_TO_SPIES = 0.15 },\n" +
			"\tcountry = { BASE_TECH_DECAY = -0.025, MAX_TECH_ABILITY = 99, TECH_ABILITY_GAIN_DIVISOR = 10, TECHNOLOGY_YEAR_IMPACT = 1.5, PEACETIME_MANPOWER_ROTATION_LOW_SUPPLY_FACTOR = 1.25 },\n" +
			"\tmilitary = { MAX_MANPOWER = 10, PRIDE_BONUS_EXP = 10, PRIDE_BASE_REDESIGNATION_DISSENT = 1, SHIP_UPGRADE_SPEED_MOD = 4.0, AIR_UPGRADE_SPEED_MOD = 1.0, UNIT_UPGRADE_COST = 0.4, UNIT_UPGRADE_TIME = 0.7, UNIT_UPGRADE_PRACTICAL_MOD = 0.9, LAND_UPGRADE_SPEED_MOD = 1.0, MAX_OFFICERS = 1.4, LOW_ORG_REGAIN_BONUS = 0.3, NAVAL_BASE_EFFICIENCY = 6 },\n}\n");
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
	public void AWarNotFoughtThatBeganByTheDateIsFinishedAndAFileWithNoWarInItIsOneOfNothing()
	{
		WriteStartFiles();
		Write("history/wars/Fought.txt", "name = \"Fought\"\n1935.1.1 = { add_attacker = ITA add_defender = ENG }");
		Write("history/wars/Later.txt", "name = \"Later\"\n1937.7.7 = { add_attacker = GER add_defender = FRA }");
		Write("history/wars/Over.txt",
			"name = \"Over\"\n1930.1.1 = { add_attacker = GER add_defender = FRA }\n1931.1.1 = { rem_attacker = GER }");
		Write("history/wars/Empty.txt", "# No separate war\n");
		CDirectorySettings directories = CDirectorySettings.At(_root);
		CLocalisation localisation = CLocalisation.Load(directories, warn: _warnings.Add);
		CCountries countries = CCountries.Load(directories, localisation, _warnings.Add);
		var finished = new List<CWar>();

		List<CWar> fought = CWarReader.Read(directories, countries, new CDate(1936, 1, 1), _warnings.Add, finished);

		Assert.Equal(new[] { "Fought" }, fought.Select(war => war.Name));
		// The one that ended keeps its name and what is left of its sides; the empty file is a war of nothing; the one
		// that begins later is on neither list (FINDINGS-war.md, C.13.1).
		Assert.Equal(new[] { "", "Over" }, finished.Select(war => war.Name).OrderBy(name => name, StringComparer.Ordinal));
		CWar over = finished.Single(war => war.Name == "Over");
		Assert.Empty(over.Attackers);
		Assert.Equal(new[] { "FRA" }, over.Defenders.Select(tag => tag.Value));
		Assert.Equal(0, over.Action);
		CWar nothing = finished.Single(war => war.Name == "");
		Assert.Empty(nothing.Attackers.Concat(nothing.Defenders));
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

		// Ten IC at 0.05, the law's +15%, and the workhorse's +5%: the empty head of state's post is filled with him, the
		// first minister who may take it (politics.md, section 7).
		Assert.Equal(CFixedPoint.Parse("0.6"), Germany(state).HomeProduced[EGoods.Money]);
	}

	[Fact]
	public void AnAmountOfGoodsThatIsNotANumberIsReported()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt", "capital = 1\nmoney = plenty");
		LoadStart();

		Assert.Contains(_warnings, warning => warning.Contains("1 amounts in country history"));
	}

	// --- research ---

	[Fact]
	public void ReadsTheTechnologyCategoriesTheoriesFirst()
	{
		WriteStartFiles();
		CGameState state = LoadStart();

		Assert.Equal(new[] { "mechanicalengineering_theory", "infantry_theory", "infantry_practical" },
			state.TechnologyCategories.Select(category => category.Name));
		Assert.Equal(new[] { true, true, false }, state.TechnologyCategories.Select(category => category.IsTheory));
	}

	[Fact]
	public void ATechnologyBlockSaysWhatResearchingItTakes()
	{
		WriteStartFiles();
		Write("technologies/more.txt", @"
			small_arms = {
				infantry_brigade = { soft_attack = 0.6 }
				allow = {
					steel_production = 2
					infantry_practical = 5
					OR = { industral_production = 1 num_of_ports = 3 }
					any_owned_province = { has_building = industry }
				}
				research_bonus_from = { infantry_theory = 0.3 infantry_practical = 0.7 }
				on_completion = infantry_theory
				decay = { infantry_theory = -0.1 }
				difficulty = 0.5
				start_year = 1918
				is_nuclear = yes
				change = no
				folder = industry_folder
			}");
		CGameState state = LoadStart();

		CTechnology steel = state.Technologies["steel_production"];
		Assert.Equal(2, steel.Difficulty);
		Assert.Equal(1918, steel.StartYear);
		Assert.Equal(1932, steel.FirstOffset);
		Assert.Equal(2, steel.AdditionalOffset);
		Assert.Equal(12, steel.HighestLevel);
		Assert.False(steel.IsSingleLevel);
		Assert.Equal("mechanicalengineering_theory", steel.OnCompletion);
		Assert.Equal(("mechanicalengineering_theory", CFixedPoint.Parse("0.5")), Assert.Single(steel.ResearchBonusFrom));
		Assert.Null(steel.Allow);

		CTechnology arms = state.Technologies["small_arms"];
		Assert.Equal(0, arms.Difficulty);                    // a fraction is dropped
		Assert.True(arms.IsSingleLevel);
		Assert.Equal(1, arms.HighestLevel);                  // no max_level
		Assert.True(arms.IsNuclear);
		Assert.Equal(("infantry_theory", CFixedPoint.Parse("-0.1")), Assert.Single(arms.Decay));
		Assert.Empty(arms.EffectPerLevel.Values);            // none of its own keys is an effect
		Assert.Equal(new[] { EResearchCondition.TechnologyLevel, EResearchCondition.Ability, EResearchCondition.Any, EResearchCondition.OwnsProvinceWithBuilding },
			arms.Allow!.Children.Select(condition => condition.Kind));
		Assert.Equal(new[] { EResearchCondition.TechnologyLevel, EResearchCondition.Ports },
			arms.Allow.Children[2].Children.Select(condition => condition.Kind));
		Assert.DoesNotContain(_warnings, warning => warning.Contains("technolog"));
	}

	[Fact]
	public void AConditionOrACategoryNobodyKnowsIsReportedAndLeftOut()
	{
		WriteStartFiles();
		Write("technologies/more.txt", @"
			odd = {
				allow = { steel_production = 1 has_flying_saucers = yes war = { limited = no } }
				research_bonus_from = { infantry_theory = 0.5 alchemy = 0.5 }
				on_completion = alchemy
				difficulty = 1
			}");
		CGameState state = LoadStart();

		CTechnology odd = state.Technologies["odd"];
		Assert.Single(odd.Allow!.Children);
		Assert.Single(odd.ResearchBonusFrom);
		Assert.Equal("", odd.OnCompletion);
		Assert.Contains(_warnings, warning => warning.Contains("2 conditions that are not understood")
			&& warning.Contains("has_flying_saucers") && warning.Contains("war"));
		Assert.Contains(_warnings, warning => warning.Contains("categories that are not defined") && warning.Contains("alchemy"));
	}

	[Fact]
	public void ACountrysHistoryGivesItsAbilities()
	{
		WriteStartFiles();
		Write("history/countries/GER - Germany.txt", @"
			capital = 1
			infantry_theory = 10
			infantry_practical = 2.5
			1937.1.1 = { infantry_theory = 99 }");
		CCountry germany = Germany(LoadStart());

		Assert.Equal(CFixedPoint.FromInt(10), germany.Ability("infantry_theory"));
		Assert.Equal(CFixedPoint.Parse("2.5"), germany.Ability("infantry_practical"));
		Assert.Equal(CFixedPoint.Zero, germany.Ability("mechanicalengineering_theory"));
	}

	// --- continents, strategic resources and leadership ---

	[Fact]
	public void ReadsWhichContinentEachProvinceIsOn()
	{
		CMap map = Load();

		Assert.Equal("west", map.Continents[1]);
		Assert.Equal("west", map.Continents[3]);
		Assert.Equal("east", map.Continents[5]);
		Assert.Equal("", map.Continents[4]);                 // the sea is on none
		Assert.Equal("", map.Continents[0]);
		Assert.Equal(new[] { "west", "east" }, map.ContinentNames);       // in the file's order
	}

	[Fact]
	public void AContinentFileNamingAProvinceTwiceOrOneThatIsNotThereIsReported()
	{
		Write("map/continent.txt", "west = { 1 2 3 99 }\neast = { 5 3 }\n");
		CMap map = Load();

		Assert.Equal("west", map.Continents[3]);             // the first to name it keeps it
		Assert.Contains(_warnings, warning => warning.Contains("1 entries are not the id of a province"));
		Assert.Contains(_warnings, warning => warning.Contains("1 provinces are listed on more than one continent"));
	}

	[Fact]
	public void AProvinceFileNamesItsStrategicResourceAndOneNobodyDeclaredIsReported()
	{
		WriteStartFiles();
		Write("common/strategic_resources.txt", "gold = { global_money = 0.33 }\nrubber = { amm_movement_speed = 0.15 }\n");
		Write("history/provinces/west/1 - Alpha.txt", "owner = GER\ncontroller = GER\nstrategic_resource = gold\ninfra = 4");
		Write("history/provinces/west/3 - Gamma.txt", "owner = GER\ncontroller = GER\nstrategic_resource = unobtainium");
		Write("history/countries/GER - Germany.txt", "capital = 1");
		CGameState state = LoadStart();
		var simulation = new CSimulation(state);

		Assert.Equal("gold", state.Province(1).StrategicResource);
		Assert.Equal("", state.Province(3).StrategicResource);
		Assert.Contains(_warnings, warning => warning.Contains("strategic resources that are not defined") && warning.Contains("unobtainium"));

		// Nobody has the use of a resource until a day has been run. Then: five IC at
		// 0.05, and a third more for the gold - each with the 5% of the workhorse the empty head of state's post is
		// filled with (politics.md, section 7) and the 15% of full_civilian_economy, the law a new game gives a country
		// with none in the group (politics.md, section 13).
		Assert.Equal(CFixedPoint.Parse("0.300"), Germany(state).HomeProduced[EGoods.Money]);
		simulation.RunHours(24);
		Assert.Equal(CFixedPoint.Parse("0.382"), Germany(state).HomeProduced[EGoods.Money]);
	}

	[Fact]
	public void ANewGameWorksOutEveryCountrysLeadershipAndSplitsItInQuarters()
	{
		WriteStartFiles();
		Write("common/static_modifiers.txt", @"
			base_values = { global_leadership = 3.5 ic = 5 }
			overseas = { local_leadership_modifier = -0.9 }");
		Write("history/provinces/west/3 - Gamma.txt", "owner = GER\ncontroller = GER\nadd_core = GER\nleadership = 2");
		Write("history/provinces/east/5 - Epsilon.txt", "owner = GER\ncontroller = GER\nadd_core = GER\nleadership = 1");
		Write("history/countries/GER - Germany.txt", "capital = 3");
		CGameState state = LoadStart();
		_ = new CSimulation(state);

		// Epsilon is on the other continent and does not touch Gamma, so it gives a tenth.
		CCountry germany = Germany(state);
		Assert.Equal(CFixedPoint.Parse("5.6"), germany.TotalLeadership);
		Assert.All(germany.LeadershipShares, share => Assert.Equal(8192, share.Raw));
		Assert.Equal(CFixedPoint.One, germany.DiplomaticInfluence);
		Assert.Equal(1, CLeadership.ResearchSlots(germany));
	}
}
