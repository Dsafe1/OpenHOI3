namespace OpenHOI3.Sim.Units;

/// <summary>A leader a file of units puts on a unit, by his number, and the kind of unit (<see cref="COrderOfBattleSummary"/>).</summary>
public readonly record struct COrderOfBattleLeader(int Leader, EUnitKind Kind);
