using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenHOI3.Content.Clausewitz;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Content.Map;

/// <summary>
/// The map as geography: which province every point belongs to, and which provinces
/// touch. None of it changes during a game, which is why it is not part of the game
/// state. The rules are in <c>docs/mechanics/map.md</c>.
///
/// <b>y counts north.</b> Row 0 of the raster is the far south, as it is in the
/// original, so a coordinate from <c>positions.txt</c> indexes it directly. Anything
/// that draws the map top-down has to flip it, and <see cref="DisplayRow"/> exists so
/// that nobody does that arithmetic twice.
/// </summary>
public sealed class CMap
{
	private const string DefinitionFile = "map/default.map";

	private readonly ushort[] _raster;
	private readonly bool[] _isSea;

	private CMap(int width, int height, ushort[] raster, bool[] isSea,
		CProvinceShape[] shapes, int[][] neighbours, CExplicitAdjacency[] explicitAdjacencies,
		CTerrain[] terrains, CTerrain?[] terrain, string[] continents, CMapPositions positions,
		CProvinceEdge[][] edges, string[] sourceFiles)
	{
		Width = width;
		Height = height;
		_raster = raster;
		_isSea = isSea;
		Shapes = shapes;
		Neighbours = neighbours;
		ExplicitAdjacencies = explicitAdjacencies;
		Terrains = terrains;
		Terrain = terrain;
		Continents = continents;
		Positions = positions.Unit;
		TextPositions = positions.Text;
		NavalBasePositions = positions.NavalBase;
		Edges = edges;
		SourceFiles = sourceFiles;
	}

	public int Width { get; }

	public int Height { get; }

	/// <summary>One more than the highest province id, because id 0 is an entry too.</summary>
	public int ProvinceCount => _isSea.Length;

	/// <summary>Per province id. An id with no pixel has an empty shape.</summary>
	public IReadOnlyList<CProvinceShape> Shapes { get; }

	/// <summary>Per province id, the ids it touches, ascending.</summary>
	public IReadOnlyList<int[]> Neighbours { get; }

	public IReadOnlyList<CExplicitAdjacency> ExplicitAdjacencies { get; }

	/// <summary>The terrain categories, in the order the definition file declares them.</summary>
	public IReadOnlyList<CTerrain> Terrains { get; }

	/// <summary>
	/// Per province id, the terrain the map gives it - the one painted most around
	/// where its name is written - or null where nothing known is painted. A province's
	/// history file may say otherwise.
	/// </summary>
	public IReadOnlyList<CTerrain?> Terrain { get; }

	/// <summary>
	/// Per province id, the ways out of it: to which neighbour, of what kind, and how
	/// far. In the original's order, which is the order a route is searched in.
	/// </summary>
	public IReadOnlyList<CProvinceEdge[]> Edges { get; }

	/// <summary>
	/// Per province id, the name of the continent it is on, or an empty string for a
	/// province no continent lists - every sea province, for one.
	/// </summary>
	public IReadOnlyList<string> Continents { get; }

	/// <summary>
	/// Per province id, the one point that stands for it: where a unit there is drawn.
	/// The centre of its bounding box unless <c>positions.txt</c> says otherwise.
	/// </summary>
	public IReadOnlyList<CMapPosition> Positions { get; }

	/// <summary>
	/// Per province id, where its name is written: <c>text_position</c>, or the centre
	/// of its bounding box. Its terrain is taken around this point.
	/// </summary>
	public IReadOnlyList<CMapPosition> TextPositions { get; }

	/// <summary>
	/// Per province id, where its port is drawn: <c>naval_base</c>, or the centre of its
	/// bounding box. The land end of a distance between land and sea.
	/// </summary>
	public IReadOnlyList<CMapPosition> NavalBasePositions { get; }

	/// <summary>Every file this was built from, for whatever needs to know when one changes.</summary>
	public IReadOnlyList<string> SourceFiles { get; }

	/// <summary>The id raster, <c>[y * Width + x]</c> with y counting north.</summary>
	public ReadOnlySpan<ushort> Raster => _raster;

