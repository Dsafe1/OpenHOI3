using System.Numerics;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// A vertex of a river's strip as <c>river.fx</c> takes it: its place in the map's world - x a column, y up, z counted from
/// the south - its coordinate in <c>river_body.tga</c> and its coordinate in <c>river_movement.tga</c>.
/// </summary>
public readonly record struct CRiverVertex(Vector3 Position, Vector2 Body, Vector2 Flow);
