using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
using OpenHOI3.Content.Settings;
using OpenHOI3.Interface.Map;
using OpenHOI3.Sim.Provinces;

/// <summary>
/// Reads the install's quad cache - the map's meshes as the original draws them - and says what it holds:
///
///   OpenHOI3.SaveCheck quads &lt;install&gt;
///
/// every record read to its stated size, and the tiles, land vertices and triangles, beach triangles and border vertices
/// counted, with the extent of the tile-local coordinates and the province ids met - to set beside
/// <c>reversing/findings/FINDINGS-mapdraw.md</c>, A.1. See <c>docs/mechanics/mapdraw.md</c>, section 2.
/// </summary>
public static class CQuadCheck
{
	public static int Run(string[] args)
	{
		if (args.Length < 2)
		{
			Console.Error.WriteLine("quads: <install>");
			return 2;
		}
		var problems = new List<string>();
		CDirectorySettings? directories = CToolDirectories.Make(args[1], ref args);
		if (directories is null)
		{
			return 2;
		}
		var watch = System.Diagnostics.Stopwatch.StartNew();
		CMapCache cache = CMapCache.Open(directories, directories.UserFolder, problems.Add);
		Console.WriteLine($"cache {(cache.IsCurrent ? "current" : "stale")}, the stamp {CMapCache.Stamp(directories):X8} "
			+ $"checked in {watch.ElapsedMilliseconds} ms; the index from {cache.Find(CQuadCache.IndexFile) ?? "nowhere"}");
		IReadOnlyList<CQuadTile> tiles = CQuadCache.Load(cache, problems.Add);
		foreach (string problem in problems)
		{
			Console.WriteLine("problem: " + problem);
		}
		long landVertices = tiles.Sum(t => (long)t.LandProvinces.Length);
		long landTriangles = tiles.Sum(t => (long)t.LandIndices.Length / 3);
		long beachTriangles = tiles.Sum(t => (long)t.BeachIndices.Length / 3);
		long borderVertices = tiles.Sum(t => (long)t.BorderColours.Length);
		int beachTiles = tiles.Count(t => t.BeachIndices.Length > 0);
		var provinces = new HashSet<ushort>(tiles.SelectMany(t => t.LandProvinces));
		short low = tiles.SelectMany(t => t.LandPositions).DefaultIfEmpty().Min();
		short high = tiles.SelectMany(t => t.LandPositions).DefaultIfEmpty().Max();
		int outOfRange = tiles.Sum(t => t.LandIndices.Count(i => i >= t.LandProvinces.Length));
		int mixed = tiles.Sum(t => Enumerable.Range(0, t.LandIndices.Length / 3).Count(k =>
			t.LandProvinces[t.LandIndices[3 * k]] != t.LandProvinces[t.LandIndices[3 * k + 1]]
			|| t.LandProvinces[t.LandIndices[3 * k]] != t.LandProvinces[t.LandIndices[3 * k + 2]]));
		Console.WriteLine($"{tiles.Count} tiles read whole; {landVertices} land vertices, {landTriangles} land triangles, " +
			$"{provinces.Count} provinces; {beachTriangles} beach triangles in {beachTiles} tiles; {borderVertices} border vertices");
		Console.WriteLine($"tile-local coordinates {low} to {high}; {outOfRange} indices out of range; {mixed} triangles mixing provinces");

		// Each tile's land rectangle as the mesh gives it, against its terrain-index texture: the rectangle and 4.
		int columns = CQuadCache.Columns(directories);
		int fits = 0;
		int differ = 0;
		foreach (CQuadTile tile in tiles)
		{
			if (cache.Find(CQuadCache.TextureFile(tile.Index)) is not { } path)
			{
				continue;
			}
			byte[] head = new byte[20];
			using (var file = System.IO.File.OpenRead(path))
			{
				file.ReadExactly(head);
			}
			int height = BitConverter.ToInt32(head, 12);
			int width = BitConverter.ToInt32(head, 16);
			(int _, int _, int w, int h) = tile.LandRectangle(columns);
			if (w + 4 == width && h + 4 == height)
			{
				fits++;
			}
			else if (differ++ < 5)
			{
				Console.WriteLine($"tile {tile.Index}: rectangle {w}x{h}, texture {width}x{height}");
			}
		}
		Console.WriteLine($"land rectangles and 4 the terrain-index texture's size: {fits} of {fits + differ}");
		CheckSurf(tiles, columns);
		CMap map = CMap.Load(directories, _ => { });
		CheckRivers(map, cache);
		CheckTrees(map);
		CheckBorders(map, tiles, columns);
		return problems.Count == 0 && outOfRange == 0 ? 0 : 1;
	}

