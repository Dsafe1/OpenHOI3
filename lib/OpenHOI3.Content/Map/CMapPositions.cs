namespace OpenHOI3.Content.Map;

/// <summary>
/// The points <c>positions.txt</c> gives a province, each indexed by province id. See
/// <c>docs/mechanics/map.md</c>, section 4.
/// </summary>
/// <param name="Unit">Where the province "is": where a unit stands, and one end of every distance.</param>
/// <param name="Text">Where its name is written, which is what its terrain is taken around.</param>
/// <param name="NavalBase">Where its port is drawn: the land end of a distance to the sea.</param>
/// <param name="Buildings">Every building the file places in the province, in the file's order.</param>
/// <param name="Ports">Every <c>naval_base</c> position, in the file's order.</param>
public sealed record CMapPositions(CMapPosition[] Unit, CMapPosition[] Text, CMapPosition[] NavalBase,
	CBuildingPosition[][] Buildings, CPortPlacement[] Ports)
{
	/// <summary>Per province id, where and how its name is written on the near map; empty where the file was not read.</summary>
	public CProvinceLabel[] Labels { get; init; } = System.Array.Empty<CProvinceLabel>();

	/// <summary>
	/// Per province id, its city: <c>city</c> as written, not cut, or the centre of its bounding box - where the capital
	/// marker stands (<c>docs/mechanics/mapunits.md</c>, section 9). Empty where nothing set it.
	/// </summary>
	public CMapPoint[] City { get; init; } = System.Array.Empty<CMapPoint>();
}
