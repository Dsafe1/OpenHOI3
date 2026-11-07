using System;
using System.Numerics;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// How large a counter is and where the things on it go, in map units with x east and
/// y north. A counter type gives its offsets and its text size in tenths of its
/// <c>scale</c>, and a counter's own scale grows with its rank. See
/// <c>docs/mechanics/mapunits.md</c>, section 2.
/// </summary>
public static class CCounterLayout
{
	/// <summary>What a counter type's offsets and text size are counted in: tenths of its scale.</summary>
	private const float Tenth = 0.1f;

	/// <summary>How far each counter of a fanned stack is drawn from the one under it, west and north.</summary>
	public const float FanStep = 0.3f;

	/// <summary>
	/// A counter's own scale: 1 for a division, more for each rank above and less for a lone brigade - the growth with rank
	/// multiplied by the player's <c>counter_scale</c> (settings.txt, 0 to 2), which leaves a division as it is [exe].
	/// </summary>
	public static float Scale(CCounterType type, int size, float counterScale = 1f)
	{
		return (size - 1) * type.LevelScale * counterScale + 1f;
	}

	/// <summary>Half a counter's height. It is as wide as its plate's picture makes it.</summary>
	public static float HalfHeight(CCounterType type, int size, float counterScale = 1f)
	{
		return type.Scale * Scale(type, size, counterScale);
	}

	/// <summary>How many of a stack's counters are drawn: the top ones, up to six.</summary>
	public static int Drawn(int count)
	{
		return Math.Min(count, CMapCounters.MostFanned);
	}

	/// <summary>Where the n-th counter drawn of a stack stands from the stack's own place, the first at nothing.</summary>
	public static Vector2 FanOffset(int drawn)
	{
		return new Vector2(-FanStep * drawn, FanStep * drawn);
	}

	/// <summary>
	/// How much of the size a counter type gives a text one line of its font comes out
	/// at. Measured on a screenshot of the original, 2026-10-09: with a font whose line is
	/// 18 units the digits are the height a line of 20 would make them, and the name the
	/// same. What the original divides by was not read.
	/// </summary>
	public const float LineOfSize = 0.9f;

	/// <summary>The size the name is written at, in tenths of a counter type's scale: the original's own figure.</summary>
	public const float NameSize = 3.5f;

	/// <summary>How wide the dark frame round the flag is, in a counter type's scales. Measured on the same screenshot.</summary>
	public const float FlagFrameWidth = 0.025f;

	/// <summary>
	/// Where the two figures start, from the counter's middle: the left end of the line
	/// their letters stand on.
	/// </summary>
	public static Vector2 FiguresPlace(CCounterType type, int size, float counterScale = 1f)
	{
		return Place(type.FiguresOffset, type, size, counterScale);
	}

	/// <summary>How high a line of the two figures is.</summary>
	public static float FiguresHeight(CCounterType type, int size, float counterScale = 1f)
	{
		return type.TextSize * type.Scale * Tenth * Scale(type, size, counterScale) * LineOfSize;
	}

	/// <summary>
	/// Where the name starts, from the counter's middle: the end of the line its letters
	/// stand on that the first letter is at. The name is written turned a quarter, running
	/// south from there with the tops of its letters to the east.
	/// </summary>
	public static Vector2 NamePlace(CCounterType type, int size, float counterScale = 1f)
	{
		return Place(type.NameOffset, type, size, counterScale);
	}

	/// <summary>How high a line of the name is.</summary>
	public static float NameHeight(CCounterType type, int size, float counterScale = 1f)
	{
		return NameSize * type.Scale * Tenth * Scale(type, size, counterScale) * LineOfSize;
	}

	/// <summary>
	/// Where the stack of units on the move stands from the province's own point, east
	/// and north. Measured on a screenshot of the original, 2026-10-09, in which a
	/// division setting off lies half a counter east and a third of one north of the
	/// stack it leaves. The original's own figure, read since, is 2.5 and 1.0; the
	/// measure stays until the maintainer has compared the two.
	/// </summary>
	public static readonly Vector2 MovingStack = new(2.6f, 1.5f);

	/// <summary>
	/// Where the middle of a moving unit's pointer goes, from the middle of its counter:
	/// the pointer's own <c>offset</c> away, in the direction the unit is going - whatever the counter's own scale, which
	/// the original hands the pointer where its draw never reads it (<c>reversing/findings/FINDINGS-billboardfx.md</c>,
	/// section 2).
	/// </summary>
	/// <param name="heading">The direction of travel as an angle from east, turning towards north.</param>
	public static Vector2 PointerPlace(CBillboardType pointer, float heading)
	{
		return new Vector2(MathF.Cos(heading) * pointer.Offset, MathF.Sin(heading) * pointer.Offset);
	}

	/// <summary>Half the side of the square the pointer is drawn in: its <c>scale</c>, at a draw scale of one.</summary>
	public static float PointerHalfSize(CBillboardType pointer)
	{
		return pointer.Scale;
	}

	/// <summary>
	/// Which way a unit in one place is going to another: the angle from east, turning
	/// towards north, of the line between them, the short way round a map so wide.
	/// </summary>
	public static float Heading(float fromX, float fromY, float toX, float toY, float mapWidth)
	{
		float across = toX - fromX;
		if (mapWidth > 0f)
		{
			across -= MathF.Round(across / mapWidth) * mapWidth;
		}
		return MathF.Atan2(toY - fromY, across);
	}

	/// <summary>How wide the dark frame round the flag is.</summary>
	public static float FlagFrame(CCounterType type)
	{
		return type.Scale * FlagFrameWidth;
	}

	/// <summary>Where the middle of the flag goes, from the counter's middle.</summary>
	public static Vector2 FlagPlace(CCounterType type, int size, float counterScale = 1f)
	{
		return Place(type.FlagOffset, type, size, counterScale);
	}

	/// <summary>The flag's width and height.</summary>
	public static Vector2 FlagSize(CCounterType type)
	{
		return new Vector2(type.FlagWidth * type.Scale, type.FlagHeight * type.Scale);
	}

	private static Vector2 Place(Vector3 offset, CCounterType type, int size, float counterScale)
	{
		// The file's three numbers are east, up off the map, and north.
		float unit = type.Scale * Tenth * Scale(type, size, counterScale);
		return new Vector2(offset.X * unit, offset.Z * unit);
	}
}
