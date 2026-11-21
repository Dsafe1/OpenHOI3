using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The map scrolled by the pointer at the screen's edge, as the original's camera does it each frame after the arrow
/// keys and the drag: four tests - left, right, top, bottom - of the pointer against the view's edges with a margin of
/// six pixels, at the one speed all scrolling has (<c>docs/mechanics/map.md</c>, section 8). An enhanced game's option
/// turns the four tests off and leaves the keys and the drag alone (<c>docs/mechanics/gameoptions.md</c>, section 5).
/// </summary>
public static class CMapEdgeScroll
{
	/// <summary>How near an edge the pointer scrolls, in pixels [exe].</summary>
	public const float Margin = 6f;

	/// <summary>
	/// Which way the pointer scrolls the map: -1, 0 or 1 across and down. A pointer past an edge - off the window, on a
	/// second screen beside it - scrolls as one at the edge does, so the map goes on moving as the pointer runs out of
	/// the window (the user's, 2026-11-22). <paramref name="pointer"/> is measured from the view's corner, in its pixels.
	/// </summary>
	public static Vector2 Direction(Vector2 pointer, Vector2 view)
	{
		float across = pointer.X <= Margin ? -1f : pointer.X >= view.X - Margin ? 1f : 0f;
		float down = pointer.Y <= Margin ? -1f : pointer.Y >= view.Y - Margin ? 1f : 0f;
		return new Vector2(across, down);
	}
}
