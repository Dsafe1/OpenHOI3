namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// What sort of connection two provinces have. The numbers are the original's. See
/// <c>docs/mechanics/movement.md</c>, section 2.
/// </summary>
public enum EProvinceEdgeKind
{
	/// <summary>The two touch, or the adjacency file joins them with nothing said.</summary>
	Plain = 0,

	/// <summary>A crossing through another province, which whoever holds that province can close.</summary>
	Crossing = 1,

	/// <summary>A river lies between the two.</summary>
	River = 2,

	/// <summary>Nothing may pass.</summary>
	Impassable = 3,
}
