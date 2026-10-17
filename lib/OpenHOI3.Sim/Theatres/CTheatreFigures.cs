using System;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Theatres;

/// <summary>What the theatre layer asks of a province: its naval base, whether it is a front province, who is beside it.</summary>
public static class CTheatreFigures
{
	public const string NavalBaseKey = "naval_base";

	/// <summary>The province's IC modifier, which a headquarters' name weighs.</summary>
	public const string Ic = "ic";

	public static CBuilding? NavalBase(CGameState state)
	{
		foreach (CBuilding building in state.Buildings)
		{
			if (building.Key.Equals(NavalBaseKey, StringComparison.OrdinalIgnoreCase))
			{
				return building;
			}
		}
		return null;
	}

	/// <summary>A building's built level in whole levels, cut.</summary>
	public static long WholeLevel(CProvince province, CBuilding? building)
	{
		return building is null ? 0 : province.BuildingMaxLevel(building).Raw / CFixedPoint.Scale;
	}

	/// <summary>
	/// The original's <c>IsFrontProvince</c>: an owned province one of whose neighbours by an edge that is not impassable - a
	/// strait among them - is land in an area, held by another, an enemy of its holder; with <paramref name="withUnits"/> that
	/// neighbour must hold a land unit too, anybody's.
	/// </summary>
	public static bool IsFrontProvince(CGameState state, CProvince province, bool withUnits)
	{
		if (province.Owner is null || province.Controller is not { } controller || state.Country(controller) is not { } holder)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.Kind == EProvinceEdgeKind.Impassable)
			{
				continue;
			}
			CProvince neighbour = state.Province(edge.To);
			if (neighbour.IsSea || network.AreaOf(neighbour.Id) is null || neighbour.Controller is not { } other
				|| other == controller || !CCountryLists.IsEnemy(state, holder, other))
			{
				continue;
			}
			if (!withUnits || HasLandUnit(state, neighbour))
			{
				return true;
			}
		}
		return false;
	}

	private static bool HasLandUnit(CGameState state, CProvince province)
	{
		foreach (CUnit unit in state.Positions.Of(province.Id))
		{
			if (CTheatres.IsLand(unit))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Whether any neighbour, by any edge, holds a land unit or a fleet carrying land units of a country at war with the
	/// one given, or the rebels' either side.
	/// </summary>
	public static bool HasHostileBeside(CGameState state, CCountry country, CProvince province)
	{
		foreach (CProvinceEdge edge in province.Edges)
		{
			foreach (CUnit unit in state.Positions.Of(edge.To))
			{
				bool counts = CTheatres.IsLand(unit) || (unit.Kind == EUnitKind.Navy && unit.Passengers.Count > 0);
				if (counts && unit.Owner != country.Tag && CCountryLists.IsEnemy(state, country, unit.Owner))
				{
					return true;
				}
			}
		}
		return false;
	}
}
