using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An invasion's hour: over once its fleets or troops are gone, its troops can walk to the objective, its fleets cannot
/// carry its smallest division, or nowhere is left to sail from; otherwise a friendly port near the objective to sail
/// from, the troops marched there, the fleet's carrier aircraft flown over the beach once it has its invasion, the empty
/// fleets brought together and merged, the troops loaded, the fleet sailed to the port and then given the invasion on the
/// landing picked again. The original's <c>CAIInvasion::Tick</c>, <c>IsInvasionOver</c>, <c>DoTick</c>,
/// <c>PickEmbarkationPort</c>, <c>MarchLandForceToPort</c>, <c>SendCarrierAirSupport</c> and
/// <c>SailAndIssueInvasionOrder</c>. See <c>docs/mechanics/moving.md</c>, section 16.
/// </summary>
public static class CInvasionTick
{
	private const long Whole = 1000;

	private const long Weak = 330;

	private const long NearLimit = 1000;

	private const long FarLimit = 2000;

	/// <summary>A port to board at is nearer the embarkation port by sea than this.</summary>
	private const long FarthestStaging = 1000000;

	/// <summary>Above any unit's weight: what the smallest of the troops still walking starts at.</summary>
	private const long MostWeight = 100000;

	/// <summary>The least daylight left, in hours, for the carrier aircraft to be sent over the beach.</summary>
	private const int LeastDaylight = 3;

	/// <summary>How many of the fleet's air units an hour sends over the beach at most.</summary>
	private const int MostAirSupport = 3;

	/// <summary>The least mean organisation and mean strength of an air unit sent, as shares of their most.</summary>
	private const float LeastAirOrganisation = 0.5f;

	private const float LeastAirStrength = 0.33f;

	public static void Run(CGameState state, CCountry country, CInvasion invasion)
	{
		if (invasion.IsFinished)
		{
			return;
		}
		invasion.LandForce.RemoveAll(unit => unit.IsRemoved || unit.Owner != country.Tag);
		invasion.Fleets.RemoveAll(unit => unit.IsRemoved);
		if (IsOver(state, country, invasion))
		{
			invasion.IsFinished = true;
			return;
		}
		if (invasion.EmbarkationPort <= 0 || !CApproaches.IsFriendly(state, country, state.Province(invasion.EmbarkationPort).Controller))
		{
			int port = EmbarkationPort(state, country, invasion);
			if (port <= 0)
			{
				invasion.IsFinished = true;
				return;
			}
			invasion.EmbarkationPort = port;
			invasion.Rendezvous = port;
		}
		March(state, country, invasion);
		AirSupport(state, country, invasion);
		Sail(state, country, invasion);
	}

