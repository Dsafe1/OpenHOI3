using System.Collections.Generic;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Transport;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Fleets put under the theatres that want them - nothing sails: each submarine fleet to the agent hunting the most
/// convoys it has not the submarines for, each surface fleet to the agent facing the largest share of the enemy's ships
/// for the share of the country's ships it holds; and transports gathered at a theatre's sealift port until they can
/// carry its troops. The original's <c>CEU3AI_DistributeFleetsToTheatres</c> and
/// <c>CEU3AI_GatherTransportsForTheatres</c>. See <c>docs/mechanics/agents.md</c>, section 10.4.
/// </summary>
public static class CFleetMovers
{
	private const long Whole = 1000;

	private const long Nobody = -10_000 * Whole;

	public static void Distribute(CGameState state, CCountry country)
	{
		var agents = new List<CAgent>();
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (agent.Unit is not null)
			{
				agents.Add(agent);
			}
		}
		if (agents.Count == 0)
		{
			return;
		}
		var fleets = new List<CUnit>();
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.Kind == EUnitKind.Navy && !unit.IsRemoved && unit.Agent is null)
			{
				fleets.Add(unit);
			}
		}
		var subs = new Dictionary<CAgent, long>();
		var holding = new Dictionary<CAgent, long>();
		long total = 0;
		foreach (CUnit fleet in fleets)
		{
			CShipCensus census = CShipCensus.Of(fleet);
			long surface = census.Carriers + census.Capitals + census.Screens;
			total += surface;
			if (Topmost(fleet) is { } at)
			{
				subs[at] = subs.GetValueOrDefault(at) + census.Submarines;
				holding[at] = holding.GetValueOrDefault(at) + surface;
			}
		}
		var wants = new Dictionary<CAgent, long>();
		long sum = 0;
		foreach (CAgent agent in agents)
		{
			if (CAgents.TheatreOf(state, country, agent) is { Ports.Count: > 0 })
			{
				long ships = 0;
				foreach (CUnit enemy in agent.EnemyFleets)
				{
					ships += enemy.SubUnits.Count;
				}
				wants[agent] = ships;
				sum += ships;
			}
		}
		foreach (CUnit fleet in fleets)
		{
			CShipCensus census = CShipCensus.Of(fleet);
			CAgent? current = Topmost(fleet);
			CAgent? best = null;
			if (census.Submarines > 0)
			{
				long currentTerm = current is null || current.SubmarineWant <= 0
					? Nobody
					: (current.SubmarineWant - subs.GetValueOrDefault(current) + fleet.SubUnits.Count) * Whole;
				long most = 0;
				foreach (CAgent agent in agents)
				{
					if (agent.SubmarineWant <= 0)
					{
						continue;
					}
					long score = (agent.SubmarineWant - subs.GetValueOrDefault(agent)) * Whole - currentTerm;
					if (score > most)
					{
						most = score;
						best = agent;
					}
				}
				if (best is not null && !ReferenceEquals(best, current))
				{
					if (current is not null)
					{
						subs[current] = subs.GetValueOrDefault(current) - census.Submarines;
					}
					subs[best] = subs.GetValueOrDefault(best) + census.Submarines;
				}
			}
			else if (sum > 0 && total > 0)
			{
				long n = fleet.SubUnits.Count;
				long most = Nobody;
				foreach ((CAgent agent, long want) in wants)
				{
					long held = holding.GetValueOrDefault(agent) + (ReferenceEquals(agent, current) ? 0 : n);
					long score = want * Whole / sum - held * Whole / total;
					if (score > most)
					{
						most = score;
						best = agent;
					}
				}
				if (best is not null && !ReferenceEquals(best, current))
				{
					if (current is not null)
					{
						holding[current] = holding.GetValueOrDefault(current) - n;
					}
					holding[best] = holding.GetValueOrDefault(best) + n;
				}
			}
			if (best is not null && !ReferenceEquals(best, current) && best.Unit is { } headquarters)
			{
				Move(state, country, fleet, best, current, headquarters);
			}
		}
	}

	/// <summary>Transport fleets taken by each agent with troops for the sea until its transports can carry them, nearest its port first.</summary>
	public static void Gather(CGameState state, CCountry country)
	{
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (agent.Unit is not { } headquarters || agent.SealiftPort <= 0 || agent.SealiftWeight <= 0)
			{
				continue;
			}
			// What this agent has taken, made again for each agent: a fleet another took this pass is still its old agent's
			// until the command runs, so a later agent may take it too and its attach, coming last, holds - Britain's 3rd
			// Transport Ship Squadron taken by Far East Command and then East Africa Command (FINDINGS-liveread.md, section 73).
			var taken = new HashSet<CUnit>();
			long have = 0;
			foreach (CUnit fleet in agent.TransportFleets)
			{
				have += CTransport.Capacity(fleet);
			}
			while (agent.SealiftWeight > have)
			{
				CUnit? best = null;
				long bestScore = long.MinValue;
				foreach (CUnit fleet in country.UnitsInOrder())
				{
					long room = CTransport.Capacity(fleet) - CTransport.Load(fleet);
					if (fleet.Kind != EUnitKind.Navy || fleet.IsRemoved || fleet.Agent is not null || taken.Contains(fleet)
						|| room < 1 || CCommandLadder.IsBlocked(fleet) || ReferenceEquals(Topmost(fleet), agent)
						|| Topmost(fleet) is { SealiftWeight: > 0 })
					{
						continue;
					}
					long distance = CSeaTargets.Sea(state, fleet.Location, agent.SealiftPort);
					if (distance < 0)
					{
						continue;
					}
					long score = 10 * Whole - System.Math.Min(distance * Whole / 400, 10 * Whole) + room * Whole;
					if (score > bestScore)
					{
						bestScore = score;
						best = fleet;
					}
				}
				if (best is null)
				{
					break;
				}
				taken.Add(best);
				have += CTransport.Capacity(best);
				Move(state, country, best, agent, Topmost(best), headquarters);
			}
		}
	}

	private static void Move(CGameState state, CCountry country, CUnit fleet, CAgent to, CAgent? from, CUnit headquarters)
	{
		state.Outbox.Add(new CAttachUnitCommand(country.Tag, fleet.Id, headquarters.Id));
		to.ReplanSubordinates = true;
		if (from is not null)
		{
			from.ReplanSubordinates = true;
		}
	}

	private static CAgent? Topmost(CUnit unit)
	{
		CAgent? found = null;
		for (CUnit? at = unit.Parent; at is not null; at = at.Parent)
		{
			if (at.Agent is { } agent)
			{
				found = agent;
			}
		}
		return found;
	}
}
