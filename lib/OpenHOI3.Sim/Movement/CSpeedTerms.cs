namespace OpenHOI3.Sim.Movement;

/// <summary>
/// The terms a unit's speed is multiplied by, each in thousandths where 1000 changes nothing, as the original's speed
/// tooltip works them out again for its lines: a battle, fuel, the next province's ground, the leaders' trait for it, a
/// river or crossing on the way, the weather and the province's own modifier apart, the mobile brigades' bonus, and
/// falling back. See <c>docs/mechanics/unitpanel.md</c>, section 10.2.
/// </summary>
public sealed record CSpeedTerms(long Combat, long Fuel, long Terrain, long Trait, long Adjacency, long Weather, long LocalModifier,
	long MobileBonus, long Retreating);
