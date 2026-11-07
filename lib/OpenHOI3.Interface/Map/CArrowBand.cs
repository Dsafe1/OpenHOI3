using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The shape of the arrow drawn along a selected unit's way, as the original makes it:
/// a smooth curve through the points of the path - a Catmull-Rom spline - cut twenty
/// times between each two, with a band of fixed width laid along it and the head of the
/// arrow's picture over its last stretch. See <c>docs/mechanics/movement.md</c>,
/// section 7, and <c>reversing/findings/FINDINGS-movement.md</c>, sections 10 and 11.
/// Lengths are map units.
/// </summary>
public static class CArrowBand
{
	/// <summary>How many cuts are made between one point of the path and the next.</summary>
	public const int Cuts = 20;

	/// <summary>Half the band's width. The shaft's two lines are well inside it; the head fills most of it.</summary>
	public const float HalfWidth = 2f;

	/// <summary>How much of the end of the arrow the head's half of the picture is laid over.</summary>
	public const float HeadLength = 4f;

	/// <summary>The column of the picture the shaft is taken from.</summary>
	public const float ShaftColumn = 0.5f;

	/// <summary>
	/// How far beyond each end a point is added in line with the end's own leg, for the
	/// curve to have something to bend from.
	/// </summary>
	public const float Lead = 5f;

	/// <summary>Only so many of the last cuts are weighed for the head: an earlier cut near the end stays shaft.</summary>
	private const int HeadCuts = 120;

	/// <summary>How far on along the curve the second point is taken that a cut's direction is worked out from.</summary>
	private const float DirectionStep = 0.05f;

	/// <summary>A point within this of the next on each axis is left out, and so is the next.</summary>
	private const float Same = 0.1f;

	/// <summary>
	/// The cuts of the band along a path, the first at the path's first point and the last a twentieth of the last leg
	/// short of its end, each with its place along the arrow - its number over the number of cuts, so never one. Empty
	/// for a path of fewer than two points left once the close ones are taken out.
	/// </summary>
	public static List<CArrowSample> Build(IReadOnlyList<Vector2> path)
	{
		var samples = new List<CArrowSample>();
		if (path.Count < 2)
		{
			return samples;
		}

		// The two points beyond the ends, from the path as it is given, and then the original's filter over the whole:
		// the first and last always, and any other but where it is within a tenth of the next on both axes - when
		// neither it nor the next is kept.
		var given = new List<Vector2>(path.Count + 2) { path[0] - Unit(path[1] - path[0], Vector2.UnitX) * Lead };
		given.AddRange(path);
		given.Add(path[^1] + Unit(path[^1] - path[^2], Vector2.UnitX) * Lead);
		var control = new List<Vector2> { given[0] };
		for (int index = 1; index < given.Count - 1; index++)
		{
			Vector2 gap = given[index + 1] - given[index];
			if (MathF.Abs(gap.X) < Same && MathF.Abs(gap.Y) < Same)
			{
				index++;
				continue;
			}
			control.Add(given[index]);
		}
		control.Add(given[^1]);
		if (control.Count < 4)
		{
			return samples;
		}

		Vector2 end = control[^2];
		int total = (control.Count - 3) * Cuts;
		Vector2 last = default;
		for (int leg = 1; leg + 2 < control.Count; leg++)
		{
			for (int cut = 0; cut < Cuts; cut++)
			{
				float t = cut / (float)Cuts;
				Vector2 at = Curve(control, leg, t);
				Vector2 across = Across(Curve(control, leg, t + DirectionStep) - at);
				Vector2 used = across;
				if (leg > 1)
				{
					// Evened out with the cut before, which keeps the band from kinking.
					used = Unit((last + across) * 0.5f, across);
				}
				last = across;
				int number = samples.Count;
				float column = number > total - HeadCuts ? Column(at, end) : ShaftColumn;
				samples.Add(new CArrowSample(at + used * HalfWidth, at - used * HalfWidth, column, number / (float)total));
			}
		}
		return samples;
	}

	/// <summary>The column of the picture a cut is taken from: the shaft's, until the head's stretch begins.</summary>
	private static float Column(Vector2 at, Vector2 end)
	{
		float distance = Vector2.Distance(at, end);
		if (distance > HeadLength)
		{
			return ShaftColumn;
		}
		return ShaftColumn + (1f - ShaftColumn) * Math.Clamp(1f - distance / HeadLength, 0f, 1f);
	}

	/// <summary>A point of the curve between the control point numbered <paramref name="leg"/> and the next.</summary>
	private static Vector2 Curve(List<Vector2> control, int leg, float t)
	{
		Vector2 before = control[leg - 1];
		Vector2 from = control[leg];
		Vector2 to = control[leg + 1];
		Vector2 after = control[leg + 2];
		float square = t * t;
		float cube = square * t;
		return 0.5f * ((2f * from)
			+ (to - before) * t
			+ (2f * before - 5f * from + 4f * to - after) * square
			+ (3f * from - before - 3f * to + after) * cube);
	}

	/// <summary>The direction across a band that runs along a direction, one unit long.</summary>
	private static Vector2 Across(Vector2 along)
	{
		return Unit(new Vector2(-along.Y, along.X), Vector2.UnitY);
	}

	private static Vector2 Unit(Vector2 vector, Vector2 fallback)
	{
		float length = vector.Length();
		return length > 1e-6f ? vector / length : fallback;
	}
}
