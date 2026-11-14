using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Modifiers;

/// <summary>
/// Which of the coastlines' regions each country's naval bases touch: the second link by which two countries share
/// a strategic resource when their capitals are on different continents. See <c>docs/mechanics/modifiers.md</c>,
/// section 6.
///
/// A country's naval bases are the provinces it controls with a naval base built to any level; a base touches every
/// region of the coastlines that lists a sea province beside it. Two countries are linked by sea when one region is
/// touched by both. Made once, from the game as it stands, and asked as often as wanted: the map does not change and
/// the game is not changed between the questions.
/// </summary>
public sealed class CCoastlineReach
{
	private readonly CGameState _state;
	private Dictionary<CCountryTag, HashSet<int>>? _touched;

	public CCoastlineReach(CGameState state)
	{
		_state = state;
	}

	/// <summary>Whether a region of the coastlines borders a naval base of each of the two countries.</summary>
	public bool Links(CCountry one, CCountry other)
	{
		if (_state.CoastlineRegions.Count == 0)
		{
			return false;
		}
		_touched ??= Build(_state);
		if (!_touched.TryGetValue(one.Tag, out HashSet<int>? mine) || !_touched.TryGetValue(other.Tag, out HashSet<int>? theirs))
		{
			return false;
		}
		foreach (int region in _state.CoastlineRegions)
		{
			if (mine.Contains(region) && theirs.Contains(region))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The regions each controller's naval bases border, every region of the map counted; only the coastlines' are asked.</summary>
	private static Dictionary<CCountryTag, HashSet<int>> Build(CGameState state)
	{
		var touched = new Dictionary<CCountryTag, HashSet<int>>();
		CBuilding? navalBase = null;
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(CMovementRules.NavalBase, StringComparison.OrdinalIgnoreCase))
			{
				navalBase = building;
				break;
			}
		}
		if (navalBase is null)
		{
			return touched;
		}
		foreach (CProvince province in state.Provinces)
		{
			// The original's list of a country's naval bases: what it controls, with the base built to any level - a bombed
			// one still counts.
			if (province.IsSea || province.Controller is not { } controller
				|| province.BuildingMaxLevel(navalBase) <= CFixedPoint.Zero)
			{
				continue;
			}
			foreach (CProvinceEdge edge in province.Edges)
			{
				CProvince neighbour = state.Provinces[edge.To];
				if (!neighbour.IsSea)
				{
					continue;
				}
				if (!touched.TryGetValue(controller, out HashSet<int>? regions))
				{
					touched[controller] = regions = new HashSet<int>();
				}
				foreach (int region in neighbour.Regions)
				{
					regions.Add(region);
				}
			}
		}
		return touched;
	}
}
