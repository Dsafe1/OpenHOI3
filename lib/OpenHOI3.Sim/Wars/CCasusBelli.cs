namespace OpenHOI3.Sim.Wars;

/// <summary>
/// A casus belli held, on the holder's record towards the other: its type's number - nought, the null
/// type, for every one made in play - and the hour it ends, or none. See <c>docs/mechanics/war.md</c>,
/// section 2.
/// </summary>
public sealed record CCasusBelli(int Type, long? End = null);
