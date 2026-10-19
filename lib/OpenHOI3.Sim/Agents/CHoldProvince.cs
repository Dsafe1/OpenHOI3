namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A province an agent wants held, and how much, in thousandths: the record of the original's <c>CAIUnit::RebuildHoldProvinces</c>. See <c>docs/mechanics/moving.md</c>,
/// section 5.
/// </summary>
public sealed record CHoldProvince(int Province, long Score);
