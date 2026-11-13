using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Missions;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's fleets each hour at war, and at peace on its full pass or a replan: out of a sea battle they are losing,
/// idle ones home, orders that make no sense any more cancelled, the enemy's power near its ports measured against its
/// own, submarines sent at the enemy's convoys or to a better hunting port, crowded fleets spread over its ports, screens
/// sent to patrol raided waters or its own, and a group of twice an enemy fleet's power sent to intercept it. The
/// original's <c>CAIUnit_ManageNavalUnits</c>, its port phase and its split and merge in <see cref="CFleetRegroup"/>.
/// See <c>docs/mechanics/moving.md</c>, section 14.
/// </summary>
public static class CNavalBody
{
	private const long Whole = 1000;

	private const long SurveyReach = 1000;

	private const long Losing = 500;

	private const int RaidHours = 504;

	private const int PatrolHours = 168;

	private const int PatrolArea = 5;

	private const long TroopCarrier = 660;

	private const long Superior = 1500;

	private const long SuperiorDefensive = 2000;

	public static void Run(CGameState state, CCountry country, CAgent agent, bool regroup, bool forced)
	{
		// At peace the body runs only when its pass is forced - the agent's full pass, or a replan of its fleets.
		if (agent.Unit is not { } headquarters || !regroup)
		{
			return;
		}
		agent.ReplanNaval = false;
		Withdraw(state, country, agent);
		IdleHome(state, country, agent, headquarters);
		List<CUnit> ready = CFleetRegroup.Ready(state, country, agent);
		var handled = new HashSet<CUnit>();
		ExistingOrders(state, country, ready, handled);
		CFleetRegroup.Merge(state, country, agent, ready, handled);
		// The rest - the survey, the ports, patrols and intercepts - only on a forced pass, at war or not
		// (FINDINGS-liveread.md, section 25).
		if (!forced)
		{
			return;
		}
		// Phase 6: what was ordered to join, and what it joins, is done with for this pass.
		ready.RemoveAll(handled.Contains);
		long enemyPower = Survey(state, country, agent, out List<CUnit> enemies);
		long ownPower = 0;
		var free = new List<CUnit>();
		ConvoysAndBases(state, country, agent, headquarters, ready, free, ref ownPower);
		Patrol(state, country, agent, headquarters, free);
		Intercept(state, country, agent, headquarters, free, enemies, enemyPower, ownPower);
	}

	/// <summary>Phase 1: a fleet whose side holds under half a sea battle goes home.</summary>
	private static void Withdraw(CGameState state, CCountry country, CAgent agent)
	{
		foreach (CUnit fleet in agent.Fleets)
		{
			foreach (CCombat combat in fleet.Combats)
			{
				if (combat.Kind != ECombatKind.Naval)
				{
					continue;
				}
				long share = CCombatShares.AttackerShare(combat);
				long ours = combat.Attacker.Units.Contains(fleet) ? share : Whole - share;
				if (ours < Losing && fleet.Base > 0)
				{
					state.Outbox.Add(new CMoveUnitCommand(country.Tag, fleet.Id, fleet.Base));
					break;
				}
			}
		}
	}

	/// <summary>Phase 2: an idle fleet away from its base sails home, a troop carrier staying in port while there is an embark.</summary>
	private static void IdleHome(CGameState state, CCountry country, CAgent agent, CUnit headquarters)
	{
		foreach (CUnit fleet in agent.Fleets)
		{
			if (fleet.IsMoving || IsBusy(fleet) || fleet.Base == 0 || fleet.Location == fleet.Base || fleet.IsFighting)
			{
				continue;
			}
			if (CTransport.Capacity(fleet) >= 1 && headquarters.Plan.Embark > 0)
			{
				continue;
			}
			state.Outbox.Add(new CMoveUnitCommand(country.Tag, fleet.Id, fleet.Base));
		}
	}

