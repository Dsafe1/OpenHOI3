using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OpenHOI3.Content.Map.Cache;

/// <summary>
/// A tile's beaches: a segment for each way a land pixel lies against the water, each a strip whose seaward edge is wobbled by noise,
/// and one triangle a segment that closes the gap to its nearest neighbour. See <c>docs/mechanics/mapcache.md</c>, section 8.
/// </summary>
public sealed class CBeachMesh
{
	private const double Third = 0.3333333432674408;

	/// <summary>The straight kinds' three points from the pixel: the two ends of the shore and the middle of the strip.</summary>
	private static readonly (int Ax, int Ay, int Bx, int By, int Mx, int My)[] Straight =
	{
		default,
		(0, -1, 0, 0, 0, -1), (-1, 0, -1, -1, -1, 0), (0, 0, -1, 0, 0, 0), (-1, -1, 0, -1, -1, -1),
		(1, -1, 0, 0, 0, -1), (-1, 0, -2, -1, -1, -1), (-1, -2, 0, -1, -1, -1), (-2, 0, -1, -1, -1, 0),
		(-1, -1, 0, -2, 0, -1), (-1, -2, 0, -1, -1, -1), (0, 0, -1, 1, -1, 0),
	};

	/// <summary>The corner kinds' four points from the pixel, kinds 12 to 15.</summary>
	private static readonly (int X, int Y)[][] Corners =
	{
		new[] { (1, 0), (1, -1), (0, 0), (0, -1) },
		new[] { (-2, -1), (-2, 0), (-1, -1), (-1, 0) },
		new[] { (-1, 1), (0, 1), (-1, 0), (0, 0) },
		new[] { (0, -2), (-1, -2), (0, -1), (-1, -1) },
	};

	/// <summary>A segment with where its links to the others come from.</summary>
	private struct Segment
	{
		public int X, Y, Kind, OffsetX;
		public int Near, Far, Start;
		public float NearX, NearY, FarX, FarY, StartX, StartY, MatchX, MatchY;
	}

	private readonly List<Segment> _segments = new();
	private readonly List<(float X, float Y, uint Colour)> _vertices = new();

	/// <summary>Sixteen-bit triangle indices.</summary>
	public List<ushort> Indices { get; } = new();

	public int VertexCount => _vertices.Count;

	/// <summary>Twelve bytes a vertex: two floats and the colour.</summary>
	public byte[] VertexBytes()
	{
		var bytes = new byte[_vertices.Count * 12];
		for (int i = 0; i < _vertices.Count; i++)
		{
			Span<byte> vertex = bytes.AsSpan(i * 12, 12);
			BinaryPrimitives.WriteSingleLittleEndian(vertex, _vertices[i].X);
			BinaryPrimitives.WriteSingleLittleEndian(vertex[4..], _vertices[i].Y);
			BinaryPrimitives.WriteUInt32LittleEndian(vertex[8..], _vertices[i].Colour);
		}
		return bytes;
	}

	/// <summary>The kinds of segment a land pixel makes, in the order they are added.</summary>
	public static void PixelKinds(CMapCacheSource source, int x, int y, List<int> kinds)
	{
		kinds.Clear();
		if (x < 1 || x >= source.Width - 1 || y < 1 || y >= source.Height - 1)
		{
			return;
		}
		bool W(int a, int b) => source.IsWaterAt(a, b);
		int north = y + 1;
		int south = y - 1;
		int east = x + 1;
		int west = x - 1;
		if (W(x, north) && W(west, north) && W(east, north)) { kinds.Add(3); }
		if (W(x, south) && W(west, south) && W(east, south)) { kinds.Add(4); }
		if (W(east, y) && W(east, south) && W(east, north)) { kinds.Add(1); }
		if (W(west, y) && W(west, south) && W(west, north)) { kinds.Add(2); }
		bool lone = false;
		if (W(x, south) && !W(west, south) && !W(east, south)) { lone = true; kinds.Add(15); }
		if (W(x, north) && !W(west, north) && !W(east, north)) { lone = true; kinds.Add(14); }
		if (W(west, y) && !W(west, north) && !W(west, south)) { lone = true; kinds.Add(13); }
		if (W(east, y) && !W(east, north) && !W(east, south))
		{
			kinds.Add(12);
			return;
		}
		if (lone)
		{
			return;
		}
		if (W(west, south) && W(x, south) && W(west, y) && !W(west, north) && W(x - 2, y)) { kinds.Add(8); }
		else if (W(east, south) && W(x, south) && W(east, y) && !W(west, south) && W(x, y - 2)) { kinds.Add(7); }
		else if (W(west, north) && W(west, y) && !W(west, south)) { kinds.Add(6); }
		else if (W(east, north) && W(east, y) && !W(east, south)) { kinds.Add(5); }
		if (W(west, south) && W(x, south) && !W(east, south) && W(x, y - 2)) { kinds.Add(9); }
		if (W(east, south) && W(x, south) && !W(west, south) && W(x, y - 2)) { kinds.Add(10); }
		if (W(east, north) && W(x, north) && !W(west, north) && W(x, y + 2)) { kinds.Add(11); }
	}