	private static bool IsOver(CGameState state, CCountry country, CInvasion invasion)
	{
		if (invasion.Fleets.Count == 0 || invasion.LandForce.Count == 0)
		{
			return true;
		}
		if (invasion.LandForce.TrueForAll(unit => CanWalkThere(state, country, invasion, unit)))
		{
			return true;
		}
		long smallest = invasion.LandForce.Min(CTransport.Weight);
		if (invasion.Fleets.Sum(CTransport.Capacity) < smallest)
		{
			return true;
		}
		CProvince objective = state.Province(invasion.Objective);
		if (!CMovementRules.IsEnemy(state, objective.Controller, country.Tag) && objective.Controller != country.Tag
			&& !CApproaches.IsFriendly(state, country, objective.Controller))
		{
			return true;
		}
		CUnit? lead = invasion.Fleets.Count == 1 ? invasion.Fleets[0] : null;
		if (lead is { Order: EUnitOrder.Invasion } && lead.Passengers.Count > 0 && HasEnemies(state, country, lead.OrderProvince))
		{
			foreach (CUnit unit in invasion.LandForce)
			{
				CUnitShares.Of(state, unit, out long strength, out long organisation);
				if (strength < Weak || organisation < Weak)
				{
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>Ashore on the objective or in its area, or beside it over land - but not still in the port's area or the capital's.</summary>
	private static bool CanWalkThere(CGameState state, CCountry country, CInvasion invasion, CUnit unit)
	{
		if (unit.Transport is not null || unit.Location <= 0 || state.Province(unit.Location).IsSea)
		{
			return false;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? target = network.AreaOf(invasion.Objective);
		CSupplyArea? here = network.AreaOf(unit.Location);
		if (unit.Location == invasion.Objective || (here is not null && ReferenceEquals(here, target)))
		{
			return true;
		}
		if (here is null || ReferenceEquals(here, network.AreaOf(invasion.EmbarkationPort))
			|| ReferenceEquals(here, network.AreaOf(country.ActingCapital)))
		{
			return false;
		}
		return target is not null && here.Neighbours.Contains(target);
	}

	/// <summary>Of the side's ports near enough the objective by sea, the least by its base, its front, its owner and its distance.</summary>
	private static int EmbarkationPort(CGameState state, CCountry country, CInvasion invasion)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? target = network.AreaOf(invasion.Objective);
		CSupplyArea? capitalArea = network.AreaOf(country.ActingCapital);
		long limit = capitalArea is not null && CAreaHostility.TouchesHostileArea(state, capitalArea) ? NearLimit : FarLimit;
		CBuilding? naval = CTheatreFigures.NavalBase(state);
		int best = 0;
		long least = long.MaxValue;
		foreach (CSupplyArea area in network.Areas)
		{
			if (ReferenceEquals(area, target) || !CApproaches.IsFriendly(state, country, area.Controller))
			{
				continue;
			}
			long weight = 1;
			if (CAreaHostility.TouchesHostileArea(state, area))
			{
				weight = state.Country(area.Controller) is { } holder && ReferenceEquals(network.AreaOf(holder.ActingCapital), area) ? 2 : 4;
			}
			bool forceHere = invasion.LandForce.Count <= 5 && invasion.LandForce.Exists(unit => ReferenceEquals(network.AreaOf(unit.Location), area));
			foreach (int port in area.Ports)
			{
				long distance = CSeaTargets.Sea(state, port, invasion.Objective);
				if (distance < 0 || !CSealiftPickers.IsSeaRouteOpen(state, country, port, invasion.Objective))
				{
					continue;
				}
				distance = System.Math.Max(distance, Whole);
				if (distance > limit)
				{
					continue;
				}
				long level = naval is null ? 0 : state.Province(port).BuildingMaxLevel(naval).Raw;
				long s = 1500 - level / 10;
				if (CTheatreFigures.IsFrontProvince(state, state.Province(port), false))
				{
					s *= 10;
				}
				if (area.Controller == country.Tag)
				{
					s = s * 3 / 10;
				}
				if (ReferenceEquals(area, capitalArea))
				{
					s = s * 3 / 4;
				}
				long score = s * weight * distance;
				if (!forceHere)
				{
					score *= 3;
				}
				if (score < least)
				{
					least = score;
					best = port;
				}
			}
		}
		return best;
	}

	/// <summary>
	/// Stage 1: the troops ashore brought to where they board. One that can walk to the objective is let go. One at the
	/// embarkation port, or bound for it, is staged there; one at or bound for its staging province already is on its
	/// way. Any other marches to the port where a way by land leads there; and where none does, to a port it can reach
	/// - of its own area, else of a neighbouring area held by a friend - the nearest by sea to the embarkation port with
	/// the straits between them open, the first of equals: that port is its staging, and where the fleets were to meet
	/// at the embarkation port they meet there instead. With every unit settled and the meeting still at the embarkation
	/// port, it moves to the staging of the last unit found on its way. The original's
	/// <c>CAIInvasion::MarchLandForceToPort</c>.
	/// </summary>
	public static void March(CGameState state, CCountry country, CInvasion invasion)
	{
		int port = invasion.EmbarkationPort;
		CSupplyNetwork? network = null;
		List<CUnit>? dropped = null;
		int lastOnItsWay = 0;
		bool settled = true;
		foreach (CUnit unit in invasion.LandForce)
		{
			if (unit.Transport is not null || state.Province(unit.Location).IsSea)
			{
				continue;
			}
			if (CanWalkThere(state, country, invasion, unit))
			{
				(dropped ??= new List<CUnit>()).Add(unit);
				continue;
			}
			int bound = CAgentUnits.MovementDestinationOrPlace(unit);
			if (bound == port)
			{
				unit.Staging = port;
				continue;
			}
			if (bound == unit.Staging)
			{
				lastOnItsWay = bound;
				continue;
			}
			settled = false;
			if (CRouteFinder.Find(state, country, unit, unit.Location, port) is not null)
			{
				CAiMoves.Move(state, country, unit, port, safe: true);
				unit.Staging = port;
				continue;
			}
			network ??= CSupplyNetworks.Refresh(state);
			int staging = StagingPort(state, country, network, unit, port);
			if (staging <= 0)
			{
				continue;
			}
			if (invasion.Rendezvous == port)
			{
				invasion.Rendezvous = staging;
			}
			CAiMoves.Move(state, country, unit, staging, safe: true);
			unit.Staging = staging;
		}
		if (settled && invasion.Rendezvous == port && lastOnItsWay > 0)
		{
			invasion.Rendezvous = lastOnItsWay;
		}
		if (dropped is not null)
		{
			foreach (CUnit unit in dropped)
			{
				invasion.Remove(unit);
			}
		}
	}

	/// <summary>
	/// Where a unit with no way by land to the embarkation port goes to board: the port of its own area nearest by sea
	/// to the embarkation port, the straits between them open; with none, the same over the ports of every neighbouring
	/// area a friend holds. The first of equals, and nought with none.
	/// </summary>
	private static int StagingPort(CGameState state, CCountry country, CSupplyNetwork network, CUnit unit, int port)
	{
		if (network.AreaOf(unit.Location) is not { } area)
		{
			return 0;
		}
		int best = 0;
		long least = FarthestStaging;
		void Look(CSupplyArea of)
		{
			foreach (int candidate in of.Ports)
			{
				long distance = CSeaTargets.Sea(state, candidate, port);
				if (distance >= 0 && distance < least && CSealiftPickers.IsSeaRouteOpen(state, country, port, candidate))
				{
					least = distance;
					best = candidate;
				}
			}
		}
		Look(area);
		if (best > 0)
		{
			return best;
		}
		foreach (CSupplyArea beside in area.Neighbours)
		{
			if (CApproaches.IsFriendly(state, country, beside.Controller))
			{
				Look(beside);
			}
		}
		return best;
	}

	/// <summary>
	/// Stage 2: the fleet's carrier aircraft flown over the beach. Only with the one fleet under its invasion order, the
	/// objective held by an enemy with an enemy land unit in it, and three hours of daylight left over the fleet's
	/// province (<see cref="Weather.CDaylight.HoursToAiSunset"/>). Up to three times the best air unit not yet chosen is
	/// taken - over the fleet's carriers in order and each one's air units in order, one standing in the fleet's province,
	/// in no battle and going nowhere, its mean organisation at least a half and its mean strength a third, the largest
	/// organisation and strength together, the first of equals - and each chosen is given close support on the objective
	/// from now till that sunset. Nothing asks what order it has, its range or the weather: the order's own test does.
	/// The original's <c>CAIInvasion::SendCarrierAirSupport</c>.
	/// </summary>
	public static void AirSupport(CGameState state, CCountry country, CInvasion invasion)
	{
		if (invasion.Fleets.Count != 1 || invasion.Fleets[0] is not { Order: EUnitOrder.Invasion } fleet)
		{
			return;
		}
		CProvince objective = state.Province(invasion.Objective);
		if (!CMovementRules.IsEnemy(state, objective.Controller, country.Tag))
		{
			return;
		}
		int left = Weather.CDaylight.HoursToAiSunset(state, state.Province(fleet.Location));
		if (left < LeastDaylight || !HasEnemies(state, country, invasion.Objective))
		{
			return;
		}
		long end = state.TotalHours + left;
		var chosen = new List<CUnit>();
		while (chosen.Count < MostAirSupport)
		{
			CUnit? best = null;
			float bestScore = 0f;
			foreach (CSubUnit ship in fleet.SubUnits)
			{
				if (!CShipCensus.IsCarrier(ship))
				{
					continue;
				}
				foreach (CUnit group in ship.Carried)
				{
					if (group.Location != fleet.Location || group.IsFighting || group.Bombing is not null || group.IsMoving
						|| chosen.Contains(group))
					{
						continue;
					}
					// In the original's own precision: the two means as singles, their sum made in double and kept as a single.
					CUnitShares.OfSingle(state, group, out float strength, out float organisation);
					if (organisation < LeastAirOrganisation || strength < LeastAirStrength)
					{
						continue;
					}
					float score = (float)((double)organisation + strength);
					if (score > bestScore)
					{
						best = group;
						bestScore = score;
					}
				}
			}
			if (best is null)
			{
				break;
			}
			chosen.Add(best);
		}
		foreach (CUnit group in chosen)
		{
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group.Id, EUnitOrder.GroundAttack, invasion.Objective,
				stance: Missions.CMission.Defensive, endHour: end));
		}
	}

	/// <summary>
	/// Whether a unit is under an order a halt would cancel: any but a support attack and carrier protection. The
	/// original's <c>CUnit_HasHaltableOrder</c>.
	/// </summary>
	private static bool HasHaltableOrder(CUnit unit)
	{
		return unit.Order is not (EUnitOrder.None or EUnitOrder.SupportAttack or EUnitOrder.CarrierProtection);
	}

	/// <summary>An order given as the AI gives every order: the province is at once where the AI has the unit bound.</summary>
	private static void Order(CGameState state, CCountry country, CUnit fleet, EUnitOrder order, int province)
	{
		state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, order, province, ai: true));
		fleet.AiBound = province;
	}

