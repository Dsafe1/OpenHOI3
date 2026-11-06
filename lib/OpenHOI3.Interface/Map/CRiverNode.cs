namespace OpenHOI3.Interface.Map;

/// <summary>
/// A pixel a river's trace passed through: x a column and y counted from the south, its width - the river pixel's index
/// less 2, 1 at the source - and whether it carries the source's mark (<c>docs/mechanics/mapdraw.md</c>, section 4).
/// </summary>
public readonly record struct CRiverNode(int X, int Y, int Width, bool IsSource);
