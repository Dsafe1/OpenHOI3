using System.Numerics;
using OpenHOI3.Sim;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// One line of the hierarchy drawn on the map for a selected unit: from a unit's province
/// to the province of the unit that commands it. See <c>docs/mechanics/mapunits.md</c>,
/// section 8.
/// </summary>
/// <param name="From">The unit's end, in map pixels.</param>
/// <param name="To">Its commander's end, the short way round the map from the other.</param>
/// <param name="HalfWidth">Half the line's width, in map pixels.</param>
/// <param name="Colour">What the line's picture is tinted with.</param>
/// <param name="IsBroken">Drawn dashed and still, for a unit out of its commander's reach.</param>
public readonly record struct CHierarchyLine(Vector2 From, Vector2 To, float HalfWidth, CColor Colour, bool IsBroken);
