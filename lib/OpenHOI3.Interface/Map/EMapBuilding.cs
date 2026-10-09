namespace OpenHOI3.Interface.Map;

/// <summary>
/// What a building's picture on the map is to a click. See
/// <c>docs/mechanics/mapunits.md</c>, section 9.
/// </summary>
public enum EMapBuilding
{
	/// <summary>Any building but the two bases.</summary>
	Other,

	/// <summary>A naval base: a click picks one of the fleets lying there.</summary>
	Port,

	/// <summary>An air base: a click picks one of the air units based there.</summary>
	Airfield,
}
