using System;
using System.Collections.Generic;
using OpenHOI3.Content.Map;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// What the far view's country names are laid out over (<c>docs/mechanics/mapnames.md</c>): the map's size, the province
/// at a pixel - x the column, y the row counted from the south, the world's z - which provinces are land, who controls
/// each, each one's box on the raster and its neighbours in the map's order.
/// </summary>
public sealed class CMapNameWorld
{
	public required int Width { get; init; }

	public required int Height { get; init; }

	/// <summary>The province at a pixel on the map, or 0.</summary>
	public required Func<int, int, int> ProvinceAt { get; init; }

	/// <summary>Whether a province is land: sea zones and lakes are not.</summary>
	public required Func<int, bool> IsLand { get; init; }

	/// <summary>A province's controller, any number standing for a country, 0 for none.</summary>
	public required Func<int, int> Controller { get; init; }

	public required Func<int, CProvinceShape> Shape { get; init; }

	public required Func<int, IEnumerable<int>> Neighbours { get; init; }
}
