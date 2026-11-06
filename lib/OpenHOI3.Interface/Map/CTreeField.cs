using System;
using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The map's trees as the original plants them once the map is made (<c>docs/mechanics/mapdraw.md</c>, section 4;
/// <c>reversing/findings/FINDINGS-mapdraw.md</c>, C.8): tile by tile, each tile's own pixels from its south row and west to
/// east, a pixel of a province whose terrain palette entry names a tree type and gives a row draws once from a generator
/// seeded 42, and has a tree where trees.txt's density is at least the draw; then a hundred more draws turn a hundred
/// template trees, each four horizontal cards of its sheet - shadow, base, middle and top - of which a tile's points take
/// one after another. A tile keeps its points by type, then by province in the order of their first tree, then as walked.
/// </summary>
public sealed class CTreeField
{
	public const int TileSize = 50;
	public const uint Seed = 42;
	public const int TemplateTrees = 100;
	public const int VerticesPerTree = 16;

	/// <summary>The floats of a vertex: x, y, z, u, v.</summary>
	public const int Stride = 5;

	/// <summary>The four cards from the top: their height and their column of the sheet's left half [exe].</summary>
	private static readonly (float Height, float U0, float U1)[] Cards =
	{
		(0.5f, 0.375f, 0.5f), (0.375f, 0.25f, 0.375f), (0.225f, 0.125f, 0.25f), (0.05f, 0f, 0.125f),
	};

	/// <summary>Half a card's diagonal, in map pixels [exe].</summary>
	private const float Reach = 1.2f;

	/// <summary>A land province's trees stand this much higher than a sea province's [exe].</summary>
	private const float OnLand = 0.02f;

	/// <summary>A card's triangles, of its own four vertices, the four cards one after another [exe].</summary>
	public static readonly int[] Triangles =
		{ 0, 1, 2, 2, 3, 0, 4, 5, 6, 6, 7, 4, 8, 9, 10, 10, 11, 8, 12, 13, 14, 14, 15, 12 };

	private readonly float[] _template;

	private CTreeField(IReadOnlyList<CTreePoint>[] tiles, int columns, float[] template)
	{
		Tiles = tiles;
		Columns = columns;
		_template = template;
	}

	/// <summary>Each tile's trees, tile i at column i mod <see cref="Columns"/> and row i / it, rows from the south.</summary>
	public IReadOnlyList<CTreePoint>[] Tiles { get; }

	public int Columns { get; }

	public int Count => Tiles.Sum(t => t.Count);

	/// <param name="terrain">The terrain bitmap's palette index at each pixel, <c>[y * width + x]</c> with y counted from the south.</param>
	/// <param name="province">The province at a pixel, 0 for none.</param>
	/// <param name="isSea">Whether a province is a sea province.</param>
	/// <param name="typeOfIndex">For each palette index, its tree type, or -1.</param>
	/// <param name="rowOfIndex">For each palette index, d: 4 − the sheet's row, nought or less for no trees.</param>
	/// <param name="densities">Each tree type's density.</param>
	public static CTreeField Build(byte[] terrain, int width, int height, Func<int, int, int> province, Func<int, bool> isSea,
		IReadOnlyList<int> typeOfIndex, IReadOnlyList<int> rowOfIndex, IReadOnlyList<float> densities)
	{
		var random = new CRandom(Seed);
		int columns = width / TileSize + 1;
		int rows = height / TileSize + 1;
		var tiles = new IReadOnlyList<CTreePoint>[columns * rows];
		var walked = new List<CTreePoint>();
		for (int t = 0; t < tiles.Length; t++)
		{
			walked.Clear();
			int x0 = t % columns * TileSize, y0 = t / columns * TileSize;
			for (int y = y0; y < Math.Min(y0 + TileSize, height); y++)
			{
				for (int x = x0; x < Math.Min(x0 + TileSize, width); x++)
				{
					int id = province(x, y);
					if (id <= 0)
					{
						continue;
					}
					int index = terrain[y * width + x];
					int type = typeOfIndex[index];
					int d = rowOfIndex[index];
					if (type < 0 || d <= 0)
					{
						continue;
					}
					// Every pixel that may have a tree spends a draw, whether or not it gets one.
					if (densities[type] >= Draw(random))
					{
						walked.Add(new CTreePoint(x, y, id, type, 4 - d, !isSea(id)));
					}
				}
			}
			tiles[t] = Order(walked);
		}
		return new CTreeField(tiles, columns, Template(random));
	}

