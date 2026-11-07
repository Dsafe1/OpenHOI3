using System;
using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The arithmetic of the minimap, as the original works it: the white frame that shows the camera's view, where a
/// press on the picture takes the camera, and how far the day and night overlay is slid. Positions are the screen's,
/// y down; the map's are its pixels with y counted north. See <c>docs/mechanics/minimap.md</c> and
/// <c>reversing/findings/FINDINGS-minimap.md</c>, sections 2 to 4.
/// </summary>
public static class CMinimapFrame
{
	/// <summary>The span of the camera's heights, highest less lowest, that the frame's size is a share of.</summary>
	private const float HeightSpan = CMapCamera.HighestHeight - CMapCamera.LowestHeight;

	/// <summary>The original's single-precision tenth, widened, that the frame's half sides are scaled by.</summary>
	private const double Tenth = 0.1f;

	/// <summary>
	/// The frame's five points, the first again at the end, each coordinate cut to a whole pixel - a straight line in the
	/// camera's height and not the view's projection, never clipped to the picture nor wrapped round it:
	///
	///     centre   the minimap's corner + (x / map width, 1 - y / map height) of its size, 4 up
	///     t        height / 960
	///     half     (int(1410 t + 70), int(820 t + 80)) x 0.1 x 0.25
	/// </summary>
	public static Vector2[] Corners(float cameraX, float cameraY, float height, float mapWidth, float mapHeight,
		Vector2 corner, Vector2 size)
	{
		float across = cameraX / mapWidth;
		float up = cameraY / mapHeight;
		float centreX = corner.X + across * size.X;
		float centreY = corner.Y + (size.Y - up * size.Y - 4f);
		float share = height / HeightSpan;
		double halfWidth = (int)(share * 1410f + 70f) * Tenth * 0.25;
		double halfHeight = (int)(share * 820f + 80f) * Tenth * 0.25;
		Vector2 At(double x, double y) => new((int)x, (int)y);
		return new[]
		{
			At(centreX - halfWidth, centreY - halfHeight),
			At(centreX - halfWidth, centreY + halfHeight),
			At(centreX + halfWidth, centreY + halfHeight),
			At(centreX + halfWidth, centreY - halfHeight),
			At(centreX - halfWidth, centreY - halfHeight),
		};
	}

	/// <summary>
	/// Where a left press on the minimap takes the camera, or false where it is not strictly inside the picture: its
	/// border row and column do nothing.
	///
	///     x = (rx - 1) x map width / its width          y = (h - (ry - 1)) x map height / h
	/// </summary>
	public static bool Press(Vector2 mouse, Vector2 corner, Vector2 size, float mapWidth, float mapHeight,
		out float x, out float y)
	{
		int across = (int)mouse.X - (int)corner.X;
		int down = (int)mouse.Y - (int)corner.Y;
		x = 0f;
		y = 0f;
		if (across <= 0 || across >= size.X || down <= 0 || down >= size.Y)
		{
			return false;
		}
		x = (across - 1) * (mapWidth / size.X);
		y = (size.Y - (down - 1)) * (mapHeight / size.Y);
		return true;
	}

	/// <summary>
	/// How far the day and night overlay is slid along its texture: its frame, the hour of the day, less one, over its
	/// steps, times the share of the texture its picture fills.
	/// </summary>
	public static float OverlayShift(int hourOfDay, int steps, float extent)
	{
		return (hourOfDay - 1) / (float)Math.Max(1, steps) * extent;
	}
}
