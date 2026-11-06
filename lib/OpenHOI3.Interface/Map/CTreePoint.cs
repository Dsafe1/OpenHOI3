namespace OpenHOI3.Interface.Map;

/// <summary>
/// A tree the map plants: its pixel, x a column and y counted from the south; the province there, for the weather mode's
/// snow; its tree type; its kind, the row of the type's sheet, 0 to 3; and whether its province is land, which lifts it by
/// 0.02 (<c>docs/mechanics/mapdraw.md</c>, section 4).
/// </summary>
public readonly record struct CTreePoint(int X, int Y, int Province, int Type, int Kind, bool OnLand);
