using System.Collections.Generic;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// An agent's committed divisions split into garrisons of its hold provinces and a line: the garrison-brigade
/// divisions marked, the garrison targets taken off the hold list, the two counts by the front and the enemy on it,
/// and the garrisons matched to their targets block of land by block of land. The original's
/// <c>CAIUnit_SplitGarrisonAndLine</c>. See <c>docs/mechanics/moving.md</c>, section 7.2.
/// </summary>
public static class CLandSplit
{
	private const long Whole = 1000;

	private const long TargetScore = 20 * Whole;

	private const long StrongScore = 50 * Whole;

	private const int SmallVictory = 10;

	private const int PerPosition = 10;

	private const int SmallFrontAreas = 7;

	public static (int Garrisons, int Line, List<int> Targets) Split(CGameState state, CCountry country, CAgent agent)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool airTransports = agent.AirTransports.Count > 0;
		int brigaded = 0;
		foreach (CUnit unit in agent.Committed)
		{
			if (unit.SubUnits.Exists(brigade => brigade.Type.Key == "garrison_brigade"))
			{
				unit.IsGarrison = true;
				brigaded++;
			}
			else if (unit.IsGarrison && (state.Province(unit.Location).Controller != country.Tag
				|| (CArmyRole.Of(unit) == CArmyRole.Airborne && airTransports)))
			{
				unit.IsGarrison = false;
			}
		}
		var targets = new List<int>();
		int strong = 0;
		foreach (CHoldProvince hold in agent.HoldProvinces)
		{
			CProvince province = state.Province(hold.Province);
			if (hold.Score < TargetScore || province.Controller != country.Tag)
			{
				continue;
			}
			if (agent.Area.Contains(hold.Province) && province.Points < SmallVictory && network.DepotOf(hold.Province) != hold.Province)
			{
				continue;
			}
			targets.Add(hold.Province);
			if (hold.Score >= StrongScore)
			{
				strong++;
			}
		}
		int n = agent.Committed.Count;
		int positions = agent.Positions.Count;
		int garrisons;
		int line;
		if (positions == 0)
		{
			garrisons = n;
			line = 0;
		}
		else if (agent.HoldProvinces.Count == 0)
		{
			line = n - brigaded;
			garrisons = n - line;
		}
		else
		{
			int front = agent.FrontProvinces.Count;
			Enemies(state, country, agent, network, out int enemies, out int areaSum);
			if (agent.FrontierAtWar)
			{
				front *= 2;
				front = System.Math.Max(front, areaSum < SmallFrontAreas ? 2 * enemies : enemies);
				if (front <= enemies / 3 && strong > 1)
				{
					strong = 1;
				}
				enemies *= 2;
			}
			int want = System.Math.Min(System.Math.Max(enemies, front), PerPosition * positions);
			garrisons = System.Math.Min(System.Math.Min(agent.HoldProvinces.Count, n), n - want);
			int floor = System.Math.Min(System.Math.Max(targets.Count, brigaded), n);
			if (garrisons < floor)
			{
				garrisons = System.Math.Min(floor, n - front);
			}
			garrisons = System.Math.Max(garrisons, strong);
			line = n - garrisons;
			if (line > PerPosition * positions)
			{
				garrisons += line - PerPosition * positions;
				line = PerPosition * positions;
			}
		}
		garrisons = System.Math.Max(garrisons, 0);
		line = System.Math.Max(line, 0);
		foreach (CHoldProvince hold in agent.HoldProvinces)
		{
			if (targets.Count >= garrisons)
			{
				break;
			}
			if (!targets.Contains(hold.Province))
			{
				targets.Add(hold.Province);
			}
		}
		Match(state, country, agent, network, targets, garrisons);
		return (garrisons, line, targets);
	}

	/// <summary>The non-friendly land units on the front list whose holders are front-worthy, and their areas' provinces.</summary>
	private static void Enemies(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, out int enemies,
		out int areaSum)
	{
		enemies = 0;
		areaSum = 0;
		var areas = new HashSet<CSupplyArea>();
		foreach (int id in agent.FrontProvinces)
		{
			CProvince province = state.Province(id);
			if (province.Controller is not { } holder || !CFrontWorthy.Is(state, country, holder))
			{
				continue;
			}
			int here = 0;
			foreach (CUnit unit in state.Positions.Of(id))
			{
				if (CTheatres.IsLand(unit) && !CApproaches.IsFriendly(state, country, unit.Owner))
				{
					here++;
				}
			}
			enemies += here;
			if (here > 0 && network.AreaOf(id) is { } area && areas.Add(area))
			{
				areaSum += area.Provinces.Count;
			}
		}
	}

	/// <summary>Each block of land's garrisons matched to its targets: the worst dropped, the best of the rest marked.</summary>
	private static void Match(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, List<int> targets,
		int garrisons)
	{
		var wanted = new Dictionary<CSupplyGroup, int>();
		foreach (int id in targets.GetRange(0, System.Math.Min(garrisons, targets.Count)))
		{
			if (network.AreaOf(id)?.RegionGroup is { } group)
			{
				wanted[group] = wanted.GetValueOrDefault(group) + 1;
			}
		}
		if (targets.Count < garrisons && CAgents.TheatreOf(state, country, agent) is { } theatre
			&& CTheatreProvinces.LargestArea(theatre)?.RegionGroup is { } largest)
		{
			wanted[largest] = wanted.GetValueOrDefault(largest) + garrisons - targets.Count;
		}
		var holdScore = new Dictionary<int, long>();
		foreach (CHoldProvince hold in agent.HoldProvinces)
		{
			holdScore.TryAdd(hold.Province, hold.Score);
		}
		foreach ((CSupplyGroup group, int count) in wanted)
		{
			var there = new List<CUnit>();
			foreach (CUnit unit in agent.Committed)
			{
				if (unit.IsGarrison && ReferenceEquals(network.AreaOf(CAgentUnits.DestinationOrPlace(unit))?.RegionGroup, group))
				{
					there.Add(unit);
				}
			}
			if (state.AiTrace is { } trace && trace.Country == country.Tag)
			{
				trace.Write($"  block of {targets.Find(id => ReferenceEquals(network.AreaOf(id)?.RegionGroup, group))}: wants {count}, " +
					$"has {there.Count}");
			}
			while (there.Count > count && Worst(there, holdScore) is { } dropped)
			{
				dropped.IsGarrison = false;
				there.Remove(dropped);
			}
			while (there.Count < count && Best(state, country, agent, network, group) is { } marked)
			{
				marked.IsGarrison = true;
				there.Add(marked);
			}
		}
	}

	private static CUnit? Worst(List<CUnit> there, Dictionary<int, long> holdScore)
	{
		CUnit? worst = null;
		long least = long.MaxValue;
		foreach (CUnit unit in there)
		{
			if (unit.SubUnits.Exists(brigade => brigade.Type.Key == "garrison_brigade"))
			{
				continue;
			}
			long score = holdScore.GetValueOrDefault(CAgentUnits.DestinationOrPlace(unit));
			if (unit.SubUnits.Exists(brigade => brigade.Type.Key == "militia_brigade"))
			{
				score += 1000 * Whole;
			}
			if (unit.SubUnits.Exists(brigade => brigade.Type.Key == "police_brigade"))
			{
				score += 500 * Whole;
			}
			if (score < least)
			{
				least = score;
				worst = unit;
			}
		}
		return worst;
	}

	private static CUnit? Best(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, CSupplyGroup group)
	{
		bool atWar = state.IsAtWar(country.Tag);
		bool airTransports = agent.AirTransports.Count > 0;
		CUnit? best = null;
		long bestScore = long.MinValue;
		foreach (CUnit unit in agent.Committed)
		{
			if (unit.IsGarrison || !ReferenceEquals(network.AreaOf(CAgentUnits.DestinationOrPlace(unit))?.RegionGroup, group)
				|| FacesPreparedWar(state, country, unit))
			{
				continue;
			}
			bool mobile = false;
			bool airborne = false;
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				mobile |= brigade.Type.Key is "armor_brigade" or "light_armor_brigade" or "heavy_armor_brigade"
					or "mechanized_brigade";
				airborne |= brigade.Type.Key == "paratrooper_brigade";
			}
			if ((atWar && mobile) || (airborne && airTransports))
			{
				continue;
			}
			CUnitShares.Of(state, unit, out _, out long organisation);
			long score = 10 * CUnitRatings.Garrison(unit) + (MeanDistance(state, agent, unit) + 10) * Whole / 5;
			if (CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false))
			{
				score = score * 3 / 4;
			}
			score = score * (1500 - organisation) / Whole;
			if (score > bestScore)
			{
				bestScore = score;
				best = unit;
			}
		}
		return best;
	}

	private static bool FacesPreparedWar(CGameState state, CCountry country, CUnit unit)
	{
		foreach (CProvinceEdge edge in state.Province(unit.Location).Edges)
		{
			if (state.Province(edge.To).Controller is { } holder && holder != country.Tag
				&& CWarTargets.IsPreparingWarWith(state, country, holder))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>The unit's mean distance to the provinces of the area of operations.</summary>
	public static long MeanDistance(CGameState state, CAgent agent, CUnit unit)
	{
		if (agent.Area.Count == 0 || unit.Location <= 0)
		{
			return 0;
		}
		long sum = 0;
		CProvince here = state.Province(unit.Location);
		foreach (int id in agent.Area)
		{
			sum += CMapDistance.Between(here, state.Province(id), state.MapWidth);
		}
		return sum / agent.Area.Count;
	}
}