	/// <summary>Phase 4, over the ready fleets: what no longer makes sense is cancelled - a fleet sent home or stopped where it is.</summary>
	private static void ExistingOrders(CGameState state, CCountry country, List<CUnit> ready, HashSet<CUnit> handled)
	{
		// A war nobody declared keeps a raid, an escort and a patrol as a war does (FINDINGS-liveread.md, section 25).
		bool atWar = state.IsAtWar(country.Tag) || Wars.CUndeclaredWars.Involves(state, country.Tag);
		foreach (CUnit fleet in ready)
		{
			switch (fleet.Order)
			{
				case EUnitOrder.JoinFleet:
					handled.Add(fleet);
					if (fleet.Mission.LiveTarget is { } target)
					{
						handled.Add(target);
					}
					continue;
				case EUnitOrder.Transport when fleet.Passengers.Count == 0:
				case EUnitOrder.ConvoyRaid or EUnitOrder.ConvoyEscort when !atWar:
				case EUnitOrder.Patrol when !atWar:
					Cancel(state, country, fleet);
					continue;
				case EUnitOrder.None:
				case EUnitOrder.Move:
					continue;
				default:
					if (!fleet.IsMoving && (fleet.Location == fleet.Base || fleet.Order == EUnitOrder.Intercept))
					{
						if (fleet.Order is not (EUnitOrder.Reserve or EUnitOrder.Rebase or EUnitOrder.ConvoyRaid
							or EUnitOrder.ConvoyEscort or EUnitOrder.Patrol or EUnitOrder.Invasion or EUnitOrder.Transport))
						{
							Cancel(state, country, fleet);
						}
					}
					continue;
			}
		}
	}

