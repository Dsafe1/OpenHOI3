using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Transport;

/// <summary>
/// A carrier as an air base: the room aboard a ship, the rebase to carrier, carrier protection, the carrier's aircraft
/// sent into its fleet's battle, and its air groups let go when it is gone. See <c>docs/mechanics/transport.md</c>,
/// sections 13.1 and 13.2.
/// </summary>
public static class CCarriers
{
	/// <summary>The define a carrier's aircraft go home under, and fight in the air from.</summary>
	public const string DutyOrganisationLimit = "CAG_DUTY_MINIMUM_ORG_LIMIT";

	/// <summary>Whether every wing of a unit is a carrier air group. A unit with none is not.</summary>
	public static bool IsCarrierAirGroup(CUnit unit)
	{
		if (unit.Kind != EUnitKind.Air || unit.SubUnits.Count == 0)
		{
			return false;
		}
		foreach (CSubUnit wing in unit.SubUnits)
		{
			if (!wing.Type.Flag(CAirFire.IsCag))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>Whether a ship has room for a unit: the wings aboard and the unit's within its <c>carrier_size</c>, cut.</summary>
	public static bool HasRoom(CSubUnit ship, CUnit unit)
	{
		long size = ship.Stats[CShipCensus.CarrierSize].Raw;
		if (size <= 0)
		{
			return false;
		}
		long wings = unit.SubUnits.Count;
		foreach (CUnit aboard in ship.Carried)
		{
			wings += aboard.SubUnits.Count;
		}
		return wings <= size / CFixedPoint.Scale;
	}

	/// <summary>The first ship of a fleet with room for a unit, or null.</summary>
	public static CSubUnit? ShipWithRoom(CUnit fleet, CUnit unit)
	{
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (HasRoom(ship, unit))
			{
				return ship;
			}
		}
		return null;
	}

	/// <summary>
	/// The fleet of the unit's own country in a province with a ship that has room for it: the first in the order the
	/// province lists its units, a port's fleets among them. Null for none. Nothing asks whether the carrier is at sea.
	/// </summary>
	public static CUnit? FindCarrierWithRoom(CGameState state, CUnit unit, CProvince province)
	{
		foreach (CUnit fleet in state.UnitsIn(province.Id))
		{
			if (fleet.Kind == EUnitKind.Navy && !fleet.IsRemoved && fleet.Owner == unit.Owner
				&& ShipWithRoom(fleet, unit) is not null)
			{
				return fleet;
			}
		}
		return null;
	}

	/// <summary>A rebase to carrier may be given where every wing is a carrier air group and a carrier of its own has room.</summary>
	public static bool MayRebase(CGameState state, CUnit unit, CProvince province)
	{
		return IsCarrierAirGroup(unit) && FindCarrierWithRoom(state, unit, province) is not null;
	}

	/// <summary>
	/// Puts an air unit aboard a ship: off the ship or out from under the commander it had, onto the ship's list, and
	/// based where the ship's fleet is.
	/// </summary>
	public static void BaseOn(CGameState state, CUnit unit, CSubUnit ship)
	{
		unit.CarriedBy?.Carried.Remove(unit);
		if (unit.CarriedBy is null)
		{
			LeaveCommander(state, unit);
		}
		unit.CarriedBy = ship;
		ship.Carried.Add(unit);
		if (ship.Unit is { } fleet)
		{
			unit.Base = fleet.Location;
		}
	}

	private static void LeaveCommander(CGameState state, CUnit unit)
	{
		if (unit.Parent is { } parent)
		{
			parent.Children.Remove(unit);
			unit.Parent = null;
		}
		else
		{
			state.Country(unit.Owner)?.Units.Remove(unit);
		}
	}

	/// <summary>
	/// The order given: the fleet is found and kept, the unit is based on its first ship with room at once, and it
	/// flies to the carrier - where no ship had room too.
	/// </summary>
	public static void StartRebase(CGameState state, CCountry country, CUnit unit)
	{
		CUnit? fleet = FindCarrierWithRoom(state, unit, state.Province(unit.OrderProvince));
		unit.Mission.Carrier = fleet;
		if (fleet is null)
		{
			return;
		}
		if (ShipWithRoom(fleet, unit) is { } ship)
		{
			BaseOn(state, unit, ship);
		}
		FlyToCarrier(state, country, unit, fleet);
	}

	/// <summary>
	/// The way to where the fleet is now, with the light at the hour of arrival judged as a strike judges it: the wrong
	/// light stops a unit standing in its base's province, and sends any other home.
	/// </summary>
	public static void FlyToCarrier(CGameState state, CCountry country, CUnit unit, CUnit fleet)
	{
		if (!CAirMissions.GoTo(state, country, unit, fleet.Location))
		{
			return;
		}
		if (!CAirMissions.ArrivesInWrongLight(state, country, unit, state.Province(fleet.Location)))
		{
			return;
		}
		if (unit.Location == unit.Base)
		{
			CMovement.Cancel(unit);
		}
		else
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
	}

	/// <summary>
	/// The rebase's hour: the fleet gone or no base, done; in a battle, a bombing or with a path, going on; at its
	/// base, done; otherwise it flies to the carrier again. No range is ever measured.
	/// </summary>
	public static bool RunRebase(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Mission.Carrier is not { IsRemoved: false } fleet || unit.Base == 0)
		{
			return false;
		}
		if (unit.IsFighting || unit.Bombing is not null || unit.IsMoving)
		{
			return true;
		}
		if (CAirMissions.IsAtOwnBase(unit))
		{
			return false;
		}
		FlyToCarrier(state, country, unit, fleet);
		return true;
	}

