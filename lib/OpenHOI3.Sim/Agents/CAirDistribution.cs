using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Air units, transport planes and paratroopers moved between a country's theatres once a day: every unit holding a
/// transport plane or a paratrooper brigade to the theatre of the capital; otherwise to the first agent the move gains more
/// than 1, over the five listed types the unit holds, by the plans' group differences and what this pass has moved already;
/// or a tactical bomber to a theatre facing a war candidate from one that is not. The original's
/// <c>CEU3AI_DistributeAirToTheatres</c> (reversing/findings/FINDINGS-agents.md, F.4). See
/// <c>docs/mechanics/agents.md</c>, section 10.3.
/// </summary>
public static class CAirDistribution
{
	private static readonly string[] Pulled = { "transport_plane", "paratrooper_brigade" };

	private static readonly string[] NeverMoved = { "interceptor", "multi_role", "cas" };

	private const string Tactical = "tactical_bomber";

	/// <summary>The types the gain is summed over; each counted by its group's entry of the plan's difference.</summary>
	private static readonly string[] Listed = { "tactical_bomber", "strategic_bomber", "naval_bomber", "transport_plane", "paratrooper_brigade" };

	/// <summary>A current agent's difference where the unit has none.</summary>
	private const int NoAgent = -1000;

	public static void Run(CGameState state, CCountry country)
	{
		int capitalTheatre = country.Capital > 0 && country.Capital < state.Provinces.Count
			? state.Province(country.Capital).TheatreId : 0;
		var moved = new HashSet<CUnit>();
		// Each agent's group difference as the pass finds it, and what the pass has moved to it (+) or from it (-) since.
		var differences = new Dictionary<CAgent, int[]>();
		var tallies = new Dictionary<CAgent, int[]>();
		int size = state.SubUnitTypes.Count + 1;
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (agent.Unit is { } unit)
			{
				differences[agent] = CPlanWants.GroupDifference(state, unit);
				tallies[agent] = new int[size];
			}
		}
		var groups = new List<int>();
		foreach (string key in Listed)
		{
			groups.Add(state.SubUnitTypes.TryGetValue(key, out CSubUnitType? type) ? CPlanWants.GroupOf(state, type).DatabaseIndex : -1);
		}
		foreach (CAgent agent in country.UnitAi.Agents)
		{
			if (agent.Unit is not { } headquarters)
			{
				continue;
			}
			foreach (CUnit unit in country.UnitsInOrder())
			{
				if (unit.IsRemoved || moved.Contains(unit) || ReferenceEquals(unit, headquarters) || unit.Kind == EUnitKind.Theatre
					|| ReferenceEquals(CAgentUnits.NearestAgent(unit), agent) || unit.CarriedBy is not null)
				{
					continue;
				}
				bool goes = agent.Theatre == capitalTheatre && Holds(unit, Pulled);
				bool tallied = false;
				int[] counts = CountListed(unit);
				CAgent? current = CAgentUnits.NearestAgent(unit);
				if (!goes)
				{
					long gain = 0;
					for (int i = 0; i < Listed.Length; i++)
					{
						int g = groups[i];
						if (counts[i] == 0 || g < 0)
						{
							continue;
						}
						int mine = differences[agent][g] - tallies[agent][g] - counts[i];
						int theirs = current is not null && differences.TryGetValue(current, out int[]? diff)
							? diff[g] + counts[i] - tallies[current][g]
							: NoAgent + counts[i];
						gain += mine - theirs;
					}
					goes = gain > 1;
					if (!goes && unit.Kind == EUnitKind.Air && Holds(unit, new[] { Tactical }) && !Holds(unit, NeverMoved)
						&& agent.FrontierWarCandidate && current is { FrontierWarCandidate: false })
					{
						goes = true;
					}
					tallied = goes;
				}
				if (!goes)
				{
					continue;
				}
				// The pass's tally, kept for every listed type on a move by the gain or the war candidate, not by the capital's pull.
				if (tallied)
				{
					for (int i = 0; i < Listed.Length; i++)
					{
						if (counts[i] == 0 || groups[i] < 0)
						{
							continue;
						}
						tallies[agent][groups[i]] += counts[i];
						if (current is not null && tallies.TryGetValue(current, out int[]? from))
						{
							from[groups[i]] -= counts[i];
						}
					}
				}
				moved.Add(unit);
				state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, headquarters.Id));
			}
		}
	}

	/// <summary>How many brigades or wings of each listed type the unit holds itself.</summary>
	private static int[] CountListed(CUnit unit)
	{
		var counts = new int[Listed.Length];
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			int i = System.Array.IndexOf(Listed, brigade.Type.Key);
			if (i >= 0)
			{
				counts[i]++;
			}
		}
		return counts;
	}

	private static bool Holds(CUnit unit, string[] keys)
	{
		foreach (CSubUnit brigade in unit.SubUnits)
		{
			if (System.Array.IndexOf(keys, brigade.Type.Key) >= 0)
			{
				return true;
			}
		}
		return false;
	}
}
