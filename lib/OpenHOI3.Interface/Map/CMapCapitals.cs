using System.Collections.Generic;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The capital markers on the map. See <c>docs/mechanics/mapunits.md</c>, section 9, from
/// <c>reversing/findings/FINDINGS-citypoint.md</c>:
///
///     a province shows the picture `capital` where it is the acting capital of its owner -
///     the owner, not the controller, and a province with no owner or no controller shows none;
///     whatever the player knows of it;
///     at the province's city point: positions.txt's `city`, or the centre of its bounding box.
/// </summary>
public static class CMapCapitals
{
	/// <summary>The name of the billboard a capital marker is drawn with, in <c>interface/mapitems.gfx</c>.</summary>
	public const string Picture = "capital";

	/// <summary>Every capital marker of the map, in the order of the provinces.</summary>
	public static List<CMapCapital> Build(CGameState state, CMap map)
	{
		return Build(state, map.CityPositions);
	}

	/// <summary>The same from the one thing of the map it reads: each province's city point.</summary>
	public static List<CMapCapital> Build(CGameState state, IReadOnlyList<CMapPoint> cities)
	{
		var found = new List<CMapCapital>();
		for (int id = 1; id < state.Provinces.Count && id < cities.Count; id++)
		{
			CProvince province = state.Provinces[id];
			if (province.Owner is not { } owner || province.Controller is null)
			{
				continue;
			}
			if (state.Country(owner) is not { } country || country.ActingCapital != id)
			{
				continue;
			}
			found.Add(new CMapCapital(id, owner, cities[id].X, cities[id].Y));
		}
		return found;
	}
}