	private static void Cancel(CGameState state, CCountry country, CUnit fleet)
	{
		state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.Rebase, fleet.Base > 0 ? fleet.Base : fleet.Location));
	}

	/// <summary>Phase 6: the power of every enemy fleet within reach of the theatre's ports, troop carriers first.</summary>
	private static long Survey(CGameState state, CCountry country, CAgent agent, out List<CUnit> enemies)
	{
		enemies = new List<CUnit>();
		long power = 0;
		if (CAgents.TheatreOf(state, country, agent) is not { } theatre || theatre.Ports.Count == 0)
		{
			return 0;
		}
		foreach (CCountry enemy in CCountryLists.Enemies(state, country))
		{
			foreach (CUnit fleet in enemy.AllUnits())
			{
				if (fleet.Kind != EUnitKind.Navy || fleet.IsRemoved || fleet.Location <= 0)
				{
					continue;
				}
				bool near = false;
				foreach (int port in theatre.Ports)
				{
					long distance = CSeaTargets.Sea(state, fleet.Location, port);
					if (distance >= 0 && distance < SurveyReach)
					{
						near = true;
						break;
					}
				}
				if (!near)
				{
					continue;
				}
				power += CUnitPower.Of(fleet);
				if (fleet.Passengers.Count > 0)
				{
					enemies.Insert(0, fleet);
				}
				else
				{
					enemies.Add(fleet);
				}
			}
		}
		return power;
	}

	/// <summary>Phase 7, over the ready fleets: submarines at the enemy's convoys or to a better port; others spread over the ports; the free gathered.</summary>
	private static void ConvoysAndBases(CGameState state, CCountry country, CAgent agent, CUnit headquarters,
		List<CUnit> ready, List<CUnit> free, ref long ownPower)
	{
		bool atWar = state.IsAtWar(country.Tag);
		var arriving = new Dictionary<int, int>();
		foreach (CUnit fleet in ready)
		{
			if (fleet.IsRetreating || fleet.SubUnits.Count == 0 || fleet.IsRemoved)
			{
				continue;
			}
			CShipCensus census = CShipCensus.Of(fleet);
			bool wolfpack = census.Submarines == fleet.SubUnits.Count;
			if (wolfpack && !IsBusy(fleet) && atWar && headquarters.Plan.NavalStance != 0)
			{
				Raid(state, country, agent, fleet);
			}
			Rebase(state, country, agent, fleet, arriving, census.Submarines);
			// A fleet carrying a transport is neither counted nor free: it patrols and intercepts nothing
			// (FINDINGS-liveread.md, section 42).
			if (census.Transports > 0)
			{
				continue;
			}
			if (fleet.Order is not (EUnitOrder.Rebase or EUnitOrder.ConvoyRaid))
			{
				ownPower += CUnitPower.Of(fleet);
			}
			if (!IsBusy(fleet) || fleet.Order is EUnitOrder.Patrol or EUnitOrder.ConvoyEscort)
			{
				free.Add(fleet);
			}
		}
	}

	/// <summary>A raid on the waters of an enemy convoy, drawn by its transports, nearer its ends the heavier; or, with few in reach, a better port.</summary>
	private static void Raid(CGameState state, CCountry country, CAgent agent, CUnit fleet)
	{
		var candidates = new List<(int Province, long Weight)>();
		int seen = 0;
		long total = 0;
		var tonnage = new Dictionary<int, long>();
		foreach (CConvoy convoy in agent.EnemyConvoys)
		{
			int transports = CConvoyRules.DesiredTransports(state, convoy);
			int count = convoy.Path.Count;
			for (int index = 0; index < count; index++)
			{
				int id = convoy.Path[index];
				if (id == convoy.Start || id == convoy.End || candidates.Exists(c => c.Province == id))
				{
					continue;
				}
				seen++;
				CProvince province = state.Province(id);
				if (!province.IsSea || !CUnitOrders.IsInRange(state, fleet, EUnitOrder.ConvoyRaid, province))
				{
					continue;
				}
				long weight = transports * (long)System.Math.Abs(index + 1 - count / 2);
				candidates.Add((id, weight));
				total += weight;
				if (CAgents.TheatreOf(state, country, agent) is { } theatre)
				{
					foreach (int port in theatre.Ports)
					{
						if (CSeaTargets.Sea(state, id, port) is var distance and >= 0
							&& distance <= CUnitOrders.RangeOf(fleet).Raw / Whole)
						{
							tonnage[port] = tonnage.GetValueOrDefault(port) + transports;
						}
					}
				}
			}
		}
		if (candidates.Count == 0 || candidates.Count * 2 < seen)
		{
			Relocate(state, country, fleet, tonnage);
			return;
		}
		if (total <= 0)
		{
			return;
		}
		long draw = state.AiRandom.Next() % total;
		int chosen = 0;
		foreach ((int province, long weight) in candidates)
		{
			if (draw <= 0)
			{
				break;
			}
			chosen = province;
			draw -= weight;
		}
		if (chosen > 0)
		{
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.ConvoyRaid, chosen,
				endHour: state.TotalHours + RaidHours));
		}
	}

	/// <summary>To the port with the most raidable tonnage, less 20 a berth over its room, half again for its own base.</summary>
	private static void Relocate(CGameState state, CCountry country, CUnit fleet, Dictionary<int, long> tonnage)
	{
		int best = 0;
		long most = 0;
		foreach ((int port, long tons) in tonnage)
		{
			long score = tons * 10 * Whole - 20 * Whole * System.Math.Max(0, Berths(state, port) - Level(state, port));
			if (port == fleet.Base)
			{
				score = score * 3 / 2;
			}
			if (score > most && CRouteFinder.Find(state, country, fleet, fleet.Location, port) is not null)
			{
				most = score;
				best = port;
			}
		}
		if (best > 0 && best != fleet.Base)
		{
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.Rebase, best));
		}
	}

	/// <summary>
	/// A fleet not busy goes to a better port of its theatre: whatever it holds where its base lies outside the theatre, and
	/// otherwise - no submarine aboard - where its base lies in another area than its commander's or is crowded past its level
	/// and two. The port is the theatre's in its commander's area with the least distance from the commander + 10, +2000 a
	/// ship over the port's level, x0.75 for staying, in single precision (reversing/findings/FINDINGS-airnaval4.md, the
	/// naval body's rebase; reversing/findings/FINDINGS-liveread.md, section 64).
	/// </summary>
	private static void Rebase(CGameState state, CCountry country, CAgent agent, CUnit fleet, Dictionary<int, int> arriving,
		int submarines)
	{
		if (IsBusy(fleet) || CAgents.TheatreOf(state, country, agent) is not { } theatre)
		{
			return;
		}
		Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
		int commanderAt = fleet.Parent?.Location ?? 0;
		var commanderArea = commanderAt > 0 ? network.AreaOf(commanderAt) : null;
		// A base outside the theatre: rebased whatever the fleet holds and however empty the port.
		bool forced = fleet.Base == 0 || state.Province(fleet.Base).TheatreId != theatre.Id;
		if (!forced)
		{
			if (submarines > 0)
			{
				return;
			}
			if (!ReferenceEquals(network.AreaOf(fleet.Base), commanderArea))
			{
				forced = true;
			}
			else if (Berths(state, fleet.Base) + arriving.GetValueOrDefault(fleet.Base) <= Level(state, fleet.Base) + 2)
			{
				return;
			}
		}
		if (commanderAt <= 0)
		{
			return;
		}
		int best = 0;
		float least = 1e6f;
		foreach (int port in theatre.Ports)
		{
			if (!ReferenceEquals(network.AreaOf(port), commanderArea))
			{
				continue;
			}
			bool there = port == fleet.Base;
			float score = (float)(CMapDistance.Between(state.Province(port), state.Province(commanderAt), state.MapWidth) + 10.0);
			long load = Berths(state, port) + arriving.GetValueOrDefault(port) + (there ? 0 : fleet.SubUnits.Count);
			long over = load - Level(state, port);
			if (over > 0)
			{
				score = (float)(over * 2000 + (double)score);
			}
			if (there)
			{
				score = (float)((double)score * 0.75);
			}
			if (score < least && CRouteFinder.Find(state, country, fleet, fleet.Location, port) is not null)
			{
				least = score;
				best = port;
			}
		}
		if (best > 0 && best != fleet.Base)
		{
			arriving[best] = arriving.GetValueOrDefault(best) + fleet.SubUnits.Count;
			if (fleet.Base > 0)
			{
				arriving[fleet.Base] = arriving.GetValueOrDefault(fleet.Base) - fleet.SubUnits.Count;
			}
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.Rebase, best));
		}
	}

	/// <summary>Phase 8: screens round the raided waters, or round the theatre's own ports' seas, five provinces a patrol.</summary>
	private static void Patrol(CGameState state, CCountry country, CAgent agent, CUnit headquarters, List<CUnit> free)
	{
		if (headquarters.Plan.NavalStance == 0 || (!state.IsAtWar(country.Tag) && !Wars.CUndeclaredWars.Involves(state, country.Tag)
			&& agent.RaidedWaters.Count == 0))
		{
			return;
		}
		var waters = new List<int>(agent.RaidedWaters);
		if (waters.Count == 0 && CAgents.TheatreOf(state, country, agent) is { } theatre)
		{
			foreach (int port in theatre.Ports)
			{
				int sea = state.Province(port).PortSea;
				if (sea > 0 && !waters.Contains(sea))
				{
					waters.Add(sea);
				}
			}
		}
		while (waters.Count > 0)
		{
			int centre = waters[(int)(state.AiRandom.Next() % waters.Count)];
			var area = new List<int> { centre };
			waters.Remove(centre);
			while (area.Count < PatrolArea && waters.Count > 0)
			{
				int nearest = waters[0];
				long least = long.MaxValue;
				foreach (int id in waters)
				{
					long distance = CMapDistance.Between(state.Province(centre), state.Province(id), state.MapWidth);
					if (distance < least)
					{
						least = distance;
						nearest = id;
					}
				}
				area.Add(nearest);
				waters.Remove(nearest);
			}
			CUnit? fleet = Nearest(state, free, centre, f => !IsBusy(f) && CShipCensus.Of(f) is { Submarines: 0, Screens: > 0 });
			if (fleet is null)
			{
				return;
			}
			free.Remove(fleet);
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, fleet.Id, EUnitOrder.Patrol, centre, provinces: area,
				endHour: state.TotalHours + PatrolHours));
		}
	}

	/// <summary>Phase 9: a group of twice an enemy fleet's power sent at it, the first fleet to intercept and the rest to join it.</summary>
	private static void Intercept(CGameState state, CCountry country, CAgent agent, CUnit headquarters, List<CUnit> free,
		List<CUnit> enemies, long enemyPower, long ownPower)
	{
		long bar = headquarters.Plan.NavalStance == CUnitPlan.DefensiveAirOrNaval ? SuperiorDefensive : Superior;
		foreach (CUnit enemy in enemies)
		{
			if (!state.Province(enemy.Location).IsSea)
			{
				continue;
			}
			bool troops = enemy.Passengers.Count > 0;
			if (!troops && enemyPower * Whole > ownPower * bar)
			{
				continue;
			}
			long need = CUnitPower.Of(enemy);
			long target = troops ? need * TroopCarrier / Whole : need;
			bool allSubs = enemy.SubUnits.TrueForAll(CShipCensus.IsSubmarine);
			var group = new List<CUnit>();
			long gathered = 0;
			while (2 * need > gathered)
			{
				CUnit? fleet = Nearest(state, free, enemy.Location, f => !group.Contains(f) && CUnitPower.Of(f) > 0
					&& (f.Order != EUnitOrder.Patrol || troops) && (!allSubs || CShipCensus.Of(f).Screens > 0)
					&& CRouteFinder.Find(state, country, f, f.Location, enemy.Location) is not null);
				if (fleet is null)
				{
					break;
				}
				group.Add(fleet);
				gathered += CUnitPower.Of(fleet);
			}
			if (group.Count == 0 || gathered < target)
			{
				continue;
			}
			// An intercept on the enemy fleet, which the order names (moving.md, section 14).
			state.Outbox.Add(new CSetUnitOrderCommand(country.Tag, group[0].Id, EUnitOrder.Intercept, enemy.Location,
				endHour: state.TotalHours, target: enemy.Id));
			for (int index = 1; index < group.Count; index++)
			{
				state.Outbox.Add(new CJoinUnitCommand(country.Tag, group[index].Id, group[0].Id));
			}
			foreach (CUnit fleet in group)
			{
				free.Remove(fleet);
				ownPower -= CUnitPower.Of(fleet);
				need -= CUnitPower.Of(fleet);
				if (need < 0)
				{
					break;
				}
			}
		}
	}

	private static CUnit? Nearest(CGameState state, List<CUnit> fleets, int province, System.Predicate<CUnit> fits)
	{
		CUnit? nearest = null;
		long least = long.MaxValue;
		foreach (CUnit fleet in fleets)
		{
			if (!fits(fleet) || fleet.Location <= 0)
			{
				continue;
			}
			long distance = CSeaTargets.Sea(state, fleet.Location, province);
			if (distance >= 0 && distance < least)
			{
				least = distance;
				nearest = fleet;
			}
		}
		return nearest;
	}

	/// <summary>An order that keeps it from taking another: anything but none, a plain move and a join being dealt with.</summary>
	private static bool IsBusy(CUnit fleet) => fleet.Order is not (EUnitOrder.None or EUnitOrder.Move);

	/// <summary>The ships based at a port.</summary>
	private static long Berths(CGameState state, int port)
	{
		long ships = 0;
		foreach (CCountry owner in state.Countries)
		{
			if (state.Province(port).Controller != owner.Tag)
			{
				continue;
			}
			foreach (CUnit fleet in owner.AllUnits())
			{
				if (fleet.Kind == EUnitKind.Navy && fleet.Base == port)
				{
					ships += fleet.SubUnits.Count;
				}
			}
		}
		return ships;
	}

	private static long Level(CGameState state, int port)
	{
		return CTheatreFigures.NavalBase(state) is { } naval ? state.Province(port).BuildingMaxLevel(naval).Raw / Whole : 0;
	}
}
