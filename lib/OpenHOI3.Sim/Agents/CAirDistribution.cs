using System.Collections.Generic;
using OpenHOI3.Sim.Commands;
using OpenHOI3.Sim.Countries;
using OpenHOI3.Sim.Theatres;
using OpenHOI3.Sim.Units;

namespace OpenHOI3.Sim.Agents;

/// <summary>
/// Air units, transport planes and paratroopers moved between a country's theatres once a day: every unit holding a
/// transport plane or a paratrooper brigade to the theatre of the capital, and a tactical bomber to a theatre facing a war
/// candidate from one that is not. The original's <c>CEU3AI_DistributeAirToTheatres</c>, without the gain against the
/// plan's group differences, which is not built. See <c>docs/mechanics/agents.md</c>, section 10.3.
/// </summary>
public static class CAirDistribution
{
	private static readonly string[] Pulled = { "transport_plane", "paratrooper_brigade" };

	private static readonly string[] NeverMoved = { "interceptor", "multi_role", "cas" };

	private const string Tactical = "tactical_bomber";

	public static void Run(CGameState state, CCountry country)
	{
		int capitalTheatre = country.Capital > 0 && country.Capital < state.Provinces.Count
			? state.Province(country.Capital).TheatreId : 0;
		var moved = new HashSet<CUnit>();
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
				if (!goes && unit.Kind == EUnitKind.Air && Holds(unit, new[] { Tactical }) && !Holds(unit, NeverMoved)
					&& agent.FrontierWarCandidate && CAgentUnits.NearestAgent(unit) is { FrontierWarCandidate: false })
				{
					goes = true;
				}
				if (!goes)
				{
					continue;
				}
				moved.Add(unit);
				state.Outbox.Add(new CAttachUnitCommand(country.Tag, unit.Id, headquarters.Id));
			}
		}
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
