using System;
using System.Collections.Generic;
using OpenHOI3.Content.Map;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Map;

/// <summary>
/// The buildings that show on the map, and what a click on a port or an airfield picks.
/// See <c>docs/mechanics/mapunits.md</c>, section 9:
///
///     a province shows the picture of every building it has any of that is declared
///     onmap, where positions.txt puts that building;
///     a naval base's picture has a second frame for a port with a fleet in it, and an
///     air base's for one with an air unit at home;
///     a click on either picks the next of the player's units there.
/// </summary>
public static class CMapBuildings
{
	/// <summary>What a building's name is put after to name its picture.</summary>
	public const string PicturePrefix = "building_";

	public static EMapBuilding KindOf(string building)
	{
		if (building.Equals(CMovementRules.NavalBase, StringComparison.OrdinalIgnoreCase))
		{
			return EMapBuilding.Port;
		}
		return building.Equals(CMovementRules.AirBase, StringComparison.OrdinalIgnoreCase)
			? EMapBuilding.Airfield
			: EMapBuilding.Other;
	}

	/// <summary>Every building picture of the map, in the order of the provinces and of the buildings' file.</summary>
	public static List<CMapBuilding> Build(CGameState state, CMap map, CCountryTag? player = null)
	{
		return Build(state, map.BuildingPositions, map.LabelPositions, player);
	}

	/// <summary>
	/// The same from the two things of the map it reads: where the positions file puts
	/// each building of each province, and each province's label point (<see cref="CMap.LabelPositions"/>).
	/// </summary>
	/// <param name="player">The country playing, whose knowledge of a province decides what of its buildings shows: a
	/// building at all where it knows the province or the building is <c>visibility</c>, a base in use from 2; null for
	/// everything (mapunits.md, section 9).</param>
	public static List<CMapBuilding> Build(CGameState state, IReadOnlyList<CBuildingPosition[]> placed,
		IReadOnlyList<CMapPosition> labels, CCountryTag? player = null)
	{
		var ports = new HashSet<int>();
		var airfields = new HashSet<int>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (IsInPort(state, unit))
				{
					ports.Add(unit.Location);
				}
				else if (IsAtAirfield(unit))
				{
					airfields.Add(unit.Location);
				}
			}
		}

		var shown = new List<CBuilding>();
		foreach (CBuilding building in state.Buildings)
		{
			if (building.IsOnMap)
			{
				shown.Add(building);
			}
		}
		var found = new List<CMapBuilding>();
		for (int id = 1; id < state.Provinces.Count && id < placed.Count && id < labels.Count; id++)
		{
			CProvince province = state.Provinces[id];
			int known = player is { } tag ? Sim.Intelligence.CProvinceIntel.Level(state, province, tag) : CUnitIntel.Full;
			foreach (CBuilding building in shown)
			{
				if (province.BuildingLevel(building) <= CFixedPoint.Zero || (known == 0 && !building.IsAlwaysVisible))
				{
					continue;
				}
				EMapBuilding kind = KindOf(building.Key);
				(float x, float y) = PlaceOf(placed, labels, id, building.Key);
				int frame = known > 1 && ((kind == EMapBuilding.Port && ports.Contains(id))
					|| (kind == EMapBuilding.Airfield && airfields.Contains(id))) ? 1 : 0;
				found.Add(new CMapBuilding(id, building.Key, kind, x, y, frame));
			}
		}
		return found;
	}

	/// <summary>
	/// Where a building's picture stands, a world point - x east, y north, a pixel's middle on whole coordinates, as
	/// positions.txt writes it: where the file puts that building in the province, as it is, or where the file says
	/// nothing the middle of the province, its label point - decided on 2026-11-07. The original hangs the
	/// picture on the province's anchor moved by the building's place less the label point, which comes to the place
	/// itself (mapunits.md, section 9); the 2D map draws it through <see cref="CWorldPoint"/>, half a pixel east and south.
	/// </summary>
	public static (float X, float Y) PlaceOf(IReadOnlyList<CBuildingPosition[]> placed, IReadOnlyList<CMapPosition> labels,
		int province, string building)
	{
		foreach (CBuildingPosition position in placed[province])
		{
			if (position.Building.Equals(building, StringComparison.OrdinalIgnoreCase))
			{
				return (position.X, position.Y);
			}
		}
		return (labels[province].X, labels[province].Y);
	}

	/// <summary>Whether a unit is a fleet lying in a port.</summary>
	public static bool IsInPort(CGameState state, CUnit unit)
	{
		return unit.Kind == EUnitKind.Navy && unit.Location > 0 && unit.CarriedBy is null
			&& !state.Province(unit.Location).IsSea;
	}

	/// <summary>Whether a unit is an air unit standing at its own base on land.</summary>
	public static bool IsAtAirfield(CUnit unit)
	{
		return unit.Kind == EUnitKind.Air && unit.Location > 0 && unit.CarriedBy is null
			&& unit.Location == unit.Base;
	}

	/// <summary>
	/// The units a click on a province's port or airfield goes through: the fleets lying
	/// in the port, or the air units based at the airfield wherever they are. In the
	/// order of their numbers.
	/// </summary>
	public static List<CUnit> UnitsOf(CGameState state, int province, EMapBuilding kind)
	{
		var units = new List<CUnit>();
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				bool belongs = kind switch
				{
					EMapBuilding.Port => IsInPort(state, unit) && unit.Location == province,
					EMapBuilding.Airfield => unit.Kind == EUnitKind.Air && unit.CarriedBy is null && unit.Base == province,
					_ => false,
				};
				if (belongs)
				{
					units.Add(unit);
				}
			}
		}
		units.Sort((a, b) => a.Id.CompareTo(b.Id));
		return units;
	}

	/// <summary>
	/// The unit a click on a port or an airfield selects, or null: the next of the
	/// player's units there after the one selected, round to the first again. With none
	/// of them selected it is the second where there are several: the original turns its
	/// list by one before it picks, each time. With no player, anybody's - a replay looking on.
	/// </summary>
	public static CUnit? Next(CGameState state, CCountryTag? player, int province, EMapBuilding kind, CUnit? selected)
	{
		List<CUnit> units = UnitsOf(state, province, kind);
		if (player is { } tag)
		{
			units.RemoveAll(unit => unit.Owner != tag);
		}
		if (units.Count == 0)
		{
			return null;
		}
		int at = selected is null ? -1 : units.FindIndex(unit => ReferenceEquals(unit, selected));
		return units[(at < 0 ? 1 : at + 1) % units.Count];
	}
}
