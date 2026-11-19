using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Provinces;

namespace OpenHOI3.Sim.Units;

/// <summary>
/// How crowded the base of a fleet or an air group is: what the base has room for over
/// what is based there. A crowded base slows the reinforcement of what it holds and
/// the return of its organisation alike. See <c>docs/mechanics/organisation.md</c>,
/// section 4.
///
/// One of these counts every base once, so it is made for a pass and thrown away.
/// </summary>
public sealed class CBaseCrowding
{
	public const string NavalBase = "naval_base";
	public const string AirBase = "air_base";

	/// <summary>The define that says how many ships a level of naval base has room for.</summary>
	public const string NavalBaseEfficiency = "NAVAL_BASE_EFFICIENCY";

	/// <summary>The modifier, and the technology's key, that add to it.</summary>
	public const string NavalBaseEfficiencyModifier = "naval_base_efficiency";

	/// <summary>However crowded a base is, it does a quarter of its work.</summary>
	private const long Least = 250;

	private readonly CGameState _state;
	private readonly CBuilding? _navalBase;
	private readonly CBuilding? _airBase;
	private readonly Dictionary<int, long> _ships = new();
	private readonly Dictionary<int, long> _wings = new();
	private readonly Dictionary<CSubUnit, long> _onCarrier = new();

	public CBaseCrowding(CGameState state)
	{
		_state = state;
		// The buildings with the two roles, by the modifier they carry (CBuildingRoles).
		_navalBase = CBuildingRoles.Find(state, NavalBase);
		_airBase = CBuildingRoles.Find(state, AirBase);

		// A base is a province's, and whoever is based there shares it.
		foreach (CCountry country in state.Countries)
		{
			foreach (CUnit unit in country.AllUnits())
			{
				if (unit.CarriedBy is { } carrier)
				{
					_onCarrier[carrier] = _onCarrier.GetValueOrDefault(carrier) + unit.SubUnits.Count;
					continue;
				}
				if (unit.Base == 0)
				{
					continue;
				}
				Dictionary<int, long>? based = unit.Kind == EUnitKind.Navy ? _ships : unit.Kind == EUnitKind.Air ? _wings : null;
				if (based is not null)
				{
					based[unit.Base] = based.GetValueOrDefault(unit.Base) + unit.SubUnits.Count;
				}
			}
		}
	}

	/// <summary>
	/// How much of its usual work a unit's base does for it, in thousandths: all of it
	/// for an army, for a unit with no base, and for an air group where its carrier is.
	/// An air group away from its carrier is measured against the air base of the province
	/// the carrier is in, usually none at sea, and so gets a quarter.
	/// </summary>
	public long Of(CUnit unit)
	{
		if (unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air)
		{
			return CFixedPoint.Scale;
		}
		if (unit.CarriedBy is { Unit: { } fleet } carrier)
		{
			if (unit.Location == fleet.Location)
			{
				return CFixedPoint.Scale;
			}
			long aboard = _onCarrier.GetValueOrDefault(carrier) * CFixedPoint.Scale;
			long share = aboard > 0 ? RoomForWings(fleet.Location) * CFixedPoint.Scale / aboard : CFixedPoint.Scale;
			return Math.Max(Least, Math.Min(share, CFixedPoint.Scale));
		}
		if (unit.CarriedBy is not null || unit.Base == 0)
		{
			return CFixedPoint.Scale;
		}

		long based = (unit.Kind == EUnitKind.Navy ? _ships : _wings).GetValueOrDefault(unit.Base) * CFixedPoint.Scale;
		long crowding = CFixedPoint.Scale;
		if (based > 0)
		{
			long room = unit.Kind == EUnitKind.Navy ? RoomForShips(unit.Base) : RoomForWings(unit.Base);
			crowding = room * CFixedPoint.Scale / based;
		}
		return Math.Max(Least, Math.Min(crowding, CFixedPoint.Scale));
	}

	/// <summary>
	/// What the base of a fleet or an air group is and how well it serves, as the unit panel's tooltip says it: the
	/// province it is named by - an air group aboard a carrier by its fleet's base where the fleet has one, else where
	/// the carrier is - what the base has room for and what is based there, both in thousandths, and the share, held
	/// between a quarter and the whole. Worked out as <see cref="Of"/> works it, but for one thing: with nothing based
	/// there the share is a quarter, not the whole. Null for an army, or a unit with no base
	/// (<c>docs/mechanics/unitpanel.md</c>, section 10.2).
	/// </summary>
	public (int Province, long Room, long Based, long Share)? Describe(CUnit unit)
	{
		if (unit.Kind != EUnitKind.Navy && unit.Kind != EUnitKind.Air)
		{
			return null;
		}
		int province;
		long based;
		long room;
		if (unit.CarriedBy is { Unit: { } fleet } carrier)
		{
			province = fleet.Base != 0 ? fleet.Base : fleet.Location;
			based = _onCarrier.GetValueOrDefault(carrier) * CFixedPoint.Scale;
			room = unit.Location == fleet.Location ? based : RoomForWings(fleet.Location);
		}
		else if (unit.CarriedBy is null && unit.Base != 0)
		{
			province = unit.Base;
			based = (unit.Kind == EUnitKind.Navy ? _ships : _wings).GetValueOrDefault(unit.Base) * CFixedPoint.Scale;
			room = unit.Kind == EUnitKind.Navy ? RoomForShips(unit.Base) : RoomForWings(unit.Base);
		}
		else
		{
			return null;
		}
		long share = based > 0 ? room * CFixedPoint.Scale / based : 0;
		return (province, room, based, Math.Max(Least, Math.Min(share, CFixedPoint.Scale)));
	}

	/// <summary>
	/// The ships a port has room for: so many for each level of its naval base, and
	/// more where whoever holds it runs its ports better.
	/// </summary>
	public long RoomForShips(int provinceId)
	{
		CProvince province = _state.Province(provinceId);
		long level = _navalBase is null ? 0 : province.BuildingLevel(_navalBase).Raw;
		long room = _state.Defines[CManpower.MilitarySection, NavalBaseEfficiency].Raw * level / CFixedPoint.Scale;

		long better = CFixedPoint.Scale;
		if (_state.Country(province.Controller) is { } controller)
		{
			better += controller.Technology[NavalBaseEfficiencyModifier].Raw + controller.Modifier[NavalBaseEfficiencyModifier].Raw;
		}
		return room * better / CFixedPoint.Scale;
	}

	/// <summary>The wings an airfield has room for: one for each level of its air base.</summary>
	public long RoomForWings(int provinceId)
	{
		return _airBase is null ? 0 : _state.Province(provinceId).BuildingLevel(_airBase).Raw;
	}
}
