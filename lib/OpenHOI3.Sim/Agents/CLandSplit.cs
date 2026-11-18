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

	/// <summary>
	/// The split, and the garrisons in the order the original's vector of them has them: those with a garrison brigade and
	/// those still marked, in the committed order, less any dropped, and then each new pick as it is made
	/// (FINDINGS-liveread.md, section 35). The garrison fill goes through them in that order, a tie to the earlier.
	/// </summary>
	public static (int Garrisons, int Line, List<int> Targets, List<CUnit> Ordered) Split(CGameState state, CCountry country, CAgent agent)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		bool airTransports = agent.AirTransports.Count > 0;
		int brigaded = 0;
		var ordered = new List<CUnit>();
		foreach (CUnit unit in agent.CommittedAtFirst)
		{
			if (unit.SubUnits.Exists(brigade => brigade.Type.Key == "garrison_brigade"))
			{
				unit.IsGarrison = true;
				brigaded++;
				ordered.Add(unit);
			}
			else if (unit.IsGarrison && (state.Province(unit.Location).Controller != country.Tag
				|| (CArmyRole.Of(unit) == CArmyRole.Airborne && airTransports)))
			{
				unit.IsGarrison = false;
			}
			else if (unit.IsGarrison)
			{
				ordered.Add(unit);
			}
		}
		var targets = new List<int>();
		// Each target's record score beside it, which the drop asks (FINDINGS-liveread.md, section 70).
		var targetScores = new List<long>();
		// The records taken, by their place on the hold list: the top-up skips a record already taken and not a province
		// already listed, so a province the list names twice may be two targets (FINDINGS-liveread.md, section 36).
		var taken = new HashSet<int>();
		int strong = 0;
		for (int at = 0; at < agent.HoldProvinces.Count; at++)
		{
			CHoldProvince hold = agent.HoldProvinces[at];
			CProvince province = state.Province(hold.Province);
			if (hold.Score < TargetScore || province.Controller != country.Tag)
			{
				continue;
			}
			if (agent.Area.Contains(hold.Province) && province.Points < SmallVictory && network.DepotOf(hold.Province) != hold.Province)
			{
				continue;
			}
			Insert(targets, targetScores, agent, hold);
			taken.Add(at);
			if (hold.Score >= StrongScore)
			{
				strong++;
			}
		}
		int n = agent.CommittedAtFirst.Count;
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
			if (state.AiTrace is { } splitTrace && splitTrace.Country == country.Tag)
			{
				splitTrace.Write($"  split: front list {agent.FrontProvinces.Count}, front {front}, enemies {enemies}, area sum {areaSum}, strong {strong}, targets {targets.Count}, brigaded {brigaded}, holds {agent.HoldProvinces.Count}");
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
		for (int at = 0; at < agent.HoldProvinces.Count; at++)
		{
			if (targets.Count >= garrisons)
			{
				break;
			}
			if (!taken.Contains(at))
			{
				Insert(targets, targetScores, agent, agent.HoldProvinces[at]);
			}
		}
		Match(state, country, agent, network, targets, targetScores, garrisons, ordered);
		return (garrisons, line, targets, ordered);
	}

	/// <summary>The target put in its place by score, the higher first and after any of its own score, wherever it was found.</summary>
	private static void Insert(List<int> targets, List<long> scores, CAgent agent, CHoldProvince hold)
	{
		int at = targets.Count;
		while (at > 0 && hold.Score > agent.HoldProvinces.Find(other => other.Province == targets[at - 1])!.Score)
		{
			at--;
		}
		targets.Insert(at, hold.Province);
		scores.Insert(at, hold.Score);
	}

	/// <summary>The non-friendly land units on the front list whose holders are front-worthy, and their areas' provinces.</summary>
	private static void Enemies(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, out int enemies,
		out int areaSum)
	{
		enemies = 0;
		areaSum = 0;
		foreach (int id in agent.FrontProvinces)
		{
			CProvince province = state.Province(id);
			if (province.Controller is not { } holder || !CFrontWorthy.Is(state, country, holder))
			{
				continue;
			}
			// Its area's size for every such province, the same area again if it comes again (the original's list of areas goes unread).
			if (network.AreaOf(id) is { } area)
			{
				areaSum += area.Provinces.Count;
			}
			int here = 0;
			foreach (CUnit unit in state.Positions.Of(id))
			{
				// Not a friend by CCountry::IsFriend, military access counting (FINDINGS-liveread.md, section 38).
				if (CTheatres.IsLand(unit) && !CCountryLists.IsFriend(state, country, unit.Owner, withAccess: true))
				{
					here++;
				}
			}
			enemies += here;
		}
	}

	/// <summary>Each block of land's garrisons matched to its targets: the worst dropped, the best of the rest marked.</summary>
	private static void Match(CGameState state, CCountry country, CAgent agent, CSupplyNetwork network, List<int> targets,
		List<long> targetScores, int garrisons, List<CUnit> ordered)
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
		// The blocks in the order their groups were made, standing in for the original's map ordered by the groups' addresses
		// (FINDINGS-liveread.md, section 130).
		var blocks = new List<CSupplyGroup>(wanted.Keys);
		blocks.Sort((a, b) => a.Order.CompareTo(b.Order));
		foreach (CSupplyGroup group in blocks)
		{
			int count = wanted[group];
			var there = new List<CUnit>();
			// Over the garrisons as the vector has them: standing in the block, or else heading there by where it moves
			// (FINDINGS-moving.md, section A.3; FINDINGS-liveread.md, section 130).
			foreach (CUnit unit in ordered)
			{
				int heading = CAgentUnits.MovementDestination(unit);
				if (ReferenceEquals(network.AreaOf(unit.Location)?.RegionGroup, group)
					|| (heading > 0 && ReferenceEquals(network.AreaOf(heading)?.RegionGroup, group)))
				{
					there.Add(unit);
				}
			}
			if (state.AiTrace is { } trace && trace.Country == country.Tag)
			{
				trace.Write($"  block of {targets.Find(id => ReferenceEquals(network.AreaOf(id)?.RegionGroup, group))}: wants {count}, " +
					$"has {there.Count}");
			}
			while (there.Count > count && Worst(there, targets, targetScores) is { } dropped)
			{
				// Out of both by the original's swap with the last, which leaves the rest out of order: the next drop's first of
				// equals and the order the garrisons are sent in hang on it (FINDINGS-liveread.md, section 71).
				dropped.IsGarrison = false;
				CStdVector.SwapRemove(there, dropped);
				CStdVector.SwapRemove(ordered, dropped);
			}
			while (there.Count < count && Best(state, country, agent, network, group) is { } marked)
			{
				marked.IsGarrison = true;
				there.Add(marked);
				ordered.Add(marked);
			}
		}
	}

	/// <summary>
	/// The garrison to drop: the least by the score of the last garrison target where it stands or is bound, 1 where it is
	/// on none, +1000 with militia and +500 with police, a garrison brigade never; the first of equals, from 10,000,000
	/// (FINDINGS-liveread.md, section 70).
	/// </summary>
	private static CUnit? Worst(List<CUnit> there, List<int> targets, List<long> targetScores)
	{
		CUnit? worst = null;
		long least = 10_000_000 * Whole;
		foreach (CUnit unit in there)
		{
			if (unit.SubUnits.Exists(brigade => brigade.Type.Key == "garrison_brigade"))
			{
				continue;
			}
			int place = CAgentUnits.DestinationOrPlace(unit);
			long score = Whole;
			for (int at = 0; at < targets.Count; at++)
			{
				if (targets[at] == place)
				{
					score = targetScores[at];
				}
			}
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
		// The agent's own frontier, not the country's wars: an agent far from the fighting does take armour for a garrison.
		bool atWar = agent.FrontierAtWar;
		bool airTransports = agent.AirTransports.Count > 0;
		CUnit? best = null;
		float bestScore = float.MinValue;
		foreach (CUnit unit in agent.CommittedAtFirst)
		{
			// In the block by where it stands or where it is bound (FINDINGS-liveread.md, section 21).
			if (unit.IsGarrison || (!ReferenceEquals(network.AreaOf(unit.Location)?.RegionGroup, group)
				&& !(CAgentUnits.Destination(unit) > 0 && ReferenceEquals(network.AreaOf(CAgentUnits.Destination(unit))?.RegionGroup, group)))
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
			// Its mean strength share, which the original asks here and not its organisation (FINDINGS-liveread.md, section 34).
			// In the original's own precision, single and double as its listing has them, so that a near tie falls its way
			// (section 35).
			CUnitShares.Of(state, unit, out long strength, out _);
			float pre = (float)((double)CUnitRatings.GarrisonExact(state, unit) * 10.0 + (float)(MeanDistanceExact(state, agent, unit) / 5.0));
			if (CTheatreFigures.IsFrontProvince(state, state.Province(unit.Location), false))
			{
				pre = (float)(pre * 0.75);
			}
			float score = (float)((1.5 - (float)((float)strength / 1000.0)) * pre);
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
		foreach (CProvinceEdge edge in state.Province(CAgentUnits.DestinationOrPlace(unit)).Edges)
		{
			if (state.Province(edge.To).Controller is { } holder && holder != country.Tag
				&& CWarTargets.IsPreparingWarWith(state, country, holder))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The unit's mean distance to the provinces of the area of operations, and ten, in the original's precision: each
	/// distance added in double and kept a single, the mean a single and the ten added only where the area has provinces.
	/// </summary>
	private static float MeanDistanceExact(CGameState state, CAgent agent, CUnit unit)
	{
		if (agent.Area.Count == 0 || unit.Location <= 0)
		{
			return 0;
		}
		float sum = 0;
		CProvince here = state.Province(unit.Location);
		foreach (int id in agent.Area)
		{
			sum = (float)((double)CMapDistance.Between(here, state.Province(id), state.MapWidth) + sum);
		}
		float mean = (float)((double)sum / agent.Area.Count);
		return (float)(mean + 10.0);
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
