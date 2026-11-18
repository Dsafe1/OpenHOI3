using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// The airborne planner, for an agent with air transports, in four steps. (1) As many of the country's weak enemy ports as
/// it has airborne armies and transports, each with the nearest air base of a country it is not hostile to within the
/// first transport's range - at war, only its own where the port is on another continent from the headquarters. (2) Round
/// the bases in turn, each takes the nearest free airborne army, a long way more for one it cannot reach by land; the base
/// is the army's staging, and it marches there where it can reach it, else to the nearest air base of its own area, else,
/// with no sealift target yet, asks for the base's area's nearest port. (3) Every airborne army at a friendly air base
/// takes the nearest empty transport: one at that base flies it to its staging, unless it is there already; one elsewhere
/// moves to it. (4) Each target now hostile: the transports idle at its base drop an army standing there on it, or one
/// already carrying is sent. The original's <c>CAIUnit_PlanParadrops</c>. See <c>docs/mechanics/moving.md</c>, section 9.1.
/// </summary>
public static class CParadropPlanner
{
	/// <summary>The first nearest of a base to a target, and of a port to a base.</summary>
	private const int FirstNearest = 1_000_000;

	/// <summary>The first nearest of an army or a base, in single precision.</summary>
	private const float FirstNearestFloat = 1_000_000f;

	/// <summary>What an army the country cannot get to its base by land counts as further.</summary>
	private const double NotJoined = 100_000.0;