	/// <summary>Adds a segment; <paramref name="offsetX"/> says which pixel beside this one the colour names.</summary>
	public void Add(int x, int y, int kind, int offsetX)
	{
		_segments.Add(new Segment { X = x, Y = y, Kind = kind, OffsetX = offsetX });
	}

	/// <summary>The segments made into vertices and triangles, with the tile's noise.</summary>
	public void Finish(CMapCacheSource source, (int X, int Y) middle, CBeachNoise noise)
	{
		Span<Segment> segments = CollectionsMarshal.AsSpan(_segments);
		for (int i = 0; i < segments.Length; i++)
		{
			ref Segment segment = ref segments[i];
			int cx = segment.X + segment.OffsetX;
			int province = cx >= 0 && cx < source.Width ? source.Provinces[segment.Y * source.Width + cx] : 0;
			uint colour = (uint)((province & 0xFF) << 16 | (province & 0xFF00));
			if (segment.Kind < 12)
			{
				AddStraight(ref segment, middle, colour, noise);
			}
			else
			{
				AddCorner(ref segment, middle, colour, noise);
			}
		}
		for (int i = 0; i < segments.Length; i++)
		{
			ref Segment segment = ref segments[i];
			int third = segment.Near;
			float best = 1e17f;
			int nearest = -1;
			for (int j = 0; j < segments.Length; j++)
			{
				ref Segment other = ref segments[j];
				if (other.X == segment.X && other.Y == segment.Y && other.Kind == segment.Kind)
				{
					continue;
				}
				float distance = Distance(segment.StartX, segment.StartY, other.MatchX, other.MatchY);
				if (distance < best)
				{
					best = distance;
					nearest = j;
				}
			}
			if (nearest >= 0 && best < 2f && Distance(segments[nearest].FarX, segments[nearest].FarY, segment.NearX, segment.NearY) < 1f)
			{
				third = segments[nearest].Far;
			}
			Indices.Add((ushort)segment.Start);
			Indices.Add((ushort)segment.Near);
			Indices.Add((ushort)third);
		}
	}

	private static float Distance(float x0, float y0, float x1, float y1)
	{
		float dx = (float)((double)x0 - (double)x1);
		float dy = (float)((double)y0 - (double)y1);
		return MathF.Sqrt((float)((double)dy * (double)dy + (double)dx * (double)dx));
	}

	private static float Point(int value, int middle) => (float)((double)(float)(value - middle) + 0.5);

	private void AddStraight(ref Segment segment, (int X, int Y) middle, uint colour, CBeachNoise noise)
	{
		var shape = Straight[segment.Kind];
		float ax = Point(segment.X + shape.Ax, middle.X);
		float ay = Point(segment.Y + shape.Ay, middle.Y);
		float bx = Point(segment.X + shape.Bx, middle.X);
		float by = Point(segment.Y + shape.By, middle.Y);
		float dx = (float)((double)bx - (double)ax);
		float dy = (float)((double)by - (double)ay);
		float scale = (float)(1.0 / (double)MathF.Sqrt((float)((double)dy * (double)dy + (double)dx * (double)dx + 0.0)));
		float ex = (float)((double)dx * (double)scale);
		float ey = (float)((double)dy * (double)scale);
		// The direction crossed with straight down, normalised, and turned about.
		float cx = (float)((double)ey * -1.0 - 0.0);
		float cy = (float)(0.0 - (double)ex * -1.0);
		scale = (float)(1.0 / (double)MathF.Sqrt((float)((double)cy * (double)cy + (double)cx * (double)cx + 0.0)));
		float px = (float)((double)(float)((double)cx * (double)scale) * -1.0);
		float py = (float)((double)(float)((double)cy * (double)scale) * -1.0);
		float mx;
		float my;
		if (segment.Kind < 5)
		{
			mx = (float)((double)ax + (double)(float)((double)ex * 0.5));
			my = (float)((double)(float)((double)ey * 0.5) + (double)ay);
		}
		else
		{
			mx = Point(segment.X + shape.Mx, middle.X);
			my = Point(segment.Y + shape.My, middle.Y);
		}
		int origin = _vertices.Count;
		_vertices.Add((ax, ay, colour));
		_vertices.Add((bx, by, colour));
		_vertices.Add((mx, my, colour));
		float firstX = 0f, firstY = 0f, lastX = 0f, lastY = 0f;
		for (int k = 0; k < 4; k++)
		{
			double step = (float)k;
			float qx = (float)((double)ax + (double)(float)((double)(float)((double)ex * step) * Third));
			float qy = (float)((double)(float)((double)(float)((double)ey * step) * Third) + (double)ay);
			float d = noise.Displacement(qx, qy);
			float vx = (float)((double)(float)((double)d * (double)px) + (double)qx);
			float vy = (float)((double)(float)((double)d * (double)py) + (double)qy);
			_vertices.Add((vx, vy, colour));
			if (k == 0)
			{
				(firstX, firstY) = (vx, vy);
			}
			(lastX, lastY) = (vx, vy);
		}
		AddTriangle(origin + 2, origin, origin + 3);
		for (int j = 0; j < 3; j++)
		{
			AddTriangle(origin + j + 3, origin + j + 4, origin + 2);
		}
		AddTriangle(origin + 6, origin + 1, origin + 2);
		segment.Near = origin + 3;
		segment.Far = origin + 6;
		segment.Start = origin;
		(segment.NearX, segment.NearY, segment.FarX, segment.FarY) = (firstX, firstY, lastX, lastY);
		(segment.StartX, segment.StartY, segment.MatchX, segment.MatchY) = (ax, ay, mx, my);
	}

