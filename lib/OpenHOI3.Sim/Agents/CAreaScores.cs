using System.Collections.Generic;
using System.Linq;
using OpenHOI3.Sim.Ai;
using OpenHOI3.Sim.Combat;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Revolts;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// One score for each province of an agent's area of operations, the line shared out by them: by its forts at peace,
/// by its friendly neighbours, forts and depot at war, by the ways to the target areas when attacking; then a narrow
/// front, the strategy's wish to hold it, its regions and the weakness of those across its borders. The original's
/// <c>CAIUnit::ScoreOpsAreaProvinces</c>. See <c>docs/mechanics/moving.md</c>, section 3.
/// </summary>
public static class CAreaScores
{
	private const long Whole = 1000;

	/// <summary>Those across a province's borders worth this share of the country or less scale its score down.</summary>
	private const long WeakShare = 150;

	private const long LeastScale = 50;

	public static void Score(CGameState state, CCountry country, CAgent agent, int stance)
	{
		agent.Positions.Clear();
		agent.PositionSum = 0;
		if (agent.Unit is not { } unit)
		{
			return;
		}
		int n = agent.Area.Count;
		if (n < 1)
		{
			return;
		}
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var values = new Dictionary<CCountryTag, long>();
		long ours = CombatValue(state, country, values);
		// The spread of the +20s is carried in single precision, each step worked in double and rounded back, as the
		// original's (FINDINGS-liveread.md, section 69): in thousandths 25 of 35 came out 24.
		float fraction = (float)((double)(float)(agent.Committed.Count % n) / (double)(float)n);
		float carry = 0f;
		var front = new HashSet<int>(agent.FrontProvinces);
		bool atWar = agent.FrontierAtWar;
		foreach (int id in agent.Area)
		{
			CProvince province = state.Province(id);
			int friendly = 0;
			int fronts = 0;
			bool interesting = false;
			bool worthy = false;
			var holders = new List<CCountryTag>();
			foreach (CProvinceEdge edge in province.Edges)
			{
				if (edge.Kind == EProvinceEdgeKind.Impassable)
				{
					continue;
				}
				CProvince beside = state.Province(edge.To);
				if (beside.IsSea)
				{
					continue;
				}
				CCountryTag? holder = beside.Controller;
				bool noted = false;
				if (!interesting && holder is { } h && CFrontWorthy.IsWarCandidate(state, country, h))
				{
					interesting = true;
					noted = true;
				}
				if (!worthy && holder is { } w && state.Country(w) is { } other && CWarTargets.Prunes(other)
					&& CFrontWorthy.Is(state, country, w))
				{
					worthy = true;
					noted = true;
				}
				if (holder is not { } real || real.Value == CRebelFactions.Rebels)
				{
					continue;
				}
				if (noted && !holders.Contains(real))
				{
					holders.Add(real);
				}
				if (CApproaches.IsFriendly(state, country, real))
				{
					friendly++;
				}
				else if (front.Contains(edge.To))
				{
					fronts++;
				}
			}
			long scale = Scale(state, values, holders, ours);
			if (state.AiTrace is { } scaleTrace && scaleTrace.Country == country.Tag && holders.Count > 0)
			{
				scaleTrace.Write($"  scale {id}: {scale} ours {ours} " + string.Join(" ", holders.ConvertAll(h => $"{h}={(state.Country(h) is { } c ? CombatValue(state, c, values) : -1)}")));
			}
			long fort = province.Modifier[CCombatModifiers.FortLevel].Raw / Whole;
			int defend = country.Strategy.DefendProvinces.WeightOf(id);
			long score = 10 * Whole;
			int prio;
			if (!atWar)
			{
				prio = defend / 2;
				carry = (float)((double)carry + (double)fraction);
				score = (10 * fort + 10) * Whole;
				if (worthy)
				{
					score += 10 * Whole;
				}
				if (carry >= 1f)
				{
					carry = (float)((double)carry - 1.0);
					score += 20 * Whole;
				}
			}
			else
			{
				prio = defend / 8;
				bool depot = network.DepotOf(id) == id;
				if (stance >= CUnitPlan.Attacking)
				{
					if (depot)
					{
						score = 15 * Whole;
					}
					foreach (int route in agent.HostileRoutes)
					{
						if (Borders(state, route, id))
						{
							score *= 2;
						}
					}
				}
				else
				{
					if (network.AreaOf(id) is { } area && area.Provinces.Count > 5)
					{
						score = (10 * friendly - 10) * Whole;
					}
					if (!(friendly <= 1 && fronts >= 3))
					{
						score += (2 * fort + 20) * Whole;
					}
					if (depot)
					{
						score += 100 * Whole;
					}
				}
			}
			if (unit.Kind == EUnitKind.Theatre && FrontLength(state, network, province) * 1000 / n < 200)
			{
				score *= 3;
			}
			if (country.UnitAi.RunsUnits)
			{
				score += prio * Whole;
			}
			score += Regions(state, province) * 100 * Whole;
			score = score * scale / Whole;
			if (!interesting && atWar)
			{
				score = 0;
			}
			agent.PositionSum += score;
			var position = new CPosition(id, score);
			int at = 0;
			while (at < agent.Positions.Count && agent.Positions[at].Score >= score)
			{
				at++;
			}
			agent.Positions.Insert(at, position);
		}
	}