	public static void Run(CGameState state, CCountry country, CAgent agent)
	{
		int wanted = System.Math.Min(agent.Airborne.Count, agent.AirTransports.Count);
		if (wanted <= 0 || CAgents.TheatreOf(state, country, agent) is null)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var targets = new List<int>();
		var bases = new List<int>();
		ChooseTargets(state, country, agent, network, wanted, targets, bases);
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			var pairs = new List<string>();
			for (int i = 0; i < targets.Count; i++)
			{
				pairs.Add($"{targets[i]} from {bases[i]}");
			}
			trace.Write($"  paradrops {agent.Unit?.Name}: {agent.Airborne.Count} airborne, {agent.AirTransports.Count} transports, " +
				$"{country.UnitAi.WeakEnemyPorts.Count} weak ports, targets {string.Join(", ", pairs)}");
		}
		if (targets.Count == 0)
		{
			return;
		}
		if (agent.Airborne.Count > 0)
		{
			Stage(state, country, agent, network, bases);
		}
		Board(state, country, agent);
		Drop(state, country, agent, targets, bases);
	}

	/// <summary>Step 1: the targets, each with its base.</summary>
	private static void ChooseTargets(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, int wanted,
		List<int> targets, List<int> bases)
	{
		long range = CUnitOrders.RangeOf(agent.AirTransports[0]).Raw;
		var friends = new List<CCountry> { country };
		foreach (CCountry other in state.Countries)
		{
			if (other.Tag != country.Tag && CTheatreFriends.IsNonHostile(state, country, other))
			{
				friends.Add(other);
			}
		}
		string? homeContinent = agent.Unit is { Location: > 0 } headquarters ? state.Province(headquarters.Location).Continent : null;
		foreach ((int id, float _) in country.UnitAi.WeakEnemyPorts)
		{
			if (targets.Count >= wanted)
			{
				break;
			}
			CProvince target = state.Province(id);
			int nearest = FirstNearest;
			int found = 0;
			foreach (CCountry friend in friends)
			{
				if (agent.FrontierAtWar && friend.Tag != country.Tag && homeContinent is not null && target.Continent != homeContinent)
				{
					continue;
				}
				foreach (CSupplyArea area in network.Areas)
				{
					if (area.Controller != friend.Tag)
					{
						continue;
					}
					foreach (int airBase in AirBases(state, area))
					{
						int distance = (int)CMapDistance.Between(state.Province(airBase), target, state.MapWidth);
						if (distance < nearest)
						{
							nearest = distance;
							found = airBase;
						}
					}
				}
			}
			if (found != 0 && (long)nearest * 1000 <= range)
			{
				targets.Add(id);
				bases.Add(found);
			}
		}
	}

	/// <summary>Step 2: round the bases, an army each, until a round gives nobody or every army has a base.</summary>
	private static void Stage(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, List<int> bases)
	{
		var given = new List<CUnit>();
		bool any;
		do
		{
			any = false;
			foreach (int baseId in bases)
			{
				CProvince airBase = state.Province(baseId);
				CUnit? army = null;
				int bound = 0;
				bool joined = false;
				float nearest = FirstNearestFloat;
				foreach (CUnit unit in agent.Airborne)
				{
					if (unit.Transport is not null || CCombats.IsHoldingGround(unit) || given.Contains(unit))
					{
						continue;
					}
					int destination = CAgentUnits.MovementDestinationOrPlace(unit);
					if (destination == baseId)
					{
						// One standing at the base or bound there already is taken at once.
						army = unit;
						bound = destination;
						break;
					}
					bool reaches = CAreaPaths.Joined(state, country, baseId, unit.Location);
					float distance = CMapDistance.Between(state.Province(unit.Location), airBase, state.MapWidth);
					if (!reaches)
					{
						distance = (float)(distance + NotJoined);
					}
					if (distance < nearest)
					{
						nearest = distance;
						army = unit;
						bound = destination;
						joined = reaches;
					}
				}
				if (army is null)
				{
					break;
				}
				any = true;
				given.Add(army);
				if (army.Staging != baseId)
				{
					army.Staging = baseId;
					state.Outbox.Add(new CSetAiParamCommand(country.Tag, army.Id, army.SealiftTarget, baseId));
				}
				if (bound == baseId)
				{
					continue;
				}
				if (joined)
				{
					CAiMoves.Move(state, country, army, baseId, safe: true);
					continue;
				}
				if (network.AreaOf(army.Location) is not { } own)
				{
					continue;
				}
				CProvince at = state.Province(army.Location);
				int closest = 0;
				float least = FirstNearestFloat;
				foreach (int ownBase in AirBases(state, own))
				{
					float distance = CMapDistance.Between(state.Province(ownBase), at, state.MapWidth);
					if (distance < least)
					{
						least = distance;
						closest = ownBase;
					}
				}
				if (closest != 0)
				{
					if (bound != closest)
					{
						CAiMoves.Move(state, country, army, closest, safe: true);
					}
					continue;
				}
				if (army.SealiftTarget == 0 && network.AreaOf(baseId) is { } baseArea)
				{
					int port = 0;
					int shortest = FirstNearest;
					foreach (int id in baseArea.Ports)
					{
						int distance = (int)CMapDistance.Between(state.Province(id), airBase, state.MapWidth);
						if (distance < shortest)
						{
							shortest = distance;
							port = id;
						}
					}
					if (port != 0)
					{
						army.SealiftTarget = port;
						state.Outbox.Add(new CSetAiParamCommand(country.Tag, army.Id, port, baseId));
					}
				}
			}
		}
		while (any && given.Count < agent.Airborne.Count);
	}

	/// <summary>Step 3: every airborne army at a friendly air base takes the nearest empty transport.</summary>
	private static void Board(CGameState state, CCountry country, CAgent agent)
	{
		var taken = new List<CUnit>();
		foreach (CUnit army in agent.Airborne)
		{
			if (army.Transport is not null || CCombats.IsHoldingGround(army))
			{
				continue;
			}
			CProvince at = state.Province(CAgentUnits.MovementDestinationOrPlace(army));
			if (CMovementRules.BuildingLevel(state, at, CMovementRules.AirBase) < CFixedPoint.One
				|| !CApproaches.MayOperateIn(state, country, at.Controller))
			{
				continue;
			}
			CUnit? plane = null;
			float nearest = FirstNearestFloat;
			CProvince stands = state.Province(army.Location);
			foreach (CUnit transport in agent.AirTransports)
			{
				if (transport.Passengers.Count > 0 || taken.Contains(transport))
				{
					continue;
				}
				float distance = CMapDistance.Between(state.Province(BaseOf(transport)), stands, state.MapWidth);
				if (distance < nearest)
				{
					nearest = distance;
					plane = transport;
				}
			}
			if (plane is null)
			{
				continue;
			}
			taken.Add(plane);
			if (BaseOf(plane) != at.Id)
			{
				// The province's holder was asked above, so a plane elsewhere always moves to the army's base.
				state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, plane.Id, EUnitOrder.RebaseAir, at.Id, ai: true));
				continue;
			}
			if (army.Staging == at.Id || army.Location != at.Id || !Transport.CAirTransport.MayLoad(state, plane, army))
			{
				continue;
			}
			state.Outbox.Add(new CLoadOnPlaneCommand(country.Tag, army.Id, plane.Id));
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, plane.Id, EUnitOrder.Paradrop, army.Staging, ai: true));
		}
	}

	/// <summary>Step 4: each target now hostile gets what is idle at its base.</summary>
	private static void Drop(CGameState state, CCountry country, CAgent agent, List<int> targets, List<int> bases)
	{
		var loaded = new List<CUnit>();
		for (int i = 0; i < targets.Count; i++)
		{
			CProvince target = state.Province(targets[i]);
			if (target.Controller is not { } holder || holder == country.Tag
				|| !CMovementRules.IsEnemyIn(state, country.Tag, holder, target.Id))
			{
				continue;
			}
			int baseId = bases[i];
			foreach (CUnit plane in agent.AirTransports)
			{
				if (BaseOf(plane) != baseId || plane.IsMoving
					|| plane.Order is not (EUnitOrder.None or EUnitOrder.SupportAttack or EUnitOrder.CarrierProtection))
				{
					continue;
				}
				if (plane.Passengers.Count < 1)
				{
					foreach (CUnit army in agent.Airborne)
					{
						if (army.Transport is not null || army.Plane is not null || army.Location != baseId || loaded.Contains(army)
							|| CCombats.IsHoldingGround(army) || !Transport.CAirTransport.MayLoad(state, plane, army))
						{
							continue;
						}
						loaded.Add(army);
						state.Outbox.Add(new CLoadOnPlaneCommand(country.Tag, army.Id, plane.Id));
						state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, plane.Id, EUnitOrder.Paradrop, target.Id, ai: true));
						break;
					}
				}
				else if (plane.Order == EUnitOrder.None && !plane.IsFighting && plane.Location == BaseOf(plane))
				{
					state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, plane.Id, EUnitOrder.Paradrop, target.Id, ai: true));
				}
			}
		}
	}

	/// <summary>An air unit's base, or where it is for one with none.</summary>
	private static int BaseOf(CUnit plane) => plane.Base > 0 ? plane.Base : plane.Location;

	/// <summary>
	/// An area's provinces with an air base built to a whole level, the largest first and of two alike the one first in the
	/// area: the original's <c>COwnerArea +0x58</c>.
	/// </summary>
	private static List<int> AirBases(CGameState state, CSupplyArea area)
	{
		var found = new List<int>();
		var levels = new List<long>();
		foreach (int id in area.Provinces)
		{
			long level = CMovementRules.BuildingLevel(state, state.Province(id), CMovementRules.AirBase).Raw;
			if (level < CFixedPoint.Scale)
			{
				continue;
			}
			int at = found.Count;
			while (at > 0 && levels[at - 1] < level)
			{
				at--;
			}
			found.Insert(at, id);
			levels.Insert(at, level);
		}
		return found;
	}
}
