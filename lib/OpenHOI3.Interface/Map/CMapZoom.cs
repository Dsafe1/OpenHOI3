using System;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The original's zoom, in the camera's heights: a wheel notch sets a target height from the current one and pulls the
/// look target towards the point under the pointer; the height and the look point then close a fixed part of the gap
/// every frame; the zoom buttons move the target by a fifth. Every figure is the original's, in the precision it works
/// in (<c>reversing/findings/FINDINGS-camerazoom.md</c>; <c>docs/mechanics/mapunits.md</c>, section 7).
/// </summary>
public static class CMapZoom
{
	/// <summary>The part of the gap the height and the look point close every frame, the double the original holds [exe].</summary>
	public const double Ease = 0.19699999690055847;

	/// <summary>The settings' <c>zoom_speed</c> in a new install [mod: settings.txt].</summary>
	public const float DefaultSpeed = 50f;

	/// <summary>How far inside the screen's edges the pointer has to be for a notch to pull the look target, in pixels [exe].</summary>
	public const float PullMargin = 6f;

	/// <summary>
	/// The step a notch is sized by: <c>float(float(height / 60 × 0.8) × zoom_speed / 100)</c>, from the current height
	/// and not the target [exe the constants, 0.8 as the float the original holds].
	/// </summary>
	public static float Step(float height, float zoomSpeed)
	{
		float scaled = (float)(height / 60.0 * 0.800000011920929);
		return (float)(scaled * (double)zoomSpeed / 100.0);
	}

	/// <summary>
	/// The target height after a notch: less <c>step × 20 × 0.75</c> towards the map, more <c>step × 20 × 1.5</c> away,
	/// held to the limits - at the default speed a tenth of the current height in and a fifth out.
	/// </summary>
	public static float NotchTarget(float wanted, float height, float zoomSpeed, bool inwards, float lowest, float highest)
	{
		double step = Step(height, zoomSpeed);
		float target = inwards ? (float)(wanted - step * 20.0 * 0.75) : (float)(wanted + step * 20.0 * 1.5);
		return inwards ? Math.Max(target, lowest) : Math.Min(target, highest);
	}

	/// <summary>
	/// What a notch leaves of the look target's distance from the point under the pointer: <c>1 − step × 20 / height</c>,
	/// held between 0 and 1 - so it moves (4/15) × zoom_speed / 100 of the way, 0.133 at the default, in or out.
	/// </summary>
	public static float PullLeft(float height, float zoomSpeed)
	{
		if (height <= 0f)
		{
			return 0f;
		}
		return (float)Math.Clamp(1.0 - Step(height, zoomSpeed) * 20.0 / height, 0.0, 1.0);
	}

	/// <summary>The look target after a notch's pull towards the point under the pointer: <c>(target − m) × left + m</c>.</summary>
	public static float Pull(float target, float under, float left)
	{
		return (float)((target - (double)under) * left + under);
	}

	/// <summary>One frame of the ease: <c>current + (target − current) × 0.197</c>, worked in double and kept as a float.</summary>
	public static float EaseTowards(float current, float target)
	{
		return (float)(current + (target - (double)current) * Ease);
	}

	/// <summary>
	/// The zoom buttons' target, Page Down and Page Up: the target less or more <c>float(float(target / 20) × 20) × 0.2</c>,
	/// a fifth of the target and not of the height, whatever <c>zoom_speed</c> is, held to the limits [exe].
	/// </summary>
	public static float ButtonTarget(float wanted, bool inwards, float lowest, float highest)
	{
		float fifth = (float)((float)((float)(wanted / 20.0) * 20.0) * 0.20000000298023224);
		return inwards ? Math.Max(wanted - fifth, lowest) : Math.Min(wanted + fifth, highest);
	}
}
