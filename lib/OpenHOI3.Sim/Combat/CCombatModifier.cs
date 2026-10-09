namespace OpenHOI3.Sim.Combat;

/// <summary>
/// One thing an hour of a battle holds for or against a unit: what it is, and how much
/// it adds to the unit's attack and to its defence, where 0.1 is a tenth more and -0.1 a
/// tenth less. On land the two are the same figure.
/// </summary>
public readonly record struct CCombatModifier(ECombatModifier Kind, CFixedPoint Attack, CFixedPoint Defence);