	/// <summary>A draw as a share of one, in single precision as the original works it out.</summary>
	private static float Draw(CRandom random) => (float)(random.Next() & 0x7FFFFFFF) * (1f / 2147483648f);

	/// <summary>By type, then by province in the order of its first tree, then as walked.</summary>
	private static CTreePoint[] Order(List<CTreePoint> walked)
	{
		var firstOf = new Dictionary<int, int>();
		foreach (CTreePoint point in walked)
		{
			firstOf.TryAdd(point.Province, firstOf.Count);
		}
		return walked.Select((point, at) => (point, at))
			.OrderBy(p => p.point.Type).ThenBy(p => firstOf[p.point.Province]).ThenBy(p => p.at)
			.Select(p => p.point).ToArray();
	}

	/// <summary>The hundred trees, each turned by a draw: four cards, each a square across the diagonal 2.4, flat [exe].</summary>
	private static float[] Template(CRandom random)
	{
		var template = new float[TemplateTrees * VerticesPerTree * Stride];
		int at = 0;
		for (int tree = 0; tree < TemplateTrees; tree++)
		{
			float angle = (float)(Draw(random) * 2.0 * 3.1415927410125732);
			float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
			foreach ((float h, float u0, float u1) in Cards)
			{
				foreach ((float x, float z, float u, float v) in new[]
				{
					(Reach * s, -Reach * c, u1, 0f), (-Reach * c, -Reach * s, u0, 0f),
					(-Reach * s, Reach * c, u0, 0.25f), (Reach * c, Reach * s, u1, 0.25f),
				})
				{
					template[at++] = x;
					template[at++] = h;
					template[at++] = z;
					template[at++] = u;
					template[at++] = v;
				}
			}
		}
		return template;
	}

	/// <summary>
	/// A tile's trees of one type as vertices, x, y, z, u, v each, in the map's own coordinates: the tile's n-th tree, of every
	/// type counted, the template's tree n mod 100 moved onto its pixel, its sheet's row by its kind, and the snowed half
	/// where <paramref name="snowed"/> says so of its province. The shader lifts them all by the land's height.
	/// </summary>
	public List<float> Vertices(int tile, int type, Func<int, bool>? snowed)
	{
		var vertices = new List<float>();
		IReadOnlyList<CTreePoint> points = Tiles[tile];
		for (int n = 0; n < points.Count; n++)
		{
			CTreePoint point = points[n];
			if (point.Type != type)
			{
				continue;
			}
			float lift = point.OnLand ? OnLand : 0f;
			float snow = snowed?.Invoke(point.Province) == true ? 0.5f : 0f;
			int from = n % TemplateTrees * VerticesPerTree * Stride;
			for (int v = 0; v < VerticesPerTree; v++)
			{
				int at = from + v * Stride;
				vertices.Add(_template[at] + point.X);
				vertices.Add(_template[at + 1] + lift);
				vertices.Add(_template[at + 2] + point.Y);
				vertices.Add(_template[at + 3] + snow);
				vertices.Add(_template[at + 4] + 0.25f * point.Kind);
			}
		}
		return vertices;
	}

	/// <summary>The template's tree, its 16 vertices of five floats, for a check.</summary>
	public ReadOnlySpan<float> TemplateTree(int tree) =>
		_template.AsSpan(tree * VerticesPerTree * Stride, VerticesPerTree * Stride);
}