	/// <summary>
	/// Stage 3, in the original's order (<c>CAIInvasion::SailAndIssueInvasionOrder</c>).
	///
	/// First each fleet's order: an empty one under any order has it taken away, its way kept, and is told the meeting
	/// place as where the AI sends it - so it sails there the next hour and not this one
	/// (<c>FINDINGS-liveread.md</c>, section 139) - which is also what brings a fleet back that has put its troops off;
	/// one carrying, not yet sent anywhere this hour, under no order a halt would cancel and not at the meeting place,
	/// is given a transport there where a land unit aboard is as free. Then each empty fleet in no battle it defends and
	/// not falling back: at the meeting place, arrived; bound there, left; else sent there by the AI's own move, which
	/// asks no range. Two or more arrived that may merge are merged, and the hour ends.
	///
	/// Then only with one fleet, at the meeting place, not sent anywhere this hour and under no order a halt would
	/// cancel: every unit of the force standing there, outside the objective's area, that the fleet can carry is loaded;
	/// while others are still walking there and the smallest of them would still fit, it waits. Otherwise, at once:
	/// away from the embarkation port, a transport there, which becomes the meeting place; at the port, the invasion on
	/// the landing picked again in an enemy's area - none left, and the area fails for the month - and a transport to an
	/// objective no enemy holds. Nothing asks whether anything is aboard.
	/// </summary>
	public static void Sail(CGameState state, CCountry country, CInvasion invasion)
	{
		int rendezvous = invasion.Rendezvous;
		foreach (CUnit fleet in invasion.Fleets)
		{
			if (fleet.Passengers.Count == 0)
			{
				if (fleet.Order != EUnitOrder.None)
				{
					state.Outbox.Add(new CDropOrderCommand(country.Tag, fleet.Id));
					fleet.AiBound = rendezvous;
				}
				continue;
			}
			if (fleet.AiBound > 0 || HasHaltableOrder(fleet) || fleet.Location == rendezvous)
			{
				continue;
			}
			if (fleet.Passengers.Exists(aboard => CMovementRules.IsLandUnit(aboard) && aboard.AiBound <= 0 && !HasHaltableOrder(aboard)))
			{
				Order(state, country, fleet, EUnitOrder.Transport, rendezvous);
			}
		}
		var arrived = new List<CUnit>();
		foreach (CUnit fleet in invasion.Fleets)
		{
			if (fleet.Passengers.Count > 0 || CCombats.IsHoldingGround(fleet) || fleet.IsRetreating)
			{
				continue;
			}
			int bound = CAgentUnits.MovementDestinationOrPlace(fleet);
			if (bound == rendezvous)
			{
				if (bound == fleet.Location)
				{
					arrived.Add(fleet);
				}
				continue;
			}
			CAiMoves.Move(state, country, fleet, rendezvous, safe: false);
		}
		if (arrived.Count > 1 && CUnitMerge.MayMerge(state, country, arrived))
		{
			for (int index = 1; index < arrived.Count; index++)
			{
				state.Outbox.Add(new CJoinUnitCommand(country.Tag, arrived[index].Id, arrived[0].Id));
			}
			return;
		}
		if (invasion.Fleets.Count > 1)
		{
			return;
		}
		CUnit lone = invasion.Fleets[0];
		if (lone.Location != rendezvous || lone.AiBound > 0 || HasHaltableOrder(lone))
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		CSupplyArea? area = network.AreaOf(invasion.Objective);
		// Whole weights, each cut, as the original counts them - which is how CTransport gives them.
		long capacity = CTransport.Capacity(lone);
		long aboard = 0;
		foreach (CUnit passenger in lone.Passengers)
		{
			if (CMovementRules.IsLandUnit(passenger))
			{
				aboard += CTransport.Weight(passenger);
			}
		}
		long walking = 0;
		long smallest = MostWeight;
		foreach (CUnit unit in invasion.LandForce)
		{
			if (unit.Transport is not null)
			{
				continue;
			}
			int bound = CAgentUnits.MovementDestinationOrPlace(unit);
			if (bound != rendezvous || (area is not null && ReferenceEquals(network.AreaOf(bound), area)))
			{
				continue;
			}
			long weight = CTransport.Weight(unit);
			if (bound != unit.Location)
			{
				walking += weight;
				smallest = System.Math.Min(smallest, weight);
				continue;
			}
			if (CTransport.CanCarry(lone, unit))
			{
				state.Outbox.Add(new CLoadUnitCommand(country.Tag, unit.Id, lone.Id));
				aboard += weight;
			}
		}
		if (walking >= 1 && capacity - aboard >= smallest)
		{
			return;
		}
		int port = invasion.EmbarkationPort;
		if (rendezvous != port)
		{
			Order(state, country, lone, EUnitOrder.Transport, port);
			invasion.Rendezvous = port;
			return;
		}
		if (area is null)
		{
			invasion.IsFinished = true;
			return;
		}
		if (!CMovementRules.IsEnemy(state, state.Province(invasion.Objective).Controller, country.Tag))
		{
			// An objective no enemy holds: the troops are carried there. Not built: the landing beside it the original gives
			// where the objective's area has under four provinces and more than ten friendly land units a province
			// (moving.md, section 20).
			Order(state, country, lone, EUnitOrder.Transport, invasion.Objective);
			return;
		}
		int landing = CLandingPicker.Pick(state, country, area, invasion);
		if (landing <= 0)
		{
			if (!country.UnitAi.FailedAreas.Contains(area))
			{
				country.UnitAi.FailedAreas.Add(area);
			}
			invasion.IsFinished = true;
			return;
		}
		invasion.Objective = landing;
		Order(state, country, lone, EUnitOrder.Invasion, landing);
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			agent.ReplanSubordinates = true;
		}
	}

	private static bool HasEnemies(CGameState state, CCountry country, int province)
	{
		foreach (CUnit unit in state.Positions.Of(province))
		{
			if (CTheatres.IsLand(unit) && CCountryLists.IsEnemy(state, country, unit.Owner))
			{
				return true;
			}
		}
		return false;
	}
}
