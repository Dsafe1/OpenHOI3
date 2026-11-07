using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Agents;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The theatre map mode: grey everywhere, and each of the player's theatres - the k-th of its list - in the colour of the
/// map's k-th region, on its provinces and on its focus areas not its own country's; a later theatre over an earlier; at
/// half brightness where the player knows a province below 2. See <c>docs/mechanics/mapmodes.md</c>, section 10, and
/// <c>reversing/findings/FINDINGS-mapmodescountries.md</c>, section 4.
/// </summary>
public static class CTheatreMapColours
{
	/// <summary>The colour of every province a theatre of the player's paints, before the brightness.</summary>
	public static Dictionary<int, CColor> Build(CGameState state, CCountryTag? player, IReadOnlyList<CColor> regionColours)
	{
		var painted = new Dictionary<int, CColor>();
		if (state.Country(player) is not { } country)
		{
			return painted;
		}
		int index = 0;
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			int k = index++;
			if (k >= regionColours.Count)
			{
				continue;
			}
			CColor colour = regionColours[k];
			foreach (int id in theatre.AllProvinces)
			{
				painted[id] = colour;
			}
			var done = new HashSet<CSupplyArea>();
			foreach (CSupplyArea area in CFocusAreas.Of(state, country, theatre))
			{
				if (area.Controller == country.Tag || !done.Add(area))
				{
					continue;
				}
				foreach (int id in area.Provinces)
				{
					painted[id] = colour;
				}
			}
		}
		return painted;
	}
}
