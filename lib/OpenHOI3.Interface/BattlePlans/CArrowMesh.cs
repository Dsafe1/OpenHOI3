using System;
using System.Collections.Generic;
using OpenHOI3.Content.BattlePlans;
using OpenHOI3.Sim.Plans;

namespace OpenHOI3.Interface.BattlePlans;

/// <summary>
/// How an arrow's points come to be and the strip they make: points some five units apart as the mouse goes, smoothed when
/// it lets go, a Catmull-Rom spline through them, and two vertices a sample with four more for the head. See
/// <c>docs/mechanics/battleplans.md</c>, section 10.
/// </summary>
public static class CArrowMesh
{
	/// <summary>How far apart, squared, two points are left.</summary>
	private const float Spacing = 25f;

	/// <summary>A last segment shorter than this, squared, is not sampled.</summary>
	private const float ShortestTail = 0.05f;

	/// <summary>The first point twice; then a new point when the last but one is over five away; and the last always follows.</summary>
	public static void AddPoint(List<(float X, float Y)> points, float x, float y)
	{
		if (points.Count == 0)
		{
			points.Add((x, y));
			points.Add((x, y));
			return;
		}
		(float X, float Y) previous = points[^2];
		if (Squared(x - previous.X, y - previous.Y) > Spacing)
		{
			points.Add((x, y));
		}
		points[^1] = (x, y);
	}

	/// <summary>Each point but the ends becomes a quarter itself and three quarters its neighbours' middle, from the old values.</summary>
	public static void Smooth(List<(float X, float Y)> points, int snapPrefix)
	{
		if (points.Count <= 2)
		{
			return;
		}
		var old = new List<(float X, float Y)>(points);
		for (int index = snapPrefix != -1 ? Math.Max(snapPrefix, 1) : 1; index <= points.Count - 2; index++)
		{
			points[index] = (0.25f * old[index].X + 0.75f * (old[index - 1].X + old[index + 1].X) / 2f,
				0.25f * old[index].Y + 0.75f * (old[index - 1].Y + old[index + 1].Y) / 2f);
		}
	}

	/// <summary>The curve's samples, four a segment and the last point, for points that make one; else none.</summary>
	public static List<(float X, float Y)> Samples(IReadOnlyList<(float X, float Y)> points)
	{
		var samples = new List<(float X, float Y)>();
		if (points.Count < 2)
		{
			return samples;
		}
		(float X, float Y) end = points[^1];
		(float X, float Y) before = points[^2];
		int tail = Squared(end.X - before.X, end.Y - before.Y) < ShortestTail ? 2 : 1;
		int segments = points.Count - tail;
		if (segments * 4 + 1 <= 4)
		{
			return samples;
		}
		for (int segment = 0; segment < segments; segment++)
		{
			(float X, float Y) p0 = points[segment];
			(float X, float Y) p1 = points[segment + 1];
			(float X, float Y) back = segment > 0 ? points[segment - 1] : (2f * p0.X - p1.X, 2f * p0.Y - p1.Y);
			(float X, float Y) next = segment + 2 < points.Count ? points[segment + 2] : (2f * p1.X - p0.X, 2f * p1.Y - p0.Y);
			for (int step = 0; step < 4; step++)
			{
				samples.Add(Curve(back, p0, p1, next, step * 0.25f));
			}
			if (segment == segments - 1)
			{
				samples.Add(Curve(back, p0, p1, next, 1f));
			}
		}
		return samples;
	}

	private static (float X, float Y) Curve((float X, float Y) back, (float X, float Y) p0, (float X, float Y) p1, (float X, float Y) next, float t)
	{
		float t2 = t * t, t3 = t2 * t;
		float a = 2f * t2 - t3 - t, b = 3f * t3 - 5f * t2 + 2f, c = 4f * t2 - 3f * t3 + t, d = t3 - t2;
		return (0.5f * (back.X * a + p0.X * b + p1.X * c + next.X * d), 0.5f * (back.Y * a + p0.Y * b + p1.Y * c + next.Y * d));
	}

	private static float Squared(float x, float y) => x * x + y * y;

	/// <summary>The strip of the body, two vertices a sample, and then the four of the head; none where the points make no curve.</summary>
	public static (List<CPlanVertex> Vertices, int BodyVertices) Build(CPlanStroke stroke, CBattleArrowType type)
	{
		var vertices = new List<CPlanVertex>();
		List<(float X, float Y)> samples = Samples(stroke.Points);
		int count = samples.Count;
		if (count == 0)
		{
			return (vertices, 0);
		}
		float travelled = 0f;
		(float X, float Y) normal = (0f, 1f), tangent = (1f, 0f);
		for (int k = 0; k < count; k++)
		{
			(float X, float Y) from = samples[Math.Max(k - 1, 0)];
			(float X, float Y) to = samples[Math.Min(k + 1, count - 1)];
			float dx = to.X - from.X, dy = to.Y - from.Y;
			float length = MathF.Sqrt(dx * dx + dy * dy);
			if (length > 0f)
			{
				tangent = (dx / length, dy / length);
				normal = (tangent.Y, -tangent.X);
			}
			if (k > 0)
			{
				float sx = samples[k].X - samples[k - 1].X, sy = samples[k].Y - samples[k - 1].Y;
				travelled += MathF.Sqrt(sx * sx + sy * sy);
			}
			float progress = Progress(stroke, k, count);
			float width = type.Start + progress * (type.Stop - type.Start);
			float u = type.TextureLoop != 0f ? travelled / type.TextureLoop : progress;
			float x = samples[k].X + stroke.OriginX, y = samples[k].Y + stroke.OriginY;
			vertices.Add(new CPlanVertex(x + normal.X * width, y + normal.Y * width, u, 0f, 0f));
			vertices.Add(new CPlanVertex(x - normal.X * width, y - normal.Y * width, u, 1f, 0f));
		}
		int body = vertices.Count;
		(float X, float Y) last = (samples[^1].X + stroke.OriginX, samples[^1].Y + stroke.OriginY);
		float hy = type.HeadHalfHeight, hx = type.HeadLength;
		vertices.Add(new CPlanVertex(last.X + normal.X * hy, last.Y + normal.Y * hy, 0f, 0f, 1f));
		vertices.Add(new CPlanVertex(last.X - normal.X * hy, last.Y - normal.Y * hy, 0f, 1f, 1f));
		vertices.Add(new CPlanVertex(last.X + normal.X * hy + tangent.X * hx, last.Y + normal.Y * hy + tangent.Y * hx, 1f, 0f, 1f));
		vertices.Add(new CPlanVertex(last.X - normal.X * hy + tangent.X * hx, last.Y - normal.Y * hy + tangent.Y * hx, 1f, 1f, 1f));
		return (vertices, body);
	}

	/// <summary>How far along it a sample is, an arrow branched off another going on from where that one's width stood.</summary>
	private static float Progress(CPlanStroke stroke, int k, int count)
	{
		if (stroke.SnapPrefix == -1 || stroke.SnapParentPoints <= 0)
		{
			return count > 1 ? k / (float)(count - 1) : 0f;
		}
		int prefix = stroke.SnapPrefix * 4;
		float whole = stroke.SnapParentPoints * 4 - 1;
		if (k < prefix)
		{
			return k / whole;
		}
		float start = prefix / whole;
		int rest = count - prefix - 1;
		return rest > 0 ? start + (k - prefix) / (float)rest * (1f - start) : start;
	}
}
