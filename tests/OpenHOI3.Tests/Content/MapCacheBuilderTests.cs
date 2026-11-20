using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Map.Cache;
using OpenHOI3.Sim;

namespace OpenHOI3.Tests.Content;

/// <summary>
/// The map cache made from the map files, as <c>docs/mechanics/mapcache.md</c> has it, on maps of a few pixels. The whole of it agrees with
/// the original's files byte for byte (<c>OpenHOI3.SaveCheck mapcache</c>); these pin down each rule so that a slip says where it is.
/// </summary>
public class MapCacheBuilderTests : IDisposable
{
	private readonly string _root = Path.Combine(Path.GetTempPath(), "openhoi3-tests-" + Guid.NewGuid().ToString("N"));

	public void Dispose()
	{
		try
		{
			if (Directory.Exists(_root))
			{
				Directory.Delete(_root, recursive: true);
			}
		}
		catch (IOException)
		{
			// A locked file must not fail the test that already passed.
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>A map from rows written north first: a digit is a province, sea those given.</summary>
	private static CMapCacheSource Source(string[] rows, int[]? sea = null, int[]? regions = null, byte[]? terrain = null, bool[]? waterIndex = null,
		int[]? priority = null, int[][]? neighbours = null)
	{
		int width = rows[0].Length;
		int height = rows.Length;
		var provinces = new ushort[width * height];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				provinces[y * width + x] = (ushort)(rows[height - 1 - y][x] - '0');
			}
		}
		var isSea = new bool[10];
		foreach (int province in sea ?? Array.Empty<int>())
		{
			isSea[province] = true;
		}
		return new CMapCacheSource
		{
			Width = width,
			Height = height,
			Provinces = provinces,
			IsSea = isSea,
			FirstRegion = regions ?? new int[10],
			Neighbours = neighbours ?? Enumerable.Range(0, 10).Select(_ => Enumerable.Range(1, 9).ToArray()).ToArray(),
			Terrain = terrain ?? new byte[width * height],
			Priority = priority ?? Enumerable.Range(0, 256).ToArray(),
			HasEntry = Enumerable.Repeat(true, 256).ToArray(),
			WaterIndex = waterIndex ?? new bool[256],
			PaletteSize = 256,
			RiverSources = new[] { (1, 0) },
		};
	}

	private static int Cell(CMapCacheSource source, int x, int y) => y * (source.Width + 1) + x;

	[Fact]
	public void RandIsTheCRuntimesFromOne()
	{
		uint state = 1;
		int[] drawn = Enumerable.Range(0, 5).Select(_ => CBeachNoise.Next(ref state)).ToArray();
		Assert.Equal(new[] { 41, 18467, 6334, 26500, 19169 }, drawn);
	}

	[Fact]
	public void ADisplacementIsAtMostAHalfAndHangsOnTheCallsBeforeIt()
	{
		var noise = new CBeachNoise();
		Assert.All(Enumerable.Range(0, 50), i => Assert.InRange(noise.Displacement(i * 0.37f - 9f, i * 0.91f - 20f), 0f, 0.5f));
		Assert.Equal(noise.Displacement(3.5f, -7.5f), new CBeachNoise().Displacement(3.5f, -7.5f));
		Assert.NotEqual(noise.Displacement(3.5f, -7.5f), new CBeachNoise(293).Displacement(3.5f, -7.5f));
	}

	[Fact]
	public void TheRasterCutsTheSouthWestCornerOfAStepWithAWholeByte()
	{
		CMapCacheSource source = Source(new[] { "22222", "22222", "11222", "11111", "11111" });

		byte[] raster = CMapBinBuilder.Raster(source);

		Assert.Equal(2, raster[Cell(source, 2, 2)]);
		Assert.Equal(0x20, raster[Cell(source, 1, 2)]);
	}

	[Fact]
	public void TheRasterCutsNoStepBetweenLandAndSea()
	{
		CMapCacheSource source = Source(new[] { "22222", "22222", "11222", "11111", "11111" }, sea: new[] { 2 });

		Assert.Equal(0, CMapBinBuilder.Raster(source)[Cell(source, 2, 2)]);
	}

