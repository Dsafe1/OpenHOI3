using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Content;
using OpenHOI3.Content.Map;
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
		IReadOnlyList<CQuadTile> tiles = CQuadCache.Load(CDirectorySettings.At(args[1]), problems.Add);
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
		var directories = CDirectorySettings.At(args[1]);
		int columns = CQuadCache.Columns(directories);
		int fits = 0;
		int differ = 0;
		foreach (CQuadTile tile in tiles)
		{
			string path = directories.Resolve($"map/cache/quad_textures/quad_texture_{tile.Index}.dds");
			if (!System.IO.File.Exists(path))
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
		CheckBorders(directories, tiles, columns);
		return problems.Count == 0 && outOfRange == 0 ? 0 : 1;
	}

	/// <summary>
	/// The border vertices against the map (<c>FINDINGS-mapdraw.md</c>, B.3): the pixel each one-pixel quad covers - its
	/// corner flag of ±1000 taken off - should be the province its colour names, its lookup texel 14 × id + a slot, and the
	/// slot one of that province's edges in the order the map gives them, whose province touches the pixel.
	/// </summary>
	private static void CheckBorders(CDirectorySettings directories, IReadOnlyList<CQuadTile> tiles, int columns)
	{
		CMap map = CMap.Load(directories, _ => { });
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