	/// <summary>
	/// The surf's quads made from every tile's wave pixels (<c>FINDINGS-mapdraw.md</c>, C.7): how many, the range of their
	/// half-pixel coordinates and how many of the records' pixels the terrain paints as water.
	/// </summary>
	private static void CheckSurf(IReadOnlyList<CQuadTile> tiles, int columns)
	{
		int records = 0, vertices = 0, skipped = 0;
		short lowX = short.MaxValue, highX = short.MinValue, lowZ = short.MaxValue, highZ = short.MinValue;
		foreach (CQuadTile tile in tiles)
		{
			CWavePixel[] pixels = CWavePixel.Parse(tile.ShoreRecords);
			(int midX, int midZ) = tile.Middle(columns);
			List<short> shorts = CSurfQuads.Vertices(pixels, midX, midZ, out int bad);
			records += pixels.Length;
			skipped += bad;
			vertices += shorts.Count / 4;
			for (int i = 0; i < shorts.Count; i += 4)
			{
				lowX = Math.Min(lowX, shorts[i]);
				highX = Math.Max(highX, shorts[i]);
				lowZ = Math.Min(lowZ, shorts[i + 1]);
				highZ = Math.Max(highZ, shorts[i + 1]);
			}
		}
		Console.WriteLine($"surf: {records} wave pixels, {vertices} vertices, {skipped} of no known type; x {lowX} to {highX}, "
			+ $"z {lowZ} to {highZ} half pixels from the tile's middle");
	}

	/// <summary>
	/// The rivers traced and made into strips (<c>FINDINGS-mapdraw.md</c>, C.6), counted: their sources map.bin's list where
	/// it is the map's, set beside the bitmap's index-0 pixels, which its builder takes them from.
	/// </summary>
	private static void CheckRivers(CMap map, CMapCache cache)
	{
		if (map.RiversFile is not { } file)
		{
			Console.WriteLine("rivers: no river bitmap");
			return;
		}
		var watch = System.Diagnostics.Stopwatch.StartNew();
		byte[] raster = CRiverTracer.Raster(CBitmapFile.Read(file));
		IReadOnlyList<(int X, int Y)> sources = CMapBin.RiverSources(cache, raster, map.Width, map.Height,
			problem => Console.WriteLine("problem: " + problem));
		List<(int X, int Y)> fromBitmap = CMapBin.BitmapSources(raster, map.Width, map.Height);
		int junctions = raster.Count(b => b == CRiverTracer.Junction);
		List<CRiverPiece> pieces = CRiverTracer.Trace(raster, map.Width, map.Height, sources, map.IsWaterPixel, Console.WriteLine);
		List<CRiverStrip> strips = CRiverMeshes.Build(pieces, (x, y) => map.IsSea(map.ProvinceAt(x, y)), 0.25f);
		Console.WriteLine($"rivers: {sources.Count} sources, {(sources.SequenceEqual(fromBitmap) ? "the" : "NOT the")} bitmap's "
			+ $"{fromBitmap.Count} index-0 pixels in order, {junctions} junctions; {pieces.Count} pieces, {pieces.Sum(p => p.Nodes.Count)} "
			+ $"nodes, {strips.Sum(s => s.Vertices.Count)} vertices, {strips.Count(s => s.Exit is not null)} mouths, in {watch.ElapsedMilliseconds} ms");
	}