	public bool IsSea(int provinceId)
	{
		return provinceId > 0 && provinceId < _isSea.Length && _isSea[provinceId];
	}

	/// <summary>
	/// The province at a point, or 0. x wraps, because the map is a cylinder; y does
	/// not, and a point above or below the map belongs to nothing.
	/// </summary>
	public int ProvinceAt(int x, int y)
	{
		if (y < 0 || y >= Height)
		{
			return 0;
		}
		int wrapped = ((x % Width) + Width) % Width;
		return _raster[y * Width + wrapped];
	}

	/// <summary>The row a north-counting y falls on when the map is drawn with north at the top.</summary>
	public int DisplayRow(int y)
	{
		return Height - 1 - y;
	}

	public static CMap Load(CDirectorySettings directories, Action<string>? warn = null)
	{
		warn ??= CDiagnostics.Warn;

		string definitionPath = directories.Resolve(DefinitionFile);
		ClausewitzNode definition = ClausewitzParser.ParseFile(definitionPath);

		string provincesCsv = directories.Resolve("map/" + definition.GetString("definitions", "definition.csv"));
		string provincesBmp = directories.Resolve("map/" + definition.GetString("provinces", "provinces.bmp"));
		string adjacenciesCsv = directories.Resolve("map/" + definition.GetString("adjacencies", "adjacencies.csv"));

		Dictionary<int, ushort> idByColour = ReadDefinitions(provincesCsv, out int highestId, warn);

		int declared = definition.GetInt("max_provinces", highestId + 1);
		if (declared != highestId + 1)
		{
			warn($"'{definitionPath}' declares max_provinces = {declared}, " +
				$"but '{provincesCsv}' runs to id {highestId}; using {highestId + 1}");
		}
		int provinceCount = highestId + 1;

		var isSea = new bool[provinceCount];
		ClausewitzNode? seaStarts = definition.Get("sea_starts");
		if (seaStarts is not null)
		{
			foreach (ClausewitzNode entry in seaStarts.Children)
			{
				if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
					&& id > 0 && id < provinceCount)
				{
					isSea[id] = true;
				}
			}
		}

		CBitmapFile bitmap = CBitmapFile.Read(provincesBmp);
		if (bitmap.BitsPerPixel != 24)
		{
			throw new InvalidDataException($"'{provincesBmp}' must be 24 bits per pixel, not {bitmap.BitsPerPixel}");
		}

		ushort[] raster = BuildRaster(bitmap, idByColour, out int unmatchedPixels, out int unmatchedColours);
		if (unmatchedPixels > 0)
		{
			warn($"'{provincesBmp}': {unmatchedPixels} pixels in {unmatchedColours} colours match no row of " +
				$"'{Path.GetFileName(provincesCsv)}' and belong to no province");
		}

		CProvinceShape[] shapes = MeasureShapes(raster, bitmap.Width, bitmap.Height, provinceCount);
		int empty = 0;
		for (int id = 1; id < provinceCount; id++)
		{
			if (shapes[id].IsEmpty)
			{
				empty++;
			}
		}
		if (empty > 0)
		{
			warn($"'{provincesBmp}': {empty} provinces have no pixel at all");
		}

		List<HashSet<int>> touching = FindRasterAdjacency(raster, bitmap.Width, bitmap.Height, provinceCount);
		CExplicitAdjacency[] explicitAdjacencies = ReadExplicitAdjacencies(adjacenciesCsv, provinceCount, warn);
		foreach (CExplicitAdjacency adjacency in explicitAdjacencies)
		{
			touching[adjacency.From].Add(adjacency.To);
			touching[adjacency.To].Add(adjacency.From);
		}

		var neighbours = new int[provinceCount][];
		for (int id = 0; id < provinceCount; id++)
		{
			var list = new int[touching[id].Count];
			touching[id].CopyTo(list);
			Array.Sort(list);
			neighbours[id] = list;
		}

