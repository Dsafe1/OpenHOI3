using System;
using System.Collections.Generic;
using System.Globalization;
using OpenHOI3.Interface.Map;
using OpenHOI3.Interface.Text;
using OpenHOI3.Sim;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Economy;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Interface.Screens;

/// <summary>
/// What the screen shows of carrying: the fleet a unit's load button finds, the strip's four buttons, and a fleet's or an
/// air unit's list of what it carries with its capacity. See <c>docs/mechanics/transport.md</c>, section 12.
/// </summary>
public static class CTransportText
{
	public const string ParatroopOrganisation = "PARATROOP_MISSION_ORG_REQ";

	/// <summary>
	/// The fleet the load button puts a land unit aboard: for a unit in a land province with a naval base built and in
	/// no battle, the first fleet lying in the port that may take it aboard. The player is never offered a choice.
	/// </summary>
	public static CUnit? FleetToBoard(CGameState state, CUnit unit)
	{
		if (!CMovementRules.IsLandUnit(unit) || unit.IsFighting || unit.Location <= 0)
		{
			return null;
		}
		CProvince province = state.Province(unit.Location);
		if (province.IsSea || CMovementRules.BuildingLevel(state, province, CMovementRules.NavalBase) <= CFixedPoint.Zero)
		{
			return null;
		}
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (other.Kind == EUnitKind.Navy && CTransport.CanCarry(other, unit))
			{
				return other;
			}
		}
		return null;
	}

	/// <summary>
	/// The air unit the planes' load button puts a land unit aboard: for a unit in a land province with an air base
	/// built and in no battle, the first air unit based there, at its base, that may take it aboard - the units based
	/// there in the order the province lists them.
	/// </summary>
	public static CUnit? PlaneToBoard(CGameState state, CUnit unit)
	{
		if (!CMovementRules.IsLandUnit(unit) || unit.IsFighting || unit.Location <= 0)
		{
			return null;
		}
		CProvince province = state.Province(unit.Location);
		if (province.IsSea || CMovementRules.BuildingLevel(state, province, CMovementRules.AirBase) <= CFixedPoint.Zero)
		{
			return null;
		}
		foreach (CUnit other in state.UnitsIn(province.Id))
		{
			if (other.Kind == EUnitKind.Air && other.Base == province.Id && Sim.Missions.CAirMissions.IsAtOwnBase(other)
				&& CAirTransport.MayLoad(state, other, unit))
			{
				return other;
			}
		}
		return null;
	}

	/// <summary>
	/// The strip's four buttons for a unit. The ships' pair: aboard a fleet, unload - pressable in port; aboard nothing,
	/// load - pressable where a fleet is found. The planes' pair: aboard an air unit, unload - pressable where it is at
	/// its base; aboard nothing, load - pressable where an air unit is found. A unit aboard one kind shows neither of the
	/// other's; anything else, none.
	/// </summary>
	public static CCarryButtons Buttons(CGameState state, CCountry owner, CUnit unit)
	{
		if (!CMovementRules.IsLandUnit(unit))
		{
			return CCarryButtons.None;
		}
		if (unit.Transport is not null)
		{
			bool inPort = unit.Location > 0 && !state.Province(unit.Location).IsSea;
			return CCarryButtons.None with { UnloadShown = true, UnloadEnabled = inPort };
		}
		if (unit.Plane is { } plane)
		{
			return CCarryButtons.None with
			{
				PlaneUnloadShown = true, PlaneUnloadEnabled = Sim.Missions.CAirMissions.IsAtOwnBase(plane),
			};
		}
		CUnit? fleet = FleetToBoard(state, unit);
		CUnit? air = PlaneToBoard(state, unit);
		return new CCarryButtons(true, fleet is not null, false, false, true, air is not null, false, false,
			fleet?.Id ?? 0, air?.Id ?? 0, IsTooWornForPlanes(state, owner, unit));
	}

	/// <summary>
	/// Whether a fleet's plain right click on land that the sea does not reach opens the orders window: the province is
	/// land with the sea beside it, the fleet carries something its first passenger may stand there, and it is in no
	/// battle. Otherwise the click is refused.
	/// </summary>
	public static bool OpensOrdersOnCoast(CGameState state, CCountry country, CUnit fleet, CProvince province)
	{
		if (fleet.Kind != EUnitKind.Navy || province.IsSea || fleet.IsFighting || fleet.Passengers.Count == 0
			|| !CMovementRules.MayStandIn(state, country, fleet.Passengers[0], province))
		{
			return false;
		}
		foreach (CProvinceEdge edge in province.Edges)
		{
			if (edge.To > 0 && edge.To < state.Provinces.Count && state.Province(edge.To).IsSea)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Whether any brigade has less of its organisation than paratroops need, <c>PARATROOP_MISSION_ORG_REQ</c>.</summary>
	public static bool IsTooWornForPlanes(CGameState state, CCountry owner, CUnit unit)
	{
		long needed = state.Defines[CManpower.MilitarySection, ParatroopOrganisation].Raw;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			long most = COrganisation.Ceiling(state, owner, brigade).Raw;
			long share = most == 0 ? 0 : brigade.Organisation.Raw * CFixedPoint.Scale / most;
			if (share < needed)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The list's header, <c>LOAD_CAPACITY_LABEL</c>: with nothing aboard what the carrier can carry, otherwise what is
	/// left and then the whole in brackets - "Can carry 90 (120) weight."
	/// </summary>
	public static string Header(Func<string, string> localise, CUnit carrier)
	{
		long capacity = CTransport.Capacity(carrier);
		string number = carrier.Passengers.Count == 0
			? capacity.ToString(CultureInfo.InvariantCulture)
			: (capacity - CTransport.Load(carrier)).ToString(CultureInfo.InvariantCulture) + " ("
				+ capacity.ToString(CultureInfo.InvariantCulture) + ")";
		return CTextTemplate.Fill(localise("LOAD_CAPACITY_LABEL"), ("NUM", number));
	}

	/// <summary>The list's rows: each passenger in order, then for a fleet every air unit aboard each of its carriers.</summary>
	public static List<CUnit> Rows(CUnit carrier)
	{
		var rows = new List<CUnit>(carrier.Passengers);
		if (carrier.Kind == EUnitKind.Navy)
		{
			foreach (CSubUnit ship in carrier.SubUnits)
			{
				if (Sim.Combat.CShipCensus.IsCarrier(ship))
				{
					rows.AddRange(ship.Carried);
				}
			}
		}
		return rows;
	}

	/// <summary>The air units aboard a fleet's carriers, which <c>select_air</c> selects.</summary>
	public static List<CUnit> Aircraft(CUnit fleet)
	{
		var aircraft = new List<CUnit>();
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (Sim.Combat.CShipCensus.IsCarrier(ship))
			{
				aircraft.AddRange(ship.Carried);
			}
		}
		return aircraft;
	}

	/// <summary><c>select_land</c> may be pressed where anything is aboard - a fleet's panel or an air unit's.</summary>
	public static bool MaySelectLand(CUnit carrier)
	{
		return carrier.Passengers.Count > 0;
	}

	/// <summary><c>select_air</c> is shown on a fleet's panel alone, and may be pressed where any of its carriers has aircraft aboard.</summary>
	public static bool ShowsSelectAir(CUnit carrier)
	{
		return carrier.Kind == EUnitKind.Navy;
	}

	/// <inheritdoc cref="ShowsSelectAir"/>
	public static bool MaySelectAir(CUnit carrier)
	{
		return ShowsSelectAir(carrier) && Aircraft(carrier).Count > 0;
	}

	/// <summary>
	/// What a press of <c>select_land</c> selects in place of everything selected: a fleet's passengers, in the order they
	/// boarded. **Null for an air unit, whose press does nothing at all** though the button may be pressed - the original's
	/// click asks first whether the carrier is a fleet (<c>transport.md</c>, section 12, and oddity 21).
	/// </summary>
	public static List<CUnit>? SelectedByLand(CUnit carrier)
	{
		return carrier.Kind == EUnitKind.Navy ? new List<CUnit>(carrier.Passengers) : null;
	}

	/// <summary>
	/// What a press of <c>select_air</c> selects in place of everything selected: every air unit aboard the fleet's
	/// carriers, ship by ship in the fleet's order. Null for anything but a fleet.
	/// </summary>
	public static List<CUnit>? SelectedByAir(CUnit carrier)
	{
		return carrier.Kind == EUnitKind.Navy ? Aircraft(carrier) : null;
	}

	/// <summary>
	/// A press's change of the selection: everything selected let go - the fleet among it - and the units selected in
	/// their order, whatever their kind, as the original's own call to select takes them. Nothing centres the map.
	/// </summary>
	public static void SelectOnly(CSelection selection, IEnumerable<CUnit> units)
	{
		selection.Clear();
		foreach (CUnit unit in units)
		{
			selection.Force(unit);
		}
	}
}
