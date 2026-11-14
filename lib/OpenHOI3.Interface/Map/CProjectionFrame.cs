namespace OpenHOI3.Interface.Map;

/// <summary>
/// One frame of a projection on the map: the side of the square its picture covers, in map units; its alpha, which the
/// effect multiplies the picture's own by; and the angle it is turned by, in radians. See <see cref="CProjectionMark"/>.
/// </summary>
public readonly record struct CProjectionFrame(float Scale, float Alpha, float Angle);
