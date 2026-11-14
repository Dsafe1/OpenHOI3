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
/// from, the troops marched there, the empty fleets brought together and merged, the troops loaded, the fleet sailed to
/// the port and then given the invasion on the landing picked again. The original's <c>CAIInvasion::Tick</c>,
/// <c>IsInvasionOver</c>, <c>DoTick</c>, <c>PickEmbarkationPort</c>, <c>MarchLandForceToPort</c> and
/// <c>SailAndIssueInvasionOrder</c>. See <c>docs/mechanics/moving.md</c>, section 16.
/// </summary>
public static class CInvasionTick
{
	private const long Whole = 1000;

	private const long Weak = 330;

	private const long NearLimit = 1000;

	private const long FarLimit = 2000;

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

	/// <summary>Stage 1: the troops ashore marched to the rendezvous, staged there once arrived.</summary>
	private static void March(CGameState state, CCountry country, CInvasion invasion)
	{
		foreach (CUnit unit in invasion.LandForce)
		{
			if (unit.Transport is not null || state.Province(unit.Location).IsSea)
			{
				continue;
			}
			if (unit.Location == invasion.Rendezvous)
			{
				unit.Staging = invasion.Rendezvous;
				continue;
			}
			if (CAgentUnits.Destination(unit) == invasion.Rendezvous)
			{
				continue;
			}
			if (CRouteFinder.Find(state, country, unit, unit.Location, invasion.Rendezvous) is not null)
			{
				CAiMoves.Move(state, country, unit, invasion.Rendezvous, safe: true);
			}
		}
	}

	/// <summary>Stage 3: the empty fleets brought to the rendezvous and merged, the troops loaded, the fleet sailed and the invasion given.</summary>
	private static void Sail(CGameState state, CCountry country, CInvasion invasion)
	{
		int rendezvous = invasion.Rendezvous;
		var arrived = new List<CUnit>();
		foreach (CUnit fleet in invasion.Fleets)
		{
			if (fleet.Passengers.Count > 0 || fleet.Order is EUnitOrder.Invasion or EUnitOrder.Transport)
			{
				continue;
			}
			if (fleet.Location == rendezvous)
			{
				if (!fleet.IsMoving)
				{
					arrived.Add(fleet);
				}
			}
			else if (CAgentUnits.Destination(fleet) != rendezvous)
			{
				state.Outbox.Add(new CMoveUnitCommand(country.Tag, fleet.Id, rendezvous));
			}
		}
		if (arrived.Count > 1)
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
		CUnit fleetAll = invasion.Fleets[0];
		if (fleetAll.Location == rendezvous && !fleetAll.IsMoving && fleetAll.Order == EUnitOrder.None)
		{
			bool loaded = false;
			foreach (CUnit unit in invasion.LandForce)
			{
				if (unit.Transport is null && unit.Location == rendezvous && CTransport.CanCarry(fleetAll, unit))
				{
					state.Outbox.Add(new CLoadUnitCommand(country.Tag, unit.Id, fleetAll.Id));
					loaded = true;
				}
			}
			bool waiting = invasion.LandForce.Exists(unit => unit.Transport is null && CAgentUnits.Destination(unit) == rendezvous);
			if (loaded || waiting)
			{
				return;
			}
		}
		if (fleetAll.Passengers.Count == 0 || fleetAll.Order is EUnitOrder.Invasion)
		{
			return;
		}
		if (rendezvous != invasion.EmbarkationPort)
		{
			invasion.Rendezvous = invasion.EmbarkationPort;
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleetAll.Id, EUnitOrder.Transport, invasion.EmbarkationPort, ai: true));
			return;
		}
		if (fleetAll.Location != invasion.EmbarkationPort || fleetAll.IsMoving)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		if (network.AreaOf(invasion.Objective) is not { } area)
		{
			invasion.IsFinished = true;
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
		state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleetAll.Id, EUnitOrder.Invasion, landing, ai: true));
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
