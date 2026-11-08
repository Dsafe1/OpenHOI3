namespace OpenHOI3.Interface.BattlePlans;

/// <summary>One vertex of an arrow: its place on the map, the two coordinates of its picture, and whether it is of the head.</summary>
public readonly record struct CPlanVertex(float X, float Y, float U, float V, float IsHead);
