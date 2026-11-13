using OpenHOI3.Sim;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What one unit's counter shows: whose it is, what kind of unit, how large, and the
/// two figures. See <c>docs/mechanics/mapunits.md</c>, sections 2 and 4.
/// </summary>
public sealed class CMapCounter
{
	public required CUnit Unit { get; init; }

	/// <summary>The owner's colour, which the plate is painted in.</summary>
	public required CColor Colour { get; init; }

	/// <summary>The key of the unit type whose picture it carries, or an empty string.</summary>
	public required string TypeKey { get; init; }

	/// <summary>0 to 5: a lone brigade to a theatre.</summary>
	public required int Size { get; init; }

	/// <summary>Its attack and its defence, as <c>1 - 2</c>.</summary>
	public required string Figures { get; init; }

	/// <summary>The first eight letters of the unit's name.</summary>
	public required string Name { get; init; }

	/// <summary>How well the player knows the unit, 0 to 9 (<see cref="CUnitIntel"/>).</summary>
	public int Intel { get; init; } = CUnitIntel.Full;

	/// <summary>Its unit's place in its province's list of units on the map (<see cref="CProvinceUnitOrder"/>), from 0.</summary>
	public int ListIndex { get; set; }
}