	[Fact]
	public void TheSecondPassCutsTheNorthWestCornerOfLandOnly()
	{
		// Pixel (2,2) has 1 west, north and north-west and 2 south and east; the 3s keep the first pass from cutting (1,2) before it.
		string[] rows = { "11111", "11111", "11222", "33222", "22222" };
		CMapCacheSource source = Source(rows);

		byte[] raster = CMapBinBuilder.Raster(source);

		Assert.Equal(4, raster[Cell(source, 2, 2)]);
		Assert.Equal(0x20, raster[Cell(source, 1, 2)]);
		CMapCacheSource withSea = Source(rows, sea: new[] { 1 });
		Assert.Equal(0, CMapBinBuilder.Raster(withSea)[Cell(withSea, 2, 2)]);
	}

	[Fact]
	public void MapBinIsItsHeadItsSourcesAndItsRaster()
	{
		CMapCacheSource source = Source(new[] { "111", "111" });
		byte[] raster = CMapBinBuilder.Raster(source);
		using var stream = new MemoryStream();

		CMapBinBuilder.Write(stream, source, raster);
		byte[] file = stream.ToArray();

		Assert.Equal("MAP_CACHE\0\0\0", System.Text.Encoding.ASCII.GetString(file, 0, 12));
		Assert.Equal(new[] { 2, 3, 1, 1, 0 }, Enumerable.Range(0, 5).Select(i => BitConverter.ToInt32(file, 12 + i * 4)));
		Assert.Equal(CMapBin.RiverSources(file), new[] { (1, 0) });
		Assert.Equal(32 + raster.Length, file.Length);
	}

	[Fact]
	public void AProvinceBorderSetsItsSideAndARegionBorderAddsSixteen()
	{
		CMapCacheSource same = Source(new[] { "11122", "11122", "11122", "11122", "11122" });
		byte[] bitmap = CBorderBitmapBuilder.Build(same, CMapBinBuilder.Raster(same));
		Assert.Equal(1, bitmap[Cell(same, 2, 2) * 4]);
		Assert.Equal(2, bitmap[Cell(same, 3, 2) * 4]);
		Assert.Equal(0x40, bitmap[Cell(same, 2, 2) * 4 + 1]);
		Assert.Equal(0, bitmap[Cell(same, 2, 2) * 4 + 2]);

		CMapCacheSource regions = Source(new[] { "11122", "11122", "11122", "11122", "11122" }, regions: new[] { 0, 1, 2, 0, 0, 0, 0, 0, 0, 0 });
		bitmap = CBorderBitmapBuilder.Build(regions, CMapBinBuilder.Raster(regions));
		Assert.Equal(0x11, bitmap[Cell(regions, 2, 2) * 4]);
	}

	[Fact]
	public void NoBorderIsDrawnBetweenLandAndSea()
	{
		CMapCacheSource source = Source(new[] { "11122", "11122", "11122", "11122", "11122" }, sea: new[] { 2 });

		byte[] bitmap = CBorderBitmapBuilder.Build(source, CMapBinBuilder.Raster(source));

		Assert.Equal(0, bitmap[Cell(source, 2, 2) * 4]);
	}

	[Fact]
	public void AnEntryBesideAProvinceInACornerOfACutGetsACutCode()
	{
		CMapCacheSource source = Source(new[] { "22222", "22222", "11222", "11111", "11111" });

		byte[] bitmap = CBorderBitmapBuilder.Build(source, CMapBinBuilder.Raster(source));

		// The cut pixel has its side's code in byte 3.
		Assert.Equal(2, bitmap[Cell(source, 2, 2) * 4 + 3]);
	}

	[Fact]
	public void ARunIsOneQuadAndItsCornersWeldWithTheNextRow()
	{
		CMapCacheSource source = Source(new[] { "000", "111", "111" });
		var pixels = new CProvincePixels(source);

		CLandMesh mesh = CLandMesh.Build(source, pixels, new byte[4 * 4], new[] { 1 }, (0, 0));

		// Two runs share a row of corners: six places, not eight, and the first occurrence keeps its number.
		Assert.Equal(6, mesh.VertexCount);
		Assert.Equal(new short[] { -1, -1, 1, 0, 2, -1, 1, 0, -1, 0, 1, 0, 2, 0, 1, 0, -1, 1, 1, 0, 2, 1, 1, 0 }, mesh.Vertices);
		Assert.Equal(new ushort[] { 1, 0, 2, 1, 2, 3, 3, 2, 4, 3, 4, 5 }, mesh.Indices);
		Assert.Equal(new[] { 0, 12 }, mesh.GroupStarts);
	}

