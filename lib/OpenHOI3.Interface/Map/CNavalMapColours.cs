using System.Collections.Generic;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The naval map mode's colours, land and sea: grey everywhere, green where the player's fleets patrol - the province and the
/// open water beside it - and along each escort's beat, red stripes where they raid convoys, the province and the open water
/// beside it; every colour at half alpha. See <c>docs/mechanics/mapmodes.md</c>, section 11, and
/// <c>reversing/findings/FINDINGS-mapmodesrest.md</c>, section 3.1.
/// </summary>
public static class CNavalMapColours
{
	public static readonly CColor Grey = new(0.5f, 0.5f, 0.5f, 0.5f);
	public static readonly CColor Patrol = new(0.25f, 1f, 0.25f, 0.5f);
	public static readonly CColor Raid = new(1f, 0.25f, 0.25f, 0.5f);
	public static readonly CColor Undeclared = new(1f, 210f / 255f, 0.25f, 0.5f);

	/// <summary>The two colours of every province the player's naval orders paint; any other is grey.</summary>
	public static Dictionary<int, (CColor Ground, CColor Stripes)> Build(CGameState state, CCountryTag? player)
	{
		var painted = new Dictionary<int, (CColor Ground, CColor Stripes)>();
		if (state.Country(player) is not { } country)
		{
			return painted;
		}
		// The regions of its undeclared wars first, which what follows may paint over (war.md, section 15).
		foreach (Sim.Wars.CUndeclaredWar war in state.UndeclaredWars)
		{
			if (!war.Attackers.Contains(country.Tag) && !war.Defenders.Contains(country.Tag))
			{
				continue;
			}
			foreach (CRegion region in state.Regions)
			{
				if (war.Regions.Contains(region.Name))
				{
					foreach (int id in region.Provinces)
					{
						painted[id] = (Undeclared, Undeclared);
					}
				}
			}
		}
		foreach (CUnit unit in country.AllUnits())
		{
			switch (unit.Order)
			{
				case EUnitOrder.Patrol:
					foreach (int id in Around(state, country, unit.OrderProvince))
					{
						(CColor ground, CColor stripes) = painted.GetValueOrDefault(id, (Grey, Grey));
						painted[id] = (Patrol, stripes == Grey ? Patrol : stripes);
					}
					break;
				case EUnitOrder.ConvoyRaid:
					foreach (int id in Around(state, country, unit.OrderProvince))
					{
						(CColor ground, CColor stripes) = painted.GetValueOrDefault(id, (Grey, Grey));
						painted[id] = (ground == Grey ? Raid : ground, Raid);
					}
					break;
				case EUnitOrder.ConvoyEscort:
					if (CConvoyEscort.Find(state, unit.Mission.Convoy) is not { } convoy)
					{
						break;
					}
					bool within = false;
					foreach (int id in convoy.Path)
					{
						within |= id == unit.Mission.BeatFirst;
						if (within)
						{
							(CColor ground, CColor stripes) = painted.GetValueOrDefault(id, (Grey, Grey));
							painted[id] = (Patrol, stripes == Grey ? Patrol : stripes);
						}
						if (id == unit.Mission.BeatLast)
						{
							break;
						}
					}
					break;
			}
		}
		return painted;
	}

	/// <summary>A province and each sea province beside it that is not closed to the country.</summary>
	private static IEnumerable<int> Around(CGameState state, CCountry country, int province)
	{
		if (province <= 0 || province >= state.Provinces.Count)
		{
			yield break;
		}
		yield return province;
		foreach (int id in state.Provinces[province].Neighbours)
		{
			CProvince beside = state.Provinces[id];
			if (beside.IsSea && !CMovementRules.IsClosedWater(state, country, beside))
			{
				yield return id;
			}
		}
	}

	/// <summary>A figure that changes when one of the player's patrols, raids or escorts starts or goes: what recolours the mode.</summary>
	public static long Figure(CGameState state, CCountryTag? player)
	{
		long figure = 19;
		if (state.Country(player) is not { } country)
		{
			return figure;
		}
		foreach (CUnit unit in country.AllUnits())
		{
			if (unit.Order is EUnitOrder.Patrol or EUnitOrder.ConvoyRaid or EUnitOrder.ConvoyEscort)
			{
				figure = figure * 31 + unit.Id;
				figure = figure * 31 + (int)unit.Order;
				figure = figure * 31 + unit.OrderProvince;
			}
		}
		return figure;
	}
}
