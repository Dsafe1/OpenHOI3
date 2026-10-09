namespace OpenHOI3.Content.Map;

/// <summary>
/// One <c>naval_base</c> position of <c>positions.txt</c>, in the order the file gives
/// them. The order is kept because it is the order the original numbers its ports in:
/// see <see cref="CMapPorts"/>.
/// </summary>
public readonly record struct CPortPlacement(int Province, float X, float Y);
