namespace OpenHOI3.Sim.Agents;

/// <summary>What a plan asks to be added to it, one count for each unit type, with the two powers worked out on the way.</summary>
public sealed record CPlanWanted(int[] Wanted, long OurPower, long TheirPower);
