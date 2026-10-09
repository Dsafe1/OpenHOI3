namespace OpenHOI3.Sim.Provinces;

/// <summary>
/// The way from one province to a neighbour. It belongs to the province it leaves, and
/// the way back is another edge with its own figures: see
/// <c>docs/mechanics/movement.md</c>, section 2.
/// </summary>
/// <param name="To">The neighbour's id.</param>
/// <param name="Through">The province a crossing goes through, or 0.</param>
/// <param name="Distance">How far it is, which is what a unit's progress is measured against.</param>
public readonly record struct CProvinceEdge(int To, EProvinceEdgeKind Kind, int Through, CFixedPoint Distance);
