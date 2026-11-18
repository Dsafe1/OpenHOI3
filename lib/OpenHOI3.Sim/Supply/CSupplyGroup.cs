using System.Collections.Generic;

namespace OpenHOI3.Sim.Supply;

/// <summary>
/// Areas that touch and are on one side - a country's with its puppets' and its
/// master's - which share a depot. See <c>docs/mechanics/supply.md</c>, section 3.
/// </summary>
public sealed class CSupplyGroup
{
	/// <summary>Its areas, the one it was started from first.</summary>
	public List<CSupplyArea> Areas { get; } = new();

	/// <summary>The province its areas draw on, or 0 for a group that was given none.</summary>
	public int Depot { get; set; }

	/// <summary>
	/// A region group's place among the region groups, in the order they were made: what stands in for the order of their
	/// addresses, by which the original's AI keeps its maps of them (<c>docs/mechanics/moving.md</c>, section 19).
	/// </summary>
	public int Order { get; set; }
}
