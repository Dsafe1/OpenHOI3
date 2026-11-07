using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The region map mode: each province in the colour of the first region that lists it, grey where none does, at half
/// brightness where the player knows it below 2; no stripes. A region's colour is handed out from
/// <c>interface/colors.txt</c> in the map's order of regions, an empty region taking none - and where the list runs out the
/// region met then keeps white and the list starts again. See <c>docs/mechanics/mapmodes.md</c>, section 7, and
/// <c>reversing/findings/FINDINGS-mapmodesrest.md</c>, section 2.
/// </summary>
public static class CRegionMapColours
{
	public static readonly CColor White = new(1f, 1f, 1f);

	/// <summary>Each region's colour, by its number, as the original hands them out once at start-up.</summary>
	public static CColor[] Assign(IReadOnlyList<CRegion> regions, IReadOnlyList<CColor> colours)
	{
		var assigned = new CColor[regions.Count];
		int next = 0;
		for (int index = 0; index < regions.Count; index++)
		{
			assigned[index] = White;
			if (regions[index].Provinces.Count == 0)
			{
				continue;
			}
			if (next >= colours.Count)
			{
				// The list has run out: this region keeps white, and the next starts it again.
				next = 0;
				continue;
			}
			assigned[index] = colours[next++];
		}
		return assigned;
	}

	/// <summary>
	/// Each continent's colour, by its key, from the list's first colour again in <c>map/continent.txt</c>'s order, with no
	/// test for an empty one - what the region mode paints with the editor's continent painting on (the console's
	/// <c>cpaint</c>). A continent past the list's end keeps white, as one the original hands nothing.
	/// </summary>
	public static Dictionary<string, CColor> AssignContinents(IReadOnlyList<string> continents, IReadOnlyList<CColor> colours)
	{
		var assigned = new Dictionary<string, CColor>(System.StringComparer.Ordinal);
		for (int index = 0; index < continents.Count; index++)
		{
			assigned[continents[index]] = index < colours.Count ? colours[index] : White;
		}
		return assigned;
	}

	/// <summary>
	/// A province's colour with continent painting on: its continent's colour, at half brightness where the player knows it
	/// below 2, grey where it has none.
	/// </summary>
	public static CColor OfContinent(CGameState state, CProvince province, CCountryTag? player,
		IReadOnlyDictionary<string, CColor> continentColours)
	{
		float b = CVictoryPointMapColours.Brightness(state, province, player);
		if (province.Continent.Length > 0 && continentColours.TryGetValue(province.Continent, out CColor colour))
		{
			return CSimplifiedTerrainColours.Scaled(colour, b);
		}
		return new CColor(0.5f * b, 0.5f * b, 0.5f * b);
	}

	public static CColor Of(CGameState state, CProvince province, CCountryTag? player, IReadOnlyList<CColor> regionColours)
	{
		float b = CVictoryPointMapColours.Brightness(state, province, player);
		if (province.Regions.Count > 0 && province.Regions[0] < regionColours.Count)
		{
			return CSimplifiedTerrainColours.Scaled(regionColours[province.Regions[0]], b);
		}
		return new CColor(0.5f * b, 0.5f * b, 0.5f * b);
	}
}
