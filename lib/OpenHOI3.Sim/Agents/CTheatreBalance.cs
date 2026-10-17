using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Movement;
using OpenHOI3.Sim.Provinces;
using OpenHOI3.Sim.Supply;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Divisions moved between a country's theatres, once a day: the theatres short of divisions, those at war first, take
/// from those with more than they need - connected ones first - by the ratio of each one's count to its need; the donor
/// gives those nearest the taker's fronts that its areas can spare, a whole corps or army where all of it was picked;
/// each is put under the taker's headquarters and nothing marches. The original's
/// <c>CEU3AI_BalanceDivisionsBetweenTheatres</c> and <c>CAIUnit_PickDivisionsToGive</c>. See
/// <c>docs/mechanics/agents.md</c>, section 10.
/// </summary>
public static class CTheatreBalance
{
	private const long Whole = 1000;

	/// <summary>An agent whose frontier is at peace sorts this much lower.</summary>
	private const long AtPeace = 1000;

	private static readonly string[] Mobile =
		{ "motorized_brigade", "mechanized_brigade", "armor_brigade", "light_armor_brigade", "heavy_armor_brigade" };

	/// <summary>The least speed a division given to a war candidate's whole surplus has, in thousandths.</summary>
	private const long Fast = 2000;

	private const long Unconnected = 250;

	private const long ParentGave = 15;

	private const long FirstGiven = 10;

