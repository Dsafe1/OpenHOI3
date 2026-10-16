using System;
using System.Collections.Generic;
using System.Globalization;
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
	/// The strip's four buttons for a unit: aboard a fleet, unload - pressable in port; aboard nothing, load - pressable
	/// where a fleet is found - and the planes' load, which nothing carries yet; anything else, none.
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
			return new CCarryButtons(false, false, true, inPort, false, false, 0, false);
		}
		CUnit? fleet = FleetToBoard(state, unit);
		return new CCarryButtons(true, fleet is not null, false, false, true, false, fleet?.Id ?? 0,
			IsTooWornForPlanes(state, owner, unit));
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
}
