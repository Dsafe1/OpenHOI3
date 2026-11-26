using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Combat;

/// <summary>What one shot asked of its target this hour, and who fired it (<see cref="CBattleDamage"/>).</summary>
public readonly record struct CBattleDamageAsk(CUnit Attacker, long Strength, long Organisation);
