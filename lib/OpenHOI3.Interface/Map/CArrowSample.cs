using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// One cut across the band of a unit's arrow: its two edges, where along the arrow's
/// picture it is taken from, and how far along the arrow it is.
/// </summary>
/// <param name="Left">One edge of the band.</param>
/// <param name="Right">The other.</param>
/// <param name="Along">The column of the picture, 0 to 1: a half for the shaft, rising to 1 at the tip.</param>
/// <param name="Progress">How far along the arrow the cut is, 0 at its start and 1 at its end.</param>
public readonly record struct CArrowSample(Vector2 Left, Vector2 Right, float Along, float Progress);
