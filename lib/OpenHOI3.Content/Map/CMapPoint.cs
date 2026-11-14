namespace OpenHOI3.Content.Map;

/// <summary>
/// A point of the map as <c>positions.txt</c> writes it, in map pixels with y counting north and not cut to whole pixels:
/// the original's own points are floats. See <c>docs/mechanics/map.md</c>, section 4.
/// </summary>
public readonly record struct CMapPoint(float X, float Y);
