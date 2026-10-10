using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the air orders window keeps of itself when it closes, and the next one opens
/// with: the session's, not a unit's. See <c>docs/mechanics/fleetsandair.md</c>, section 4.
/// </summary>
/// <param name="Order">The order that was being made, or none - which ticks the unit's own.</param>
public readonly record struct CAirOrdersMemory(int Priority, int Stance, EAirAreaShape Shape, EUnitOrder Order)
{
	/// <summary>A session's first: priority 0, the aggressive stance, the point, the unit's own order.</summary>
	public static CAirOrdersMemory First => new(0, CMission.Aggressive, EAirAreaShape.Point, EUnitOrder.None);
}