		string positionsTxt = directories.Resolve("map/" + definition.GetString("positions", "positions.txt"));
		CMapPositions positions = CPositionsReader.Read(positionsTxt, shapes, id => isSea[id], warn);

		string terrainTxt = directories.Resolve("map/" + definition.GetString("terrain_definition", "terrain.txt"));
		string terrainBmp = directories.Resolve("map/" + definition.GetString("terrain", "terrain.bmp"));
		string riversBmp = directories.Resolve("map/" + definition.GetString("rivers", "rivers.bmp"));
		CTerrain[] terrains = Array.Empty<CTerrain>();
		var terrain = new CTerrain?[provinceCount];
		CBitmapFile? terrainBitmap = null;
		CTerrainReader.Definition terrainDefinition = default;
		if (File.Exists(terrainTxt) && File.Exists(terrainBmp))
		{
			terrainDefinition = CTerrainReader.ReadDefinition(terrainTxt, warn);
			terrains = terrainDefinition.Categories;
			terrainBitmap = CBitmapFile.Read(terrainBmp);
			if (terrainBitmap.BitsPerPixel != 8
				|| terrainBitmap.Width != bitmap.Width || terrainBitmap.Height != bitmap.Height)
			{
				warn($"'{terrainBmp}' must be 8 bits per pixel and {bitmap.Width} x {bitmap.Height} like the province " +
					"bitmap; no province was given a terrain and no distance was measured");
				terrainBitmap = null;
			}
			else
			{
				terrain = CTerrainReader.FindTerrain(terrainBitmap, raster, terrainDefinition, positions,
					id => isSea[id]);
				int unknown = CTerrainReader.CountUnknown(terrainBitmap, terrainDefinition);
				if (unknown > 0)
				{
					warn($"'{terrainBmp}': {unknown} pixels use a palette index that '{Path.GetFileName(terrainTxt)}' " +
						"gives no terrain");
				}
			}
		}
		else
		{
			warn($"no terrain: '{terrainTxt}' or '{terrainBmp}' is missing");
		}

		CBitmapFile? riverBitmap = null;
		if (File.Exists(riversBmp))
		{
			riverBitmap = CBitmapFile.Read(riversBmp);
			if (riverBitmap.BitsPerPixel != 8 || riverBitmap.Width != bitmap.Width || riverBitmap.Height != bitmap.Height)
			{
				warn($"'{riversBmp}' must be 8 bits per pixel and {bitmap.Width} x {bitmap.Height} like the province " +
					"bitmap; no river was read from it");
				riverBitmap = null;
			}
		}
		else
		{
			warn($"no rivers: '{riversBmp}' is missing");
		}

		CProvinceEdge[][] edges = CMapGraph.Build(raster, bitmap.Width, bitmap.Height, provinceCount,
			explicitAdjacencies, id => isSea[id], positions, terrainBitmap, terrainDefinition, riverBitmap);

		string continentTxt = directories.Resolve("map/" + definition.GetString("continent", "continent.txt"));
		string[] continents = ReadContinents(continentTxt, provinceCount, warn);

