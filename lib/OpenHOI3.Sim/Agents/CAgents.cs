using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's life: made with its theatre, bound to the theatre's headquarters by the country's pass, let go and removed;
/// and the country's top-level units with no theatre attached under one. The original's <c>EnsureAgentForArea</c>,
/// <c>CreateAgentsForTheatres</c>, <c>SetUnit</c>, <c>ClearUnit</c> and <c>CEU3AI_BindAgentsAndAttachOrphans</c>. See
/// <c>docs/mechanics/agents.md</c>, section 1.
/// </summary>
public static class CAgents
{
	/// <summary>The nearest agent is looked for from this distance.</summary>
	private const long Far = 10_000_000;

	/// <summary>
	/// The theatre layer telling the agents of a theatre made or kept, or - <paramref name="going"/> - one dropped or left
	/// with no provinces: the agent covering it is marked to set its area, or let go and removed; a theatre none covers is
	/// given a new, unbound agent where the AI runs the country's units.
	/// </summary>
	public static void Ensure(CGameState state, CCountry country, CTheatre theatre, bool going)
	{
		CUnitAi ai = country.UnitAi;
		CAgent? covering = null;
		foreach (CAgent agent in ai.Agents)
		{
			if (!going && agent.Unit is { } unit && unit.Id == theatre.Headquarters && agent.Theatre != theatre.Id)
			{
				agent.Theatre = theatre.Id;
			}
			if (agent.Theatre == theatre.Id && (agent.Unit is null || agent.Unit.Kind == EUnitKind.Theatre))
			{
				covering ??= agent;
			}
		}
		if (going)
		{
			// Called from the theatres' hour, which every machine runs: the headquarters' plan goes off by what the game
			// holds of it, and not by an agent only the AI's machine has (docs/multiplayer.md, section 3).
			if (CTheatres.Headquarters(state, theatre) is { } headquarters)
			{
				LetGo(state, headquarters);
			}
			if (covering is not null)
			{
				Detach(covering);
				ai.Agents.Remove(covering);
				MarkChanged(state, country);
			}
			return;
		}
		if (covering is not null)
		{
			covering.AreaDirty = true;
			return;
		}
		if (!going && ai.RunsUnits)
		{
			ai.Agents.Add(new CAgent(country.Tag, theatre.Id));
			MarkChanged(state, country);
		}
	}

	/// <summary>A country nobody plays waits at its pass's gate after a theatre is made or dropped.</summary>
	private static void MarkChanged(CGameState state, CCountry country)
	{
		if (!state.Played.Contains(country.Tag))
		{
			country.UnitAi.Waiting = true;
		}
	}

	/// <summary>
	/// The agent bound to a unit: any other agent bound to it let go; its area set; the unit's plan switched on; its
	/// subordinates sorted and its powers worked out.
	/// </summary>
	public static bool Bind(CGameState state, CCountry country, CAgent agent, CUnit unit)
	{
		if (agent.Unit is not null)
		{
			return false;
		}
		LetGo(state, unit);
		unit.Agent = agent;
		agent.Unit = unit;
		CAgentArea.Set(state, country, agent);
		state.Outbox.Add(new CActivatePlanCommand(country.Tag, unit.Id, active: true, unit.Plan.AllowHqReorg));
		CSubordinates.Sort(state, country, agent);
		CPlanPowers.Update(state, country, agent);
		return true;
	}

	/// <summary>The agent let go of its unit, whose plan is switched off and reset.</summary>
	public static void Unbind(CGameState state, CAgent agent)
	{
		if (agent.Unit is not { } unit)
		{
			return;
		}
		state.Outbox.Add(new CActivatePlanCommand(unit.Owner, unit.Id, active: false, unit.Plan.AllowHqReorg));
		if (ReferenceEquals(unit.Agent, agent))
		{
			unit.Agent = null;
		}
		agent.Unit = null;
	}