	/// <summary>A unit's wings' organisation summed over their count, as the original's carrier protection weighs it.</summary>
	public static long PlainAverageOrganisation(CUnit unit)
	{
		return CUnitTotals.AverageOrganisation(unit).Raw;
	}

	/// <summary>
	/// Carrier protection's hour: no base, done; too little organisation, home; standing still somewhere else than its
	/// base's province, home; and the order's province becomes the carrier's. Never done by itself.
	/// </summary>
	public static bool RunProtection(CGameState state, CCountry country, CUnit unit)
	{
		if (unit.Base == 0)
		{
			return false;
		}
		if (PlainAverageOrganisation(unit) < state.Defines[CManpower.MilitarySection, DutyOrganisationLimit].Raw)
		{
			CAirMissions.ReturnToBase(state, country, unit);
		}
		if (!unit.IsFighting && unit.Bombing is null && !unit.IsMoving)
		{
			if (unit.Location != unit.Base)
			{
				CAirMissions.ReturnToBase(state, country, unit);
			}
			unit.OrderProvince = unit.Base;
		}
		return true;
	}

	/// <summary>
	/// Whether a unit under carrier protection fights in the air: enough organisation, and a battle of any kind but a
	/// bombing going on in the order's province.
	/// </summary>
	public static bool FightsInTheAir(CGameState state, CUnit unit)
	{
		if (unit.Order != EUnitOrder.CarrierProtection || unit.OrderProvince <= 0
			|| PlainAverageOrganisation(unit) < state.Defines[CManpower.MilitarySection, DutyOrganisationLimit].Raw)
		{
			return false;
		}
		foreach (CCombat combat in state.Combats)
		{
			if (combat.Province == unit.OrderProvince && !CBombing.IsBombing(combat.Kind))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// A fleet in a battle or a bombing sends each air unit aboard its carriers under carrier protection, standing in
	/// its province, in no battle and no bombing, going nowhere and able to fight, to look for a fight where it stands.
	/// </summary>
	public static void SendAircraft(CGameState state, CUnit fleet)
	{
		foreach (CSubUnit ship in new List<CSubUnit>(fleet.SubUnits))
		{
			if (!CShipCensus.IsCarrier(ship))
			{
				continue;
			}
			foreach (CUnit unit in new List<CUnit>(ship.Carried))
			{
				if (unit.Order == EUnitOrder.CarrierProtection && unit.Location == fleet.Location && !unit.IsRemoved
					&& unit.Bombing is null && !unit.IsFighting && !unit.IsMoving && CUnitTotals.CanFight(unit))
				{
					CCombats.CheckForCombat(state, unit, unit.Location);
				}
			}
		}
	}

	/// <summary>
	/// What a unit with no order does aboard a carrier: it is given carrier protection for the province it is in.
	/// </summary>
	public static void GiveDuty(CUnit unit)
	{
		if (unit.Kind != EUnitKind.Air || unit.CarriedBy is null || unit.Order != EUnitOrder.None || unit.IsRemoved)
		{
			return;
		}
		unit.Order = EUnitOrder.CarrierProtection;
		unit.OrderProvince = unit.Location;
		unit.Mission = new CMission();
	}

	/// <summary>
	/// A fleet entering another province takes its carriers' air groups that are going nowhere, in no battle and no
	/// bombing, and standing where it was or nowhere; every group based on it has its base where the fleet now is.
	/// </summary>
	public static void Follow(CGameState state, CUnit fleet, int left, int province)
	{
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			foreach (CUnit group in ship.Carried)
			{
				group.Base = province;
				if (province != left && !group.IsMoving && !group.IsFighting && group.Bombing is null
					&& (group.Location == left || group.Location == 0))
				{
					state.Positions.Move(group, group.Location, province);
					group.Location = province;
				}
			}
		}
	}

	/// <summary>
	/// A ship gone - sunk, or with its fleet - lets go every air group based on it: off its list, with no base and no
	/// order, then given the nearest air base of its country in a straight line, where it stands.
	/// </summary>
	public static void LetGo(CGameState state, CSubUnit ship)
	{
		foreach (CUnit group in new List<CUnit>(ship.Carried))
		{
			LeaveCarrier(state, group);
			group.Base = 0;
			CUnitOrders.Clear(group);
			group.Base = NearestAirBase(state, group);
		}
	}

	/// <summary>An air group leaves the ship it is aboard, and stands in the order of battle on its own.</summary>
	public static void LeaveCarrier(CGameState state, CUnit unit)
	{
		if (unit.CarriedBy is not { } ship)
		{
			return;
		}
		ship.Carried.Remove(unit);
		unit.CarriedBy = null;
		if (!unit.IsRemoved && state.Country(unit.Owner) is { } country && !country.Units.Contains(unit))
		{
			country.Units.Add(unit);
		}
	}

	/// <summary>Every air group aboard a fleet's carriers let go, as the fleet leaves the game.</summary>
	public static void LetGoAll(CGameState state, CUnit fleet)
	{
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			LetGo(state, ship);
		}
	}

	/// <summary>
	/// The province nearest a unit in a straight line that its country holds with an air base built, the first of equals
	/// in the map's order; 0 for none.
	/// </summary>
	public static int NearestAirBase(CGameState state, CUnit unit)
	{
		if (unit.Location <= 0)
		{
			return 0;
		}
		CProvince here = state.Province(unit.Location);
		int best = 0;
		long nearest = long.MaxValue;
		foreach (CProvince province in state.Provinces)
		{
			if (province.Id == 0 || province.IsSea || province.Controller != unit.Owner
				|| CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) <= CFixedPoint.Zero)
			{
				continue;
			}
			long distance = CMapDistance.Between(here, province, state.MapWidth);
			if (distance < nearest)
			{
				nearest = distance;
				best = province.Id;
			}
		}
		return best;
	}
}