	public static void Run(CGameState state, CCountry country)
	{
		var sorted = new List<CAgent>();
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (agent.Unit is null || CAgents.TheatreOf(state, country, agent) is null)
			{
				continue;
			}
			CPlanPowers.Update(state, country, agent);
			long key = Key(agent);
			int at = 0;
			while (at < sorted.Count && Key(sorted[at]) >= key)
			{
				at++;
			}
			sorted.Insert(at, agent);
		}
		foreach (CAgent taker in sorted)
		{
			if (taker.DivisionNeed < taker.DivisionCount && !taker.FrontierWarCandidate)
			{
				continue;
			}
			CTheatre takerTheatre = CAgents.TheatreOf(state, country, taker)!;
			var donors = new List<CAgent>();
			for (int index = sorted.Count - 1; index >= 0; index--)
			{
				CAgent donor = sorted[index];
				if (ReferenceEquals(donor, taker))
				{
					continue;
				}
				if (IsConnected(state, country, donor, taker))
				{
					donors.Insert(0, donor);
				}
				else
				{
					donors.Add(donor);
				}
			}
			foreach (CAgent donor in donors)
			{
				Give(state, country, donor, taker, takerTheatre);
			}
		}
	}

	private static long Key(CAgent agent)
	{
		return agent.DivisionNeed - agent.DivisionCount - (agent.FrontierAtWar ? 0 : AtPeace);
	}

	/// <summary>The original's <c>CTheatre_IsConnectedTo</c>: an owner area shared, or the donor's headquarters can reach the taker's.</summary>
	private static bool IsConnected(CGameState state, CCountry country, CAgent donor, CAgent taker)
	{
		CTheatre? a = CAgents.TheatreOf(state, country, donor);
		CTheatre? b = CAgents.TheatreOf(state, country, taker);
		if (a is null || b is null)
		{
			return false;
		}
		foreach (CSupplyArea area in a.Areas)
		{
			if (b.Areas.Contains(area))
			{
				return true;
			}
		}
		return donor.Unit is { Location: > 0 } from && taker.Unit is { Location: > 0 } to
			&& CRouteFinder.Find(state, country, from, from.Location, to.Location) is not null;
	}

	private static void Give(CGameState state, CCountry country, CAgent donor, CAgent taker, CTheatre takerTheatre)
	{
		bool takerCandidate = taker.FrontierWarCandidate;
		bool donorCandidate = donor.FrontierWarCandidate;
		long needA = taker.DivisionNeed, countA = taker.DivisionCount;
		long needB = donor.DivisionNeed, countB = donor.DivisionCount;
		int stance = donor.Unit?.Plan.Stance ?? CUnitPlan.Defensive;
		if (!takerCandidate && donor.FrontierAtWar && stance <= CUnitPlan.Defensive)
		{
			return;
		}
		if (3 * needA - countA < 1)
		{
			return;
		}
		if (!takerCandidate && donor.FrontierAtWar && countA >= 2 * taker.Area.Count / 3)
		{
			return;
		}
		bool connected = IsConnected(state, country, donor, taker);
		if (connected && takerCandidate && !donorCandidate && needA > needB)
		{
			var mobile = new List<CUnit>();
			foreach (CUnit unit in donor.Land)
			{
				if (unit.Kind == EUnitKind.Division && !unit.IsRemoved && HasMobile(unit))
				{
					mobile.Add(unit);
				}
			}
			Post(state, country, donor, taker, RollUp(mobile, Parents(mobile)));
		}
		long ratioA = Ratio(countA, needA);
		long ratioB = Ratio(countB, needB);
		(long t1, long t2) = takerCandidate && donorCandidate ? (350L, 125L) : takerCandidate ? (300L, 100L) : (400L, 150L);
		long n = 0;
		if (ratioB - t1 > ratioA)
		{
			while (n < needA - countA && Ratio(countA + n, needA) < Ratio(countB - n, needB) - t2)
			{
				n++;
			}
		}
		long minGive = 0;
		bool fastOnly = false;
		if (takerCandidate && connected && !donorCandidate)
		{
			minGive = countB - needB;
			fastOnly = true;
		}
		else if (takerCandidate && donorCandidate && connected && countB > 2 * needB)
		{
			minGive = (countB - needB) / 2;
		}
		n = Math.Min(Math.Max(n, minGive), 3 * needA - countA);
		if (n < 1)
		{
			return;
		}
		long divisor = countB - n - minGive;
		long scale = divisor > 0 ? Whole - Math.Min(n * Whole / divisor, Whole) : Whole;
		Post(state, country, donor, taker, Pick(state, country, donor, (int)n, fastOnly, takerTheatre, scale));
	}

	/// <summary>A count against a need as a share, in thousandths: a whole where the need is none.</summary>
	private static long Ratio(long count, long need)
	{
		return need <= 0 ? Whole : count * Whole / need;
	}

	private static bool HasMobile(CUnit division)
	{
		foreach (CSubUnit brigade in division.SubUnits)
		{
			if (Array.IndexOf(Mobile, brigade.Type.Key) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// The original's <c>CAIUnit_PickDivisionsToGive</c>: what the donor's areas can spare, the candidates scored by how
	/// near the taker's fronts they stand, the best taken until there are enough, then rolled up into whole commands.
	/// </summary>
	private static List<CUnit> Pick(CGameState state, CCountry country, CAgent donor, int wanted, bool fastOnly,
		CTheatre takerTheatre, long scale)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var keep = new Dictionary<CSupplyArea, long>();
		var have = new Dictionary<CSupplyArea, long>();
		if (CAgents.TheatreOf(state, country, donor) is { } donorTheatre)
		{
			foreach (CSupplyArea area in donorTheatre.Areas)
			{
				long need = CPlanPowers.NeedOf(state, country, donor, area);
				long kept = HalfToEven(need * scale);
				keep[area] = need > 0 ? Math.Max(1, kept) : kept;
			}
		}
		var candidates = new List<CUnit>();
		foreach (CUnit unit in donor.Land)
		{
			if (unit.Kind == EUnitKind.Division && unit.Location > 0 && network.AreaOf(unit.Location) is { } area)
			{
				have[area] = have.GetValueOrDefault(area) + 1;
			}
		}
		foreach (CUnit unit in donor.Land)
		{
			if (MayGive(state, country, donor, unit, fastOnly, network, keep, have))
			{
				candidates.Add(unit);
			}
		}
		long most = 0;
		var distances = new Dictionary<CUnit, long>();
		foreach (CUnit unit in candidates)
		{
			long distance = DistanceToFronts(state, unit.Location, takerTheatre);
			distances[unit] = distance;
			most = Math.Max(most, distance);
		}
		if (most == 0)
		{
			most = 1;
		}
		var picked = new List<CUnit>();
		var parents = new List<CUnit>();
		while (picked.Count < wanted)
		{
			CUnit? best = null;
			long bestScore = 0;
			foreach (CUnit unit in candidates)
			{
				if (picked.Contains(unit) || (donor.FrontierWarCandidate && !fastOnly && HasMobile(unit)))
				{
					continue;
				}
				long score = (Whole - distances[unit] * Whole / most)
					* (unit.Parent is { } parent && parents.Contains(parent) ? ParentGave : FirstGiven);
				if (!TouchesTheatre(network, unit.Location, takerTheatre))
				{
					score = score * Unconnected / Whole;
				}
				if (score > bestScore)
				{
					bestScore = score;
					best = unit;
				}
			}
			if (best is null)
			{
				break;
			}
			picked.Add(best);
			if (best.Parent is { } noted && !parents.Contains(noted))
			{
				parents.Add(noted);
			}
		}
		return RollUp(picked, parents);
	}

	private static bool MayGive(CGameState state, CCountry country, CAgent donor, CUnit unit, bool fastOnly,
		CSupplyNetwork network, Dictionary<CSupplyArea, long> keep, Dictionary<CSupplyArea, long> have)
	{
		if (unit.Kind != EUnitKind.Division || unit.SubUnits.Count == 0 || ReferenceEquals(unit, donor.Unit) || unit.IsRemoved
			|| unit.Location <= 0 || unit.IsRetreating || unit.IsFighting)
		{
			return false;
		}
		int role = CArmyRole.Of(unit);
		if (role == CArmyRole.Airborne)
		{
			return false;
		}
		int place = CAgentUnits.DestinationOrPlace(unit);
		if (role == CArmyRole.Garrison && place > 0 && state.Province(place).Points > 9)
		{
			return false;
		}
		if (place > 0 && state.Province(place).Controller is { } holder && CCountryLists.IsEnemy(state, country, holder))
		{
			return false;
		}
		if (fastOnly && Slowest(unit) < Fast)
		{
			return false;
		}
		if (network.AreaOf(unit.Location) is { } area && keep.TryGetValue(area, out long kept) && kept > 0
			&& have.GetValueOrDefault(area) <= kept)
		{
			return false;
		}
		return true;
	}

	private static long Slowest(CUnit unit)
	{
		long slowest = long.MaxValue;
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			slowest = Math.Min(slowest, brigade.Stats["maximum_speed"].Raw);
		}
		return slowest;
	}

	/// <summary>The original's <c>DistanceToTheatreFronts</c>: the least distance to a province of the theatre's fronts.</summary>
	private static long DistanceToFronts(CGameState state, int province, CTheatre theatre)
	{
		long least = 1_000_000;
		foreach (CFront front in theatre.Fronts)
		{
			foreach (int id in front.Provinces)
			{
				least = Math.Min(least, CMapDistance.Between(state.Province(province), state.Province(id), state.MapWidth));
			}
		}
		return least;
	}

	/// <summary>Whether the province's area is one of the theatre's areas or beside one.</summary>
	private static bool TouchesTheatre(CSupplyNetwork network, int province, CTheatre theatre)
	{
		if (network.AreaOf(province) is not { } area)
		{
			return false;
		}
		foreach (CSupplyArea theirs in theatre.Areas)
		{
			if (ReferenceEquals(theirs, area) || theirs.Neighbours.Contains(area))
			{
				return true;
			}
		}
		return false;
	}

	private static List<CUnit> Parents(List<CUnit> units)
	{
		var parents = new List<CUnit>();
		foreach (CUnit unit in units)
		{
			if (unit.Parent is { } parent && !parents.Contains(parent))
			{
				parents.Add(parent);
			}
		}
		return parents;
	}

	/// <summary>
	/// The original's <c>CAIUnit_RollUpWholeCommands</c>: a noted command, not fighting, retreating or a theatre, all of
	/// whose subordinates were picked, takes their place - and its own commander the same, once.
	/// </summary>
	private static List<CUnit> RollUp(List<CUnit> picked, List<CUnit> parents)
	{
		var result = new List<CUnit>(picked);
		foreach (CUnit parent in parents)
		{
			RollUpOne(result, parent, twice: true);
		}
		return result;
	}

	private static void RollUpOne(List<CUnit> result, CUnit parent, bool twice)
	{
		if (parent.Kind == EUnitKind.Theatre || parent.IsFighting || parent.IsRetreating || parent.Children.Count == 0
			|| result.Contains(parent))
		{
			return;
		}
		foreach (CUnit child in parent.Children)
		{
			if (!result.Contains(child))
			{
				return;
			}
		}
		int at = result.IndexOf(parent.Children[0]);
		foreach (CUnit child in parent.Children)
		{
			result.Remove(child);
		}
		result.Insert(Math.Max(0, Math.Min(at, result.Count)), parent);
		if (twice && parent.Parent is { } above)
		{
			RollUpOne(result, above, twice: false);
		}
	}

	/// <summary>Each unit's power and divisions moved from the donor to the taker, and the unit put under the taker's headquarters.</summary>
	private static void Post(CGameState state, CCountry country, CAgent donor, CAgent taker, List<CUnit> units)
	{
		if (taker.Unit is not { } headquarters)
		{
			return;
		}
		foreach (CUnit unit in units)
		{
			long power = CUnitPower.Of(unit);
			donor.OurPower -= power;
			taker.OurPower += power;
			int divisions = unit.Kind == EUnitKind.Division ? 1 : CountDivisions(unit);
			taker.DivisionCount += divisions;
			donor.DivisionCount -= divisions;
			state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, headquarters.Id));
		}
	}

	private static int CountDivisions(CUnit unit)
	{
		int count = 0;
		foreach (CUnit below in unit.WithAllBelow())
		{
			if (below.Kind == EUnitKind.Division)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>A count in thousandths to a whole, a half to even.</summary>
	private static long HalfToEven(long thousandths)
	{
		long whole = thousandths / Whole;
		long rest = thousandths % Whole;
		const long half = Whole / 2;
		return rest > half || (rest == half && whole % 2 == 1) ? whole + 1 : whole;
	}
}
