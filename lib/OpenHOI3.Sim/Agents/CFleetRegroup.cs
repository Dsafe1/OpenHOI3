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
				if (StrengthShare(state, ship) < TransportWorn)
				{
					return true;
				}
			}
			else if (StrengthShare(state, ship) < ShipWorn || OrganisationShare(state, ship) < OrganisationWorn)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Its strength's share of its most, in thousandths: the most as the original's <c>CSubUnit::GetMaxStrength</c> gives it,
	/// a reserve's less what its country's law keeps back (reversing/findings/FINDINGS-liveread.md, section 38).
	/// </summary>
	public static long StrengthShare(CGameState state, CSubUnit ship)
	{
		long most = ship.Unit is { } unit && state.Country(unit.Owner) is { } owner
			? CUnits.MaxStrength(state, owner, ship).Raw
			: ship.Stats[CSubUnitType.MaxStrength].Raw;
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

	/// <summary>The regrouping alone, for an agent whose naval body does not run: the port phase, then the split and merge.</summary>
	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		List<CUnit> ready = Ready(state, country, agent);
		Merge(state, country, agent, ready, new List<CUnit>());
		agent.ReplanNaval = false;
	}

	/// <summary>
	/// The port phase (the naval body's phase 3): a worn fleet away from its base sent to port, a damaged one at its base
	/// split; every other fleet with ships, a base and no retreat is ready, in the agent's order.
	/// </summary>
	public static List<CUnit> Ready(CGameState state, CCountry country, CAgent agent)
	{
		var ready = new List<CUnit>();
		if (agent.Unit is not { } headquarters)
		{
			return ready;
		}
		bool mayReorganise = country.UnitAi.RunsUnits || headquarters.Plan.AllowHqReorg;
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
				if (StrengthShare(state, ship) < (transport ? TransportWorn : Damaged))
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
		return ready;
	}

	/// <summary>
	/// The split and merge (the naval body's phase 5), where the country may reorganise: what it orders to join, and the
	/// fleets they join, go on the handled list with those the existing orders put there.
	/// </summary>
	public static void Merge(CGameState state, CCountry country, CAgent agent, List<CUnit> ready, List<CUnit> handled)
	{
		if (agent.Unit is not { } headquarters || !(country.UnitAi.RunsUnits || headquarters.Plan.AllowHqReorg))
		{
			return;
		}
		SplitAndMerge(state, country, headquarters, ready, handled);
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
		return state.TakeUnitIds(1);
	}

	private static void SplitAndMerge(CGameState state, CCountry country, CUnit headquarters, List<CUnit> ready,
		List<CUnit> handled)
	{
		// A fleet joining another, and the other, are handled before the merge (the naval body's phase 4).
		foreach (CUnit fleet in ready)
		{
			if (fleet.Order == EUnitOrder.JoinFleet)
			{
				// The fleet joined first, then the joiner (reversing/findings/FINDINGS-liveread.md, section 77).
				if (fleet.Mission.LiveTarget is { } target)
				{
					handled.Add(target);
				}
				handled.Add(fleet);
			}
		}
		var counts = new Dictionary<CUnit, CShipCensus>();
		foreach (CUnit fleet in ready)
		{
			counts[fleet] = CShipCensus.Of(fleet);
		}
		foreach (CUnit fleet in ready)
		{
			if (fleet.IsRetreating || fleet.IsFighting || fleet.SubUnits.Count == 0 || handled.Contains(fleet)
				|| fleet.Order != EUnitOrder.None || CAgentUnits.Destination(fleet) > 0 || fleet.Location != fleet.Base)
			{
				continue;
			}
			CShipCensus before = counts[fleet];
			CShipCensus left = Split(state, country, headquarters, fleet, before);
			counts[fleet] = left;
			// A fleet that split goes on the handled list, so it is no partner; it may still look for one. Its ships are counted
			// as it stands - those split off leave by command, after the pass (FINDINGS-liveread.md, section 57).
			if (left != before)
			{
				handled.Add(fleet);
			}
			if (left.Transports > 0 || fleet.SubUnits.Count >= Small)
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
				handled.Add(partner);
				handled.Add(fleet);
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
