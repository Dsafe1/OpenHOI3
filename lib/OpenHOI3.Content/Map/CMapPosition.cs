namespace OpenHOI3.Content.Map;

/// <summary>
/// The one point that stands for a province, in whole pixels of the raster with y
/// counting north. See <c>docs/mechanics/map.md</c>, section 4.
/// </summary>
public readonly record struct CMapPosition(int X, int Y);
