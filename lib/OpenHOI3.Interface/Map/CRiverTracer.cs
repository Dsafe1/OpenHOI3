using System;
using System.Collections.Generic;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The rivers traced out of <c>map/rivers.bmp</c> as the original traces them once the map is made
/// (<c>docs/mechanics/mapdraw.md</c>, section 4; <c>reversing/findings/FINDINGS-mapdraw.md</c>, C.6). The sources come from
/// map.bin's list - every pixel of index 0, by rows from the south and then from the west, while that file is the map's
/// (<c>CMapBin</c>); from each the walk steps onto the first river pixel
/// (index 2 to 22) east, west, north or south, never straight back, drops the corner where a step north or south follows
/// one east or west, and stops on a pixel the terrain paints as water; with no step left it takes a junction (index 1) not
/// yet visited and the pixel beyond it, and stops. One list of visited pixels serves every source, and only junctions are
/// tested against it.
/// </summary>
public static class CRiverTracer
{
	public const byte Source = 0;
	public const byte Junction = 1;
	public const byte FirstRiver = 2;
	public const byte FirstExit = 12;
	public const byte LastRiver = 22;

	/// <summary>The four directions in the order they are tried: east, west, north, south.</summary>
	private static readonly (int Dx, int Dy)[] Directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };

	/// <summary>A river bitmap's palette indices, <c>[y * width + x]</c> with y counted from the south.</summary>
	public static byte[] Raster(Content.Map.CBitmapFile rivers)
	{
		var raster = new byte[rivers.Width * rivers.Height];
		for (int y = 0; y < rivers.Height; y++)
		{
			rivers.StoredRow(rivers.Height - 1 - y).CopyTo(raster.AsSpan(y * rivers.Width, rivers.Width));
		}
		return raster;
	}

	/// <param name="raster">The river bitmap's palette index at each pixel, <c>[y * width + x]</c> with y counted from the south.</param>
	/// <param name="sources">Where the rivers start, in the order they are traced: map.bin's list (<c>CMapBin</c>).</param>
	/// <param name="isWater">Whether the terrain paints a pixel as water.</param>
	/// <param name="problem">Told of a walk that went round in a loop, which the original would walk for ever.</param>
	public static List<CRiverPiece> Trace(byte[] raster, int width, int height, IEnumerable<(int X, int Y)> sources,
		Func<int, int, bool> isWater, Action<string>? problem = null)
	{
		var pieces = new List<CRiverPiece>();
		var visited = new HashSet<long>();
		// A pixel off the map is no river: the original reads past its raster there, which was not established.
		int At(int x, int y) => x >= 0 && y >= 0 && x < width && y < height ? raster[y * width + x] : 255;
		long Key(int x, int y) => (long)y * width + x;
		foreach ((int sx, int sy) in sources)
		{
			visited.Add(Key(sx, sy));
			var nodes = new List<CRiverNode> { new(sx, sy, 1, true) };
			int exit = -1;
			int x = sx, y = sy;
			int last = -1;
			// No visited test guards a step onto a river pixel, so a loop of them would never end; ours stops it.
			int steps = 0;
			int limit = width * height;
			while (true)
			{
				int taken = -1;
				for (int d = 0; d < 4; d++)
				{
					if (last >= 0 && d == (last ^ 1))
					{
						continue;
					}
					int nx = x + Directions[d].Dx, ny = y + Directions[d].Dy;
					int value = At(nx, ny);
					if (value < FirstRiver || value > LastRiver)
					{
						continue;
					}
					if (value >= FirstExit)
					{
						exit = value - FirstExit;
					}
					bool stops = isWater(nx, ny);
					if (d >= 2 && last is 0 or 1 && !stops)
					{
						nodes.RemoveAt(nodes.Count - 1);
					}
					nodes.Add(new CRiverNode(nx, ny, value - FirstRiver, false));
					visited.Add(Key(nx, ny));
					x = nx;
					y = ny;
					last = d;
					taken = stops ? 2 : 1;
					break;
				}
				if (taken == 2)
				{
					break;
				}
				if (taken == 1)
				{
					if (++steps > limit)
					{
						problem?.Invoke($"rivers: the river from ({sx}, {sy}) goes round in a loop at ({x}, {y}); it is cut there");
						break;
					}
					continue;
				}
				// No step: any junction not yet visited, and the pixel beyond it, each with the last node's width and mark.
				for (int d = 0; d < 4; d++)
				{
					int jx = x + Directions[d].Dx, jy = y + Directions[d].Dy;
					if (At(jx, jy) != Junction || !visited.Add(Key(jx, jy)))
					{
						continue;
					}
					CRiverNode end = nodes[^1];
					nodes.Add(new CRiverNode(jx, jy, end.Width, end.IsSource));
					nodes.Add(new CRiverNode(jx + Directions[d].Dx, jy + Directions[d].Dy, end.Width, end.IsSource));
					last = -1;
					// A junction found west moves x, and one found north moves y, for the tests after it [exe].
					if (d == 1)
					{
						x--;
					}
					else if (d == 2)
					{
						y++;
					}
				}
				break;
			}
			if (exit != -1 && nodes.Count > 2)
			{
				nodes.RemoveRange(nodes.Count - 2, 2);
			}
			if (last >= 0)
			{
				CRiverNode end = nodes[^1];
				nodes.Add(end with { X = end.X + Directions[last].Dx, Y = end.Y + Directions[last].Dy });
			}
			if (nodes.Count >= 2)
			{
				pieces.Add(new CRiverPiece(nodes, exit));
			}
		}
		return pieces;
	}
}
