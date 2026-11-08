using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// What makes a line's brigade a change of type: the type it was, how much of its full strength it had, in thousandths, and
/// the three defines its price is worked out by, which do not change in a game. See <c>docs/mechanics/upgrades.md</c>, section 3.
/// </summary>
public sealed record CBrigadeUpgradeOrder(CSubUnitType From, long Strength, CFixedPoint CostFactor, CFixedPoint TimeFactor,
	CFixedPoint PracticalFactor);