	/// <summary>
	/// The trees planted out of terrain.bmp (<c>FINDINGS-mapdraw.md</c>, C.8), counted by type - a type of density 1 or more
	/// whatever the draws, the others their share of the pixels that drew - by row, and by tile.
	/// </summary>
	private static void CheckTrees(CMap map)
	{
		if (map.TerrainFile is not { } file)
		{
			Console.WriteLine("trees: no terrain bitmap");
			return;
		}
		var watch = System.Diagnostics.Stopwatch.StartNew();
		byte[] terrain = CBitmapFile.Read(file).NorthRaster();
		CTreeField field = CTreeField.Build(terrain, map.Width, map.Height, map.ProvinceAt, map.IsSea, map.TreeTypeOfIndex,
			map.TreeRowOfIndex, map.TreeTypes.Select(t => t.Density).ToArray());
		long elapsed = watch.ElapsedMilliseconds;
		var eligible = new int[map.TreeTypes.Count];
		for (int y = 0; y < map.Height; y++)
		{
			for (int x = 0; x < map.Width; x++)
			{
				int index = terrain[y * map.Width + x];
				int type = map.TreeTypeOfIndex[index];
				if (type >= 0 && map.TreeRowOfIndex[index] > 0 && map.ProvinceAt(x, y) > 0)
				{
					eligible[type]++;
				}
			}
		}
		var points = field.Tiles.SelectMany(t => t).ToList();
		Console.WriteLine($"trees: {points.Count} of {eligible.Sum()} pixels that drew, in {elapsed} ms; "
			+ string.Join(", ", map.TreeTypes.Select((t, i) => $"{t.Name} {points.Count(p => p.Type == i)} of {eligible[i]}")));
		Console.WriteLine($"trees by row 0..3: {string.Join(" / ", Enumerable.Range(0, 4).Select(k => points.Count(p => p.Kind == k)))}; "
			+ $"{field.Tiles.Count(t => t.Count > 0)} of {field.Tiles.Length} tiles with trees, at most {field.Tiles.Max(t => t.Count)}");
	}