	/// <summary>A province beside another by an edge.</summary>
	public static bool Borders(CGameState state, int one, int other)
	{
		foreach (CProvinceEdge edge in state.Province(one).Edges)
		{
			if (edge.To == other)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Where those across its borders are worth at most 15% of the country, its share of that, at least 5%.</summary>
	private static long Scale(CGameState state, Dictionary<CCountryTag, long> values, List<CCountryTag> holders, long ours)
	{
		if (holders.Count == 0 || ours <= 0)
		{
			return Whole;
		}
		long total = 0;
		foreach (CCountryTag tag in holders)
		{
			if (state.Country(tag) is { } other)
			{
				total += CombatValue(state, other, values);
			}
		}
		long ratio = total / holders.Count * Whole / ours;
		return ratio > WeakShare ? Whole : System.Math.Max(ratio * Whole / WeakShare, LeastScale);
	}

	/// <summary>The country's brigades' worth to the AI, summed, as the original keeps it on the country.</summary>
	public static long CombatValue(CGameState state, CCountry country, Dictionary<CCountryTag, long> values)
	{
		if (values.TryGetValue(country.Tag, out long known))
		{
			return known;
		}
		long value = 0;
		// Its own, what it has lent and what waits to be placed, and not what is lent to it (the original's RecountUnitTotals).
		foreach (CUnit unit in country.AllUnits().Where(unit => unit.Lender is null).Concat(country.LentUnits).Concat(country.Deployments))
		{
			foreach (CSubUnit brigade in unit.SubUnits)
			{
				value += CUnitPower.Value(brigade);
			}
		}
		values[country.Tag] = value;
		return value;
	}

	/// <summary>
	/// The length of the front of its area the province lies on, in provinces: the first of its area's fronts holding it,
	/// none 0. The fronts are asked for and not read off the area, where they are made only on the first asking - read off
	/// it, an area nothing else had asked about since the network was made again looked to have no front, and its
	/// provinces were tripled as on a narrow one.
	/// </summary>
	private static int FrontLength(CGameState state, CSupplyNetwork network, CProvince province)
	{
		if (network.AreaOf(province.Id) is not { } area)
		{
			return 0;
		}
		foreach (List<int> front in CAreaFronts.Of(state, area))
		{
			if (front.Contains(province.Id))
			{
				return front.Count;
			}
		}
		return 0;
	}

	/// <summary>How many of its regions are marked to be held; ours: none is, the mark not being read.</summary>
	private static int Regions(CGameState state, CProvince province)
	{
		int marked = 0;
		foreach (int index in province.Regions)
		{
			if (state.Regions[index].AiPriority)
			{
				marked++;
			}
		}
		return marked;
	}
}