	private void AddCorner(ref Segment segment, (int X, int Y) middle, uint colour, CBeachNoise noise)
	{
		(int X, int Y)[] shape = Corners[segment.Kind - 12];
		Span<float> x = stackalloc float[4];
		Span<float> y = stackalloc float[4];
		for (int i = 0; i < 4; i++)
		{
			x[i] = Point(segment.X + shape[i].X, middle.X);
			y[i] = Point(segment.Y + shape[i].Y, middle.Y);
		}
		(float e1x, float e1y) = Unit(x[1], y[1], x[0], y[0]);
		(float e2x, float e2y) = Unit(x[2], y[2], x[0], y[0]);
		float d = noise.Displacement(x[0], y[0]);
		float q0x = (float)((double)x[0] + (double)(float)((double)d * (double)e1x));
		float q0y = (float)((double)(float)((double)e1y * (double)d) + (double)y[0]);
		d = noise.Displacement(x[1], y[1]);
		float q1x = (float)((double)x[1] - (double)(float)((double)d * (double)e1x));
		float q1y = (float)((double)y[1] - (double)(float)((double)e1y * (double)d));
		d = noise.Displacement(q0x, q0y);
		float q2x = (float)((double)q0x + (double)(float)((double)d * (double)e2x));
		float q2y = (float)((double)(float)((double)e2y * (double)d) + (double)q0y);
		d = noise.Displacement(q1x, q1y);
		float q3x = (float)((double)q1x + (double)(float)((double)d * (double)e2x));
		float q3y = (float)((double)(float)((double)e2y * (double)d) + (double)q1y);
		int origin = _vertices.Count;
		for (int i = 0; i < 4; i++)
		{
			_vertices.Add((x[i], y[i], colour));
		}
		_vertices.Add((q0x, q0y, colour));
		_vertices.Add((q1x, q1y, colour));
		_vertices.Add((q2x, q2y, colour));
		_vertices.Add((q3x, q3y, colour));
		AddTriangle(origin, origin + 4, origin + 2);
		AddTriangle(origin + 4, origin + 2, origin + 6);
		AddTriangle(origin + 6, origin + 2, origin + 7);
		AddTriangle(origin + 7, origin + 2, origin + 3);
		AddTriangle(origin + 5, origin + 7, origin + 3);
		AddTriangle(origin + 1, origin + 5, origin + 3);
		segment.Near = origin + 5;
		segment.Far = origin + 4;
		segment.Start = origin + 1;
		(segment.NearX, segment.NearY, segment.FarX, segment.FarY) = (q1x, q1y, q0x, q0y);
		(segment.StartX, segment.StartY, segment.MatchX, segment.MatchY) = (x[1], y[1], x[0], y[0]);
	}

	private static (float X, float Y) Unit(float toX, float toY, float fromX, float fromY)
	{
		float dx = (float)((double)toX - (double)fromX);
		float dy = (float)((double)toY - (double)fromY);
		float scale = (float)(1.0 / (double)MathF.Sqrt((float)((double)dx * (double)dx + (double)dy * (double)dy)));
		return ((float)((double)dx * (double)scale), (float)((double)dy * (double)scale));
	}

	private void AddTriangle(int a, int b, int c)
	{
		Indices.Add((ushort)a);
		Indices.Add((ushort)b);
		Indices.Add((ushort)c);
	}
}