	[Theory]
	[InlineData(8, -2, -1)]
	[InlineData(4, -1, 0)]
	[InlineData(1, -1, -2)]
	[InlineData(2, -1, -1)]
	public void TheRasterMovesARunsLeftEndByTheFirstBitFound(int cut, int south, int north)
	{
		CMapCacheSource source = Source(new[] { "0110" });
		var pixels = new CProvincePixels(source);
		var raster = new byte[5 * 2];
		raster[0] = (byte)cut;

		CLandMesh mesh = CLandMesh.Build(source, pixels, raster, new[] { 1 }, (0, 0));

		// xs is 1, so the corners would be at 0: the file's numbers run from the tile's middle, here (0, 0).
		Assert.Equal(south + 1, mesh.Vertices[0]);
		Assert.Equal(north + 1, mesh.Vertices[8]);
	}

	[Fact]
	public void ASeaProvinceHasAGroupAndNoMesh()
	{
		CMapCacheSource source = Source(new[] { "1122" }, sea: new[] { 2 });
		var pixels = new CProvincePixels(source);

		CLandMesh mesh = CLandMesh.Build(source, pixels, new byte[5 * 2], new[] { 1, 2 }, (0, 0));

		Assert.Equal(new[] { 0, 6, 6 }, mesh.GroupStarts);
	}

	[Fact]
	public void APixelBesideLandIsAQuadNamingItsNeighboursTexel()
	{
		CMapCacheSource source = Source(new[] { "112", "112", "112" }, neighbours: Enumerable.Repeat(new[] { 2 }, 10).ToArray());
		var pixels = new CProvincePixels(source);
		var bitmap = new byte[4 * 4 * 4];
		// Only this pixel has a code, so the others' quads are welded away.
		bitmap[Cell(source, 1, 1) * 4] = 1;

		CBorderMesh mesh = CBorderMesh.Build(source, pixels, new byte[16], bitmap, new[] { 1 }, (0, 0), new List<(int, int)>?[10]);

		Assert.Equal(new ushort[] { 0, 1, 2, 2, 3, 0 }, mesh.Indices);
		// Corner A: x the pixel's less one and less 1000, z its 1 and so plus 1000; the texel is province 1's first; the colour the province and the code times 8.
		byte[] vertices = mesh.Vertices.ToArray();
		Assert.Equal(-1000, BitConverter.ToInt16(vertices, 0));
		Assert.Equal(1000, BitConverter.ToInt16(vertices, 2));
		Assert.Equal(14, BitConverter.ToInt16(vertices, 4));
		Assert.Equal(0, BitConverter.ToInt16(vertices, 6));
		Assert.Equal(0x10008u, BitConverter.ToUInt32(vertices, 8));
	}

	[Fact]
	public void AVertexWithNoBorderCodeIsWeldedAway()
	{
		CMapCacheSource source = Source(new[] { "12", "12" });
		var pixels = new CProvincePixels(source);

		CBorderMesh mesh = CBorderMesh.Build(source, pixels, new byte[9], new byte[3 * 3 * 4], new[] { 1 }, (0, 0), new List<(int, int)>?[10]);

		Assert.Empty(mesh.Indices);
		Assert.Empty(mesh.Vertices);
	}

	[Fact]
	public void AJunctionGoesOnTheProvincesListOnceAndReadsPastItsNeighbours()
	{
		// (1, 1) of province 1 has 2 to the north and 3 to the east.
		CMapCacheSource source = Source(new[] { "122", "113", "111" }, neighbours: new[] { Array.Empty<int>(), new[] { 2, 3 }, new[] { 1 }, new[] { 1 }, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>() });
		var pixels = new CProvincePixels(source);
		var junctions = new List<(int, int)>?[10];
		var bitmap = new byte[4 * 4 * 4];
		Array.Fill(bitmap, (byte)1);

		CBorderMesh mesh = CBorderMesh.Build(source, pixels, new byte[16], bitmap, new[] { 1 }, (0, 0), junctions);

		// The map's edge counts as a neighbour of id 0, so the corners of the walk are on the list as well; south first.
		Assert.Equal(new[] { (2, 0), (1, 1), (0, 2) }, junctions[1]);
		// (1, 1) is second on its list and the province has two neighbours: slot 3.
		Assert.Contains(1 * 14 + 3, Enumerable.Range(0, mesh.Vertices.Count / 12).Select(i => (int)BitConverter.ToInt16(mesh.Vertices.ToArray(), i * 12 + 4)));
	}