	/// <summary>
	/// A unit let go by whatever every machine runs - a command, the theatres' hour, a change of owner: its plan switched
	/// off and reset **where the game holds it on**, and an agent on it taken off without a word. The plan's being on is
	/// what the game holds of "the AI runs this headquarters"; the agent is the AI's own, and a machine that does not run
	/// the AI may hold none, or one long let go elsewhere - so nothing here is posted for an agent's sake
	/// (<c>docs/multiplayer.md</c>, section 3).
	/// </summary>
	public static void LetGo(CGameState state, CUnit unit)
	{
		if (unit.Plan.Active)
		{
			state.Outbox.Add(new CActivatePlanCommand(unit.Owner, unit.Id, active: false, unit.Plan.AllowHqReorg));
		}
		if (unit.Agent is { } agent)
		{
			Detach(agent);
		}
	}

	/// <summary>An agent and its unit parted, nothing posted.</summary>
	private static void Detach(CAgent agent)
	{
		if (agent.Unit is { } unit && ReferenceEquals(unit.Agent, agent))
		{
			unit.Agent = null;
		}
		agent.Unit = null;
	}

	/// <summary>
	/// The highest unit, the unit itself or one above it, whose plan is on: the headquarters the AI runs it under, as the
	/// game holds it and every machine alike. What a command or a screen asks, where the AI's own code asks
	/// <see cref="HighestOver"/>.
	/// </summary>
	public static CUnit? HighestAutomated(CUnit unit)
	{
		CUnit? found = null;
		for (CUnit? above = unit; above is not null; above = above.Parent)
		{
			if (above.Plan.Active)
			{
				found = above;
			}
		}
		return found;
	}

