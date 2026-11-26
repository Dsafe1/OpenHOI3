namespace OpenHOI3.Sim.Units;

/// <summary>A province a file of units puts units in, and how many (<see cref="COrderOfBattleSummary"/>).</summary>
public readonly record struct COrderOfBattlePlace(int Province, int Units);
