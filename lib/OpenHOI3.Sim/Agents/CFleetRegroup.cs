using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's fleets sent to port, their worn ships put in a fleet of their own, the ships over a fleet's caps split into
/// fleets of one, and small fleets ordered to join others. The regrouping of the original's
/// <c>CAIUnit_ManageNavalUnits</c>. See <c>docs/mechanics/agents.md</c>, section 5.2.
/// </summary>
public static class CFleetRegroup
{
	/// <summary>Shares of strength and organisation, in thousandths.</summary>
	private const long TransportWorn = 500;

	private const long ShipWorn = 750;

	private const long OrganisationWorn = 500;

	private const long Damaged = 900;

	private const long Resting = 900;

	/// <summary>The current base's distance counts at this share, in thousandths.</summary>
	private const long BaseShare = 750;

	private const long NoPort = 1_000_000;

	private const int MostCapitals = 4;

	private const int MostTransports = 4;

	private const int MostSubmarines = 3;

	private const int MostScreens = 4;

	/// <summary>A fleet of fewer ships than this, with no transport, looks for another to join.</summary>
	private const int Small = 6;

	private const int JoinedCapitals = 3;

	private const int JoinedScreens = 3;

	private const int JoinedSubmarines = 3;

	/// <summary>
	/// The original's <c>CUnit_FleetNeedsPort</c>: a transport below half its strength, or another ship below three
	/// quarters of its strength or half its organisation.
	/// </summary>
	public static bool NeedsPort(CGameState state, CUnit fleet)
	{
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			if (CShipCensus.IsTransport(ship))
			{
				if (StrengthShare(ship) < TransportWorn)
				{
					return true;
				}
			}
			else if (StrengthShare(ship) < ShipWorn || OrganisationShare(state, ship) < OrganisationWorn)
			{
				return true;
			}
		}
		return false;
	}

	public static long StrengthShare(CSubUnit ship)
	{
		long most = ship.Stats[CSubUnitType.MaxStrength].Raw;
		return most <= 0 ? CFixedPoint.Scale : ship.Strength.Raw * CFixedPoint.Scale / most;
	}

	/// <summary>Its organisation's share of its ceiling, in thousandths.</summary>
	public static long OrganisationShare(CGameState state, CSubUnit ship)
	{
		if (ship.Unit is not { } unit || state.Country(unit.Owner) is not { } owner)
		{
			return CFixedPoint.Scale;
		}
		long most = COrganisation.Ceiling(state, owner, ship).Raw;
		return most <= 0 ? CFixedPoint.Scale : ship.Organisation.Raw * CFixedPoint.Scale / most;
	}

	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		if (agent.Unit is not { } headquarters)
		{
			return;
		}
		bool mayReorganise = country.UnitAi.RunsUnits || headquarters.Plan.AllowHqReorg;
		var ready = new List<CUnit>();
		foreach (CUnit fleet in agent.Fleets)
		{
			if (fleet.IsRemoved || fleet.SubUnits.Count == 0 || fleet.Base == 0 || fleet.IsRetreating)
			{
				continue;
			}
			bool atBase = fleet.Location == fleet.Base;
			if (NeedsPort(state, fleet) && !atBase && fleet.Passengers.Count == 0)
			{
				ToPort(state, country, fleet);
				continue;
			}
			var damaged = new List<CSubUnit>();
			bool resting = false;
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				bool transport = CShipCensus.IsTransport(ship);
				if (StrengthShare(ship) < (transport ? TransportWorn : Damaged))
				{
					damaged.Add(ship);
				}
				if (!transport && OrganisationShare(state, ship) < Resting)
				{
					resting = true;
				}
			}
			if ((damaged.Count > 0 || resting) && fleet.Order == EUnitOrder.None && !fleet.IsMoving && atBase)
			{
				if (mayReorganise && damaged.Count > 0 && damaged.Count < fleet.SubUnits.Count && !fleet.IsFighting)
				{
					int id = NewId(state);
					state.Outbox.Add(new CCreateUnitCommand(country.Tag, id, EUnitKind.Navy, fleet.Location, fleet.Base,
						headquarters.Id));
					foreach (CSubUnit ship in damaged)
					{
						state.Outbox.Add(new CTransferSubUnitCommand(country.Tag, ship.Id, id));
					}
				}
				continue;
			}
			ready.Add(fleet);
		}
		if (mayReorganise)
		{
			SplitAndMerge(state, country, headquarters, ready);
		}
		agent.ReplanNaval = false;
	}

	/// <summary>The nearest of the country's ports, its base's distance counted at three quarters: rebased there or sent home.</summary>
	private static void ToPort(CGameState state, CCountry country, CUnit fleet)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		int best = 0;
		long least = NoPort;
		foreach (CSupplyArea area in network.Areas)
		{
			if (area.Controller != country.Tag)
			{
				continue;
			}
			foreach (int port in area.Ports)
			{
				long distance = state.NavalDistances.Between(state.Province(fleet.Location), state.Province(port));
				if (distance <= 0 && fleet.Location != port)
				{
					continue;
				}
				if (port == fleet.Base)
				{
					distance = distance * BaseShare / CFixedPoint.Scale;
				}
				if (distance < least)
				{
					least = distance;
					best = port;
				}
			}
		}
		if (best == 0)
		{
			return;
		}
		if (best == fleet.Base)
		{
			if (!fleet.IsFighting)
			{
				state.Outbox.Add(new CMoveUnitCommand(country.Tag, fleet.Id, fleet.Base));
			}
			return;
		}
		state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.Rebase, best));
	}

	/// <summary>A new unit's number, taken now as the original takes it as it posts the command.</summary>
	public static int NewId(CGameState state)
	{
		return ++state.LastUnitId;
	}

	private static void SplitAndMerge(CGameState state, CCountry country, CUnit headquarters, List<CUnit> ready)
	{
		var handled = new HashSet<CUnit>();
		var counts = new Dictionary<CUnit, CShipCensus>();
		foreach (CUnit fleet in ready)
		{
			counts[fleet] = CShipCensus.Of(fleet);
		}
		foreach (CUnit fleet in ready)
		{
			if (fleet.IsRetreating || fleet.IsFighting || fleet.SubUnits.Count == 0 || handled.Contains(fleet)
				|| fleet.Order != EUnitOrder.None || fleet.IsMoving || fleet.Location != fleet.Base)
			{
				continue;
			}
			CShipCensus left = Split(state, country, headquarters, fleet, counts[fleet]);
			counts[fleet] = left;
			int ships = left.Capitals + left.Transports + left.Submarines + left.Carriers + left.Screens;
			if (left.Transports > 0 || ships >= Small)
			{
				continue;
			}
			foreach (CUnit partner in ready)
			{
				if (ReferenceEquals(partner, fleet) || handled.Contains(partner) || partner.IsRemoved)
				{
					continue;
				}
				if (!MayJoin(state, country, fleet, left, partner, counts[partner]))
				{
					continue;
				}
				state.Outbox.Add(new CJoinUnitCommand(country.Tag, fleet.Id, partner.Id));
				handled.Add(fleet);
				handled.Add(partner);
				break;
			}
		}
	}

	/// <summary>The ships over the fleet's caps, walked in order, each made a fleet of its own; what the fleet keeps.</summary>
	private static CShipCensus Split(CGameState state, CCountry country, CUnit headquarters, CUnit fleet, CShipCensus counts)
	{
		int capitals = counts.Capitals, transports = counts.Transports, submarines = counts.Submarines;
		int carriers = counts.Carriers, screens = counts.Screens;
		int others = capitals + transports + carriers + screens;
		bool byScreens = headquarters.Plan.Embark != 0;
		foreach (CSubUnit ship in fleet.SubUnits)
		{
			bool goes;
			if (CShipCensus.IsCapital(ship))
			{
				goes = capitals > MostCapitals;
				if (goes)
				{
					capitals--;
				}
			}
			else if (CShipCensus.IsTransport(ship))
			{
				goes = byScreens ? screens > MostScreens : transports > MostTransports || capitals > 0 || carriers > 0;
				if (goes)
				{
					transports--;
				}
			}
			else if (CShipCensus.IsSubmarine(ship))
			{
				goes = submarines > MostSubmarines || others > 0;
				if (goes)
				{
					submarines--;
				}
			}
			else
			{
				goes = screens > MostScreens;
				if (goes)
				{
					if (CShipCensus.IsCarrier(ship))
					{
						carriers--;
					}
					else
					{
						screens--;
					}
				}
			}
			if (!goes)
			{
				continue;
			}
			int id = NewId(state);
			state.Outbox.Add(new CCreateUnitCommand(country.Tag, id, EUnitKind.Navy, fleet.Location, fleet.Base,
				headquarters.Id));
			state.Outbox.Add(new CTransferSubUnitCommand(country.Tag, ship.Id, id));
		}
		return new CShipCensus(capitals, transports, submarines, carriers, screens);
	}

	/// <summary>Whether the two fleets' ships together pass the caps of a joined fleet, and the other can be reached.</summary>
	private static bool MayJoin(CGameState state, CCountry country, CUnit fleet, CShipCensus mine, CUnit partner,
		CShipCensus theirs)
	{
		int capitals = mine.Capitals + theirs.Capitals;
		int transports = mine.Transports + theirs.Transports;
		int submarines = mine.Submarines + theirs.Submarines;
		int carriers = mine.Carriers + theirs.Carriers;
		int screens = mine.Screens + theirs.Screens;
		bool mineOthers = mine.Capitals + mine.Transports + mine.Carriers + mine.Screens > 0;
		bool theirOthers = theirs.Capitals + theirs.Transports + theirs.Carriers + theirs.Screens > 0;
		if (submarines > 0 && (mineOthers || theirOthers || submarines > JoinedSubmarines || !SameRange(fleet, partner)))
		{
			return false;
		}
		if (capitals + carriers > JoinedCapitals && mine.Capitals + mine.Carriers > 0)
		{
			return false;
		}
		if (screens > 0 && transports == 0 && capitals == 0 && carriers == 0)
		{
			return false;
		}
		if (transports > 0 && capitals + carriers > 0)
		{
			return false;
		}
		if (screens > JoinedScreens)
		{
			return false;
		}
		return partner.Location == fleet.Location
			|| CRouteFinder.Find(state, country, fleet, fleet.Location, partner.Location) is not null;
	}

	private static bool SameRange(CUnit fleet, CUnit partner)
	{
		if (fleet.SubUnits.Count == 0 || partner.SubUnits.Count == 0)
		{
			return true;
		}
		return fleet.SubUnits[0].Stats[CUnitOrders.Range] == partner.SubUnits[0].Stats[CUnitOrders.Range];
	}
}
