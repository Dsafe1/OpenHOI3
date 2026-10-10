namespace OpenHOI3.Interface.Map;

/// <summary>
/// One province's three counts of the past week in the air map mode. Each is a byte read back
/// signed, as the original keeps it: 128 of a kind reads below one and shows no icon. See
/// <c>docs/mechanics/airmap.md</c>, section 4.
/// </summary>
public readonly record struct CAirMapCount(sbyte BombedUs, sbyte BombedThem, sbyte Battles);