		return new CMap(bitmap.Width, bitmap.Height, raster, isSea, shapes, neighbours,
			explicitAdjacencies, terrains, terrain, continents, positions, edges,
			new[]
			{
				definitionPath, provincesCsv, provincesBmp, adjacenciesCsv, terrainTxt, terrainBmp, riversBmp,
				continentTxt, positionsTxt,
			});
	}

	/// <summary>
	/// Reads the continents: one block each, named, listing the provinces on it. A
	/// province listed twice keeps the first continent that named it.
	/// </summary>
	private static string[] ReadContinents(string path, int provinceCount, Action<string> warn)
	{
		var continents = new string[provinceCount];
		Array.Fill(continents, "");
		if (!File.Exists(path))
		{
			warn($"no continents: '{path}' is missing");
			return continents;
		}

		int unknown = 0;
		int repeated = 0;
		foreach (ClausewitzNode block in ClausewitzParser.ParseFile(path).Children)
		{
			if (block.Key is null || !block.IsBlock)
			{
				continue;
			}
			foreach (ClausewitzNode entry in block.Children)
			{
				if (!int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
					|| id <= 0 || id >= provinceCount)
				{
					unknown++;
				}
				else if (continents[id].Length != 0)
				{
					repeated++;
				}
				else
				{
					continents[id] = block.Key;
				}
			}
		}
		if (unknown > 0)
		{
			warn($"'{path}': {unknown} entries are not the id of a province");
		}
		if (repeated > 0)
		{
			warn($"'{path}': {repeated} provinces are listed on more than one continent; the first was kept");
		}
		return continents;
	}

	private static int ColourKey(int red, int green, int blue)
	{
		return (red << 16) | (green << 8) | blue;
	}

	/// <summary>
	/// Reads <c>id;red;green;blue;name;x</c>. A row with a colour and no id is not a
	/// province - the shipped file ends with 59 of them - and is skipped silently.
	/// </summary>
	private static Dictionary<int, ushort> ReadDefinitions(string path, out int highestId,
		Action<string> warn)
	{
		var idByColour = new Dictionary<int, ushort>();
		var seen = new HashSet<int>();
		highestId = 0;
		int malformed = 0;
		int duplicateColours = 0;

		string[] lines = Encoding.Latin1.GetString(File.ReadAllBytes(path)).Split('\n');
		for (int i = 1; i < lines.Length; i++)                 // row 0 is the header
		{
			string line = lines[i].TrimEnd('\r');
			if (line.Length == 0)
			{
				continue;
			}
			string[] columns = line.Split(';');
			if (columns.Length < 4 || columns[0].Trim().Length == 0)
			{
				continue;
			}
			if (!int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
				|| !int.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int red)
				|| !int.TryParse(columns[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int green)
				|| !int.TryParse(columns[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int blue)
				|| id <= 0 || id > ushort.MaxValue)
			{
				malformed++;
				continue;
			}
			seen.Add(id);
			highestId = Math.Max(highestId, id);
			if (!idByColour.TryAdd(ColourKey(red, green, blue), (ushort)id))
			{
				duplicateColours++;
			}
		}

		if (malformed > 0)
		{
			warn($"'{path}': {malformed} rows could not be read");
		}
		if (duplicateColours > 0)
		{
			warn($"'{path}': {duplicateColours} rows repeat an earlier row's colour and can never be matched");
		}
		if (seen.Count != highestId)
		{
			warn($"'{path}': ids run to {highestId} but only {seen.Count} are defined, so there are gaps");
		}
		return idByColour;
	}

	/// <summary>
	/// Turns the bitmap into province ids. The file's first row is the northernmost,
	/// so it becomes the raster's last.
	/// </summary>
	private static ushort[] BuildRaster(CBitmapFile bitmap, Dictionary<int, ushort> idByColour,
		out int unmatchedPixels, out int unmatchedColours)
	{
		int width = bitmap.Width;
		int height = bitmap.Height;
		var raster = new ushort[width * height];
		var unmatched = new HashSet<int>();
		unmatchedPixels = 0;

		// Neighbouring pixels are nearly always the same province, so remembering the
		// last colour saves almost every dictionary lookup.
		int lastKey = -1;
		ushort lastId = 0;

		for (int stored = 0; stored < height; stored++)
		{
			ReadOnlySpan<byte> row = bitmap.StoredRow(stored);
			int y = height - 1 - stored;
			Span<ushort> target = raster.AsSpan(y * width, width);
			for (int x = 0; x < width; x++)
			{
				int key = ColourKey(row[x * 3 + 2], row[x * 3 + 1], row[x * 3]);
				if (key != lastKey)
				{
					lastKey = key;
					if (!idByColour.TryGetValue(key, out lastId))
					{
						lastId = 0;
						unmatched.Add(key);
					}
				}
				if (lastId == 0)
				{
					unmatchedPixels++;
				}
				target[x] = lastId;
			}
		}

		unmatchedColours = unmatched.Count;
		return raster;
	}

	private static CProvinceShape[] MeasureShapes(ushort[] raster, int width, int height, int provinceCount)
	{
		var minX = new int[provinceCount];
		var minY = new int[provinceCount];
		var maxX = new int[provinceCount];
		var maxY = new int[provinceCount];
		var count = new int[provinceCount];
		Array.Fill(minX, int.MaxValue);
		Array.Fill(minY, int.MaxValue);
		Array.Fill(maxX, int.MinValue);
		Array.Fill(maxY, int.MinValue);

		for (int y = 0; y < height; y++)
		{
			ReadOnlySpan<ushort> row = raster.AsSpan(y * width, width);
			for (int x = 0; x < width; x++)
			{
				int id = row[x];
				if (id >= provinceCount)
				{
					continue;
				}
				count[id]++;
				if (x < minX[id]) { minX[id] = x; }
				if (x > maxX[id]) { maxX[id] = x; }
				if (y < minY[id]) { minY[id] = y; }
				if (y > maxY[id]) { maxY[id] = y; }
			}
		}

		var shapes = new CProvinceShape[provinceCount];
		for (int id = 0; id < provinceCount; id++)
		{
			shapes[id] = count[id] == 0
				? default
				: new CProvinceShape(minX[id], minY[id], maxX[id], maxY[id], count[id]);
		}
		return shapes;
	}

	/// <summary>
	/// Two provinces touch when two of their pixels sit side by side or one above the
	/// other; diagonals do not count. The last column is beside the first, because the
	/// map is a cylinder - the one part of this the original has not been read for.
	/// </summary>
	private static List<HashSet<int>> FindRasterAdjacency(ushort[] raster, int width, int height,
		int provinceCount)
	{
		var touching = new List<HashSet<int>>(provinceCount);
		for (int id = 0; id < provinceCount; id++)
		{
			touching.Add(new HashSet<int>());
		}

		// A border is many pixels long, so the same pair comes up again and again.
		int lastA = 0;
		int lastB = 0;
		void Touch(int a, int b)
		{
			if (a == b || a == 0 || b == 0 || (a == lastA && b == lastB))
			{
				return;
			}
			lastA = a;
			lastB = b;
			touching[a].Add(b);
			touching[b].Add(a);
		}

		for (int y = 0; y < height; y++)
		{
			int rowStart = y * width;
			for (int x = 0; x < width; x++)
			{
				int here = raster[rowStart + x];
				Touch(here, raster[rowStart + (x + 1 == width ? 0 : x + 1)]);
				if (y + 1 < height)
				{
					Touch(here, raster[rowStart + width + x]);
				}
			}
		}
		return touching;
	}

	/// <summary>Reads <c>From;To;Type;Through;Comment</c>, which ends with a row of -1s.</summary>
	private static CExplicitAdjacency[] ReadExplicitAdjacencies(string path, int provinceCount,
		Action<string> warn)
	{
		var result = new List<CExplicitAdjacency>();
		if (!File.Exists(path))
		{
			return result.ToArray();
		}

		int malformed = 0;
		string[] lines = Encoding.Latin1.GetString(File.ReadAllBytes(path)).Split('\n');
		for (int i = 1; i < lines.Length; i++)                 // row 0 is the header
		{
			string line = lines[i].TrimEnd('\r');
			if (line.Trim().Length == 0)
			{
				continue;
			}
			string[] columns = line.Split(';');
			if (columns.Length < 3
				|| !int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int from)
				|| !int.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int to))
			{
				malformed++;
				continue;
			}
			if (from < 0 && to < 0)
			{
				continue;                                     // the terminating row
			}
			if (from <= 0 || to <= 0 || from >= provinceCount || to >= provinceCount)
			{
				malformed++;
				continue;
			}
			int through = columns.Length > 3
				&& int.TryParse(columns[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
				&& parsed > 0 && parsed < provinceCount
				? parsed
				: 0;
			result.Add(new CExplicitAdjacency(from, to, columns[2].Trim(), through));
		}

		if (malformed > 0)
		{
			warn($"'{path}': {malformed} rows could not be read");
		}
		return result.ToArray();
	}
}