	/// <summary>
	/// The border vertices against the map (<c>FINDINGS-mapdraw.md</c>, B.3): the pixel each one-pixel quad covers - its
	/// corner flag of ±1000 taken off - should be the province its colour names, its lookup texel 14 × id + a slot, and the
	/// slot one of that province's edges in the order the map gives them, whose province touches the pixel.
	/// </summary>
	private static void CheckBorders(CMap map, IReadOnlyList<CQuadTile> tiles, int columns)
	{
		ushort[] raster = map.Raster.ToArray();
		int seaLand = tiles.Sum(t => Enumerable.Range(0, t.LandIndices.Length / 3).Count(k => map.IsSea(t.LandProvinces[t.LandIndices[3 * k]])));
		var seaIds = tiles.SelectMany(t => t.LandProvinces).Where(p => p > 0 && p < map.ProvinceCount && map.IsSea(p)).Distinct().Count();
		Console.WriteLine($"land triangles of sea provinces: {seaLand}, over {seaIds} sea provinces");
		// Each land triangle's middle against the province map: the world point a vertex is drawn at is its corner + 0.5
		// and the tile's middle, a pixel's middle on whole numbers.
		int onOwn = 0, offOwn = 0;
		var offExamples = new List<string>();
		foreach (CQuadTile tile in tiles)
		{
			(int midX, int midZ) = tile.Middle(columns);
			for (int k = 0; k < tile.LandIndices.Length / 3; k++)
			{
				float cx = 0, cz = 0;
				for (int j = 0; j < 3; j++)
				{
					int v = tile.LandIndices[3 * k + j];
					cx += tile.LandPositions[2 * v] + 0.5f + midX;
					cz += tile.LandPositions[2 * v + 1] + 0.5f + midZ;
				}
				int px = (int)MathF.Floor(cx / 3 + 0.5f), pz = (int)MathF.Floor(cz / 3 + 0.5f);
				int id = tile.LandProvinces[tile.LandIndices[3 * k]];
				int here = px >= 0 && pz >= 0 && px < map.Width && pz < map.Height ? raster[pz * map.Width + px] : -1;
				if (here == id)
				{
					onOwn++;
				}
				else
				{
					offOwn++;
					if (offExamples.Count < 5)
					{
						offExamples.Add($"tile {tile.Index} triangle {k}: province {id}, pixel ({px}, {pz}) holds {here}");
					}
				}
			}
		}
		Console.WriteLine($"land triangles whose middle is their own province's pixel: {onOwn}, not: {offOwn}");
		offExamples.ForEach(Console.WriteLine);
		var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
		void Count(string what) => counts[what] = counts.GetValueOrDefault(what) + 1;
		int Province(int cx, int cz) => cz >= 0 && cz < map.Height ? raster[cz * map.Width + ((cx % map.Width) + map.Width) % map.Width] : -1;
		bool Near(int px, int pz, int id, int reach)
		{
			for (int dz = -reach; dz <= reach; dz++)
			{
				for (int dx = -reach; dx <= reach; dx++)
				{
					if (Province(px + dx, pz + dz) == id)
					{
						return true;
					}
				}
			}
			return false;
		}
		var examples = new List<string>();
		var past = new SortedDictionary<int, int>();
		int vertices = 0;
		foreach (CQuadTile tile in tiles)
		{
			(int midX, int midZ) = tile.Middle(columns);
			for (int v = 0; v < tile.BorderColours.Length; v++)
			{
				vertices++;
				int x = tile.BorderShorts[4 * v], z = tile.BorderShorts[4 * v + 1];
				int texel = tile.BorderShorts[4 * v + 2] + 512 * tile.BorderShorts[4 * v + 3];
				uint colour = tile.BorderColours[v];
				int coloured = (int)((colour >> 16) & 0xFF) | (int)((colour >> 8) & 0xFF) << 8;
				int looked = texel / 14;
				int slot = texel % 14;
				// pos = value − 1000·sign + 0.5; the pixel is half a pixel in from the corner, by the corner's side.
				int px = midX + (x > 0 ? x - 1000 : x + 1000 + 1);
				int pz = midZ + (z > 0 ? z - 1000 : z + 1000 + 1);
				int pixel = Province(px, pz);
				Count(coloured == looked ? "colour = lookup" : "colour != lookup");
				Count(pixel == looked ? "pixel = lookup" : pixel == coloured ? "pixel = colour (not lookup)" : "pixel neither");
				CProvinceEdge[] edges = looked > 0 && looked < map.ProvinceCount ? map.Edges[looked] : Array.Empty<CProvinceEdge>();
				if (slot >= edges.Length)
				{
					Count("slot past the lookup province's edges");
					past[slot - edges.Length] = past.GetValueOrDefault(slot - edges.Length) + 1;
					continue;
				}
				int neighbour = edges[slot].To;
				Count(neighbour == coloured ? "edge = colour" : "edge != colour");
				Count(Near(px, pz, neighbour, 1) ? "edge within 1 px" : Near(px, pz, neighbour, 3) ? "edge within 3 px" : "edge not within 3 px");
				Count(Near(px, pz, looked, 1) ? "lookup within 1 px" : "lookup not within 1 px");
				if (!Near(px, pz, neighbour, 3) && examples.Count < 6)
				{
					examples.Add($"tile {tile.Index} v {v}: pixel ({px},{pz}) {pixel}, colour {coloured}, lookup {looked} slot {slot} -> {neighbour}, " +
						$"edges {string.Join(" ", edges.Select(e => e.To))}");
				}
			}
		}
		Console.WriteLine($"border vertices {vertices}:");
		foreach ((string what, int n) in counts)
		{
			Console.WriteLine($"  {what}: {n}");
		}
		Console.WriteLine("  slots past the edges, by how far: " + string.Join(", ", past.Select(p => $"+{p.Key}: {p.Value}")));
		examples.ForEach(e => Console.WriteLine("  " + e));
	}
}
