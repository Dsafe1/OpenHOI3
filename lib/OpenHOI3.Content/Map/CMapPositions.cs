namespace OpenHOI3.Content.Map;

/// <summary>
/// The points <c>positions.txt</c> gives a province, each indexed by province id. See
/// <c>docs/mechanics/map.md</c>, section 4.
/// </summary>
/// <param name="Unit">Where the province "is": where a unit stands, and one end of every distance.</param>
/// <param name="Text">Where its name is written, which is what its terrain is taken around.</param>
/// <param name="NavalBase">Where its port is drawn: the land end of a distance to the sea.</param>
public sealed record CMapPositions(CMapPosition[] Unit, CMapPosition[] Text, CMapPosition[] NavalBase);
