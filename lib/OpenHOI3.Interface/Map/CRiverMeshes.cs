using System;
using System.Collections.Generic;
using System.Numerics;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// Each traced river made into a strip as the original makes it (<c>docs/mechanics/mapdraw.md</c>, section 4;
/// <c>reversing/findings/FINDINGS-mapdraw.md</c>, C.6): a cardinal spline of tension 0.7 through the nodes' middles, three
/// steps to a pair of nodes, its half-width a tenth of the spline's width and 0.15 and a tenth of a draw of the generator
/// seeded 42, its height 0.40 and 0.02 more over land. The figures are single precision, as the original's.
/// </summary>
public static class CRiverMeshes
{
	/// <summary>The seed of the rivers' own generator [exe].</summary>
	public const uint Seed = 42;

	private const float Tension = 0.7f;
	private const float Height = 0.40f;
	private const float OverLand = 0.02f;

	/// <summary>Every piece's strip, in the pieces' order, every draw of one generator made in that order.</summary>
	/// <param name="overSea">Whether the province at a pixel, x a column and y counted from the south, is a sea province.</param>
	/// <param name="exitFrameWidth">128 over the width of <c>river_exit.dds</c>, the share of it one frame takes.</param>
	public static List<CRiverStrip> Build(IReadOnlyList<CRiverPiece> pieces, Func<int, int, bool> overSea, float exitFrameWidth)
	{
		var random = new CRandom(Seed);
		var strips = new List<CRiverStrip>(pieces.Count);
		foreach (CRiverPiece piece in pieces)
		{
			strips.Add(Build(piece, random, overSea, exitFrameWidth));
		}
		return strips;
	}

	public static CRiverStrip Build(CRiverPiece piece, CRandom random, Func<int, int, bool> overSea, float exitFrameWidth)
	{
		IReadOnlyList<CRiverNode> nodes = piece.Nodes;
		int count = nodes.Count;
		Vector2 Node(int j) => new(nodes[j].X, nodes[j].Y);
		// One point before the first and one past the last, carried on as the original carries them.
		Vector2 Point(int j) => j < 0 ? 2 * Node(0) - Node(1)
			: j >= count ? Point(count - 1) + (Point(count - 2) - Point(count - 3))
			: Node(j);
		float Width(int j) => j < 0 ? 1f : j >= count ? 2 * Width(count - 1) - Width(count - 2) : nodes[j].Width;
		float Lift(Vector2 p) => overSea((int)(p.X - 1), (int)(p.Y - 1)) ? 0f : OverLand;

		var vertices = new List<CRiverVertex>(6 * (count - 1));
		for (int s = 0; s + 1 < count; s++)
		{
			Vector2 p0 = Point(s - 1), p1 = Point(s), p2 = Point(s + 1), p3 = Point(s + 2);
			float w0 = Width(s - 1), w1 = Width(s), w2 = Width(s + 1), w3 = Width(s + 2);
			for (int k = 0; k < 3; k++)
			{
				// A draw for each step, made even where the step adds nothing new.
				float draw = (random.Next() & 0x7FFFFFFF) / 2147483648f;
				if (s >= 1 && k == 0)
				{
					// The previous pair again, at the top of the body's picture: a seam and two degenerate triangles.
					CRiverVertex plus = vertices[^2], minus = vertices[^1];
					vertices.Add(plus with { Body = new Vector2(1, 0.25f), Flow = new Vector2(1, vertices.Count / 3f) });
					vertices.Add(minus with { Body = new Vector2(0, 0.25f), Flow = new Vector2(0, vertices.Count / 3f) });
					continue;
				}
				float t = k / 2f;
				Vector2 centre = Spline(p0, p1, p2, p3, t);
				Vector2 ahead = Spline(p0, p1, p2, p3, (k + 1) / 2f);
				Vector2 along = Unit(ahead - centre);
				Vector2 across = Unit(new Vector2(-along.Y, along.X));
				float width = Weight(w0, w1, w2, w3, t);
				float half = (0.1f * width + 0.15f + 0.1f * draw) / 2;
				float v = k / 8f + (s == 0 ? 0 : 0.25f);
				float centreLift = Lift(centre);
				Vector2 plusAt = centre + across * half, minusAt = centre - across * half;
				vertices.Add(new CRiverVertex(new Vector3(plusAt.X, Height + Math.Max(Lift(plusAt), centreLift), plusAt.Y),
					new Vector2(1, v), new Vector2(1, vertices.Count / 3f)));
				vertices.Add(new CRiverVertex(new Vector3(minusAt.X, Height + Math.Max(Lift(minusAt), centreLift), minusAt.Y),
					new Vector2(0, v), new Vector2(0, vertices.Count / 3f)));
			}
		}
		return new CRiverStrip(vertices, piece.ExitFrame < 0 || vertices.Count < 2 ? null : Exit(vertices, piece.ExitFrame, exitFrameWidth));
	}

	/// <summary>
	/// The mouth: the last pair's middle, twice their spacing across and three pixels on downstream, a frame of
	/// <c>river_exit.dds</c> across it. Only a river pixel of index 12 or more gives one, and Their Finest Hour's map has
	/// none, so nothing of the original's has been seen to check it; its height is the last pair's (*inferred*).
	/// </summary>
	private static CRiverVertex[] Exit(List<CRiverVertex> vertices, int frame, float frameWidth)
	{
		Vector3 a = vertices[^2].Position, b = vertices[^1].Position;
		Vector3 middle = (a + b) / 2;
		Vector3 across = a - b;
		Vector3 on = 3 * Vector3.Normalize(new Vector3(across.Z, 0, -across.X));
		float near = (frame + 1) * frameWidth, far = frame * frameWidth;
		int n = vertices.Count;
		return new[]
		{
			new CRiverVertex(middle + across, new Vector2(near, 0), new Vector2(1, n)),
			new CRiverVertex(middle - across, new Vector2(far, 0), new Vector2(0, n + 1)),
			new CRiverVertex(middle + across + on, new Vector2(near, 1), new Vector2(1, n + 2)),
			new CRiverVertex(middle - across + on, new Vector2(far, 1), new Vector2(0, n + 3)),
		};
	}

	/// <summary>A direction of length one, or nought for none, as D3DX's normalising leaves a zero vector (*inferred*).</summary>
	private static Vector2 Unit(Vector2 v)
	{
		float length = v.Length();
		return length > 0 ? v / length : Vector2.Zero;
	}

	private static Vector2 Spline(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		(float a, float b, float c, float d) = Weights(t);
		return a * p0 + b * p1 + c * p2 + d * p3;
	}

	private static float Weight(float w0, float w1, float w2, float w3, float t)
	{
		(float a, float b, float c, float d) = Weights(t);
		return a * w0 + b * w1 + c * w2 + d * w3;
	}

	/// <summary>The cardinal spline's four weights at t, tension 0.7 [exe].</summary>
	private static (float, float, float, float) Weights(float t)
	{
		float t2 = t * t, t3 = t2 * t;
		return (-Tension * t3 + 2 * Tension * t2 - Tension * t,
			(2 - Tension) * t3 + (Tension - 3) * t2 + 1,
			(Tension - 2) * t3 + (3 - 2 * Tension) * t2 + Tension * t,
			Tension * t3 - Tension * t2);
	}
}
