namespace OpenHOI3.Sim.Leaders;

/// <summary>
/// One thing a trait does: which kind, by how much in thousandths, and for the three
/// kinds that are about one terrain, which terrain - otherwise empty.
/// </summary>
public readonly record struct CTraitEffect(ETraitEffect Kind, CFixedPoint Value, string Terrain);
