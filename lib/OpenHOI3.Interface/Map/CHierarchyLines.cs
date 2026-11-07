using System;
using System.Collections.Generic;
using System.Numerics;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The lines the original draws on the map for a selected unit, when its
/// <c>render_hierarchy</c> setting is on: one from the unit to the unit that commands
/// it, and one from every unit under it, at any depth, to its own commander. A line's
/// colour says how far below the selected unit its unit is, and its width the unit's
/// rank. See <c>docs/mechanics/mapunits.md</c>, section 8.
/// </summary>
public static class CHierarchyLines
{
	/// <summary>The selected unit's own line, up to its commander.</summary>
	public static readonly CColor Own = Bytes(0, 120, 0);

	/// <summary>The lines of the units directly under it.</summary>
	public static readonly CColor Under = Bytes(20, 50, 200);

	/// <summary>The lines of the units under those.</summary>
	public static readonly CColor TwoUnder = Bytes(100, 100, 210);

	/// <summary>Every line further down.</summary>
	public static readonly CColor Further = Bytes(200, 200, 200);

	/// <summary>A unit out of its commander's radio range, whatever its place.</summary>
	public static readonly CColor OutOfReach = Bytes(128, 0, 0);

	/// <summary>Half a line's width for each step of the size mark its unit's counter carries.</summary>
	public const float HalfWidthAStep = 0.2f;

	/// <summary>How much of the picture's length a stretch of line as long as it is wide takes.</summary>
	public const float PictureAlongAWidth = 0.2f;

	/// <summary>What the picture's phase gains a second, at the sixty frames a second the original adds 0.2 in each of.</summary>
	public const float PhaseASecond = 12f;

	/// <summary>Where the phase starts again.</summary>
	public const float PhaseEnd = 100f;

	/// <summary>How far the picture has slid along a line at a phase, in pictures.</summary>
	public const float SlideAPhase = 0.03f;

	/// <summary>
	/// The lines for a selected unit: none for a fleet or an air unit, and none for a
	/// unit that stands where its commander does.
	/// </summary>
	/// <param name="pointOf">The unit point of a province, in map pixels.</param>
	/// <param name="mapWidth">The map's width, for the short way round; 0 for a map with no seam.</param>
	/// <param name="isOutOfReach">Whether a unit is out of radio range of the chain above it; nobody is where it is not given.</param>
	public static List<CHierarchyLine> Build(CUnit selected, Func<int, Vector2> pointOf, float mapWidth,
		Func<CUnit, bool>? isOutOfReach = null)
	{
		var lines = new List<CHierarchyLine>();
		Add(selected, selected, pointOf, mapWidth, isOutOfReach ?? (_ => false), lines);
		return lines;
	}

	private static void Add(CUnit unit, CUnit selected, Func<int, Vector2> pointOf, float mapWidth,
		Func<CUnit, bool> isOutOfReach, List<CHierarchyLine> lines)
	{
		if (CMovementSpeed.IsLand(unit) && unit.Parent is { } commander && unit.Location > 0 && commander.Location > 0
			&& unit.Location != commander.Location)
		{
			Vector2 from = pointOf(unit.Location);
			Vector2 to = pointOf(commander.Location);
			if (mapWidth > 0f)
			{
				// The short way round: whichever end lies further east is moved a map's width west, so a line across the
				// seam hangs off the map's west edge [exe] (reversing/findings/FINDINGS-hierarchy.md, section 7.2).
				if (to.X - from.X > mapWidth * 0.5f)
				{
					to.X -= mapWidth;
				}
				else if (from.X - to.X > mapWidth * 0.5f)
				{
					from.X -= mapWidth;
				}
			}
			bool outOfReach = isOutOfReach(unit);
			lines.Add(new CHierarchyLine(from, to, CCounterText.SizeFrame(unit) * HalfWidthAStep,
				ColourOf(unit, selected, outOfReach), outOfReach));
		}
		foreach (CUnit child in unit.Children)
		{
			Add(child, selected, pointOf, mapWidth, isOutOfReach, lines);
		}
	}

	/// <summary>A line's colour, by where its unit stands under the selected one: dark red out of reach, wherever that is.</summary>
	public static CColor ColourOf(CUnit unit, CUnit selected, bool outOfReach = false)
	{
		if (outOfReach)
		{
			return OutOfReach;
		}
		if (ReferenceEquals(unit, selected))
		{
			return Own;
		}
		if (ReferenceEquals(unit.Parent, selected))
		{
			return Under;
		}
		return unit.Parent is { } commander && ReferenceEquals(commander.Parent, selected) ? TwoUnder : Further;
	}

	/// <summary>
	/// The four corners of a line - left and right of the unit's end, then left and right
	/// of its commander's - and how many pictures long it is. Nothing for a line of no
	/// length.
	/// </summary>
	public static bool Corners(CHierarchyLine line, Span<Vector2> corners, out float pictures)
	{
		Vector2 along = line.To - line.From;
		float length = along.Length();
		pictures = 0f;
		if (length <= 0f || corners.Length < 4)
		{
			return false;
		}
		Vector2 across = new Vector2(along.Y, -along.X) * (line.HalfWidth / length);
		corners[0] = line.From - across;
		corners[1] = line.From + across;
		corners[2] = line.To - across;
		corners[3] = line.To + across;
		pictures = length / (2f * line.HalfWidth) * PictureAlongAWidth;
		return true;
	}

	private static CColor Bytes(int red, int green, int blue)
	{
		return new CColor(red / 255f, green / 255f, blue / 255f);
	}
}
