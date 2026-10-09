namespace OpenHOI3.Content.Map;

/// <summary>
/// Where <c>positions.txt</c> puts one kind of building in a province, in pixels of the
/// raster with y counting north. Its picture on the map stands there. See
/// <c>docs/mechanics/mapunits.md</c>, section 9.
/// </summary>
/// <param name="Building">The building's name, as <c>common/buildings.txt</c> declares it.</param>
public readonly record struct CBuildingPosition(string Building, float X, float Y);
