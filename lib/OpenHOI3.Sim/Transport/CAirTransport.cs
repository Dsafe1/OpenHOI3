using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Transport;

/// <summary>
/// Land units carried by an air unit: who may board, boarding, getting off, going where the air unit goes, and the
/// drop. See <c>docs/mechanics/transport.md</c>, sections 13.3 and 13.4.
/// </summary>
public static class CAirTransport
{
	public const string CanParadrop = "can_paradrop";
	public const string OrganisationRequired = "PARATROOP_MISSION_ORG_REQ";
	public const string DropDays = "PARASUPPLYPOOL_DAYS";
	public const string PlaneOrganisationAfterDrop = "PARATROOP_DROP_ORG_MULT_PLANE";
	public const string OrganisationAfterDrop = "PARATROOP_DROP_ORG_MULT";

	/// <summary>
	/// Whether every brigade of a unit is at the organisation a drop asks: its organisation x 1000 over its most - a
	/// most of nought counting as -1 - at least <c>PARATROOP_MISSION_ORG_REQ</c>.
	/// </summary>
	public static bool IsOrganisedForDrop(CGameState state, CUnit unit)
	{
		CCountry? owner = state.Country(unit.Owner);
		long required = state.Defines[CManpower.MilitarySection, OrganisationRequired].Raw;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			long most = owner is null ? brigade.Stats[CSubUnitType.DefaultOrganisation].Raw
				: COrganisation.Ceiling(state, owner, brigade).Raw;
			long share = most == 0 ? -1 : brigade.Organisation.Raw * CFixedPoint.Scale / most;
			if (share < required)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>Whether every brigade of a unit may be dropped.</summary>
	public static bool AllCanParadrop(CUnit unit)
	{
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			if (!brigade.Type.Flag(CanParadrop))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// Whether a land unit may board an air unit: one owner; the air unit in no battle, going nowhere, not falling
	/// back, at its base, with an air base working there; the land unit's attack delay not running; every brigade one
	/// that may be dropped and organised for it; and room for its weight. Where the land unit stands is not asked.
	/// </summary>
	public static bool MayLoad(CGameState state, CUnit plane, CUnit unit)
	{
		if (plane.Kind != EUnitKind.Air || !CMovementRules.IsLandUnit(unit) || plane.IsRemoved || unit.IsRemoved
			|| plane.Owner != unit.Owner)
		{
			return false;
		}
		if (plane.IsFighting || plane.IsMoving || plane.IsRetreating || !CAirMissions.IsAtOwnBase(plane) || plane.Base == 0)
		{
			return false;
		}
		if (unit.AttackDelay > CFixedPoint.Zero)
		{
			return false;
		}
		if (CBombingOrders.BuildingOf(state, CBombingOrders.AirBaseLevel) is not { } airBase
			|| state.Province(plane.Base).BuildingLevel(airBase) < CFixedPoint.One)
		{
			return false;
		}
		if (!AllCanParadrop(unit) || !IsOrganisedForDrop(state, unit))
		{
			return false;
		}
		return CTransport.Weight(unit) + CTransport.Load(plane) <= CTransport.Capacity(plane);
	}

	/// <summary>Puts a land unit aboard an air unit, at the end of its list, where it stands.</summary>
	public static void Board(CUnit plane, CUnit unit)
	{
		unit.Plane = plane;
		if (!plane.Passengers.Contains(unit))
		{
			plane.Passengers.Add(unit);
		}
	}

	/// <summary>
	/// Lets go of a passenger where it stands: off the list; dropped, it brings <c>PARASUPPLYPOOL_DAYS</c> of what it
	/// uses into the province; and it looks for a battle there.
	/// </summary>
	public static void Release(CGameState state, CUnit unit, bool parachuted)
	{
		if (unit.Plane is not { } plane)
		{
			return;
		}
		unit.Plane = null;
		plane.Passengers.Remove(unit);
		if (parachuted && state.Country(unit.Owner) is { } owner)
		{
			long days = state.Defines[CManpower.MilitarySection, DropDays].Raw / CFixedPoint.Scale;
			CSupplyCarry.Bring(state, owner, unit, unit.Previous, unit.Location, fromStockpile: true, days: days);
		}
		CCombats.CheckForCombat(state, unit, unit.Location);
	}

	/// <summary>
	/// The drop over the province the air unit is in: each passenger in order marked dropped where the air unit's
	/// country is at war with the province's holder, given an attack delay there, and let go with its days of supplies.
	/// </summary>
	public static void Drop(CGameState state, CUnit plane)
	{
		Provinces.CProvince province = state.Province(plane.Location);
		bool hostile = CMovementRules.IsEnemy(state, plane.Owner, province.Controller);
		foreach (CUnit unit in new List<CUnit>(plane.Passengers))
		{
			unit.WasParadropped = hostile;
			if (hostile && state.Country(unit.Owner) is { } owner)
			{
				CAttackDelay.Start(state, owner, unit);
			}
			Release(state, unit, parachuted: true);
		}
	}

	/// <summary>An air unit entering a province takes its passengers with it.</summary>
	public static void Follow(CGameState state, CUnit plane, int province)
	{
		foreach (CUnit passenger in plane.Passengers)
		{
			state.Positions.Move(passenger, passenger.Location, province);
			passenger.Location = province;
		}
	}

	/// <summary>
	/// What the first hour out of a battle does to a dropped unit: the mark goes, and every brigade's organisation is
	/// multiplied by <c>PARATROOP_DROP_ORG_MULT</c>, cut.
	/// </summary>
	public static void Settle(CGameState state, CUnit unit)
	{
		if (!unit.WasParadropped || unit.IsFighting)
		{
			return;
		}
		unit.WasParadropped = false;
		long factor = state.Defines[CManpower.MilitarySection, OrganisationAfterDrop].Raw;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			long kept = brigade.Organisation.Raw * factor / CFixedPoint.Scale;
			brigade.Organisation = new CFixedPoint(kept < 0 ? 0 : kept);
		}
	}

	/// <summary>The planes after a drop: every wing's organisation multiplied by <c>PARATROOP_DROP_ORG_MULT_PLANE</c>, cut.</summary>
	public static void SpendPlanes(CGameState state, CUnit plane)
	{
		long factor = state.Defines[CManpower.MilitarySection, PlaneOrganisationAfterDrop].Raw;
		foreach (CSubUnit wing in plane.SubUnits)
		{
			long kept = wing.Organisation.Raw * factor / CFixedPoint.Scale;
			wing.Organisation = new CFixedPoint(kept < 0 ? 0 : kept);
		}
	}
}
