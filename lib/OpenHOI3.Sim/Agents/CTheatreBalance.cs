using System;
using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
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
	/// <summary>An agent whose frontier is at peace sorts this much lower.</summary>
	private const long AtPeace = 1000;

	private const string Mountaineer = "bergsjaeger_brigade";

	private const string Mountain = "mountain";

	private static readonly string[] Mobile =
		{ "motorized_brigade", "mechanized_brigade", "armor_brigade", "light_armor_brigade", "heavy_armor_brigade" };

	/// <summary>The least speed a division given to a war candidate's whole surplus has, in thousandths.</summary>
	private const long Fast = 2000;

	private const double Unconnected = 0.25;

	private const float ParentGave = 15f;

	private const float FirstGiven = 10f;

	/// <summary>A mountaineer division at rest in the mountains of the donor's area is this much less worth giving.</summary>
	private const double MountaineerAtRest = 3.0;

	/// <summary>The idle divisions beside a candidate from which its score is whole; under it, a hundredth each.</summary>
	private const int IdleForWhole = 100;

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
			if (state.AiTrace is { } trace && trace.Country == country.Tag)
			{
				var names = new List<string>();
				foreach (CUnit unit in agent.Land)
				{
					if (unit.Kind == EUnitKind.Division)
					{
						names.Add($"{unit.Id} {unit.Name}");
					}
				}
				trace.Write($"balance {state.Date} {state.TotalHours % 24} agent {CAgents.TheatreOf(state, country, agent)?.Id} need {agent.DivisionNeed} count {agent.DivisionCount} [{string.Join("; ", names)}]");
			}
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
			&& CAiMoves.ReachesByLand(state, country, from, to.Location);
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
			Post(state, country, donor, taker, RollUp(mobile, Parents(mobile, donor.Unit), donor.Unit));
		}
		float ratioA = Ratio(countA, needA);
		float ratioB = Ratio(countB, needB);
		(float t1, float t2) = takerCandidate && donorCandidate ? (0.35f, 0.125f) : takerCandidate ? (0.3f, 0.1f) : (0.4f, 0.15f);
		long n = 0;
		if ((double)ratioB - t1 > ratioA && (double)ratioB - t2 > ratioA)
		{
			while (n < needA - countA)
			{
				n++;
				if (!((double)Ratio(countB - n, needB) - t2 > Ratio(countA + n, needA)))
				{
					break;
				}
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
		float scale = 1f;
		if (divisor > 0)
		{
			scale = (float)(1.0 - Math.Min((float)((double)(float)n / (float)divisor), 1f));
		}
		List<CUnit> given = Pick(state, country, donor, (int)n, fastOnly, takerTheatre, scale);
		if (state.AiTrace is { } trace && trace.Country == country.Tag)
		{
			trace.Write($"balance {state.Date} {state.TotalHours % 24} from {CAgents.TheatreOf(state, country, donor)?.Id} need {needB} count {countB}"
				+ $" to {takerTheatre.Id} n {n} scale {scale:F4} gave {given.Count} [{string.Join("; ", given.ConvertAll(unit => unit.Name))}]");
		}
		Post(state, country, donor, taker, given);
	}

	/// <summary>A count against a need as a share, in single precision as the original has it: a whole where the need is none.</summary>
	private static float Ratio(long count, long need)
	{
		return need <= 0 ? 1f : (float)((double)(float)count / (float)need);
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
		CTheatre takerTheatre, float scale)
	{
		CSupplyNetwork network = CSupplyNetworks.Refresh(state);
		var keep = new Dictionary<CSupplyArea, long>();
		var have = new Dictionary<CSupplyArea, long>();
		if (CAgents.TheatreOf(state, country, donor) is { } donorTheatre)
		{
			var visited = new HashSet<CSupplyArea>();
			foreach (CSupplyArea area in donorTheatre.Areas)
			{
				long need = CPlanPowers.NeedOf(state, country, donor, area, true, visited);
				long kept = (long)Math.Round((double)((float)need * scale), MidpointRounding.ToEven);
				keep[area] = need > 0 ? Math.Max(1, kept) : kept;
				have[area] = CountIn(state, country, donor, network, area);
			}
		}
		var candidates = new List<CUnit>();
		foreach (CUnit unit in donor.Land)
		{
			if (MayGive(state, country, donor, unit, fastOnly, network, keep, have, candidates))
			{
				candidates.Add(unit);
			}
		}
		float most = 0f;
		var distances = new Dictionary<CUnit, float>();
		foreach (CUnit unit in candidates)
		{
			float distance = DistanceToFronts(state, unit.Location, takerTheatre);
			distances[unit] = distance;
			most = Math.Max(most, distance);
		}
		if (most == 0f)
		{
			most = 0.1f;
		}
		var picked = new List<CUnit>();
		var parents = new List<CUnit>();
		while (picked.Count < wanted)
		{
			CUnit? best = null;
			float bestScore = 0f;
			foreach (CUnit unit in candidates)
			{
				if (picked.Contains(unit) || (donor.FrontierWarCandidate && !fastOnly && HasMobile(unit)))
				{
					continue;
				}
				// Only with an idle division left beside it on a border, and the fewer there the less worth giving.
				int idle = CUnitMayLeave.IdleBeside(state, country, unit, picked);
				if (idle < 1)
				{
					continue;
				}
				float near = (float)(1.0 - (double)distances[unit] / most);
				float score = (float)((double)near * (unit.Parent is { } parent && parents.Contains(parent) ? ParentGave : FirstGiven));
				if (network.AreaOf(unit.Location) is not null && !TouchesTheatre(network, unit.Location, takerTheatre))
				{
					score = (float)((double)score * Unconnected);
				}
				if (IsMountaineerAtRest(state, donor, unit))
				{
					score = (float)((double)score / MountaineerAtRest);
				}
				float share = idle < IdleForWhole ? (float)((double)idle / IdleForWhole) : 1f;
				score = (float)((double)score * share);
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
			if (best.Parent is { } noted && !ReferenceEquals(noted, donor.Unit) && !parents.Contains(noted))
			{
				parents.Add(noted);
			}
		}
		return RollUp(picked, parents, donor.Unit);
	}

	private static bool MayGive(CGameState state, CCountry country, CAgent donor, CUnit unit, bool fastOnly,
		CSupplyNetwork network, Dictionary<CSupplyArea, long> keep, Dictionary<CSupplyArea, long> have, List<CUnit> candidates)
	{
		if (unit.Kind != EUnitKind.Division || unit.SubUnits.Count == 0 || ReferenceEquals(unit, donor.Unit) || unit.IsRemoved
			|| unit.Location <= 0)
		{
			return false;
		}
		int role = CArmyRole.Of(unit);
		if (role == CArmyRole.Airborne)
		{
			return false;
		}
		int place = CAgentUnits.DestinationOrPlace(unit);
		if (role == CArmyRole.Garrison && place > 0 && (state.Province(place).Points > 9 || network.DepotOf(place) == place))
		{
			return false;
		}
		// Not one defending a battle, nor one its friends out in the enemy's land are coming back to; the border is
		// weighed in the choosing (FINDINGS-moving.md A.4e).
		if (!CUnitMayLeave.Allows(state, country, unit, candidates, strict: false, notDefending: true))
		{
			return false;
		}
		int bound = CAgentUnits.Destination(unit);
		if (bound > 0 && state.Province(bound).Controller is { } holder && CCountryLists.IsEnemy(state, country, holder))
		{
			return false;
		}
		if (fastOnly && Slowest(unit) < Fast)
		{
			return false;
		}
		if (donor.TargetProvinces.Contains(unit.Location))
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

	/// <summary>
	/// The original's <c>CountDivisionsInArea</c> for a donor: its divisions heading for or standing in the area, or in
	/// another country's area of the area's group where it may operate.
	/// </summary>
	private static long CountIn(CGameState state, CCountry country, CAgent donor, CSupplyNetwork network, CSupplyArea area)
	{
		long count = 0;
		foreach (CUnit unit in donor.Land)
		{
			if (unit.Kind != EUnitKind.Division || CAgentUnits.DestinationOrPlace(unit) is not (> 0 and var anchor)
				|| network.AreaOf(anchor) is not { } theirs)
			{
				continue;
			}
			if (ReferenceEquals(theirs, area)
				|| (theirs.RegionGroup is { } group && ReferenceEquals(group, area.RegionGroup) && theirs.Controller != country.Tag
					&& state.Country(theirs.Controller) is { } holder && CSupplyRelations.CanOperateIn(state, country, holder)))
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>A division with a mountaineer brigade, doing nothing, on a mountain of the donor's area of operations.</summary>
	private static bool IsMountaineerAtRest(CGameState state, CAgent donor, CUnit unit)
	{
		if (CCommandLadder.IsBlocked(unit) || !donor.Area.Contains(unit.Location) || state.Province(unit.Location).Terrain?.Name != Mountain)
		{
			return false;
		}
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			if (brigade.Type.Key == Mountaineer)
			{
				return true;
			}
		}
		return false;
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

	/// <summary>
	/// The original's <c>DistanceToTheatreFronts</c>: the least distance to a province of the theatre's fronts, which stops
	/// at 1000 (<see cref="CMapDistance.LeastFromAMillion"/>) - and a candidate at the greatest distance scores nothing, so
	/// a donor gives only what already stands near the taker's fronts.
	/// </summary>
	private static long DistanceToFronts(CGameState state, int province, CTheatre theatre)
	{
		var squares = new List<long>();
		foreach (CFront front in theatre.Fronts)
		{
			foreach (int id in front.Provinces)
			{
				squares.Add(CMapDistance.Squared(state.Province(province), state.Province(id), state.MapWidth));
			}
		}
		return CMapDistance.LeastFromAMillion(squares);
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

	private static List<CUnit> Parents(List<CUnit> units, CUnit? agentUnit)
	{
		var parents = new List<CUnit>();
		foreach (CUnit unit in units)
		{
			if (unit.Parent is { } parent && !ReferenceEquals(parent, agentUnit) && !parents.Contains(parent))
			{
				parents.Add(parent);
			}
		}
		return parents;
	}

	/// <summary>
	/// The original's <c>CAIUnit_RollUpWholeCommands</c>: a noted command, not fighting, retreating or a theatre, all of
	/// whose subordinates were picked, goes on the end in their place - and the commanders of those, not noted already, the
	/// same once more.
	/// </summary>
	private static List<CUnit> RollUp(List<CUnit> picked, List<CUnit> parents, CUnit? agentUnit)
	{
		var result = new List<CUnit>(picked);
		var above = new List<CUnit>();
		RollOnce(result, parents, parents, above, agentUnit);
		RollOnce(result, above, parents, null, agentUnit);
		return result;
	}

	private static void RollOnce(List<CUnit> result, List<CUnit> commanders, List<CUnit> noted, List<CUnit>? above, CUnit? agentUnit)
	{
		foreach (CUnit commander in commanders)
		{
			if (commander.Kind == EUnitKind.Theatre || commander.IsFighting || commander.IsRetreating
				|| !commander.Children.TrueForAll(result.Contains))
			{
				continue;
			}
			foreach (CUnit child in commander.Children)
			{
				result.Remove(child);
			}
			result.Add(commander);
			if (above is not null && commander.Parent is { } next && !ReferenceEquals(next, agentUnit) && !noted.Contains(next))
			{
				above.Add(next);
			}
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

}
