using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Every holder's provinces with an air base built, in the map's order: worked out in one walk of the provinces and kept
/// for the hour while no province changes hands, as the agents' air-base lists ask it over and over in an hour's pass
/// (<see cref="CAirBaseLists"/>). Ours: the original walks the map for each country it asks about.
/// </summary>
public sealed class CAirBaseHolders
{
	private readonly Dictionary<CCountryTag, List<int>> _bases = new();

	private CAirBaseHolders(long hour, int controlChanges)
	{
		Hour = hour;
		ControlChanges = controlChanges;
	}

	private long Hour { get; }

	private int ControlChanges { get; }

	/// <summary>The bases for the state as it stands, worked out again where the hour or the map has moved on.</summary>
	public static CAirBaseHolders Of(CGameState state, CBuilding? airBase)
	{
		if (state.AirBaseHolders is { } kept && kept.Hour == state.TotalHours && kept.ControlChanges == state.ControlChanges)
		{
			return kept;
		}
		var holders = new CAirBaseHolders(state.TotalHours, state.ControlChanges);
		if (airBase is not null)
		{
			foreach (CProvince province in state.Provinces)
			{
				if (province.Controller is { } holder && province.BuildingMaxLevel(airBase).Raw > 0)
				{
					if (!holders._bases.TryGetValue(holder, out List<int>? held))
					{
						holders._bases[holder] = held = new List<int>();
					}
					held.Add(province.Id);
				}
			}
		}
		state.AirBaseHolders = holders;
		return holders;
	}

	/// <summary>The provinces a country holds with an air base built, in their order. Not to be changed.</summary>
	public IReadOnlyList<int> Of(CCountryTag country)
	{
		return _bases.TryGetValue(country, out List<int>? held) ? held : System.Array.Empty<int>();
	}
}