	[Fact]
	public void ACutPixelsSecondEntryCarriesItsNeighbourInTheColour()
	{
		CMapCacheSource source = Source(new[] { "2111" }, neighbours: Enumerable.Repeat(new[] { 1, 2 }, 10).ToArray());
		var pixels = new CProvincePixels(source);
		var raster = new byte[5 * 2];
		raster[1] = 2;
		var bitmap = new byte[5 * 2 * 4];
		bitmap[1 * 4] = 1;

		CBorderMesh mesh = CBorderMesh.Build(source, pixels, raster, bitmap, new[] { 1 }, (0, 0), new List<(int, int)>?[10]);

		// Each half is a triangle - a doubled corner welded - the second with the province across the cut in its colour.
		byte[] vertices = mesh.Vertices.ToArray();
		Assert.Equal(6, mesh.Indices.Count);
		Assert.Equal(new[] { 0x10008u, 0x10008u, 0x10008u, 0x20008u, 0x20008u, 0x20008u },
			Enumerable.Range(0, vertices.Length / 12).Select(i => BitConverter.ToUInt32(vertices, i * 12 + 8)));
	}

	private static CMapCacheSource Coast(string[] water)
	{
		int width = water[0].Length;
		int height = water.Length;
		var terrain = new byte[width * height];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				terrain[y * width + x] = (byte)(water[height - 1 - y][x] == '~' ? 1 : 0);
			}
		}
		var isWater = new bool[256];
		isWater[1] = true;
		return Source(water.Select(row => row.Replace('~', '2').Replace('.', '1')).ToArray(), sea: new[] { 2 }, terrain: terrain, waterIndex: isWater);
	}

	[Fact]
	public void AStraightCoastIsItsOwnKind()
	{
		CMapCacheSource north = Coast(new[] { "~~~~~", "~~~~~", ".....", ".....", "....." });
		var kinds = new List<int>();

		CBeachMesh.PixelKinds(north, 2, 2, kinds);

		Assert.Equal(new[] { 3 }, kinds);
	}

	[Fact]
	public void ALoneWaterPixelSouthOfTheLandEndsTheList()
	{
		CMapCacheSource source = Coast(new[] { ".....", ".....", ".....", "..~..", "....." });
		var kinds = new List<int>();

		CBeachMesh.PixelKinds(source, 2, 2, kinds);

		Assert.Equal(new[] { 15 }, kinds);
	}

	[Fact]
	public void ASegmentIsSevenVerticesAndALinkTriangle()
	{
		CMapCacheSource source = Coast(new[] { "~~~~~", "~~~~~", ".....", ".....", "....." });
		var mesh = new CBeachMesh();
		mesh.Add(2, 2, 3, 0);

		mesh.Finish(source, (0, 0), new CBeachNoise());

		Assert.Equal(7, mesh.VertexCount);
		Assert.Equal(15 + 3, mesh.Indices.Count);
		// Alone, its link triangle closes on itself: the near index twice.
		Assert.Equal(new ushort[] { 0, 3, 3 }, mesh.Indices.Skip(15));
		// Its colour is the province under the pixel, its low byte in the third place.
		Assert.Equal(1, mesh.VertexBytes()[10]);
	}

	[Fact]
	public void TheSurfOfAStraightCoastIsItsKindAtTheWaterPixel()
	{
		CMapCacheSource source = Coast(new[] { "~~~~~", "~~~~~", ".....", ".....", "....." });
		var records = new List<CWavePixel>();

		CSurfRecords.Add(source, 2, 2, records);

		Assert.Equal(new[] { new CWavePixel(2, 3, 7) }, records);
		Assert.Equal((0f, 0.5f, 0f, 0.5f), CSurfRecords.OffsetsOf(7));
	}

	private static byte[] Texels(CTerrainIndexTexture texture)
	{
		using var stream = new MemoryStream();
		texture.Write(stream);
		return stream.ToArray()[128..];
	}

	private static byte Channel(CTerrainIndexTexture texture, byte[] texels, int x, int y, int channel)
	{
		return texels[(y * texture.Width + x) * 4 + channel];
	}

	[Fact]
	public void ATexelHoldsTheEntriesOfThePixelAndItsNeighbours()
	{
		CMapCacheSource source = Source(new[] { "0000", "0110", "0000" }, terrain: new byte[] { 0, 0, 0, 0, 0, 5, 6, 0, 0, 7, 8, 0 });
		var pixels = new CProvincePixels(source);

		CTerrainIndexTexture texture = CTerrainIndexTexture.Build(source, pixels, new[] { 1 });
		byte[] texels = Texels(texture);

		Assert.Equal(6, texture.Width);
		Assert.Equal(5, texture.Height);
		// The pixel (2, 1) is texel (3, 2): itself 6, up-left 7, left 5, up 8.
		Assert.Equal(6, Channel(texture, texels, 3, 2, 0));
		Assert.Equal(7, Channel(texture, texels, 3, 2, 1));
		Assert.Equal(5, Channel(texture, texels, 3, 2, 2));
		Assert.Equal(8, Channel(texture, texels, 3, 2, 3));
	}

	[Fact]
	public void ANeighbourWithNoPriorityTakesThePixelsOwn()
	{
		int[] priority = Enumerable.Repeat(-1, 256).ToArray();
		priority[5] = 9;
		CMapCacheSource source = Source(new[] { "0000", "0110", "0000" }, terrain: new byte[] { 0, 0, 0, 0, 0, 3, 5, 0, 0, 0, 0, 0 }, priority: priority);
		var pixels = new CProvincePixels(source);

		CTerrainIndexTexture texture = CTerrainIndexTexture.Build(source, pixels, new[] { 1 });
		byte[] texels = Texels(texture);

		Assert.Equal(9, Channel(texture, texels, 3, 2, 2));
		// A pixel of its own with no priority is 2.
		Assert.Equal(2, Channel(texture, texels, 2, 2, 0));
	}

	[Fact]
	public void AWaterPixelOfALandProvinceWritesNothing()
	{
		var water = new bool[256];
		water[4] = true;
		CMapCacheSource source = Source(new[] { "0000", "0100", "0000" }, terrain: new byte[] { 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0 }, waterIndex: water);
		var pixels = new CProvincePixels(source);

		CTerrainIndexTexture texture = CTerrainIndexTexture.Build(source, pixels, new[] { 1 });

		Assert.All(Texels(texture), value => Assert.Equal(0xFF, value));
	}

	[Fact]
	public void ATexelBesideLandKeepsTheFirstValueOfferedAndTheFirstRowAndColumnNone()
	{
		CMapCacheSource source = Source(new[] { "0000", "0110", "0000" }, terrain: new byte[] { 0, 0, 0, 0, 0, 5, 6, 0, 0, 0, 0, 0 });
		var pixels = new CProvincePixels(source);

		CTerrainIndexTexture texture = CTerrainIndexTexture.Build(source, pixels, new[] { 1 });
		byte[] texels = Texels(texture);

		// North of the two pixels, the west's 5 came first and stays under the east's 6.
		Assert.Equal(5, Channel(texture, texels, 2, 3, 0));
		Assert.Equal(0xFF, Channel(texture, texels, 0, 2, 0));
		Assert.Equal(0xFF, Channel(texture, texels, 2, 0, 0));
	}

	[Fact]
	public void TheTexturesHeadIsD3dxs()
	{
		CMapCacheSource source = Source(new[] { "1" });
		var pixels = new CProvincePixels(source);
		using var stream = new MemoryStream();

		CTerrainIndexTexture.Build(source, pixels, new[] { 1 }).Write(stream);
		byte[] file = stream.ToArray();

		Assert.Equal("DDS ", System.Text.Encoding.ASCII.GetString(file, 0, 4));
		Assert.Equal(128 + 5 * 5 * 4, file.Length);
		Assert.Equal(new uint[] { 124, 0x1007, 5, 5 }, Enumerable.Range(1, 4).Select(i => BitConverter.ToUInt32(file, i * 4)));
		Assert.Equal(0x41u, BitConverter.ToUInt32(file, 80));
		Assert.Equal(0xFF000000u, BitConverter.ToUInt32(file, 104));
		Assert.Equal(0x1002u, BitConverter.ToUInt32(file, 108));
	}

	[Fact]
	public void AProvinceGoesInTheTileOfItsCentreAndTheListIsByIds()
	{
		string[] rows = Enumerable.Repeat(new string('0', 120), 3).ToArray();
		rows[1] = "2" + new string('0', 58) + "1" + new string('0', 60);
		CMapCacheSource source = Source(rows);
		var pixels = new CProvincePixels(source);

		var tiles = new CCacheTiles(source, pixels);

		Assert.Equal(3, tiles.Columns);
		Assert.Equal(new[] { 1 }, tiles.Provinces(1));
		Assert.Equal(new[] { 2 }, tiles.Provinces(0));
		Assert.Equal((75, 25), tiles.Middle(1));
	}

	private void WriteMap()
	{
		string map = Path.Combine(_root, "install", "map");
		Directory.CreateDirectory(map);
		File.WriteAllText(Path.Combine(map, "default.map"), "max_provinces = 3\nsea_starts = { 2 }\n");
		File.WriteAllText(Path.Combine(map, "definition.csv"), "province;red;green;blue;x;x\n1;10;0;0;x;x\n2;20;0;0;x;x\n");
		File.WriteAllText(Path.Combine(map, "adjacencies.csv"), "From;To;Type;Through;Comment\n");
		File.WriteAllText(Path.Combine(map, "positions.txt"), "");
		File.WriteAllText(Path.Combine(map, "terrain.txt"), "terrain = 256\ncategories = { plains = { color = { 1 1 1 } } ocean = { color = { 2 2 2 } is_water = yes } }\nland = { type = plains color = { 0 } priority = 7 }\nsea = { type = ocean color = { 1 } }\n");
		File.WriteAllText(Path.Combine(map, "continent.txt"), "");
		File.WriteAllText(Path.Combine(map, "region.txt"), "");
		File.WriteAllBytes(Path.Combine(map, "provinces.bmp"), Bmp24(new byte[] { 10, 10, 20 }));
		File.WriteAllBytes(Path.Combine(map, "terrain.bmp"), Bmp8(new byte[] { 0, 0, 1 }));
		File.WriteAllBytes(Path.Combine(map, "rivers.bmp"), Bmp8(new byte[] { 255, 255, 255 }));
		File.WriteAllBytes(Path.Combine(map, "climate.bmp"), Bmp8(new byte[] { 100, 100, 100 }));
	}

	private static byte[] Bmp24(byte[] reds)
	{
		var pixels = new byte[(reds.Length * 3 + 3) & ~3];
		for (int i = 0; i < reds.Length; i++)
		{
			pixels[i * 3 + 2] = reds[i];
		}
		return Bmp(reds.Length, 24, 54, Array.Empty<byte>(), pixels);
	}

	private static byte[] Bmp8(byte[] indices)
	{
		var pixels = new byte[(indices.Length + 3) & ~3];
		indices.CopyTo(pixels, 0);
		return Bmp(indices.Length, 8, 54 + 1024, new byte[1024], pixels);
	}

	private static byte[] Bmp(int width, int bits, int offset, byte[] palette, byte[] pixels)
	{
		var file = new byte[offset + pixels.Length];
		file[0] = (byte)'B';
		file[1] = (byte)'M';
		BitConverter.GetBytes(offset).CopyTo(file, 10);
		BitConverter.GetBytes(40).CopyTo(file, 14);
		BitConverter.GetBytes(width).CopyTo(file, 18);
		BitConverter.GetBytes(1).CopyTo(file, 22);
		BitConverter.GetBytes((short)bits).CopyTo(file, 28);
		palette.CopyTo(file, 54);
		pixels.CopyTo(file, offset);
		return file;
	}

	private CDirectorySettings Install => CDirectorySettings.At(Path.Combine(_root, "install"));

	private string User => Path.Combine(_root, "user");

	private string UserCache => Path.Combine(User, "map", "cache");

	[Fact]
	public void AMapWithNoCacheInThePlayersFolderIsBuiltThereOnceAndReadBack()
	{
		WriteMap();
		CMap map = CMap.Load(Install, _ => { });

		CMapCache cache = CMapCache.OpenOrBuild(Install, User, map, _ => { });

		Assert.True(cache.IsCurrent);
		Assert.Equal(Path.Combine(UserCache, CQuadCache.IndexFile), cache.Find(CQuadCache.IndexFile));
		Assert.Single(CQuadCache.Load(cache, problem => Assert.Fail(problem)));
		Assert.False(Directory.Exists(Path.Combine(_root, "install", "map", "cache")));

		DateTime first = File.GetLastWriteTimeUtc(cache.Find(CQuadCache.QuadsFile)!);
		CMapCache.OpenOrBuild(Install, User, map, _ => { });
		Assert.Equal(first, File.GetLastWriteTimeUtc(cache.Find(CQuadCache.QuadsFile)!));
	}

	[Fact]
	public void WhatIsBuiltIsTheWholeFolderUnderTheOriginalsStamp()
	{
		WriteMap();

		CMapCache.OpenOrBuild(Install, User, CMap.Load(Install, _ => { }), _ => { });

		Assert.Equal(BitConverter.GetBytes(CMapCache.Stamp(Install)), File.ReadAllBytes(Path.Combine(UserCache, CMapCache.StampFile)));
		foreach (string name in new[]
			{
				CMapDataFiles.AdjacenciesFile, CMapDataFiles.NavalFile, CMapDataFiles.ClimateFile, CMapDataFiles.ProvincesFile, CMapDataFiles.FormatFile,
				CMapBin.File, CBorderCache.BitmapFile, CBorderCache.PairsFile, CQuadCache.IndexFile, CQuadCache.QuadsFile,
			})
		{
			Assert.True(File.Exists(Path.Combine(UserCache, name)), name);
		}
	}

	[Fact]
	public void AWayIsItsFiveNumbers()
	{
		using var stream = new MemoryStream();
		var edges = new[]
		{
			Array.Empty<OpenHOI3.Sim.Provinces.CProvinceEdge>(),
			new[] { new OpenHOI3.Sim.Provinces.CProvinceEdge(7, OpenHOI3.Sim.Provinces.EProvinceEdgeKind.River, 3, new CFixedPoint(1234)) { Bearing = 750 } },
		};

		CMapDataFiles.WriteAdjacencies(stream, edges);

		byte[] file = stream.ToArray();
		Assert.Equal(new[] { 0, 1, 2, 7, 3, 1234, 750 }, Enumerable.Range(0, 7).Select(i => BitConverter.ToInt32(file, i * 4)));
	}

	[Fact]
	public void TheProvinceRasterIsABitmapFromTheNorthWithTheOriginalsSizeSlip()
	{
		using var stream = new MemoryStream();

		CMapDataFiles.WriteProvinceRaster(stream, 3, 2, new ushort[] { 1, 2, 3, 4, 5, 6 });

		byte[] file = stream.ToArray();
		Assert.Equal(54 + 12, file.Length);
		Assert.Equal("BM", System.Text.Encoding.ASCII.GetString(file, 0, 2));
		Assert.Equal(3 + 3 * 2 + 110, BitConverter.ToInt32(file, 2));
		Assert.Equal(16, BitConverter.ToInt16(file, 28));
		Assert.Equal(new ushort[] { 4, 5, 6, 1, 2, 3 }, Enumerable.Range(0, 6).Select(i => BitConverter.ToUInt16(file, 54 + i * 2)));
	}

	[Fact]
	public void TheFormatFileIsTheTwoDeclarationsTheSecondCountedOneLong()
	{
		using var stream = new MemoryStream();

		CMapDataFiles.WriteQuadFormat(stream);

		byte[] file = stream.ToArray();
		Assert.Equal(68, file.Length);
		Assert.Equal(3, BitConverter.ToInt32(file, 0));
		Assert.Equal(4, BitConverter.ToInt32(file, 28));
		Assert.Equal(1, BitConverter.ToInt32(file, 64));
		// Each element: method, offset, stream, type, usage, index. The land's second is SHORT2 at 4, a texture coordinate.
		Assert.Equal(new byte[] { 0, 4, 0, 0, 0, 6, 5, 0 }, file[12..20]);
		// The end marker, stream 255 and type unused.
		Assert.Equal(new byte[] { 0, 0, 0, 0xFF, 0, 0x11, 0, 0 }, file[20..28]);
	}

	[Fact]
	public void TheInstallsCacheIsNeverAskedEvenWhereItIsTheMaps()
	{
		WriteMap();
		string installed = Path.Combine(_root, "install", "map", "cache");
		Directory.CreateDirectory(installed);
		File.WriteAllBytes(Path.Combine(installed, CMapCache.StampFile), BitConverter.GetBytes(CMapCache.Stamp(Install)));
		File.WriteAllBytes(Path.Combine(installed, "only_in_the_install.bin"), new byte[4]);

		CMapCache cache = CMapCache.OpenOrBuild(Install, User, CMap.Load(Install, _ => { }), _ => { });

		Assert.StartsWith(User, cache.Find(CQuadCache.IndexFile));
		Assert.Null(cache.Find("only_in_the_install.bin"));
	}

	/// <summary>
	/// The stamp alone makes no cache: the original keeps part of one in the install and part in the player's folder, and
	/// a player's folder with the map's stamp and half the files drew a broken map. A cache with no list of its contents
	/// is built again, whole.
	/// </summary>
	[Fact]
	public void ACacheWithTheMapsStampAndNoListOfItsContentsIsBuiltAgainWhole()
	{
		WriteMap();
		Directory.CreateDirectory(UserCache);
		File.WriteAllBytes(Path.Combine(UserCache, CMapCache.StampFile), BitConverter.GetBytes(CMapCache.Stamp(Install)));

		CMapCache cache = CMapCache.OpenOrBuild(Install, User, CMap.Load(Install, _ => { }), _ => { });

		Assert.True(cache.IsCurrent);
		Assert.StartsWith(User, cache.Find(CQuadCache.IndexFile));
		Assert.True(File.Exists(Path.Combine(UserCache, CMapCache.ContentsFile)));
	}

	[Fact]
	public void AWholeCacheIsKeptAndOneWithAFileGoneOrCutIsBuiltAgain()
	{
		WriteMap();
		CMap map = CMap.Load(Install, _ => { });
		CMapCache.OpenOrBuild(Install, User, map, _ => { });
		string index = Path.Combine(UserCache, CQuadCache.IndexFile);
		DateTime built = File.GetLastWriteTimeUtc(index);

		// Whole: nothing is built again.
		var said = new System.Collections.Generic.List<string>();
		CMapCache.OpenOrBuild(Install, User, map, said.Add);
		Assert.Empty(said);
		Assert.Equal(built, File.GetLastWriteTimeUtc(index));

		// A file gone: built again.
		File.Delete(index);
		CMapCache.OpenOrBuild(Install, User, map, said.Add);
		Assert.Single(said);
		Assert.True(File.Exists(index));

		// A file cut short: built again.
		long whole = new FileInfo(index).Length;
		File.WriteAllBytes(index, new byte[1]);
		CMapCache.OpenOrBuild(Install, User, map, said.Add);
		Assert.Equal(2, said.Count);
		Assert.Equal(whole, new FileInfo(index).Length);
	}

	[Fact]
	public void AStaleCacheInThePlayersFolderIsBuiltOverAndABuildCutShortIsNotRead()
	{
		WriteMap();
		Directory.CreateDirectory(UserCache);
		File.WriteAllBytes(Path.Combine(UserCache, CMapCache.StampFile), BitConverter.GetBytes(0x12345678u));
		File.WriteAllBytes(Path.Combine(UserCache, CQuadCache.IndexFile), new byte[12]);

		CMapCache cache = CMapCache.OpenOrBuild(Install, User, CMap.Load(Install, _ => { }), _ => { });

		Assert.Single(CQuadCache.Load(cache, problem => Assert.Fail(problem)));
		Assert.Equal(BitConverter.GetBytes(CMapCache.Stamp(Install)), File.ReadAllBytes(Path.Combine(UserCache, CMapCache.StampFile)));
	}
}
