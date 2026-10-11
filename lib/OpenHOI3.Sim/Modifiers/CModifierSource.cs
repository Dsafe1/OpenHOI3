namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// One of the things a country's modifier was summed from: the modifier as it was added -
/// a scaled one as its scaled copy - and, for a province's, the province's name to head it
/// with. See <c>docs/mechanics/modifiers.md</c>, section 8.
/// </summary>
public readonly record struct CModifierSource(CModifier Modifier, string? Heading = null);