	/// <summary>
	/// For a country whose units the AI does not run - a played one: each of its theatres' headquarters whose plan is on
	/// and that no agent has is given one, bound. A player's handing a headquarters to the AI only switches its plan on,
	/// as a command, on every machine (<see cref="Commands.CAutomateUnitCommand"/>); the agent is made here, in the AI's
	/// pass, by the machine that runs the AI.
	/// </summary>
	public static void Adopt(CGameState state, CCountry country)
	{
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (CTheatres.Headquarters(state, theatre) is { Plan.Active: true, Agent: null } headquarters)
			{
				var made = new CAgent(country.Tag, theatre.Id);
				country.UnitAi.Agents.Add(made);
				Bind(state, country, made, headquarters);
			}
		}
	}

	/// <summary>The highest agent on a unit or any unit above it, or null.</summary>
	public static CAgent? HighestOver(CUnit unit)
	{
		CAgent? found = null;
		for (CUnit? above = unit; above is not null; above = above.Parent)
		{
			found = above.Agent ?? found;
		}
		return found;
	}

	/// <summary>The theatre a unit is the headquarters of, or null.</summary>
	public static CTheatre? TheatreHeadedBy(CGameState state, CCountry country, CUnit unit)
	{
		return unit.Kind != EUnitKind.Theatre ? null
			: country.Strategy.Theatres.Find(theatre => CTheatres.Headquarters(state, theatre) == unit);
	}

	public static void Remove(CGameState state, CCountry country, CAgent agent)
	{
		Unbind(state, agent);
		country.UnitAi.Agents.Remove(agent);
	}

	/// <summary>
	/// Every hour, for a country the AI runs: an agent for each theatre that has none, bound at once where the theatre's
	/// headquarters exists.
	/// </summary>
	/// <param name="bind">False for a loaded game's agents, which its first pass ticks with no unit and binds after
	/// (FINDINGS-liveread.md, section 39).</param>
	public static void CreateForTheatres(CGameState state, CCountry country, bool bind = true)
	{
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (CAgentUnits.AgentFor(country, theatre.Id) is not null)
			{
				continue;
			}
			var agent = new CAgent(country.Tag, theatre.Id);
			country.UnitAi.Agents.Add(agent);
			if (bind && CTheatres.Headquarters(state, theatre) is { } headquarters)
			{
				Bind(state, country, agent, headquarters);
			}
		}
	}

	/// <summary>
	/// Every hour, for a country the AI runs: each unbound agent bound to its theatre's headquarters; then, once every
	/// agent has posted its plan, a theatre-level unit no theatre has let go and disbanded, and every other top-level
	/// unit attached under the headquarters of the theatre its province is in, or else under the nearest agent's unit.
	/// </summary>
	public static void BindAndAttachOrphans(CGameState state, CCountry country)
	{
		foreach (CAgent agent in country.UnitAi.Agents.ToArray())
		{
			if (agent.Unit is null && TheatreOf(state, country, agent) is { } theatre
				&& CTheatres.Headquarters(state, theatre) is { } headquarters)
			{
				Bind(state, country, agent, headquarters);
			}
		}
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (!agent.PlanPosted)
			{
				return;
			}
		}
		bool played = state.Played.Contains(country.Tag);
		foreach (CUnit unit in country.UnitsInOrder())
		{
			if (unit.IsRemoved)
			{
				continue;
			}
			if (unit.Kind == EUnitKind.Theatre)
			{
				if (!IsHeadquarters(country, unit))
				{
					if (unit.Agent is { } agent)
					{
						Unbind(state, agent);
					}
					if (!played)
					{
						state.Outbox.Add(new CDisbandCommand(unit.Id));
					}
				}
				continue;
			}
			if (unit.Agent is not null || unit.Parent is not null || unit.Transport is not null || unit.Plane is not null
				|| unit.CarriedBy is not null)
			{
				continue;
			}
			AttachOrphan(state, country, unit, played);
		}
	}

	private static void AttachOrphan(CGameState state, CCountry country, CUnit unit, bool played)
	{
		if (unit.Location > 0 && CTheatreProvinces.Of(state, unit.Location) is { } theatre && theatre.Country == country.Tag
			&& CTheatres.Headquarters(state, theatre) is { } headquarters)
		{
			if (headquarters.Kind < unit.Kind)
			{
				state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, headquarters.Id));
			}
			else if (!played)
			{
				state.Outbox.Add(new CDisbandCommand(unit.Id));
			}
			return;
		}
		CUnit? nearest = null;
		long least = Far;
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (agent.Unit is not { } candidate || candidate.Location <= 0 || unit.Location <= 0)
			{
				continue;
			}
			long distance = CMapDistance.Between(state.Province(unit.Location), state.Province(candidate.Location),
				state.MapWidth);
			if (distance < least)
			{
				least = distance;
				nearest = candidate;
			}
		}
		if (nearest is not null && nearest.Kind < unit.Kind)
		{
			state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, nearest.Id));
		}
	}

	/// <summary>Whether the unit heads one of the country's theatres.</summary>
	public static bool IsHeadquarters(CCountry country, CUnit unit)
	{
		foreach (CTheatre theatre in country.Strategy.Theatres)
		{
			if (theatre.Headquarters == unit.Id)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The agent's theatre, where it is still the country's.</summary>
	public static CTheatre? TheatreOf(CGameState state, CCountry country, CAgent agent)
	{
		return state.Theatres.TryGetValue(agent.Theatre, out CTheatre? theatre) && theatre.Country == country.Tag
			? theatre : null;
	}

	/// <summary>
	/// Every hour: an agent whose theatre is gone is removed, and one whose unit is gone or no longer its theatre's
	/// headquarters is let go - as the original's <c>DisbandTheatrelessHQs</c> and its unit's changes of place do.
	/// </summary>
	public static void Prune(CGameState state, CCountry country)
	{
		foreach (CAgent agent in new List<CAgent>(country.UnitAi.Agents))
		{
			if (TheatreOf(state, country, agent) is not { } theatre)
			{
				Remove(state, country, agent);
				continue;
			}
			if (agent.Unit is { } unit && (unit.IsRemoved || theatre.Headquarters != unit.Id))
			{
				Unbind(state, agent);
			}
		}
	}
}
