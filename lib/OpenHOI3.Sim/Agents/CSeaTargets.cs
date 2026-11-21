using System.Collections.Generic;
using OpenHOI3.Sim.Convoys;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// A country's sea targets handed to its agents once a day, with no sighting asked: each convoy of an enemy or a war
/// target to the agent whose ports best cover its route, and its transports to that agent's submarine want; each of
/// their fleets to the agent with the most ports nearest it for the ships it has already; each province of their war
/// industry in their capital's area to the agent whose air bases are nearest and largest; the waters where the
/// country's convoys were raided in the last month to every agent whose ports reach them; and the country's and its
/// fellow fighters' fleets to the nearest agent. The original's <c>CEU3AI_AssignSeaTargetsToAgents</c>. See
/// <c>docs/mechanics/moving.md</c>, section 12.
/// </summary>
public static class CSeaTargets
{
	private const long Whole = 1000;

	private const long ConvoyReach = 3000;

	private const long FleetReach = 2000;

	private const long Unreached = 10_000_000;

	/// <summary>A theatre holding the country's capital's area sees a fleet five times nearer.</summary>
	private const long CapitalTheatre = 200;

	private const long RaidMemory = 30 * CGameState.HoursPerDay;

	private const int TransportsPerWant = 20;

	/// <summary>How far, in the map's measure, an air base may lie from the war industry it is to bomb [exe].</summary>
	private const long AirBaseReach = 800;

	/// <summary>What a province with a strategic resource divides its score by [exe].</summary>
	private const float StrategicWeight = 2f;

	/// <summary>The score an agent has to beat for a province of war industry [exe].</summary>
	private const float WarIndustryStart = 10_000_000f;

