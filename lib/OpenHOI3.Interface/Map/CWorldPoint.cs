using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// Where the 2D map draws a point of the original's world - a point of <c>positions.txt</c>, x east and y north, a pixel's
/// middle on whole coordinates. See <c>docs/mechanics/mapdraw.md</c>, section 3:
///
///     the 2D map has north at the top and a pixel spanning [x, x+1], so the world's (x, y) is drawn at
///     (x + 0.5, H - 0.5 - y), H the map's height in pixels.
///
/// Every picture the original hangs on a province - a building's, a capital marker, a large base's, a ring of range, a
/// resource icon - stands at such a point, and goes through here to be drawn.
/// </summary>
public static class CWorldPoint
{
	/// <summary>The 2D map's point for the world's (x, y), on a map <paramref name="mapHeight"/> pixels high.</summary>
	public static Vector2 ToMap(float x, float y, int mapHeight)
	{
		return new Vector2(x + 0.5f, mapHeight - 0.5f - y);
	}
}
