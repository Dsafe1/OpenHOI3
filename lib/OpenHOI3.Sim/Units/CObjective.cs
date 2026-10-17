namespace OpenHOI3.Sim.Units;

/// <summary>
/// A province a battle plan aims at, with a priority, and whether it is to be held and whether it is reached from the
/// sea. See <c>docs/mechanics/agents.md</c>, section 7.4.
/// </summary>
public sealed record CObjective(int Province, int Priority, bool Hold = false, bool Invasion = false);