	public static void Assign(CGameState state, CCountry country)
	{
		var agents = new List<(CAgent Agent, CTheatre Theatre)>();
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			agent.SubmarineWant = 0;
			agent.EnemyConvoys.Clear();
			agent.EnemyFleets.Clear();
			agent.StrategicTargets.Clear();
			agent.FriendlyFleets.Clear();
			agent.RaidedWaters.Clear();
			if (CAgents.TheatreOf(state, country, agent) is { } theatre)
			{
				agents.Add((agent, theatre));
			}
		}
		if (agents.Count == 0)
		{
			return;
		}
		var wants = new Dictionary<CAgent, long>();
		// Each agent's air bases, place and level in the order the walk below adds them: the same for every enemy province
		// within one assignment, where the walk made each area's list again for every province and agent.
		List<(int Province, float Level)>[]? bases = null;
		foreach (CCountry enemy in Targets(state, country))
		{
			foreach (CConvoy convoy in enemy.Convoys)
			{
				Convoy(state, agents, convoy, wants);
			}
			foreach (CUnit fleet in enemy.AllUnits())
			{
				if (fleet.Kind == EUnitKind.Navy && !fleet.IsRemoved && fleet.Location > 0)
				{
					Fleet(state, country, agents, fleet, enemyLoad: true);
				}
			}
			bases ??= AirBasesOf(state, agents);
			WarIndustry(state, country, agents, enemy, bases);
		}
		Raided(state, country, agents);
		foreach (CCountry friend in Fighters(state, country))
		{
			foreach (CUnit fleet in friend.AllUnits())
			{
				if (fleet.Kind == EUnitKind.Navy && !fleet.IsRemoved && fleet.Location > 0)
				{
					Fleet(state, country, agents, fleet, enemyLoad: false);
				}
			}
		}
		foreach ((CAgent agent, CTheatre _) in agents)
		{
			agent.SubmarineWant = (int)(wants.GetValueOrDefault(agent) / TransportsPerWant);
		}
	}

	private static void Convoy(CGameState state, List<(CAgent Agent, CTheatre Theatre)> agents, CConvoy convoy,
		Dictionary<CAgent, long> wants)
	{
		int transports = CConvoyRules.DesiredTransports(state, convoy);
		CAgent? best = null;
		long least = Unreached;
		foreach ((CAgent agent, CTheatre theatre) in agents)
		{
			if (theatre.Ports.Count == 0)
			{
				continue;
			}
			long bases = 0;
			long distances = 0;
			foreach (int step in convoy.Path)
			{
				foreach (int port in theatre.Ports)
				{
					long distance = Sea(state, step, port);
					if (distance < 0 || distance >= ConvoyReach)
					{
						continue;
					}
					bases += Level(state, port);
					distances += distance;
				}
			}
			if (bases < 1)
			{
				continue;
			}
			agent.EnemyConvoys.Add(convoy);
			long score = System.Math.Max((wants.GetValueOrDefault(agent) + transports) * Whole / TransportsPerWant / bases, Whole)
				* distances / theatre.Ports.Count;
			if (score < least)
			{
				least = score;
				best = agent;
			}
		}
		if (best is not null)
		{
			wants[best] = wants.GetValueOrDefault(best) + transports;
		}
	}

	private static void Fleet(CGameState state, CCountry country, List<(CAgent Agent, CTheatre Theatre)> agents, CUnit fleet,
		bool enemyLoad)
	{
		CAgent? best = null;
		long least = long.MaxValue;
		// The fifth is for a theatre with the acting capital's area among its areas - the original walks the theatre's list
		// for it - and not for the one theatre the capital province is filed under: Germany's Berlin HQ holds Berlin's area,
		// the province being Ob.kdo. des Heeres' (FINDINGS-liveread.md, section 138).
		Supply.CSupplyArea? capitalArea = country.ActingCapital > 0 ? Supply.CSupplyNetworks.Refresh(state).AreaOf(country.ActingCapital) : null;
		foreach ((CAgent agent, CTheatre theatre) in agents)
		{
			long distances = 0;
			long bases = 0;
			int count = 0;
			foreach (int port in theatre.Ports)
			{
				long distance = Sea(state, fleet.Location, port);
				// A port 2000 away still counts, for its own side's fleets as for the enemy's; and either needs a whole level of
				// naval base among those ports (on the listing; FINDINGS-liveread.md, section 78).
				if (distance < 0 || distance > FleetReach)
				{
					continue;
				}
				distances += distance;
				bases += Level(state, port);
				count++;
			}
			if (count == 0 || bases < 1)
			{
				continue;
			}
			long score = distances * Whole / count;
			if (enemyLoad)
			{
				long ships = Ships(agent.EnemyFleets) + fleet.SubUnits.Count;
				score = System.Math.Max(ships * Whole / bases, Whole) * distances / count;
			}
			if (capitalArea is not null && theatre.Areas.Contains(capitalArea))
			{
				score = score * CapitalTheatre / Whole;
			}
			if (score < least)
			{
				least = score;
				best = agent;
			}
		}
		(enemyLoad ? best?.EnemyFleets : best?.FriendlyFleets)?.Add(fleet);
	}

	/// <summary>
	/// The enemy's war industry, the third of what step 3 hands out for each listed country (FINDINGS-moving.md, D.1; read
	/// again on the listing 2026-11-21): each province of the area of its acting capital, in the area's order, held by its
	/// owner - whoever that is - while we are its enemy, the rebels either of us, or in a war nobody declared over the
	/// province; and with a working level of industry or a day's crude oil. Each goes to the agent whose theatre's areas have
	/// air bases within 800 of it, their working levels a whole one or more together: the least of the distances summed over
	/// the levels summed, halved for a province with a strategic resource, in single precision, strictly below 10,000,000 and
	/// the first of equals.
	/// </summary>
	private static void WarIndustry(CGameState state, CCountry country, List<(CAgent Agent, CTheatre Theatre)> agents,
		CCountry enemy, List<(int Province, float Level)>[] bases)
	{
		Supply.CSupplyNetwork network = Supply.CSupplyNetworks.Refresh(state);
		if (enemy.ActingCapital <= 0 || network.AreaOf(enemy.ActingCapital) is not { } home)
		{
			return;
		}
		CBuilding? industry = CBuildingRoles.Find(state, Supply.CSupplyNetworks.Industry);
		foreach (int id in home.Provinces)
		{
			CProvince province = state.Province(id);
			if (province.Owner is null || province.Owner != province.Controller || enemy == country
				|| !Movement.CMovementRules.IsEnemyIn(state, country.Tag, enemy.Tag, id))
			{
				continue;
			}
			float weight = province.StrategicResource.Length > 0 ? StrategicWeight : 1f;
			if (province.CurrentProducing[Economy.EGoods.CrudeOil].Raw < Whole
				&& (industry is null || province.BuildingLevel(industry).Raw < Whole))
			{
				continue;
			}
			CAgent? best = null;
			float least = WarIndustryStart;
			for (int index = 0; index < agents.Count; index++)
			{
				CAgent agent = agents[index].Agent;
				float levels = 0f;
				float distances = 0f;
				foreach ((int at, float level) in bases[index])
				{
					long distance = CMapDistance.Between(state.Province(at), province, state.MapWidth);
					if (distance > AirBaseReach)
					{
						continue;
					}
					levels += level;
					distances += distance;
				}
				if (levels < 1f)
				{
					continue;
				}
				float score = distances / levels / weight;
				if (score < least)
				{
					least = score;
					best = agent;
				}
			}
			best?.StrategicTargets.Add(id);
		}
	}

	/// <summary>
	/// Each agent's air bases over its theatre's areas in order, each area's largest first (<see cref="CParadropPlanner.AirBases"/>),
	/// with the working level the war industry's score adds - worked out once for an assignment.
	/// </summary>
	private static List<(int Province, float Level)>[] AirBasesOf(CGameState state, List<(CAgent Agent, CTheatre Theatre)> agents)
	{
		CBuilding? airBase = CBuildingRoles.Find(state, Movement.CMovementRules.AirBase);
		var bases = new List<(int Province, float Level)>[agents.Count];
		for (int index = 0; index < agents.Count; index++)
		{
			var list = new List<(int Province, float Level)>();
			foreach (Supply.CSupplyArea area in agents[index].Theatre.Areas)
			{
				foreach (int at in CParadropPlanner.AirBases(state, area))
				{
					list.Add((at, (airBase is null ? 0 : state.Province(at).BuildingLevel(airBase).Raw) / (float)Whole));
				}
			}
			bases[index] = list;
		}
		return bases;
	}

	/// <summary>
	/// The provinces of the finished battles that were raids on a convoy of our side - its defender a real country, not the
	/// rebels, ourselves or under the same overlord, or fighting beside us and no enemy - ended within the month, each to every
	/// agent with a port near it by an open way, once. The original's step 4 of <c>CEU3AI_AssignSeaTargetsToAgents</c>
	/// (FINDINGS-moving.md, D.1; FINDINGS-liveread.md, section 115): a country whose own convoys nobody raided has none, so a
	/// neutral's fleets do not go out on patrol over another's raided waters.
	/// </summary>
	private static void Raided(CGameState state, CCountry country, List<(CAgent Agent, CTheatre Theatre)> agents)
	{
		foreach (Combat.CFinishedCombat entry in state.CombatHistory.Entries)
		{
			if (entry.Kind != Combat.ECombatKind.Naval || !entry.IsConvoy || entry.Defender is not { } defender
				|| defender.Value == Revolts.CRebelFactions.Rebels || entry.Hour + RaidMemory < state.TotalHours
				|| !OnOurSide(state, country, defender))
			{
				continue;
			}
			int water = entry.Province;
			foreach ((CAgent agent, CTheatre theatre) in agents)
			{
				if (agent.RaidedWaters.Contains(water))
				{
					continue;
				}
				foreach (int port in theatre.Ports)
				{
					long distance = Sea(state, water, port);
					if (distance >= 0 && distance < FleetReach && CSealiftPickers.IsSeaRouteOpen(state, country, port, water))
					{
						agent.RaidedWaters.Add(water);
						break;
					}
				}
			}
		}
	}

	/// <summary>Ourselves, our overlord, its other subjects or ours; or a country fighting beside us that is no enemy.</summary>
	private static bool OnOurSide(CGameState state, CCountry country, CCountryTag tag)
	{
		if (tag == country.Tag || country.Overlord == tag)
		{
			return true;
		}
		CCountry? other = state.Country(tag);
		if (other?.Overlord == country.Tag || (country.Overlord is { } overlord && other?.Overlord == overlord))
		{
			return true;
		}
		return CCountryLists.FightingTogether(state, country.Tag, tag) && !CCountryLists.IsEnemy(state, country, tag);
	}

	/// <summary>The country and those fighting beside it.</summary>
	private static List<CCountry> Fighters(CGameState state, CCountry country)
	{
		var list = new List<CCountry> { country };
		foreach (CCountry other in state.Countries)
		{
			if (!ReferenceEquals(other, country) && CCountryLists.FightingTogether(state, country.Tag, other.Tag)
				&& !CCountryLists.IsEnemy(state, country, other.Tag))
			{
				list.Add(other);
			}
		}
		return list;
	}

	/// <summary>
	/// Whose convoys and fleets are handed out: the country's enemies, then those it has a war nobody declared with, then its
	/// strategy's war targets, each once - the original's enemies and <c>undeclared_war_opponents</c> appended, then the
	/// WarTargets (FINDINGS-moving.md, D.1). The undeclared war's are what puts the USA's fleets near Germany's ports on
	/// Germany's agents in July 1941 (FINDINGS-liveread.md, section 138).
	/// </summary>
	private static List<CCountry> Targets(CGameState state, CCountry country)
	{
		List<CCountry> list = CCountryLists.Enemies(state, country);
		foreach (Wars.CUndeclaredWar war in state.UndeclaredWars)
		{
			List<CCountryTag>? others = war.Attackers.Contains(country.Tag) ? war.Defenders
				: war.Defenders.Contains(country.Tag) ? war.Attackers : null;
			foreach (CCountryTag tag in others ?? new List<CCountryTag>())
			{
				if (state.Country(tag) is { } other && !list.Contains(other))
				{
					list.Add(other);
				}
			}
		}
		foreach (Ai.CWarTarget target in country.WarTargets)
		{
			if (state.Country(target.Target) is { } other && !list.Contains(other))
			{
				list.Add(other);
			}
		}
		return list;
	}

	/// <summary>The distance by sea, or -1 where there is no way.</summary>
	public static long Sea(CGameState state, int from, int to)
	{
		if (from == to)
		{
			return 0;
		}
		long distance = state.NavalDistances.Between(state.Province(from), state.Province(to));
		return distance > 0 ? distance : -1;
	}

	private static long Level(CGameState state, int port)
	{
		return CTheatreFigures.NavalBase(state) is { } naval ? state.Province(port).BuildingMaxLevel(naval).Raw / Whole : 0;
	}

	private static long Ships(List<CUnit> fleets)
	{
		long ships = 0;
		foreach (CUnit fleet in fleets)
		{
			ships += fleet.SubUnits.Count;
		}
		return ships;
	}
}
