namespace OpenHOI3.Interface.Map;

/// <summary>
/// One building's picture on the map: whose it is, where it stands - a world point, as positions.txt writes it, in pixels
/// of the raster with y counting north and a pixel's middle on whole coordinates, which the 2D map draws through
/// <see cref="CWorldPoint"/> - and which frame of its picture shows.
/// </summary>
/// <param name="Building">The building's name; its picture is the billboard <c>building_</c> and that name.</param>
/// <param name="Frame">0, or for a base with something in it 1.</param>
public readonly record struct CMapBuilding(int Province, string Building, EMapBuilding Kind, float X, float Y, int Frame);
