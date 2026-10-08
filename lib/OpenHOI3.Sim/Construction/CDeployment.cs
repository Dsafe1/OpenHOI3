using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Construction;

/// <summary>
/// Where a finished unit may be put, and putting it there. See
/// <c>docs/mechanics/buildqueue.md</c>, section 7.
/// </summary>
public static class CDeployment
{
	/// <summary>
	/// The land a country may place units in: what it owns and holds that is joined to
	/// its capital, over land, through land it owns and holds.
	/// </summary>
	public static HashSet<int> HomeArea(CGameState state, CCountry country)
	{
		var area = new HashSet<int>();
		if (!IsItsOwn(state.Province(country.Capital), country))
		{
			return area;
		}

		var toVisit = new Queue<int>();
		area.Add(country.Capital);
		toVisit.Enqueue(country.Capital);
		while (toVisit.Count > 0)
		{
			foreach (int neighbour in state.Province(toVisit.Dequeue()).Neighbours)
			{
				if (!area.Contains(neighbour) && IsItsOwn(state.Province(neighbour), country))
				{
					area.Add(neighbour);
					toVisit.Enqueue(neighbour);
				}
			}
		}
		return area;
	}

	/// <summary>
	/// Whether one of a country's waiting units may be put in a province: a fleet needs
	/// a naval base there, an air unit an air base, and all of them need the province to
	/// be in the country's home area.
	/// </summary>
	public static bool MayPlace(CGameState state, CCountry country, CUnit unit, int provinceId)
	{
		CProvince province = state.Province(provinceId);
		if (province.Id == 0)
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Navy && !HasBase(state, province, CBaseCrowding.NavalBase))
		{
			return false;
		}
		if (unit.Kind == EUnitKind.Air && !HasBase(state, province, CBaseCrowding.AirBase))
		{
			return false;
		}
		return HomeArea(state, country).Contains(provinceId);
	}

	/// <summary>Takes a unit off the waiting list and puts it in a province, as a unit nothing commands.</summary>
	public static void Place(CCountry country, CUnit unit, int provinceId)
	{
		country.Deployments.Remove(unit);
		unit.Location = provinceId;
		if (unit.Kind == EUnitKind.Navy || unit.Kind == EUnitKind.Air)
		{
			unit.Base = provinceId;
		}
		country.Units.Add(unit);
	}

	private static bool IsItsOwn(CProvince province, CCountry country)
	{
		return province.Id != 0 && !province.IsSea
			&& province.Owner is { } owner && owner.Equals(country.Tag)
			&& province.Controller is { } controller && controller.Equals(country.Tag);
	}

	private static bool HasBase(CGameState state, CProvince province, string key)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
			{
				return province.BuildingLevel(building) > CFixedPoint.Zero;
			}
		}
		return false;
	}
}
